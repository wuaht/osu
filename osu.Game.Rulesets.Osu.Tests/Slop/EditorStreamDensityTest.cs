// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit.Streams;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class EditorStreamDensityTest
    {
        [Test]
        public void TestSegmentsKeepTheirDensity()
        {
            var stream = createTwoSegmentStream();

            float[] xs = layout(stream);

            // 25 units apart up to the middle anchor at density 1, then 50 units apart at density 2.
            Assert.That(xs, Is.EqualTo(new float[] { 0, 25, 50, 75, 100, 125, 150, 175, 200, 250, 300, 350, 400 }).Within(0.01f));
        }

        [Test]
        public void TestDensityChangesAtAnchorBetweenCircles()
        {
            var stream = createTwoSegmentStream();
            var points = stream.Path.Path.ControlPoints;

            points[1].Position = new Vector2(210, 0);
            points[2].Position = new Vector2(410, 0);

            float[] xs = layout(stream);

            // the circle after 200 covers the remaining 10 units at density 1, and the rest of its step at density 2.
            Assert.That(xs, Is.EqualTo(new float[] { 0, 25, 50, 75, 100, 125, 150, 175, 200, 240, 290, 340, 390 }).Within(0.01f));
        }

        [Test]
        public void TestAcceleratingSegment()
        {
            var stream = createTwoSegmentStream();
            var points = stream.Path.Path.ControlPoints;

            stream.SetDensity(points[1], 1);
            stream.SetDensity(points[2], 2);
            stream.SetAccelerates(points[1], true);

            Assert.That(stream.UsesDensity(points[2]), Is.True);

            float[] xs = layout(stream);
            float[] spacings = xs.Zip(xs.Skip(1), (a, b) => b - a).ToArray();

            Assert.That(spacings.Take(8), Is.All.EqualTo(25).Within(0.01f));
            Assert.That(spacings.Skip(8), Is.Ordered.Ascending);
            Assert.That(spacings[^1], Is.GreaterThan(spacings[8]));
        }

        [Test]
        public void TestLastAnchorDensityUnusedWithoutAcceleration()
        {
            var stream = createTwoSegmentStream();
            var points = stream.Path.Path.ControlPoints;

            Assert.That(stream.UsesDensity(points[1]), Is.True);
            Assert.That(stream.UsesDensity(points[2]), Is.False);
        }

        [Test]
        public void TestSerialisationRoundTrip()
        {
            var stream = createTwoSegmentStream();
            var points = stream.Path.Path.ControlPoints;
            stream.SetAccelerates(points[1], true);

            string line = stream.Serialise();
            Assert.That(line.Split(','), Has.Length.EqualTo(7));

            var parsed = EditorStream.Parse(line)!;
            var parsedPoints = parsed.Path.Path.ControlPoints;

            Assert.That(parsed.GetAccelerates(parsedPoints[0]), Is.False);
            Assert.That(parsed.GetAccelerates(parsedPoints[1]), Is.True);
            Assert.That(parsed.GetDensity(parsedPoints[1]), Is.EqualTo(2));
        }

        [Test]
        public void TestLegacyLinesAccelerate()
        {
            var parsed = EditorStream.Parse("0,0,0,4,L:0:0|L:200:0|:400:0,1|2|2")!;
            var points = parsed.Path.Path.ControlPoints;

            Assert.That(parsed.GetAccelerates(points[0]), Is.True);
            Assert.That(parsed.GetAccelerates(points[1]), Is.True);
        }

        private static EditorStream createTwoSegmentStream()
        {
            var stream = new EditorStream { BeatDivisor = 4 };
            var points = stream.Path.Path.ControlPoints;

            points.Add(new PathControlPoint(Vector2.Zero, PathType.LINEAR));
            points.Add(new PathControlPoint(new Vector2(200, 0), PathType.LINEAR));
            points.Add(new PathControlPoint(new Vector2(400, 0)));

            stream.SetDensity(points[0], 1);
            stream.SetDensity(points[1], 2);
            stream.SetDensity(points[2], 2);

            return stream;
        }

        private static float[] layout(EditorStream stream)
            => stream.Generate(new ControlPointInfo(), 1).Select(c => c.Position.X).ToArray();
    }
}
