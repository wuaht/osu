// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using SharpCompress.Compressors.LZMA;

namespace osu.Game.IO
{
    /// <summary>
    /// Writes data as a single-stream, single-block .xz file (LZMA2 filter, CRC32 check).
    /// </summary>
    /// <remarks>
    /// SharpCompress can only read .xz files, so the container is written manually as per the .xz file format specification (https://tukaani.org/xz/xz-file-format.txt).
    /// The LZMA2 data consists of independently encoded LZMA chunks (each resetting the dictionary and encoder state), produced by SharpCompress' LZMA encoder.
    /// Chunks which cannot be compressed within the limits of the LZMA2 format are stored uncompressed.
    /// </remarks>
    public static class XzCompressor
    {
        /// <summary>
        /// The dictionary size used for compression, which is also the largest uncompressed size of a single LZMA chunk.
        /// </summary>
        private const int dictionary_size = 1 << 20;

        /// <summary>
        /// The LZMA2 dictionary size property representing <see cref="dictionary_size"/>.
        /// Decoded as <c>(2 | (property &amp; 1)) &lt;&lt; (property / 2 + 11)</c>.
        /// </summary>
        private const byte dictionary_size_property = 16;

        /// <summary>
        /// LZMA2 limits the compressed size of a single chunk to 64 KiB.
        /// </summary>
        private const int max_compressed_chunk_size = 1 << 16;

        /// <summary>
        /// LZMA2 limits the size of a single uncompressed chunk to 64 KiB.
        /// </summary>
        private const int max_uncompressed_chunk_size = 1 << 16;

        /// <summary>
        /// The smallest chunk size to attempt LZMA compression with before falling back to storing data uncompressed.
        /// </summary>
        private const int min_lzma_chunk_size = 1 << 16;

        private const byte lzma2_filter_id = 0x21;

        /// <summary>
        /// LZMA2 chunk control byte: dictionary reset, state reset and new properties, followed by LZMA data.
        /// </summary>
        private const byte lzma2_control_lzma_full_reset = 0xE0;

        /// <summary>
        /// LZMA2 chunk control byte: dictionary reset, followed by uncompressed data.
        /// </summary>
        private const byte lzma2_control_uncompressed_dictionary_reset = 0x01;

        private const byte lzma2_end_marker = 0x00;

        private static readonly byte[] header_magic = { 0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00 };
        private static readonly byte[] footer_magic = { 0x59, 0x5A };

        /// <summary>
        /// Stream flags indicating a CRC32 check.
        /// </summary>
        private static readonly byte[] stream_flags = { 0x00, 0x01 };

        private const int check_size = 4;

        /// <summary>
        /// Compresses <paramref name="data"/> into .xz format, writing the result to <paramref name="output"/>.
        /// </summary>
        public static void Compress(ReadOnlySpan<byte> data, Stream output)
        {
            var block = new MemoryStream();

            byte[] blockHeader = createBlockHeader();
            block.Write(blockHeader);

            long compressedSize = writeLzma2(data, block);
            long unpaddedSize = blockHeader.Length + compressedSize + check_size;

            writePadding(block, compressedSize);
            writeUInt32(block, Crc32.Compute(data));

            byte[] index = createIndex(unpaddedSize, data.Length);

            // stream header
            output.Write(header_magic);
            output.Write(stream_flags);
            writeUInt32(output, Crc32.Compute(stream_flags));

            block.Position = 0;
            block.CopyTo(output);

            output.Write(index);

            // stream footer
            var footer = new MemoryStream();
            writeUInt32(footer, (uint)(index.Length / 4 - 1));
            footer.Write(stream_flags);

            byte[] footerBody = footer.ToArray();
            writeUInt32(output, Crc32.Compute(footerBody));
            output.Write(footerBody);
            output.Write(footer_magic);
        }

        private static byte[] createBlockHeader()
        {
            var header = new MemoryStream();

            // header size placeholder, block flags (one filter, no optional size fields), filter flags.
            header.Write(new byte[] { 0x00, 0x00, lzma2_filter_id, 0x01, dictionary_size_property });

            // the header (including the CRC32 at its end) must be a multiple of four bytes.
            while ((header.Length + 4) % 4 != 0)
                header.WriteByte(0x00);

            byte[] result = header.ToArray();
            result[0] = (byte)((result.Length + 4) / 4 - 1);

            var output = new MemoryStream();
            output.Write(result);
            writeUInt32(output, Crc32.Compute(result));
            return output.ToArray();
        }

        private static byte[] createIndex(long unpaddedSize, long uncompressedSize)
        {
            var index = new MemoryStream();

            index.WriteByte(0x00); // index indicator
            writeVarInt(index, 1); // number of records
            writeVarInt(index, (ulong)unpaddedSize);
            writeVarInt(index, (ulong)uncompressedSize);

            while (index.Length % 4 != 0)
                index.WriteByte(0x00);

            writeUInt32(index, Crc32.Compute(index.ToArray()));
            return index.ToArray();
        }

        private static long writeLzma2(ReadOnlySpan<byte> data, Stream output)
        {
            long start = output.Position;

            int offset = 0;
            int chunkSize = dictionary_size;

            while (offset < data.Length)
            {
                int length = Math.Min(chunkSize, data.Length - offset);
                var chunk = data.Slice(offset, length);

                byte[] compressed = compressLzma(chunk, out byte properties);

                if (compressed.Length > max_compressed_chunk_size || compressed.Length >= length)
                {
                    // try again with a smaller chunk, which should compress to a smaller size.
                    if (length > min_lzma_chunk_size)
                    {
                        chunkSize = Math.Max(min_lzma_chunk_size, length / 2);
                        continue;
                    }

                    writeUncompressedChunks(chunk, output);
                }
                else
                {
                    int uncompressedSizeMinusOne = length - 1;
                    int compressedSizeMinusOne = compressed.Length - 1;

                    output.WriteByte((byte)(lzma2_control_lzma_full_reset | (uncompressedSizeMinusOne >> 16)));
                    output.WriteByte((byte)(uncompressedSizeMinusOne >> 8));
                    output.WriteByte((byte)uncompressedSizeMinusOne);
                    output.WriteByte((byte)(compressedSizeMinusOne >> 8));
                    output.WriteByte((byte)compressedSizeMinusOne);
                    output.WriteByte(properties);
                    output.Write(compressed);
                }

                offset += length;
                chunkSize = dictionary_size;
            }

            output.WriteByte(lzma2_end_marker);
            return output.Position - start;
        }

        private static void writeUncompressedChunks(ReadOnlySpan<byte> data, Stream output)
        {
            for (int offset = 0; offset < data.Length; offset += max_uncompressed_chunk_size)
            {
                int length = Math.Min(max_uncompressed_chunk_size, data.Length - offset);
                int sizeMinusOne = length - 1;

                output.WriteByte(lzma2_control_uncompressed_dictionary_reset);
                output.WriteByte((byte)(sizeMinusOne >> 8));
                output.WriteByte((byte)sizeMinusOne);
                output.Write(data.Slice(offset, length));
            }
        }

        /// <summary>
        /// Encodes <paramref name="data"/> as raw LZMA data without an end marker, as required for an LZMA2 chunk.
        /// </summary>
        private static byte[] compressLzma(ReadOnlySpan<byte> data, out byte properties)
        {
            var result = new MemoryStream();

            using (var lzma = LzmaStream.Create(new LzmaEncoderProperties(false, dictionary_size), false, result))
            {
                lzma.Write(data);
                properties = lzma.Properties[0];
            }

            return result.ToArray();
        }

        private static void writePadding(Stream output, long size)
        {
            for (; size % 4 != 0; size++)
                output.WriteByte(0x00);
        }

        private static void writeUInt32(Stream output, uint value)
        {
            output.WriteByte((byte)value);
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 24));
        }

        private static void writeVarInt(Stream output, ulong value)
        {
            while (value >= 0x80)
            {
                output.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }

            output.WriteByte((byte)value);
        }

        private static class Crc32
        {
            private static readonly uint[] table = createTable();

            private static uint[] createTable()
            {
                uint[] result = new uint[256];

                for (uint i = 0; i < 256; i++)
                {
                    uint crc = i;

                    for (int k = 0; k < 8; k++)
                        crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;

                    result[i] = crc;
                }

                return result;
            }

            public static uint Compute(ReadOnlySpan<byte> data)
            {
                uint crc = 0xFFFFFFFF;

                foreach (byte b in data)
                    crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);

                return ~crc;
            }
        }
    }
}
