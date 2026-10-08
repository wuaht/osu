// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class PatternSnappingTest
    {
        /// <summary>
        /// The height of an equilateral triangle with a side length of 150.
        /// </summary>
        private static readonly float triangle_height = 150 * MathF.Sqrt(3) / 2;

        [Test]
        public void TestVisualSpacingTriangle()
        {
            var points = visualSpacing(new HitCircle { Position = new Vector2(100, 200) }, new HitCircle { Position = new Vector2(250, 200) });

            Assert.That(points, Has.Count.EqualTo(2));
            assertContains(points, new Vector2(175, 200 - triangle_height));
            assertContains(points, new Vector2(175, 200 + triangle_height));

            foreach (var point in points)
            {
                Assert.That(Vector2.Distance(point, new Vector2(100, 200)), Is.EqualTo(150).Within(0.01));
                Assert.That(Vector2.Distance(point, new Vector2(250, 200)), Is.EqualTo(150).Within(0.01));
            }
        }

        [Test]
        public void TestVisualSpacingRequiresDistanceBetweenObjects()
        {
            // overlapping objects (closer than two radii).
            Assert.That(visualSpacing(new HitCircle { Position = new Vector2(100, 200) }, new HitCircle { Position = new Vector2(150, 200) }), Is.Empty);

            // objects too far apart.
            Assert.That(visualSpacing(new HitCircle { Position = new Vector2(50, 200) }, new HitCircle { Position = new Vector2(450, 200) }), Is.Empty);
        }

        [Test]
        public void TestVisualSpacingExcludesPointsOutsidePlayfield()
        {
            // one of the triangles lies above the top of the playfield.
            var points = visualSpacing(new HitCircle { Position = new Vector2(100, 50) }, new HitCircle { Position = new Vector2(250, 50) });

            Assert.That(points, Has.Count.EqualTo(1));
            assertContains(points, new Vector2(175, 50 + triangle_height));
        }

        [Test]
        public void TestVisualSpacingIgnoresHeadAndTailOfSameSlider()
        {
            var slider = createSlider(new Vector2(100, 200), new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(150, 0)));

            Assert.That(visualSpacing(slider), Is.Empty);
        }

        [Test]
        public void TestVisualSpacingWithSliderTail()
        {
            var slider = createSlider(new Vector2(0, 200), new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(100, 0)));

            // the tail at (100, 200) forms triangles with the circle, the head at (0, 200) is too close to the circle.
            var points = visualSpacing(slider, new HitCircle { Position = new Vector2(250, 200) });

            Assert.That(points, Has.Count.EqualTo(2));
            assertContains(points, new Vector2(175, 200 - triangle_height));
        }

        [Test]
        public void TestBlanketCentre()
        {
            var slider = createSlider(new Vector2(100, 100),
                new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(100, 100)),
                new PathControlPoint(new Vector2(200, 0)));

            var points = blankets(slider);

            Assert.That(points, Has.Count.EqualTo(1));
            assertContains(points, new Vector2(200, 100));
        }

        [Test]
        public void TestBlanketCentresOfMultipleSegments()
        {
            var slider = createSlider(new Vector2(100, 200),
                new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(50, 50)),
                new PathControlPoint(new Vector2(100, 0), PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(150, -50)),
                new PathControlPoint(new Vector2(200, 0)));

            var points = blankets(slider);

            Assert.That(points, Has.Count.EqualTo(2));
            assertContains(points, new Vector2(150, 200));
            assertContains(points, new Vector2(250, 200));
        }

        [Test]
        public void TestNoBlanketForSegmentsNotDisplayedAsArc()
        {
            // perfect curves with more than three points are displayed as bezier curves.
            Assert.That(blankets(createSlider(new Vector2(100, 200),
                new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(50, 50)),
                new PathControlPoint(new Vector2(100, 50)),
                new PathControlPoint(new Vector2(150, 0)))), Is.Empty);

            // collinear points can't form an arc.
            Assert.That(blankets(createSlider(new Vector2(100, 200),
                new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(50, 0)),
                new PathControlPoint(new Vector2(100, 0)))), Is.Empty);

            // bezier curves are not arcs.
            Assert.That(blankets(createSlider(new Vector2(100, 100),
                new PathControlPoint(Vector2.Zero, PathType.BEZIER),
                new PathControlPoint(new Vector2(100, 100)),
                new PathControlPoint(new Vector2(200, 0)))), Is.Empty);
        }

        private static List<Vector2> visualSpacing(params OsuHitObject[] objects)
        {
            var output = new List<PatternSnapPoint>();
            PatternSnapping.AddVisualSpacingSnapPoints(objects, output);
            return output.Select(p => p.Position).ToList();
        }

        private static List<Vector2> blankets(params OsuHitObject[] objects)
        {
            var output = new List<PatternSnapPoint>();
            PatternSnapping.AddBlanketSnapPoints(objects, output);
            return output.Select(p => p.Position).ToList();
        }

        private static Slider createSlider(Vector2 position, params PathControlPoint[] controlPoints) => new Slider
        {
            Position = position,
            Path = new SliderPath(controlPoints),
        };

        private static void assertContains(IEnumerable<Vector2> points, Vector2 expected)
            => Assert.That(points.Any(p => Precision.AlmostEquals(p, expected, 0.01f)), $"Expected a snap point at {expected}, but got {string.Join(", ", points)}.");
    }
}
