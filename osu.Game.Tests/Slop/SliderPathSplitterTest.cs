// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class SliderPathSplitterTest
    {
        private static IEnumerable<PathControlPoint[]> paths()
        {
            yield return new[] { new PathControlPoint(Vector2.Zero, PathType.LINEAR), new PathControlPoint(new Vector2(100, 0)), new PathControlPoint(new Vector2(100, 100)) };
            yield return new[] { new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE), new PathControlPoint(new Vector2(100, 50)), new PathControlPoint(new Vector2(200, 0)) };
            yield return new[] { new PathControlPoint(Vector2.Zero, PathType.BEZIER), new PathControlPoint(new Vector2(50, 150)), new PathControlPoint(new Vector2(150, -50)), new PathControlPoint(new Vector2(200, 100)) };
            yield return new[] { new PathControlPoint(Vector2.Zero, PathType.CATMULL), new PathControlPoint(new Vector2(50, 80)), new PathControlPoint(new Vector2(120, 20)), new PathControlPoint(new Vector2(200, 90)) };
            yield return new[]
            {
                new PathControlPoint(Vector2.Zero, PathType.BEZIER), new PathControlPoint(new Vector2(60, 100)), new PathControlPoint(new Vector2(120, 0), PathType.LINEAR),
                new PathControlPoint(new Vector2(200, 50))
            };
        }

        [TestCaseSource(nameof(paths))]
        public void TestSplitFollowsOriginalPath(PathControlPoint[] points)
        {
            var path = new SliderPath(points);

            foreach (double proportion in new[] { 0.1, 0.37, 0.5, 0.83 })
            {
                double distance = path.Distance * proportion;
                var (headPoints, tailPoints) = SliderPathSplitter.Split(path, distance);

                var head = new SliderPath(headPoints, distance);
                var tail = new SliderPath(tailPoints.Select(p => new PathControlPoint(p.Position - tailPoints[0].Position, p.Type)).ToArray(), path.Distance - distance);

                Assert.That(head.Distance, Is.EqualTo(distance).Within(0.5));
                Assert.That(tail.Distance, Is.EqualTo(path.Distance - distance).Within(0.5));

                for (int i = 0; i <= 10; i++)
                {
                    double t = i / 10.0;

                    assertClose(head.PositionAt(t), path.PositionAt(t * proportion));
                    assertClose(tailPoints[0].Position + tail.PositionAt(t), path.PositionAt(proportion + t * (1 - proportion)));
                }
            }
        }

        private static void assertClose(Vector2 actual, Vector2 expected) => Assert.That(Vector2.Distance(actual, expected), Is.LessThan(1.5f), $"{actual} != {expected}");
    }
}
