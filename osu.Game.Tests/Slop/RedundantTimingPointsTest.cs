// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Timing;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class RedundantTimingPointsTest
    {
        [Test]
        public void TestRemovesTimingPointsOnBarLinesOfSameTempo()
        {
            var beatmap = createBeatmap((0, 500), (4000, 500), (8000, 500));

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false, 1), Is.EqualTo(2));
            Assert.That(beatmap.ControlPointInfo.TimingPoints.Select(p => p.Time), Is.EqualTo(new double[] { 0 }));
        }

        [Test]
        public void TestKeepsTimingPointBetweenBarLines()
        {
            // 6 beats after the first timing point, so it starts a bar which the first one doesn't.
            var beatmap = createBeatmap((0, 500), (3000, 500));

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false, 1), Is.Zero);
        }

        [Test]
        public void TestKeepsTimingPointWithDifferentTempo()
        {
            var beatmap = createBeatmap((0, 500), (4000, 480), (8000, 500));

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false, 1), Is.Zero);
        }

        [Test]
        public void TestKeepsTimingPointWithDifferentMeter()
        {
            var beatmap = createBeatmap((0, 500), (4000, 500));
            beatmap.ControlPointInfo.TimingPoints[1].TimeSignature = TimeSignature.SimpleTriple;

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false, 1), Is.Zero);
        }

        [Test]
        public void TestRemovesTimingPointWithinLeeway()
        {
            // 16 beats of 500 ms followed by beats of 500.2 ms move the last beat by less than 2 ms.
            var beatmap = createBeatmap((0, 500), (8000, 500.2), (12000, 500));

            // the timing point at 12000 lies on the merged beats exactly, so it goes as well.
            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false, 2), Is.EqualTo(2));
            Assert.That(beatmap.ControlPointInfo.TimingPoints.Select(p => p.Time), Is.EqualTo(new double[] { 0 }));
        }

        [Test]
        public void TestKeepsTimingPointOutsideLeeway()
        {
            var beatmap = createBeatmap((0, 500), (8000, 500.2), (12000, 500));

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false, 0.5), Is.Zero);
        }

        [Test]
        public void TestLastTimingPointChecksUntilEndOfBeatmap()
        {
            var beatmap = createBeatmap((0, 500), (4000, 501));
            beatmap.Add(new HitCircle { StartTime = 20000 });

            // the beats after the last timing point would drift by 1 ms per beat until the end.
            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false, 4), Is.Zero);
        }

        [Test]
        public void TestDriftDoesNotAddUpOverRemovedTimingPoints()
        {
            // every timing point is close to the one before it, but the tempo drifts by more than the leeway overall.
            var beatmap = createBeatmap((0, 500), (2000, 500.5), (4002, 501), (6006, 501.5), (8012, 502), (10020, 500));

            VariableTiming.RemoveRedundantTimingPoints(beatmap, false, 4);

            var points = beatmap.ControlPointInfo.TimingPoints;
            double[] originalTimes = { 2000, 4002, 6006, 8012, 10020 };

            foreach (double time in originalTimes)
            {
                var point = points.Last(p => p.Time <= time + 0.5);
                double beats = Math.Round((time - point.Time) / point.BeatLength);
                Assert.That(point.Time + beats * point.BeatLength, Is.EqualTo(time).Within(4.001), $"beat at {time}");
            }
        }

        [Test]
        public void TestKeepsHitObjectsOnTheirBeats()
        {
            var beatmap = createBeatmap((0, 500), (4000, 500), (8000, 500));
            var circle = new HitCircle { StartTime = 6000 };
            beatmap.Add(circle);

            VariableTiming.RemoveRedundantTimingPoints(beatmap, true, 1);

            Assert.That(circle.StartTime, Is.EqualTo(6000).Within(0.001));
        }

        private static EditorBeatmap createBeatmap(params (double time, double beatLength)[] timingPoints)
        {
            var osuBeatmap = new OsuBeatmap
            {
                BeatmapInfo =
                {
                    Ruleset = new OsuRuleset().RulesetInfo,
                },
            };

            foreach (var (time, beatLength) in timingPoints)
                osuBeatmap.ControlPointInfo.Add(time, new TimingControlPoint { BeatLength = beatLength });

            return new EditorBeatmap(osuBeatmap);
        }
    }
}
