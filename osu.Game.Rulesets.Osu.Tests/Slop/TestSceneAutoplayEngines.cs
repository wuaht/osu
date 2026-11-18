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

        [Test]
        public void TestRealisticCursorWithSimultaneousObjects()
        {
            AddAssert("cursor position always valid", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = new Beatmap { BeatmapInfo = { Ruleset = ruleset.RulesetInfo } };
                double time = 500;

                // a stream which has several circles at the same time in its middle.
                for (int i = 0; i < 12; i++)
                {
                    beatmap.HitObjects.Add(new HitCircle { StartTime = time, Position = new Vector2(100 + i * 25, 192) });

                    if (i == 5)
                        beatmap.HitObjects.Add(new HitCircle { StartTime = time, Position = new Vector2(100 + i * 25, 300) });
                    else
                        time += 90;
                }

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                var processor = ruleset.CreateBeatmapProcessor(converted);
                processor?.PreProcess();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);
                processor?.PostProcess();

                var frames = new OsuRealisticAutoGenerator(converted, new List<Mod>()).Generate().Frames.Cast<OsuReplayFrame>().ToList();

                return frames.All(f => float.IsFinite(f.Position.X) && float.IsFinite(f.Position.Y));
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
        [TestCase(150)]
        [TestCase(500)]
        [TestCase(1500)]
        public void TestRealisticCursorLeavesSpinnersSmoothly(double gap)
        {
            AddAssert("no kinks or u-turns after the spinner", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = new Beatmap
                {
                    HitObjects =
                    {
                        new Spinner { StartTime = 1000, EndTime = 2500, NewCombo = true },
                        new HitCircle { StartTime = 2500 + gap, Position = new Vector2(400, 80), NewCombo = true },
                        new HitCircle { StartTime = 2800 + gap, Position = new Vector2(100, 80) },
                    },
                    BeatmapInfo = { Difficulty = { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 }, Ruleset = ruleset.RulesetInfo },
                };

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

                var generator = new OsuRealisticAutoGenerator(converted, new List<Mod>());
                var allFrames = generator.Generate().Frames.Cast<OsuReplayFrame>().ToList();
                var circle = (OsuHitObject)converted.HitObjects[1];
                var exit = generator.GetSpinnerExit((Spinner)converted.HitObjects[0], circle);
                double exitTime = exit.Time;
                var frames = allFrames.Where(f => f.Time >= exitTime - 20 && f.Time <= circle.StartTime).ToList();

                // a kink shows as a sharp change of direction while moving fast, a u-turn as moving away from the object.
                double maxTurn = 0;
                double maxAway = 0;
                // the movement carries the spin's momentum, so it only ever slows down instead of slowing and then speeding up again.
                double maxRise = 0;

                for (int i = 1; i < frames.Count - 1; i++)
                {
                    Vector2 before = frames[i].Position - frames[i - 1].Position;
                    Vector2 after = frames[i + 1].Position - frames[i].Position;
                    double speedBefore = before.Length / (frames[i].Time - frames[i - 1].Time);
                    double speedAfter = after.Length / (frames[i + 1].Time - frames[i].Time);
                    double speed = Math.Min(speedBefore, speedAfter);

                    if (frames[i].Time > exitTime)
                        maxRise = Math.Max(maxRise, speedAfter - speedBefore);

                    TestContext.WriteLine($"{frames[i].Time:F0}: {frames[i].Position} speed {speedAfter:F2}");

                    if (speed < 0.3)
                        continue;

                    double turn = Math.Acos(Math.Clamp(Vector2.Dot(before.Normalized(), after.Normalized()), -1, 1)) * 180 / Math.PI;
                    maxTurn = Math.Max(maxTurn, turn);

                    // the cursor still lags behind the spin right after letting go of it.
                    if (frames[i].Time > exitTime + 50)
                        maxAway = Math.Max(maxAway, -Vector2.Dot(after, (circle.StackedPosition - frames[i].Position).Normalized()));
                }

                // the cursor is on the object in time. it can't hold there for long, as it only starts moving once it has seen the object.
                double arrival = frames.First(f => Vector2.Distance(f.Position, frames[^1].Position) < 1).Time;
                const double expectedHold = 0;

                TestContext.WriteLine($"exit {exitTime:F0}, arrival {arrival:F0}, max turn {maxTurn:F1}, max away {maxAway:F2}, max speed rise {maxRise:F3}");
                return maxTurn < 40 && maxAway < 1 && maxRise < 0.05 && arrival <= circle.StartTime - expectedHold;
            });
        }

        [TestCase(400, 330, 2000)]
        [TestCase(100, 100, 2000)]
        [TestCase(300, 200, 2000)]
        [TestCase(200, 40, 300)]
        [TestCase(200, 40, 150)]
        public void TestRealisticCursorEntersSpinnersWithoutLooping(float x, float y, double gap)
        {
            AddAssert("no loops on the way into the spinner", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = new Beatmap
                {
                    HitObjects =
                    {
                        new HitCircle { StartTime = 2500 - gap, Position = new Vector2(x, y), NewCombo = true },
                        new Spinner { StartTime = 2500, EndTime = 4000, NewCombo = true },
                    },
                    BeatmapInfo = { Difficulty = { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 }, Ruleset = ruleset.RulesetInfo },
                };

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

                var generator = new OsuRealisticAutoGenerator(converted, new List<Mod>());
                var allFrames = generator.Generate().Frames.Cast<OsuReplayFrame>().ToList();
                // the entry is cached while generating, so the arguments don't matter here.
                var spinner = (Spinner)converted.HitObjects[1];
                double joinTime = generator.GetSpinnerEntry(spinner, Vector2.Zero, 0);
                // before the spinner is seen (it appears, then takes a reaction time), the cursor only drifts around, which may cross itself.
                double moveStart = Math.Max(2500 - gap, spinner.StartTime - spinner.TimePreempt + 170);
                var path = allFrames.Where(f => f.Time > moveStart && f.Time <= joinTime + 50).Select(f => f.Position).ToList();

                int crossings = 0;

                for (int i = 1; i < path.Count; i++)
                {
                    for (int j = i + 2; j < path.Count; j++)
                    {
                        if (segmentsCross(path[i - 1], path[i], path[j - 1], path[j]))
                            crossings++;
                    }
                }

                TestContext.WriteLine($"join {joinTime:F0}, {path.Count} frames, {crossings} crossings");

                // the cursor is already spinning when the spinner starts, instead of only heading for its centre.
                return path.Count > 2 && crossings == 0 && joinTime < 2500 - (2500 - moveStart) * 0.15;
            });

            static bool segmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            {
                static float cross(Vector2 o, Vector2 p, Vector2 q) => (p.X - o.X) * (q.Y - o.Y) - (p.Y - o.Y) * (q.X - o.X);

                return cross(a, b, c) * cross(a, b, d) < 0 && cross(c, d, a) * cross(c, d, b) < 0;
            }
        }

        [Test]
        public void TestRealisticCursorFlowsStraightThroughTriples()
        {
            AddAssert("cursor moves in a straight line", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = new Beatmap { BeatmapInfo = { Difficulty = { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 }, Ruleset = ruleset.RulesetInfo } };

                for (int i = 0; i < 3; i++)
                    beatmap.HitObjects.Add(new HitCircle { StartTime = 2000 + i * 100, Position = new Vector2(200 + i * 20, 250 - i * 25), NewCombo = i == 0 });

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

                var frames = new OsuRealisticAutoGenerator(converted, new List<Mod>()).Generate().Frames.Cast<OsuReplayFrame>()
                                                                                       .Where(f => f.Time > 2000 && f.Time < 2200)
                                                                                       .ToList();

                Vector2 direction = Vector2.Normalize(new Vector2(20, -25));
                Vector2 normal = new Vector2(-direction.Y, direction.X);
                float sideways = frames.Max(f => Math.Abs(Vector2.Dot(f.Position - new Vector2(200, 250), normal)));

                TestContext.Out.WriteLine($"sideways {sideways:0.00}");
                return frames.Count > 0 && sideways < 1.5f;
            });
        }

        [TestCase(100)]
        [TestCase(180)]
        [TestCase(400)]
        public void TestRealisticCursorStaysStillOnStacks(double interval)
        {
            AddAssert("cursor follows offset stacks", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = new Beatmap { BeatmapInfo = { Difficulty = { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 }, Ruleset = ruleset.RulesetInfo } };

                // each object is offset by a few pixels like a stack, and the whole stack is longer than a circle radius.
                for (int i = 0; i < 12; i++)
                    beatmap.HitObjects.Add(new HitCircle { StartTime = 2000 + i * interval, Position = new Vector2(256 + i * 4, 192 + i * 4), NewCombo = i == 0 });

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

                var frames = new OsuRealisticAutoGenerator(converted, new List<Mod>()).Generate().Frames.Cast<OsuReplayFrame>().ToList();

                // the cursor is on every object when it's hit, and never further than a few pixels from the stack line.
                bool hitsAll = converted.HitObjects.Cast<OsuHitObject>().All(h =>
                    Vector2.Distance(frames.Last(f => f.Time <= h.StartTime).Position, h.StackedPosition) < h.Radius);
                float largestStep = frames.Where(f => f.Time > 2000 + interval && f.Time < 2000 + 11 * interval)
                                          .Zip(frames.Where(f => f.Time > 2000 + interval && f.Time < 2000 + 11 * interval).Skip(1), (a, b) => Vector2.Distance(a.Position, b.Position)).Max();

                TestContext.Out.WriteLine($"interval {interval}: largest step {largestStep:0.00}");
                return hitsAll && largestStep < 3;
            });

            AddAssert("cursor doesn't move on the stack", () =>
            {
                var ruleset = new OsuRuleset();
                var beatmap = new Beatmap { BeatmapInfo = { Difficulty = { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 }, Ruleset = ruleset.RulesetInfo } };

                for (int i = 0; i < 8; i++)
                    beatmap.HitObjects.Add(new HitCircle { StartTime = 2000 + i * interval, Position = new Vector2(256, 192), NewCombo = i == 0 });

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

                var frames = new OsuRealisticAutoGenerator(converted, new List<Mod>()).Generate().Frames.Cast<OsuReplayFrame>()
                                                                                       .Where(f => f.Time > 2000 + 2 * interval && f.Time < 2000 + 7 * interval)
                                                                                       .ToList();

                float spread = frames.Max(f => Vector2.Distance(f.Position, frames[0].Position));
                TestContext.Out.WriteLine($"interval {interval}: spread {spread:0.000}");
                return frames.Count > 0 && spread < 0.5f;
            });
        }

        [Test]
        public void TestRealisticCursorIgnoresObjectsBeforeSeeingThem()
        {
            AddAssert("cursor doesn't move towards an object before seeing it", () =>
            {
                var first = generate(new Vector2(450, 300));
                var second = generate(new Vector2(60, 60));

                // the second object appears at 5000 - 600 (AR9) and is reacted to 170 ms later.
                const double seen = 5000 - 600 + 170;
                var before = first.Where(f => f.Time < seen).ToList();

                return before.Count > 100 && before.Zip(second, (a, b) => a.Time == b.Time && a.Position == b.Position).All(same => same)
                       && first.Last(f => f.Time <= 5000).Position != second.Last(f => f.Time <= 5000).Position;
            });

            static List<OsuReplayFrame> generate(Vector2 secondPosition)
            {
                var ruleset = new OsuRuleset();
                var beatmap = new Beatmap
                {
                    HitObjects =
                    {
                        new HitCircle { StartTime = 1000, Position = new Vector2(256, 192), NewCombo = true },
                        new HitCircle { StartTime = 5000, Position = secondPosition },
                    },
                    BeatmapInfo = { Difficulty = { ApproachRate = 9, CircleSize = 4, OverallDifficulty = 8 }, Ruleset = ruleset.RulesetInfo },
                };

                var converted = ruleset.CreateBeatmapConverter(beatmap).Convert();
                foreach (var h in converted.HitObjects)
                    h.ApplyDefaults(converted.ControlPointInfo, converted.Difficulty);

                return new OsuRealisticAutoGenerator(converted, new List<Mod>()).Generate().Frames.Cast<OsuReplayFrame>().ToList();
            }
        }

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
