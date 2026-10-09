// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneEditorVolumeMuting : EditorTestScene
    {
        [Resolved]
        private AudioManager audio { get; set; } = null!;

        private double masterBefore;
        private double musicBefore;
        private double effectsBefore;

        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        public override void SetUpSteps()
        {
            AddStep("store volumes", () =>
            {
                masterBefore = audio.Volume.Value;
                musicBefore = audio.VolumeTrack.Value;
                effectsBefore = audio.VolumeSample.Value;
            });

            base.SetUpSteps();
        }

        public override void TearDownSteps()
        {
            base.TearDownSteps();

            AddStep("restore volumes", () =>
            {
                audio.Volume.Value = masterBefore;
                audio.VolumeTrack.Value = musicBefore;
                audio.VolumeSample.Value = effectsBefore;
            });
        }

        [Test]
        public void TestMuteMusic()
        {
            AddStep("set volumes", () =>
            {
                audio.Volume.Value = 0.8;
                audio.VolumeTrack.Value = 0.6;
                audio.VolumeSample.Value = 0.7;
            });

            AddStep("press M", () => InputManager.Key(Key.M));
            AddAssert("music muted", () => audio.VolumeTrack.Value, () => Is.Zero);
            AddAssert("master unchanged", () => audio.Volume.Value, () => Is.EqualTo(0.8).Within(0.001));
            AddAssert("effects unchanged", () => audio.VolumeSample.Value, () => Is.EqualTo(0.7).Within(0.001));

            AddStep("press M", () => InputManager.Key(Key.M));
            AddAssert("music restored", () => audio.VolumeTrack.Value, () => Is.EqualTo(0.6).Within(0.001));
        }

        [Test]
        public void TestMuteEffects()
        {
            AddStep("set volumes", () =>
            {
                audio.Volume.Value = 0.8;
                audio.VolumeTrack.Value = 0.6;
                audio.VolumeSample.Value = 0.7;
            });

            AddStep("press Ctrl+M", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.M);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddAssert("effects muted", () => audio.VolumeSample.Value, () => Is.Zero);
            AddAssert("master unchanged", () => audio.Volume.Value, () => Is.EqualTo(0.8).Within(0.001));
            AddAssert("music unchanged", () => audio.VolumeTrack.Value, () => Is.EqualTo(0.6).Within(0.001));

            AddStep("press Ctrl+M", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.M);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddAssert("effects restored", () => audio.VolumeSample.Value, () => Is.EqualTo(0.7).Within(0.001));
        }
    }
}
