// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace osu.Game.Online.Discord.Full
{
    /// <summary>
    /// Reads and writes the favourite GIFs in Discord's frecency settings, which are a protobuf message.
    /// Only the favourite GIFs are decoded, all other settings are kept byte for byte so that nothing else is changed.
    /// </summary>
    public static class DiscordFavouriteGifs
    {
        // FrecencyUserSettings.favorite_gifs
        private const int favourite_gifs_field = 2;

        // FavoriteGIFs.gifs, a map of the URL of each GIF to a FavoriteGIF.
        private const int gifs_field = 1;

        private const int wire_varint = 0;
        private const int wire_fixed64 = 1;
        private const int wire_length = 2;
        private const int wire_fixed32 = 5;

        // FavoriteGIF.format
        private const int format_image = 1;
        private const int format_video = 2;

        /// <summary>
        /// Reads the favourite GIFs, the newest first.
        /// </summary>
        public static List<DiscordGif> Read(byte[] settings)
        {
            var gifs = new List<DiscordGif>();

            foreach (var field in readFields(settings).Where(f => f.Number == favourite_gifs_field && f.WireType == wire_length))
            {
                foreach (var entry in readFields(field.Data).Where(f => f.Number == gifs_field && f.WireType == wire_length))
                {
                    if (readGif(entry.Data) is DiscordGif gif)
                        gifs.Add(gif);
                }
            }

            return gifs.OrderByDescending(g => g.Order).ToList();
        }

        /// <summary>
        /// Replaces the favourite GIFs in the settings, keeping everything else as it is.
        /// </summary>
        public static byte[] Write(byte[] settings, IEnumerable<DiscordGif> gifs)
        {
            var output = new MemoryStream();
            byte[]? otherFavouriteFields = null;

            foreach (var field in readFields(settings))
            {
                if (field.Number == favourite_gifs_field && field.WireType == wire_length)
                {
                    // Keeps the other fields of the favourite GIFs, such as whether the tooltip was hidden.
                    var others = new MemoryStream();

                    foreach (var inner in readFields(field.Data).Where(f => f.Number != gifs_field))
                        others.Write(inner.Raw);

                    otherFavouriteFields = others.ToArray();
                    continue;
                }

                output.Write(field.Raw);
            }

            var favourites = new MemoryStream();

            foreach (var gif in gifs)
                writeLength(favourites, gifs_field, writeEntry(gif));

            if (otherFavouriteFields != null)
                favourites.Write(otherFavouriteFields);

            writeLength(output, favourite_gifs_field, favourites.ToArray());

            return output.ToArray();
        }

        private static DiscordGif? readGif(byte[] entry)
        {
            string? url = null;
            var gif = new DiscordGif();

            foreach (var field in readFields(entry))
            {
                if (field.Number == 1 && field.WireType == wire_length)
                    url = Encoding.UTF8.GetString(field.Data);
                else if (field.Number == 2 && field.WireType == wire_length)
                {
                    foreach (var value in readFields(field.Data))
                    {
                        switch (value.Number)
                        {
                            case 1 when value.WireType == wire_varint:
                                gif.IsVideo = value.Value == format_video;
                                break;

                            case 2 when value.WireType == wire_length:
                                gif.Src = Encoding.UTF8.GetString(value.Data);
                                break;

                            case 3 when value.WireType == wire_varint:
                                gif.Width = (int)value.Value;
                                break;

                            case 4 when value.WireType == wire_varint:
                                gif.Height = (int)value.Value;
                                break;

                            case 5 when value.WireType == wire_varint:
                                gif.Order = (int)value.Value;
                                break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(gif.Src))
                return null;

            gif.Url = url;
            return gif;
        }

        private static byte[] writeEntry(DiscordGif gif)
        {
            var value = new MemoryStream();
            writeVarint(value, 1, (ulong)(gif.IsVideo ? format_video : format_image));
            writeLength(value, 2, Encoding.UTF8.GetBytes(gif.Src ?? string.Empty));
            writeVarint(value, 3, (ulong)Math.Max(0, gif.Width));
            writeVarint(value, 4, (ulong)Math.Max(0, gif.Height));
            writeVarint(value, 5, (ulong)Math.Max(0, gif.Order));

            var entry = new MemoryStream();
            writeLength(entry, 1, Encoding.UTF8.GetBytes(gif.Url));
            writeLength(entry, 2, value.ToArray());
            return entry.ToArray();
        }

        private static IEnumerable<Field> readFields(byte[] data)
        {
            int position = 0;

            while (position < data.Length)
            {
                int start = position;
                ulong key = readVarint(data, ref position);
                int number = (int)(key >> 3);
                int wireType = (int)(key & 7);

                ulong value = 0;
                byte[] payload = Array.Empty<byte>();

                switch (wireType)
                {
                    case wire_varint:
                        value = readVarint(data, ref position);
                        break;

                    case wire_fixed64:
                        position += 8;
                        break;

                    case wire_length:
                        int length = checked((int)readVarint(data, ref position));
                        if (length < 0 || position + length > data.Length)
                            throw new InvalidDataException(@"The settings are truncated.");

                        payload = data.AsSpan(position, length).ToArray();
                        position += length;
                        break;

                    case wire_fixed32:
                        position += 4;
                        break;

                    default:
                        throw new InvalidDataException($@"The settings contain the unsupported wire type {wireType}.");
                }

                if (position > data.Length)
                    throw new InvalidDataException(@"The settings are truncated.");

                yield return new Field(number, wireType, value, payload, data.AsSpan(start, position - start).ToArray());
            }
        }

        private static ulong readVarint(byte[] data, ref int position)
        {
            ulong result = 0;

            for (int shift = 0; shift < 64; shift += 7)
            {
                if (position >= data.Length)
                    throw new InvalidDataException(@"The settings are truncated.");

                byte b = data[position++];
                result |= (ulong)(b & 0x7f) << shift;

                if ((b & 0x80) == 0)
                    return result;
            }

            throw new InvalidDataException(@"The settings contain an invalid number.");
        }

        private static void writeVarint(Stream stream, int number, ulong value)
        {
            writeRawVarint(stream, (ulong)number << 3 | wire_varint);
            writeRawVarint(stream, value);
        }

        private static void writeLength(Stream stream, int number, byte[] data)
        {
            writeRawVarint(stream, (ulong)number << 3 | wire_length);
            writeRawVarint(stream, (ulong)data.Length);
            stream.Write(data);
        }

        private static void writeRawVarint(Stream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }

            stream.WriteByte((byte)value);
        }

        private readonly record struct Field(int Number, int WireType, ulong Value, byte[] Data, byte[] Raw);
    }
}
