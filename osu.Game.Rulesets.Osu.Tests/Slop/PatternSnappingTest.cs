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

            // objects far apart still form a triangle.
            Assert.That(visualSpacing(new HitCircle { Position = new Vector2(150, 300) }, new HitCircle { Position = new Vector2(350, 300) }),
                Has.One.Matches<Vector2>(p => Vector2.Distance(p, new Vector2(250, 300 - 100 * MathF.Sqrt(3))) < 0.1f));
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
        public void TestVisualSpacingWithHeadAndTailOfSameSlider()
        {
            var slider = createSlider(new Vector2(100, 200), new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(150, 0)));

            var points = visualSpacing(slider);

            Assert.That(points, Has.Count.EqualTo(2));
            assertContains(points, new Vector2(175, 200 - triangle_height));
            assertContains(points, new Vector2(175, 200 + triangle_height));
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

        [Test]
        public void TestLineContinuesInBothDirections()
        {
            var points = lines(new HitCircle { Position = new Vector2(200, 200) }, new HitCircle { Position = new Vector2(280, 240) });

            // both continuations and the midpoint.
            Assert.That(points, Has.Count.EqualTo(3));
            assertContains(points.Select(p => p.Position), new Vector2(360, 280));
            assertContains(points.Select(p => p.Position), new Vector2(120, 160));
            assertContains(points.Select(p => p.Position), new Vector2(240, 220));
        }

        [Test]
        public void TestLineGapIsFilled()
        {
            // the second object of a line is missing.
            var points = lines(
                new HitCircle { Position = new Vector2(100, 100) },
                new HitCircle { Position = new Vector2(260, 180) },
                new HitCircle { Position = new Vector2(340, 220) });

            var gap = points.Single(p => Precision.AlmostEquals(p.Position, new Vector2(180, 140), 0.01f));

            // the guide lines contain the whole line.
            Assert.That(gap.Line, Is.EqualTo(new[] { new Vector2(100, 100), new Vector2(180, 140), new Vector2(260, 180), new Vector2(340, 220) })
                                    .Or.EqualTo(new[] { new Vector2(340, 220), new Vector2(260, 180), new Vector2(180, 140), new Vector2(100, 100) }));
        }


        [Test]
        public void TestLineOfMoreThanTwoObjects()
        {
            var points = lines(
                new HitCircle { Position = new Vector2(100, 100) },
                new HitCircle { Position = new Vector2(180, 140) },
                new HitCircle { Position = new Vector2(260, 180) });

            // positions of existing objects and multiples of the spacing are not snap points, but the two midpoints are.
            Assert.That(points, Has.Count.EqualTo(4));
            assertContains(points.Select(p => p.Position), new Vector2(140, 120));
            assertContains(points.Select(p => p.Position), new Vector2(220, 160));

            var forward = points.Single(p => Precision.AlmostEquals(p.Position, new Vector2(340, 220), 0.01f));
            var backward = points.Single(p => Precision.AlmostEquals(p.Position, new Vector2(20, 60), 0.01f));

            // all objects of the line are part of the guide lines.
            Assert.That(forward.Line, Is.EqualTo(new[] { new Vector2(100, 100), new Vector2(180, 140), new Vector2(260, 180), new Vector2(340, 220) }));
            Assert.That(backward.Line, Is.EqualTo(new[] { new Vector2(260, 180), new Vector2(180, 140), new Vector2(100, 100), new Vector2(20, 60) }));
        }

        [Test]
        public void TestLineRequiresSpacing()
        {
            // stacked objects.
            Assert.That(lines(new HitCircle { Position = new Vector2(200, 200) }, new HitCircle { Position = new Vector2(210, 200) }), Is.Empty);

            // the gap between objects far apart is still filled.
            Assert.That(lines(new HitCircle { Position = new Vector2(50, 200) }, new HitCircle { Position = new Vector2(450, 200) }).Select(p => p.Position),
                Has.One.Matches<Vector2>(p => Vector2.Distance(p, new Vector2(250, 200)) < 0.1f));
        }

        [Test]
        public void TestLineIgnoresHeadAndTailOfSameSlider()
        {
            var slider = createSlider(new Vector2(100, 200), new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(150, 0)));

            Assert.That(lines(slider), Is.Empty);
        }

        private static List<LineSnapPoint> lines(params OsuHitObject[] objects)
        {
            var output = new List<PatternSnapPoint>();
            PatternSnapping.AddLineSnapPoints(objects, output);
            return output.Cast<LineSnapPoint>().ToList();
        }

        [Test]
        public void TestTriangleCentre()
        {
            var points = triangleCentres(
                new HitCircle { Position = new Vector2(100, 300) },
                new HitCircle { Position = new Vector2(250, 300) },
                new HitCircle { Position = new Vector2(175, 300 - triangle_height) });

            Assert.That(points, Has.Count.EqualTo(1));
            assertContains(points, new Vector2(175, 300 - triangle_height / 3));
        }

        [Test]
        public void TestTriangleCentreOfRoughlyEquilateralTriangle()
        {
            // the third object is placed slightly off, as when placing it by hand.
            var points = triangleCentres(
                new HitCircle { Position = new Vector2(100, 300) },
                new HitCircle { Position = new Vector2(250, 300) },
                new HitCircle { Position = new Vector2(185, 300 - triangle_height + 5) });

            Assert.That(points, Has.Count.EqualTo(1));
            assertContains(points, new Vector2((100 + 250 + 185) / 3f, (900 - triangle_height + 5) / 3));
        }

        [Test]
        public void TestNoTriangleCentreForOtherTriangles()
        {
            // a right-angled triangle.
            Assert.That(triangleCentres(
                new HitCircle { Position = new Vector2(100, 300) },
                new HitCircle { Position = new Vector2(250, 300) },
                new HitCircle { Position = new Vector2(100, 150) }), Is.Empty);

            // objects on a line.
            Assert.That(triangleCentres(
                new HitCircle { Position = new Vector2(100, 300) },
                new HitCircle { Position = new Vector2(200, 300) },
                new HitCircle { Position = new Vector2(300, 300) }), Is.Empty);
        }

        [Test]
        public void TestNoTriangleCentreForOverlappingObjects()
        {
            // an equilateral triangle whose objects overlap each other.
            Assert.That(triangleCentres(
                new HitCircle { Position = new Vector2(100, 300) },
                new HitCircle { Position = new Vector2(130, 300) },
                new HitCircle { Position = new Vector2(115, 300 - 15 * MathF.Sqrt(3)) }), Is.Empty);
        }

        [Test]
        public void TestNoTriangleCentreWithHeadAndTailOfSameSlider()
        {
            // the head and tail of the slider form an equilateral triangle with the circle, but a slider is only one corner.
            var slider = createSlider(new Vector2(100, 300), new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(150, 0)));

            Assert.That(triangleCentres(slider, new HitCircle { Position = new Vector2(175, 300 - triangle_height) }), Is.Empty);
        }

        [Test]
        public void TestTriangleCentreWithSliderTail()
        {
            var slider = createSlider(new Vector2(0, 300), new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(100, 0)));

            var points = triangleCentres(
                slider,
                new HitCircle { Position = new Vector2(250, 300) },
                new HitCircle { Position = new Vector2(175, 300 - triangle_height) });

            Assert.That(points, Has.Count.EqualTo(1));
            assertContains(points, new Vector2(175, 300 - triangle_height / 3));
        }

        [Test]
        public void TestNoTriangleCentreAtOccupiedPosition()
        {
            Assert.That(triangleCentres(
                new HitCircle { Position = new Vector2(100, 300) },
                new HitCircle { Position = new Vector2(250, 300) },
                new HitCircle { Position = new Vector2(175, 300 - triangle_height) },
                new HitCircle { Position = new Vector2(175, 300 - triangle_height / 3) }), Is.Empty);
        }

        [Test]
        public void TestTriangleCentreGuideLines()
        {
            var output = new List<PatternSnapPoint>();
            PatternSnapping.AddTriangleCentreSnapPoints(new OsuHitObject[]
            {
                new HitCircle { Position = new Vector2(100, 350) },
                new HitCircle { Position = new Vector2(400, 350) },
                new HitCircle { Position = new Vector2(250, 350 - 2 * triangle_height) },
            }, output);

            // three edges and three lines to the centre (the triangle is large enough for the lines to the centre to be visible between the objects).
            Assert.That(output.Single().CreateGuideLines().Count(), Is.EqualTo(6));
        }

        [TestCase(150, 150, 150, true)]
        [TestCase(150, 160, 170, true)]
        [TestCase(150, 150, 180, false)]
        [TestCase(0, 0, 0, false)]
        public void TestIsRoughlyEquilateral(float a, float b, float c, bool expected)
            => Assert.That(PatternSnapping.IsRoughlyEquilateral(a, b, c), Is.EqualTo(expected));

        private static List<Vector2> triangleCentres(params OsuHitObject[] objects)
        {
            var output = new List<PatternSnapPoint>();
            PatternSnapping.AddTriangleCentreSnapPoints(objects, output);
            return output.Select(p => p.Position).ToList();
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
