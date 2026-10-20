// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.Replays.AutoPlus;
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
