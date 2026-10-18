// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Overlays.Visualizer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class BackgroundPaletteTest
    {
        private const int size = 32;

        [Test]
        public void TestSolidColour()
        {
            var palette = BackgroundPalette.FromPixels(solid(new Rgba32(255, 0, 0)), size);

            Assert.That(palette.IsChromatic, Is.True);
            Assert.That(circularDistance(palette.DominantHue, 0), Is.LessThan(0.01f));
            Assert.That(palette.AverageLuminance, Is.EqualTo(0.2126f).Within(0.001f));
            Assert.That(palette.LeftLuminance, Is.EqualTo(0.2126f).Within(0.001f));
        }

        [Test]
        public void TestGreyIsNotChromatic()
        {
            var palette = BackgroundPalette.FromPixels(solid(new Rgba32(128, 128, 128)), size);

            Assert.That(palette.IsChromatic, Is.False);
        }

        [Test]
        public void TestDullColourIsNotChromatic()
        {
            var palette = BackgroundPalette.FromPixels(solid(new Rgba32(30, 20, 20)), size);

            Assert.That(palette.IsChromatic, Is.False);
        }

        [Test]
        public void TestMajorityHueWins()
        {
            // 70% blue, 30% red.
            var pixels = Enumerable.Range(0, size * size)
                                   .Select(i => i % 10 < 7 ? new Rgba32(0, 0, 255) : new Rgba32(255, 0, 0))
                                   .ToArray();

            var palette = BackgroundPalette.FromPixels(pixels, size);

            Assert.That(circularDistance(palette.DominantHue, 240 / 360f), Is.LessThan(0.01f));
        }

        [Test]
        public void TestSaturatedHueBeatsLargerDullArea()
        {
            // 60% dull brown, 40% vivid cyan.
            var pixels = Enumerable.Range(0, size * size)
                                   .Select(i => i % 10 < 6 ? new Rgba32(70, 55, 45) : new Rgba32(0, 255, 255))
                                   .ToArray();

            var palette = BackgroundPalette.FromPixels(pixels, size);

            Assert.That(circularDistance(palette.DominantHue, 180 / 360f), Is.LessThan(0.01f));
        }

        [Test]
        public void TestHueWrapsAround()
        {
            // hues just either side of red must average to red, not to cyan.
            var pixels = Enumerable.Range(0, size * size)
                                   .Select(i => i % 2 == 0 ? hsv(355) : hsv(5))
                                   .ToArray();

            var palette = BackgroundPalette.FromPixels(pixels, size);

            Assert.That(circularDistance(palette.DominantHue, 0), Is.LessThan(0.01f));
        }

        [Test]
        public void TestLeftLuminance()
        {
            // left half white, right half black.
            var pixels = Enumerable.Range(0, size * size)
                                   .Select(i => i % size < size / 2 ? new Rgba32(255, 255, 255) : new Rgba32(0, 0, 0))
                                   .ToArray();

            var palette = BackgroundPalette.FromPixels(pixels, size);

            Assert.That(palette.LeftLuminance, Is.EqualTo(1).Within(0.001f));
            Assert.That(palette.AverageLuminance, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void TestTextColourFollowsBrightness()
        {
            var bright = BackgroundPalette.FromPixels(solid(new Rgba32(240, 240, 230)), size);
            var dark = BackgroundPalette.FromPixels(solid(new Rgba32(20, 30, 40)), size);

            Assert.That(bright.PrefersDarkText(0.25f), Is.True);
            Assert.That(bright.PrefersDarkText(0.8f), Is.False, "a heavily dimmed background is dark");
            Assert.That(dark.PrefersDarkText(0), Is.False);
        }

        [Test]
        public void TestAccentColourIsDeterministic()
        {
            var palette = BackgroundPalette.FromPixels(solid(new Rgba32(40, 200, 90)), size);

            for (int seed = 0; seed < 50; seed++)
                Assert.That(palette.ChooseAccentColour(seed), Is.EqualTo(palette.ChooseAccentColour(seed)));
        }

        [Test]
        public void TestAccentColourIsAlwaysBright()
        {
            var palettes = new[]
            {
                BackgroundPalette.FromPixels(solid(new Rgba32(40, 200, 90)), size),
                BackgroundPalette.FromPixels(solid(new Rgba32(128, 128, 128)), size),
                BackgroundPalette.FromPixels(solid(new Rgba32(10, 5, 5)), size),
                BackgroundPalette.DEFAULT,
            };

            foreach (var palette in palettes)
            {
                for (int seed = 0; seed < 200; seed++)
                {
                    var colour = palette.ChooseAccentColour(seed);
                    Assert.That(Math.Max(colour.R, Math.Max(colour.G, colour.B)), Is.EqualTo(1).Within(0.001f));
                }
            }
        }

        [Test]
        public void TestAccentColourVariety()
        {
            var palette = BackgroundPalette.FromPixels(solid(new Rgba32(255, 0, 0)), size);
            var colours = Enumerable.Range(0, 200).Select(palette.ChooseAccentColour).ToArray();

            Assert.That(colours.Any(c => c == Colour4.White), "monochrome is chosen sometimes");
            Assert.That(colours.Any(c => circularDistance(c.ToHSV().X, 0) < 0.01f && c != Colour4.White), "a fitting colour is chosen sometimes");
            Assert.That(colours.Any(c => circularDistance(c.ToHSV().X, 0.5f) < 0.01f), "a complementary colour is chosen sometimes");
        }

        [Test]
        public void TestNonChromaticNeverUsesFittingColour()
        {
            var palette = BackgroundPalette.FromPixels(solid(new Rgba32(128, 120, 120)), size);
            var colours = Enumerable.Range(0, 200).Select(palette.ChooseAccentColour).ToArray();

            Assert.That(colours.Where(c => c != Colour4.White).All(c => circularDistance(c.ToHSV().X, (palette.DominantHue + 0.5f) % 1) < 0.01f));
        }

        [Test]
        public void TestFromImageDownscales()
        {
            using var image = new Image<Rgba32>(1920, 1080, new Rgba32(0, 0, 255));

            var palette = BackgroundPalette.FromImage(image);

            Assert.That(image.Width, Is.LessThanOrEqualTo(64));
            Assert.That(circularDistance(palette.DominantHue, 240 / 360f), Is.LessThan(0.01f));
        }

        [Test]
        public void TestInvalidInputReturnsDefault()
        {
            Assert.That(BackgroundPalette.FromPixels(ReadOnlySpan<Rgba32>.Empty, size), Is.EqualTo(BackgroundPalette.DEFAULT));
            Assert.That(BackgroundPalette.FromPixels(solid(new Rgba32(255, 0, 0)), 0), Is.EqualTo(BackgroundPalette.DEFAULT));
        }

        private static Rgba32[] solid(Rgba32 colour) => Enumerable.Repeat(colour, size * size).ToArray();

        private static Rgba32 hsv(float hueDegrees)
        {
            var colour = Colour4.FromHSV(hueDegrees / 360, 1, 1);
            return new Rgba32(colour.R, colour.G, colour.B);
        }

        private static float circularDistance(float a, float b)
        {
            float distance = Math.Abs(a - b) % 1;
            return Math.Min(distance, 1 - distance);
        }
    }
}
