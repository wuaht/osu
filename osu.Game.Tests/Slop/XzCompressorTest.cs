// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.IO;
using SharpCompress.Compressors.Xz;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class XzCompressorTest
    {
        [Test]
        public void TestEmpty() => assertRoundTrip(Array.Empty<byte>());

        [Test]
        public void TestSingleByte() => assertRoundTrip(new byte[] { 0x61 });

        [Test]
        public void TestBeatmapLikeText()
        {
            string line = "64,192,1000,5,0,0:0:0:0:\n";
            assertRoundTrip(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(line, 20_000))));
        }

        /// <summary>
        /// Incompressible data has to be stored in uncompressed LZMA2 chunks.
        /// </summary>
        [Test]
        public void TestIncompressibleData()
        {
            byte[] data = new byte[300_000];
            new Random(1234).NextBytes(data);
            assertRoundTrip(data);
        }

        /// <summary>
        /// Data larger than a single LZMA2 chunk, mixing compressible and incompressible parts.
        /// </summary>
        [Test]
        public void TestLargeMixedData()
        {
            byte[] random = new byte[500_000];
            new Random(5678).NextBytes(random);

            byte[] text = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("[HitObjects]\n256,192,1500,1,0\n", 200_000)));

            assertRoundTrip(text.Concat(random).Concat(text).ToArray());
        }

        private static void assertRoundTrip(byte[] data)
        {
            using var compressed = new MemoryStream();
            XzCompressor.Compress(data, compressed);

            compressed.Position = 0;
            Assert.That(XZStream.IsXZStream(compressed), Is.True);

            compressed.Position = 0;
            using var decompressed = new MemoryStream();
            using (var xz = new XZStream(compressed))
                xz.CopyTo(decompressed);

            Assert.That(decompressed.ToArray(), Is.EqualTo(data));
        }
    }
}
