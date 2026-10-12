// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneEditorTimelineCtrlShiftZoom : OsuGameTestScene
    {
        private BeatmapSetInfo beatmapSet = null!;
        private float zoomBefore;

        private Editor editor => (Editor)Game.ScreenStack.CurrentScreen;

        private Timeline timeline => editor.ChildrenOfType<Timeline>().First();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("apply user settings", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.SlopEditorShowDifficultyStrains, true);
                Game.LocalConfig.SetValue(OsuSetting.SlopEditorShowOffscreenObjects, true);
                Game.LocalConfig.SetValue(OsuSetting.SlopEditorWaveformStyle, EditorWaveformStyle.ThreeBand);
                Game.LocalConfig.SetValue(OsuSetting.SlopEditorLoopMusic, true);
                Game.LocalConfig.SetValue(OsuSetting.SlopFrostedSliders, true);
                Game.LocalConfig.SetValue(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, true);
                Game.LocalConfig.SetValue(OsuSetting.EditorContractSidebars, false);
                Game.LocalConfig.SetValue(OsuSetting.SlopEditorShowSelectionBox, false);
                Game.LocalConfig.SetValue(OsuSetting.SlopEditorShowSelectionBoxButtons, false);
            });
            AddStep("import test beatmap", () => Game.BeatmapManager.Import(TestResources.GetTestBeatmapForImport()).WaitSafely());
            AddStep("retrieve beatmap", () => beatmapSet = Game.BeatmapManager.QueryBeatmapSet(set => !set.Protected).AsNonNull().Value.Detach());
            AddStep("present beatmap", () => Game.PresentBeatmap(beatmapSet));
            AddUntilStep("wait for song select", () => Game.Beatmap.Value.BeatmapSetInfo.Equals(beatmapSet)
                                                       && Game.ScreenStack.CurrentScreen is SoloSongSelect songSelect
                                                       && songSelect.CarouselItemsPresented);
            AddStep("open editor", () => ((SoloSongSelect)Game.ScreenStack.CurrentScreen).Edit(beatmapSet.Beatmaps.First(b => b.Ruleset.OnlineID == 0)));
            AddUntilStep("wait for editor", () => Game.ScreenStack.CurrentScreen is Editor e && e.ReadyForUse);
            AddUntilStep("wait for timeline", () => timeline.IsLoaded && timeline.DrawWidth > 0);
        }

        [Test]
        public void TestCtrlShiftScrollOnTimelineZooms()
        {
            AddStep("hover timeline", () => InputManager.MoveMouseTo(timeline));
            checkZoom();
            AddAssert("volume meter not displayed", () => Game.ChildrenOfType<VolumeOverlay>().All(overlay => overlay.State.Value == Visibility.Hidden));
        }

        [Test]
        public void TestAltScrollOnTimelineAdjustsVolume()
        {
            AddStep("hover timeline", () => InputManager.MoveMouseTo(timeline));
            AddStep("store zoom", () => zoomBefore = timeline.Zoom);
            AddStep("alt+scroll", () =>
            {
                InputManager.PressKey(Key.AltLeft);
                InputManager.ScrollVerticalBy(1);
                InputManager.ReleaseKey(Key.AltLeft);
            });
            AddAssert("volume meter displayed", () => Game.ChildrenOfType<VolumeOverlay>().Any(overlay => overlay.State.Value == Visibility.Visible));
            AddAssert("zoom unchanged", () => timeline.Zoom, () => Is.EqualTo(zoomBefore));
        }

        private void checkZoom()
        {
            AddStep("set zoom to middle", () =>
            {
                timeline.Zoom = float.MaxValue;
                float max = timeline.Zoom;
                timeline.Zoom = 0;
                float min = timeline.Zoom;
                timeline.Zoom = (min + max) / 2;
            });
            AddStep("store zoom", () => zoomBefore = timeline.Zoom);

            AddStep("ctrl+shift+scroll up", () => zoomScroll(1));
            AddAssert("zoomed in", () => timeline.Zoom, () => Is.GreaterThan(zoomBefore));

            AddStep("store zoom", () => zoomBefore = timeline.Zoom);
            AddStep("ctrl+shift+scroll down", () => zoomScroll(-1));
            AddAssert("zoomed out", () => timeline.Zoom, () => Is.LessThan(zoomBefore));
        }

        private void zoomScroll(float amount)
        {
            InputManager.PressKey(Key.ControlLeft);
            InputManager.PressKey(Key.ShiftLeft);
            InputManager.ScrollVerticalBy(amount);
            InputManager.ReleaseKey(Key.ShiftLeft);
            InputManager.ReleaseKey(Key.ControlLeft);
        }
    }
}
