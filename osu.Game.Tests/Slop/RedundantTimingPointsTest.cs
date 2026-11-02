// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
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

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false), Is.EqualTo(2));
            Assert.That(beatmap.ControlPointInfo.TimingPoints.Select(p => p.Time), Is.EqualTo(new double[] { 0 }));
        }

        [Test]
        public void TestKeepsTimingPointBetweenBarLines()
        {
            // 6 beats after the first timing point, so it starts a bar which the first one doesn't.
            var beatmap = createBeatmap((0, 500), (3000, 500));

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false), Is.Zero);
        }

        [Test]
        public void TestKeepsTimingPointWithDifferentTempo()
        {
            var beatmap = createBeatmap((0, 500), (4000, 480), (8000, 500));

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false), Is.Zero);
        }

        [Test]
        public void TestKeepsTimingPointWithDifferentMeter()
        {
            var beatmap = createBeatmap((0, 500), (4000, 500));
            beatmap.ControlPointInfo.TimingPoints[1].TimeSignature = TimeSignature.SimpleTriple;

            Assert.That(VariableTiming.RemoveRedundantTimingPoints(beatmap, false), Is.Zero);
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
