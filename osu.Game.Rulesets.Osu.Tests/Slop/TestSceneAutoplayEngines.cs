// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Replays.AutoPlus;
using osu.Game.Rulesets.Osu.Replays.Realistic;
using osu.Game.Rulesets.Osu.Tests.Mods;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneAutoplayEngines : OsuModTestScene
    {
        protected override bool AllowFail => true;

        private static readonly OsuSetting[] reset_settings =
        {
            OsuSetting.SlopAutoPlusCubicSplineInterpolation,
            OsuSetting.SlopAutoPlusHalfCircleStreamInterpolation,
            OsuSetting.SlopAutoPlusAngularStreamInterpolation,
            OsuSetting.SlopAutoPlusFollowSliders,
            OsuSetting.SlopAutoPlusHalfCircleInterpolation,
            OsuSetting.SlopAutoPlusRotation,
            OsuSetting.SlopAutoPlusBounceBorder,
            OsuSetting.SlopAutoPlusZeroInterpolation,
        };

        [TestCase(SlopAutoplayEngine.Default)]
        [TestCase(SlopAutoplayEngine.Pippi)]
        [TestCase(SlopAutoplayEngine.AutoPlus)]
        [TestCase(SlopAutoplayEngine.Realistic)]
        public void TestPerfectPlay(SlopAutoplayEngine engine)
        {
            setEngine(engine, false);
            runPerfectPlay();
        }

        [TestCase(AutoPlusTagSplitMode.Combo)]
        [TestCase(AutoPlusTagSplitMode.Colour)]
        [TestCase(AutoPlusTagSplitMode.Object)]
        public void TestTagCursors(AutoPlusTagSplitMode mode)
        {
            setEngine(SlopAutoplayEngine.AutoPlus, true);
            AddStep("set split mode", () => LocalConfig.SetValue(OsuSetting.SlopAutoPlusTagSplit, mode));
            AddStep("set cursor count", () => LocalConfig.SetValue(OsuSetting.SlopAutoPlusTagCount, 3));
            runPerfectPlay();
            AddAssert("three tag cursors", () => this.ChildrenOfType<AutoPlusTagCursorContainer>().Single().CursorCount, () => Is.EqualTo(3));
        }

        [Test]
        public void TestAllInterpolationsDisabled()
        {
            setEngine(SlopAutoplayEngine.AutoPlus, false);
            AddStep("disable interpolations", () =>
            {
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusCubicSplineInterpolation, false);
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusHalfCircleStreamInterpolation, false);
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusAngularStreamInterpolation, false);
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusFollowSliders, false);
            });
            runPerfectPlay();
        }

        [Test]
        public void TestHalfCircleAndRotation()
        {
            setEngine(SlopAutoplayEngine.AutoPlus, false);
            AddStep("enable half circle and rotation", () =>
            {
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusHalfCircleInterpolation, true);
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusRotation, true);
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusBounceBorder, true);
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusZeroInterpolation, true);
            });
            runPerfectPlay();
        }

        [Test]
        public void TestGeneratedFramesAreValid()
        {
            AddAssert("frames finite and ordered", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = createBeatmap();
                beatmap.BeatmapInfo.Ruleset = ruleset.RulesetInfo;

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                var processor = ruleset.CreateBeatmapProcessor(converted);
                processor?.PreProcess();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);
                processor?.PostProcess();

                var frames = new OsuAutoPlusGenerator(converted, new List<Mod>()).Generate().Frames.Cast<OsuReplayFrame>().ToList();

                for (int i = 1; i < frames.Count; i++)
                {
                    if (frames[i].Time < frames[i - 1].Time)
                        return false;
                }

                return frames.Count > 0 && frames.All(f => float.IsFinite(f.Position.X) && float.IsFinite(f.Position.Y));
            });
        }

        [Test]
        public void TestRealisticCursorDoesNotTeleport()
        {
            AddAssert("no sudden jumps", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = createSliderBeatmap();
                beatmap.BeatmapInfo.Ruleset = ruleset.RulesetInfo;

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                var processor = ruleset.CreateBeatmapProcessor(converted);
                processor?.PreProcess();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);
                processor?.PostProcess();

                var frames = new OsuRealisticAutoGenerator(converted, new List<Mod>()).Generate().Frames.Cast<OsuReplayFrame>().ToList();

                // a teleport shows as a frame which moves much further than the frames around it would suggest.
                double maxRatio = 0;

                for (int i = 2; i < frames.Count - 1; i++)
                {
                    double dt = frames[i].Time - frames[i - 1].Time;
                    if (dt <= 0)
                        continue;

                    float step = Vector2.Distance(frames[i].Position, frames[i - 1].Position);
                    double neighbourSpeed = Math.Max(Vector2.Distance(frames[i - 1].Position, frames[i - 2].Position) / Math.Max(frames[i - 1].Time - frames[i - 2].Time, 1),
                        Vector2.Distance(frames[i + 1].Position, frames[i].Position) / Math.Max(frames[i + 1].Time - frames[i].Time, 1));

                    // ignore small movements, which can't be seen as jumps.
                    if (step < 8)
                        continue;

                    double ratio = step / dt / Math.Max(neighbourSpeed, 0.05);
                    TestContext.WriteLine($"{frames[i].Time:F1}: step {step:F1} dt {dt:F1} ratio {ratio:F2}");
                    maxRatio = Math.Max(maxRatio, ratio);
                }

                TestContext.WriteLine($"max ratio {maxRatio:F2}");
                return maxRatio < 3;
            });
        }

        [Test]
        public void TestRealisticCursorFlowsThroughStreams()
        {
            AddAssert("cursor flows through streams", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = createBeatmap();
                beatmap.BeatmapInfo.Ruleset = ruleset.RulesetInfo;

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                var processor = ruleset.CreateBeatmapProcessor(converted);
                processor?.PreProcess();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);
                processor?.PostProcess();

                var frames = new OsuRealisticAutoGenerator(converted, new List<Mod>()).Generate().Frames.Cast<OsuReplayFrame>().ToList();

                // the stream is the 12 circles after the 6 jumps. leave out its first and last notes, where the cursor speeds up and slows down.
                var stream = converted.HitObjects.Skip(6).Take(12).ToList();
                double start = stream[2].StartTime;
                double end = stream[^3].StartTime;

                // the cursor's speed, measured over windows of 30ms.
                var speeds = new List<double>();

                for (double time = start; time < end; time += 30)
                    speeds.Add(Vector2.Distance(positionAt(frames, time), positionAt(frames, time + 30)) / 30);

                double average = speeds.Average();
                double minimum = speeds.Min();
                double maximum = speeds.Max();

                TestContext.WriteLine($"stream speed average {average:F3} min {minimum:F3} max {maximum:F3}");

                // streams are slightly shaky, but the cursor never stops between objects.
                return minimum > average * 0.4 && maximum < average * 2.2;
            });
        }

        private static Vector2 positionAt(List<OsuReplayFrame> frames, double time)
        {
            int index = frames.FindLastIndex(f => f.Time <= time);
            var from = frames[index];
            var to = frames[Math.Min(index + 1, frames.Count - 1)];

            return to.Time > from.Time ? Vector2.Lerp(from.Position, to.Position, (float)((time - from.Time) / (to.Time - from.Time))) : from.Position;
        }

        private void setEngine(SlopAutoplayEngine engine, bool tag)
        {
            AddStep($"set engine to {engine}", () =>
            {
                SlopAutoplaySettings.Config = LocalConfig;

                foreach (var setting in reset_settings)
                    LocalConfig.GetBindable<bool>(setting).SetDefault();

                LocalConfig.SetValue(OsuSetting.SlopAutoplayEngine, engine);
                LocalConfig.SetValue(OsuSetting.SlopAutoPlusTag, tag);
            });
        }

        private void runPerfectPlay()
        {
            CreateModTest(new ModTestData
            {
                Autoplay = true,
                CreateBeatmap = createBeatmap,
                PassCondition = () => Player.ScoreProcessor.HasCompleted.Value
                                      && Player.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.Miss) == 0
                                      && Player.ScoreProcessor.Statistics.GetValueOrDefault(HitResult.LargeTickMiss) == 0
                                      && Player.ScoreProcessor.Accuracy.Value == 1,
            });
        }

        /// <summary>
        /// Jumps onto sliders of different speeds and lengths, including short kick and buzz sliders.
        /// </summary>
        private static Beatmap createSliderBeatmap()
        {
            var objects = new List<HitObject>();
            double time = 500;

            for (int i = 0; i < 24; i++)
            {
                objects.Add(new HitCircle { StartTime = time, Position = new Vector2(i % 2 == 0 ? 80 : 420, 300), NewCombo = true });
                time += 200;

                double velocity = new[] { 0.6, 1.5, 3 }[i % 3];
                float length = new[] { 30f, 60f, 200f, 25f }[i % 4];

                objects.Add(new Slider
                {
                    StartTime = time,
                    Position = new Vector2(i % 2 == 0 ? 300 : 120, 80),
                    RepeatCount = length <= 30 && i % 5 == 0 ? 4 : 0,
                    SliderVelocityMultiplier = velocity,
                    Path = new SliderPath(new[]
                    {
                        new PathControlPoint(Vector2.Zero, PathType.LINEAR),
                        new PathControlPoint(new Vector2(length * 0.7f, length * 0.7f)),
                    }),
                });

                time += 1000;
            }

            var beatmap = new Beatmap
            {
                HitObjects = objects,
                BeatmapInfo = { Difficulty = { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 } },
            };

            // 200 bpm, so that no slider lasts until the next object.
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 300 });
            return beatmap;
        }

        private static Beatmap createBeatmap()
        {
            var objects = new List<HitObject>();
            double time = 500;

            // jumps
            for (int i = 0; i < 6; i++, time += 300)
                objects.Add(new HitCircle { StartTime = time, Position = new Vector2(i % 2 == 0 ? 60 : 450, i % 3 == 0 ? 60 : 320), NewCombo = i % 2 == 0 });

            // stream
            for (int i = 0; i < 12; i++, time += 90)
                objects.Add(new HitCircle { StartTime = time, Position = new Vector2(100 + i * 25, 192 + (i % 2) * 10), NewCombo = i == 0 });

            time += 300;

            // stacked notes
            for (int i = 0; i < 3; i++, time += 150)
                objects.Add(new HitCircle { StartTime = time, Position = new Vector2(256, 192) });

            time += 300;

            // sliders with repeats and ticks
            for (int i = 0; i < 3; i++)
            {
                objects.Add(new Slider
                {
                    StartTime = time,
                    Position = new Vector2(100 + i * 100, 100),
                    NewCombo = true,
                    RepeatCount = i,
                    SliderVelocityMultiplier = 3,
                    Path = new SliderPath(new[]
                    {
                        new PathControlPoint(Vector2.Zero, PathType.BEZIER),
                        new PathControlPoint(new Vector2(80, 150)),
                        new PathControlPoint(new Vector2(160, 0)),
                    }),
                });

                time += 800 + i * 700;
            }

            objects.Add(new Spinner { StartTime = time, EndTime = time + 1500, Position = new Vector2(256, 192), NewCombo = true });
            time += 2000;

            objects.Add(new HitCircle { StartTime = time, Position = new Vector2(400, 300), NewCombo = true });

            return new Beatmap
            {
                HitObjects = objects,
                BeatmapInfo = { Difficulty = { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 } },
            };
        }
    }
}
