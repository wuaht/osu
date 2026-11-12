// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        public void TestCircleLiesOnAnchor()
        {
            var stream = createTwoSegmentStream();
            var points = stream.Path.Path.ControlPoints;

            points[1].Position = new Vector2(210, 0);
            points[2].Position = new Vector2(410, 0);

            float[] xs = layout(stream);

            // the first section is stretched slightly so that its last circle lies on the anchor at 210.
            Assert.That(xs, Is.EqualTo(new float[] { 0, 26.25f, 52.5f, 78.75f, 105, 131.25f, 157.5f, 183.75f, 210, 260, 310, 360, 410 }).Within(0.01f));
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
        public void TestChangingDensityKeepsFollowingSections()
        {
            var stream = new EditorStream { BeatDivisor = 4 };
            var points = stream.Path.Path.ControlPoints;

            // the following anchors have no density of their own, so they continue the first one's.
            points.Add(new PathControlPoint(Vector2.Zero, PathType.LINEAR));
            stream.SetDensity(points[0], 2);
            points.Add(new PathControlPoint(new Vector2(200, 0), PathType.LINEAR));
            points.Add(new PathControlPoint(new Vector2(400, 0)));

            stream.SetDensity(points[0], 3);

            Assert.That(stream.GetDensity(points[1]), Is.EqualTo(2));
            Assert.That(stream.GetDensity(points[2]), Is.EqualTo(2));
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

        [Test]
        public void TestTrimmedPartsKeepTheirCircles()
        {
            var stream = createTwoSegmentStream();
            float[] full = layout(stream);

            var part = stream.Clone()!;
            part.TrimStart = 2;
            part.TrimEnd = 1;

            var parsed = EditorStream.Parse(part.Serialise())!;

            Assert.That(parsed.TrimStart, Is.EqualTo(2));
            Assert.That(parsed.TrimEnd, Is.EqualTo(1));
            Assert.That(layout(parsed), Is.EqualTo(full[2..^1]).Within(0.01f));

            // trimming more than the stream holds still keeps a circle.
            parsed.TrimEnd = 1000;
            Assert.That(layout(parsed), Has.Length.EqualTo(1));
        }

        [Test]
        public void TestPartsOnlyCoverTheirCircles()
        {
            var stream = createTwoSegmentStream();
            stream.SetAccelerates(stream.Path.Path.ControlPoints[1], true);
            var full = stream.Generate(new ControlPointInfo(), 1);

            // cut inside the first segment, and at the middle anchor (x = 200).
            int anchorIndex = full.FindIndex(c => Math.Abs(c.Position.X - 200) < 0.01f);

            foreach (var (first, last) in new[] { (0, 3), (5, full.Count - 1), (anchorIndex, full.Count - 1), (2, anchorIndex) })
            {
                var part = stream.CreatePart(first, last, new ControlPointInfo(), 1)!;
                var expected = full.GetRange(first, last - first + 1);
                var actual = part.Generate(new ControlPointInfo(), 1);

                Assert.That(actual.Select(c => c.Position.X), Is.EqualTo(expected.Select(c => c.Position.X)).Within(0.5f), $"{first}..{last}");
                Assert.That(actual.Select(c => c.Time), Is.EqualTo(expected.Select(c => c.Time)).Within(0.01), $"{first}..{last}");
                Assert.That(part.Path.Position.X + part.Path.Path.ControlPoints[^1].Position.X, Is.EqualTo(expected[^1].Position.X).Within(0.01f));
            }
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
