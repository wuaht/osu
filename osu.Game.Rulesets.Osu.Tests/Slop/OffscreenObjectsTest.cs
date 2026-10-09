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
    public class OffscreenObjectsTest
    {
        [Test]
        public void TestCircleInsidePlayfield()
        {
            Assert.That(OffscreenObjects.IsOffscreen(circle(new Vector2(256, 192)), out _), Is.False);

            // the playfield edges are still on screen.
            Assert.That(OffscreenObjects.IsOffscreen(circle(new Vector2(0, 0)), out _), Is.False);
            Assert.That(OffscreenObjects.IsOffscreen(circle(new Vector2(512, 384)), out _), Is.False);
        }

        [Test]
        public void TestCircleOffscreen()
        {
            var hitCircle = circle(new Vector2(256, 400));

            Assert.That(OffscreenObjects.IsOffscreen(hitCircle, out var bounds), Is.True);

            // the bounds include the radius.
            Assert.That(bounds.Bottom, Is.EqualTo(400 + hitCircle.Radius).Within(0.01));
            Assert.That(bounds.Left, Is.EqualTo(256 - hitCircle.Radius).Within(0.01));
        }

        [Test]
        public void TestBorderlineCircle()
        {
            // at circle size 4, the bottom edge is at 427.48, within a pixel of the bottom of the screen at 428.
            var hitCircle = new HitCircle { Position = new Vector2(160, 391) };
            hitCircle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { CircleSize = 4 });

            Assert.That(OffscreenObjects.IsOffscreen(hitCircle, out _), Is.True);

            // further away from the edge.
            hitCircle.Position = new Vector2(160, 389);
            Assert.That(OffscreenObjects.IsOffscreen(hitCircle, out _), Is.False);
        }

        [Test]
        public void TestSliderInsidePlayfield()
        {
            var slider = createSlider(new Vector2(100, 200),
                new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(150, -50)),
                new PathControlPoint(new Vector2(300, 0)));

            Assert.That(OffscreenObjects.IsOffscreen(slider, out _), Is.False);
        }

        [Test]
        public void TestSliderBodyOffscreenWithHeadAndTailInside()
        {
            // the arc bulges far above the top of the screen, while head and tail are inside the playfield.
            var slider = createSlider(new Vector2(100, 40),
                new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(150, -120)),
                new PathControlPoint(new Vector2(300, 0)));

            Assert.That(OffscreenObjects.IsOffscreen(circle(slider.Position), out _), Is.False, "head is offscreen");
            Assert.That(OffscreenObjects.IsOffscreen(circle(slider.EndPosition), out _), Is.False, "tail is offscreen");

            Assert.That(OffscreenObjects.IsOffscreen(slider, out var bounds), Is.True);
            Assert.That(bounds.Top, Is.LessThan(OffscreenObjects.MIN_Y));
        }

        [Test]
        public void TestSpinnerNeverOffscreen()
        {
            Assert.That(OffscreenObjects.IsOffscreen(new Spinner { Position = new Vector2(-500) }, out _), Is.False);
        }

        private static HitCircle circle(Vector2 position)
        {
            var hitCircle = new HitCircle { Position = position };
            hitCircle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return hitCircle;
        }

        private static Slider createSlider(Vector2 position, params PathControlPoint[] controlPoints)
        {
            var slider = new Slider
            {
                Position = position,
                Path = new SliderPath(controlPoints),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return slider;
        }
    }
}
