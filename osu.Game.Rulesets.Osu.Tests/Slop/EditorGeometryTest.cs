// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class EditorGeometryTest
    {
        [TestCase(0, 100, 50, 100, -50)]
        [TestCase(90, 100, 50, -100, 50)]
        [TestCase(45, 100, 0, 0, 100)]
        [TestCase(180, 100, 50, 100, -50)]
        [TestCase(30, 0, 0, 0, 0)]
        public void TestFlippedPosition(float degrees, float x, float y, float expectedX, float expectedY)
        {
            Vector2 origin = new Vector2(256, 192);
            Vector2 flipped = OsuSelectionAngledFlipHandler.GetFlippedPosition(origin + new Vector2(x, y), origin, degrees);

            Assert.That(flipped.X, Is.EqualTo(origin.X + expectedX).Within(0.01));
            Assert.That(flipped.Y, Is.EqualTo(origin.Y + expectedY).Within(0.01));
        }

        [Test]
        public void TestFlippingTwiceRestoresPosition()
        {
            Vector2 position = new Vector2(123, 45);
            Vector2 origin = new Vector2(200, 300);

            Vector2 restored = OsuSelectionAngledFlipHandler.GetFlippedPosition(OsuSelectionAngledFlipHandler.GetFlippedPosition(position, origin, 37), origin, 37);

            Assert.That(Vector2.Distance(restored, position), Is.LessThan(0.01f));
        }

        [TestCase(10, 3, 10, 0)]
        [TestCase(-3, 10, 0, 10)]
        [TestCase(-10, -9, -10, 0)]
        [TestCase(5, 5, 0, 5)]
        [TestCase(0, 0, 0, 0)]
        public void TestSnapToAxis(float x, float y, float expectedX, float expectedY)
            => Assert.That(OsuBlueprintContainer.SnapToAxis(new Vector2(x, y)), Is.EqualTo(new Vector2(expectedX, expectedY)));

        [Test]
        public void TestTrueEndOfLongSlider()
        {
            // 280 osu!pixels at the default velocity of 0.14 osu!pixels per millisecond take 2000ms.
            var slider = createSlider(280, 0);

            Assert.That(slider.Duration, Is.EqualTo(2000).Within(0.01));
            Assert.That(TrueSliderEndOverlay.GetTrueEndTime(slider), Is.EqualTo(1000 + 2000 + SliderEventGenerator.TAIL_LENIENCY).Within(0.01));
            Assert.That(TrueSliderEndOverlay.GetTrueEndPosition(slider).X, Is.EqualTo(100 + 280 * (1 + SliderEventGenerator.TAIL_LENIENCY / 2000)).Within(0.1));
        }

        [Test]
        public void TestTrueEndOfShortSlider()
        {
            // the tail of very short sliders is judged at half of their duration.
            var slider = createSlider(7, 0);

            Assert.That(TrueSliderEndOverlay.GetTrueEndTime(slider), Is.EqualTo(1000 + slider.Duration / 2).Within(0.01));
            Assert.That(TrueSliderEndOverlay.GetTrueEndPosition(slider).X, Is.EqualTo(103.5).Within(0.1));
        }

        [Test]
        public void TestTrueEndOfRepeatingSlider()
        {
            // the slider ends where it started, so the true end lies shortly before the head.
            var slider = createSlider(280, 1);

            Assert.That(TrueSliderEndOverlay.GetTrueEndTime(slider), Is.EqualTo(1000 + 4000 + SliderEventGenerator.TAIL_LENIENCY).Within(0.01));
            Assert.That(TrueSliderEndOverlay.GetTrueEndPosition(slider).X, Is.EqualTo(100 - 280 * SliderEventGenerator.TAIL_LENIENCY / 2000).Within(0.1));
        }

        [Test]
        public void TestTrueEndIncludesStacking()
        {
            var slider = createSlider(280, 0);
            slider.StackHeight = 2;

            Assert.That(TrueSliderEndOverlay.GetTrueEndPosition(slider).Y, Is.EqualTo(slider.StackedPosition.Y).Within(0.01));
            Assert.That(slider.StackedPosition.Y, Is.Not.EqualTo(slider.Position.Y));
        }

        private static Slider createSlider(float length, int repeats)
        {
            var slider = new Slider
            {
                StartTime = 1000,
                Position = new Vector2(100, 200),
                RepeatCount = repeats,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(length, 0) }),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return slider;
        }
    }
}
