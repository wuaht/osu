// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Timing;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class DivisorTimingPlacementTest
    {
        [Test]
        public void TestPlacesOntoHalfBeatInFirstBar()
        {
            var beatmap = createBeatmap((0, 500), (8000, 500));

            var preview = VariableTiming.CalculatePlacement(beatmap.ControlPointInfo, 1240, divisor: 2);

            Assert.That(preview.PreviousBeats, Is.EqualTo(2.5));
            Assert.That(preview.Time, Is.EqualTo(1240));
            Assert.That(preview.PreviousBeatLength, Is.EqualTo(1240 / 2.5).Within(0.001));
        }

        [Test]
        public void TestWholeBeatsWithoutDivisor()
        {
            var beatmap = createBeatmap((0, 500), (8000, 500));

            var preview = VariableTiming.CalculatePlacement(beatmap.ControlPointInfo, 1240);

            Assert.That(preview.PreviousBeats, Is.EqualTo(2));
        }

        [Test]
        public void TestPlacesHalfBeatAtStartOfItsBar()
        {
            var beatmap = createBeatmap((0, 500), (8000, 500));

            // 10.5 beats: the timing point goes onto the bar line at 8 beats, and the clicked half beat is 2.5 beats into the bar.
            var preview = VariableTiming.CalculatePlacement(beatmap.ControlPointInfo, 5250, divisor: 2);

            Assert.That(preview.AnchorBeat, Is.EqualTo(2.5));
            Assert.That(preview.Time, Is.EqualTo(4000).Within(0.001));
        }

        [Test]
        public void TestMovingKeepsHalfBeats()
        {
            var beatmap = createBeatmap((0, 500), (8000, 500));

            var placed = VariableTiming.TimingPointMove.Place(beatmap, 1250, false, divisor: 2);
            double previousBeatLength = placed.Previous!.BeatLength;
            double beatLength = placed.Point.BeatLength;

            var move = VariableTiming.TimingPointMove.Begin(beatmap, placed.Point, false, 2);
            move.MoveTo(placed.Point.Time);

            Assert.That(move.PreviousBeats, Is.EqualTo(2.5));
            Assert.That(move.Previous!.BeatLength, Is.EqualTo(previousBeatLength).Within(0.001));
            Assert.That(move.Point.BeatLength, Is.EqualTo(beatLength).Within(0.001));
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
