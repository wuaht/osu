// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Online.Discord;
using osu.Game.Online.Discord.Full;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class DiscordFavouriteGifsTest
    {
        // Field 1 (versions) with a varint, a field 3 with a string and a fixed32 field 9, which aren't favourite GIFs.
        private static readonly byte[] other_settings = { 0x0a, 0x02, 0x08, 0x05, 0x1a, 0x03, 0x61, 0x62, 0x63, 0x4d, 0x01, 0x02, 0x03, 0x04 };

        [Test]
        public void TestEmptySettings()
        {
            Assert.That(DiscordFavouriteGifs.Read(Array.Empty<byte>()), Is.Empty);

            byte[] written = DiscordFavouriteGifs.Write(Array.Empty<byte>(), new[] { createGif("https://tenor.com/a", 1) });
            Assert.That(DiscordFavouriteGifs.Read(written).Single().Url, Is.EqualTo("https://tenor.com/a"));
        }

        [Test]
        public void TestRoundTripKeepsOtherSettings()
        {
            var gifs = new[] { createGif("https://tenor.com/a", 1), createGif("https://tenor.com/b", 2) };

            byte[] written = DiscordFavouriteGifs.Write(other_settings, gifs);
            var read = DiscordFavouriteGifs.Read(written);

            Assert.That(read.Select(g => g.Url), Is.EqualTo(new[] { "https://tenor.com/b", "https://tenor.com/a" }));
            Assert.That(read[0].Src, Is.EqualTo("https://media.tenor.com/b.mp4"));
            Assert.That(read[0].Width, Is.EqualTo(498));
            Assert.That(read[0].Height, Is.EqualTo(280));
            Assert.That(read[0].IsVideo, Is.True);

            Assert.That(written.Take(other_settings.Length), Is.EqualTo(other_settings));

            // Replacing the favourites again keeps the other settings once, without the old favourites.
            byte[] removed = DiscordFavouriteGifs.Write(written, gifs.Take(1));
            Assert.That(DiscordFavouriteGifs.Read(removed).Single().Url, Is.EqualTo("https://tenor.com/a"));
            Assert.That(removed.Take(other_settings.Length), Is.EqualTo(other_settings));
            Assert.That(DiscordFavouriteGifs.Write(removed, Array.Empty<DiscordGif>()), Is.EqualTo(other_settings.Concat(new byte[] { 0x12, 0x00 })));
        }

        private static DiscordGif createGif(string url, int order) => new DiscordGif
        {
            Url = url,
            Src = $"https://media.tenor.com/{url[^1]}.mp4",
            Width = 498,
            Height = 280,
            IsVideo = true,
            Order = order,
        };
    }
}
