// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Hitsounding;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneHitsoundEditor : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override bool IsolateSavingFromDatabase => false;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null) => new DummyWorkingBeatmap(Audio, null);

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        private HitsoundScreen? screen => Editor.ChildrenOfType<HitsoundScreen>().SingleOrDefault();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private string firstDifficultyName = null!;

        public override void SetUpSteps()
        {
            AddStep("expand sidebars", () => config.SetValue(OsuSetting.EditorContractSidebars, false));

            base.SetUpSteps();

            AddStep("make beatmap unique", () =>
            {
                EditorBeatmap.Metadata.Title = Guid.NewGuid().ToString();
                EditorBeatmap.BeatmapInfo.DifficultyName = firstDifficultyName = Guid.NewGuid().ToString();
            });

            AddStep("add timing point", () => EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 }));
            AddStep("add hit objects", () => EditorBeatmap.AddRange(new[]
            {
                createCircle(0),
                createCircle(500, HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_SOFT),
            }));
            AddStep("save", () => Editor.Save());

            AddStep("switch to hitsound screen", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.Key(Key.H);
                InputManager.ReleaseKey(Key.ShiftLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            waitForScreen();
        }

        [Test]
        public void TestToggleAdditionAtPlayhead()
        {
            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddStep("press W", () => InputManager.Key(Key.W));
            AddAssert("whistle added", () => hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_WHISTLE));
            AddAssert("other object unchanged", () => EditorBeatmap.HitObjects[1].Samples.Count, () => Is.EqualTo(2));

            AddStep("undo", () => Editor.Undo());
            AddAssert("whistle removed", () => !hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_WHISTLE));
        }

        [Test]
        public void TestSetBankOfSelection()
        {
            AddStep("select all", () => InputManager.Keys(PlatformAction.SelectAll));
            AddStep("press Shift+E", () =>
            {
                InputManager.PressKey(Key.ShiftLeft);
                InputManager.Key(Key.E);
                InputManager.ReleaseKey(Key.ShiftLeft);
            });

            AddAssert("all hitnormals use soft bank", () => EditorBeatmap.HitObjects.All(h => HitsoundSamples.GetNormalBank(h.Samples) == HitSampleInfo.BANK_SOFT));
            AddAssert("whistle kept", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
        }

        [Test]
        public void TestToolsFollowPlayhead()
        {
            AddStep("set custom index of second object", () =>
            {
                var hitsoundEditor = Editor.ChildrenOfType<HitsoundEditor>().Single();
                hitsoundEditor.SetCustomIndex(new[] { hitsoundEditor.Map.Columns[1] }, 3);
            });

            AddStep("seek to second object", () => EditorClock.Seek(500));
            AddWaitStep("wait for tools to update", 2);
            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddWaitStep("wait for tools to update", 2);
            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddWaitStep("wait for tools to update", 2);

            AddAssert("custom index kept", () => HitsoundSamples.GetCustomIndex(EditorBeatmap.HitObjects[1].Samples), () => Is.EqualTo(3));
            AddAssert("first object unchanged", () => HitsoundSamples.GetCustomIndex(EditorBeatmap.HitObjects[0].Samples), () => Is.EqualTo(0));
        }

        [Test]
        public void TestCreateHitsoundDifficultyAndCopyBack()
        {
            AddStep("create hitsound difficulty", () => screen!.CreateHitsoundDifficulty(HitSampleInfo.BANK_SOFT, 60, false));
            AddUntilStep("switched to hitsound difficulty", () => Editor.ChildrenOfType<EditorBeatmap>().SingleOrDefault()?.BeatmapInfo.DifficultyName == "Hitsounds");
            AddUntilStep("wait for editor", () => Editor.ReadyForUse);
            waitForScreen();

            AddAssert("hitsound difficulty mode enabled", () => Editor.ChildrenOfType<HitsoundEditor>().Single().HitsoundDifficultyMode.Value);
            AddAssert("contains both hitsounds", () => EditorBeatmap.HitObjects.Select(h => h.StartTime), () => Is.EqualTo(new double[] { 0, 500 }));
            AddAssert("whistle copied", () => EditorBeatmap.HitObjects[1].Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE && s.Bank == HitSampleInfo.BANK_SOFT));
            AddAssert("starting bank applied", () => EditorBeatmap.HitObjects.All(h => HitsoundSamples.GetNormalBank(h.Samples) == HitSampleInfo.BANK_SOFT));
            AddAssert("starting volume applied", () => EditorBeatmap.HitObjects.All(h => HitsoundSamples.GetVolume(h.Samples) == 60));
            AddAssert("no stacking", () => EditorBeatmap.StackLeniency, () => Is.EqualTo(0));
            AddAssert("recommended difficulty settings", () => (EditorBeatmap.Difficulty.CircleSize, EditorBeatmap.Difficulty.ApproachRate), () => Is.EqualTo((2f, 8f)));

            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddStep("press E", () => InputManager.Key(Key.E));
            AddAssert("finish added", () => hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_FINISH));

            AddStep("copy to other difficulties", () => screen!.CopyToDifficulties(screen.GetOtherDifficulties(), new HitsoundCopyOptions()));
            AddUntilStep("wait for confirmation", () => DialogOverlay.CurrentDialog is HitsoundCopyConfirmationDialog);
            AddStep("confirm", () => DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());

            AddUntilStep("finish copied to first difficulty", () =>
            {
                var info = beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName);
                return info != null && hasSample(beatmapManager.GetWorkingBeatmap(info).Beatmap.HitObjects[0], HitSampleInfo.HIT_FINISH);
            });

            AddUntilStep("editor reloaded", () => Editor.ReadyForUse && Editor.ChildrenOfType<EditorBeatmap>().SingleOrDefault()?.BeatmapInfo.DifficultyName == "Hitsounds");
            AddStep("save again", () => Editor.Save());

            AddAssert("database hashes match files", () =>
            {
                var set = beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!.BeatmapSet!;
                return set.Beatmaps.All(b => b.Hash == b.File!.File.Hash);
            });
        }

        [Test]
        public void TestPianoRollClicks()
        {
            var whistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);
            var clap = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP);

            AddStep("seek to second object", () => EditorClock.Seek(500));

            AddStep("left click whistle", () => clickLane(whistle, MouseButton.Left));
            AddAssert("hitsound selected", () => Editor.ChildrenOfType<HitsoundEditor>().Single().IsSelected(500));
            AddAssert("whistle kept", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));

            AddStep("left click whistle again", () => clickLane(whistle, MouseButton.Left));
            AddAssert("hitsound deselected", () => !Editor.ChildrenOfType<HitsoundEditor>().Single().IsSelected(500));

            AddStep("left click clap", () => clickLane(clap, MouseButton.Left));
            AddAssert("clap added", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_CLAP));

            AddStep("right click whistle", () => clickLane(whistle, MouseButton.Right));
            AddAssert("whistle removed", () => !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
            AddAssert("clap kept", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_CLAP));
        }

        [Test]
        public void TestRightDragRemovesSamples()
        {
            var whistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);

            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddStep("right drag over whistle lane", () =>
            {
                InputManager.MoveMouseTo(lanePosition(whistle, -100));
                InputManager.PressButton(MouseButton.Right);
                InputManager.MoveMouseTo(lanePosition(whistle, 300));
                InputManager.MoveMouseTo(lanePosition(whistle, 700));
            });
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Right));
            AddAssert("whistle removed", () => !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
        }

        [Test]
        public void TestRightDragRemovesAcrossLanes()
        {
            var normalClap = new HitsoundLane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_CLAP);
            var softWhistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);

            AddStep("add clap to first object", () => hitsoundEditor.SetLane(hitsoundEditor.Map.Columns[0], normalClap, true));
            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddStep("right drag diagonally from clap to whistle", () =>
            {
                InputManager.MoveMouseTo(lanePosition(normalClap, -100));
                InputManager.PressButton(MouseButton.Right);
                InputManager.MoveMouseTo((lanePosition(normalClap, 250) + lanePosition(softWhistle, 250)) / 2);
                InputManager.MoveMouseTo(lanePosition(softWhistle, 600));
            });
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Right));
            AddAssert("clap removed", () => !hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_CLAP));
            AddAssert("whistle removed", () => !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
        }

        [Test]
        public void TestClickingSelectedObjectSeeks()
        {
            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddStep("click second object", () => clickStrip(500));
            AddAssert("selected", () => hitsoundEditor.SelectedKeys, () => Is.EquivalentTo(new[] { 500 }));
            AddAssert("not seeked", () => EditorClock.CurrentTime, () => Is.EqualTo(250).Within(1));

            AddStep("click second object again", () => clickStrip(500));
            AddUntilStep("seeked to object", () => EditorClock.CurrentTime, () => Is.EqualTo(500).Within(1));
        }

        private void clickStrip(double time)
        {
            var strip = Editor.ChildrenOfType<HitsoundObjectStrip>().Single();
            var timeline = Editor.ChildrenOfType<HitsoundTimeline>().Single();

            InputManager.MoveMouseTo(strip.ToScreenSpace(new Vector2(timeline.TimeToX(time, strip.DrawWidth), strip.DrawHeight / 2)));
            InputManager.Click(MouseButton.Left);
        }

        [Test]
        public void TestModifierClicksInLanes()
        {
            var softNormal = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_NORMAL);
            var normalNormal = new HitsoundLane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL);

            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddStep("ctrl click second hitsound", () => clickLaneWith(Key.ControlLeft, softNormal, 500));
            AddStep("ctrl click first hitsound", () => clickLaneWith(Key.ControlLeft, normalNormal, 0));
            AddAssert("both selected", () => hitsoundEditor.SelectedKeys.Count, () => Is.EqualTo(2));

            AddStep("ctrl click second hitsound again", () => clickLaneWith(Key.ControlLeft, softNormal, 500));
            AddAssert("only first selected", () => hitsoundEditor.SelectedKeys, () => Is.EquivalentTo(new[] { 0 }));

            AddStep("shift click second hitsound", () => clickLaneWith(Key.ShiftLeft, softNormal, 500));
            AddAssert("range selected", () => hitsoundEditor.SelectedKeys, () => Is.EquivalentTo(new[] { 0, 500 }));

            AddStep("click empty space", () =>
            {
                InputManager.MoveMouseTo(lanePosition(softNormal, 250));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("selection cleared", () => hitsoundEditor.SelectedKeys, () => Is.Empty);
        }

        [Test]
        public void TestRectangleSelectionOnlySelectsCoveredLanes()
        {
            var whistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);

            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddStep("drag along whistle lane", () =>
            {
                InputManager.MoveMouseTo(lanePosition(whistle, -100));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(lanePosition(whistle, 300));
                InputManager.MoveMouseTo(lanePosition(whistle, 700));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddAssert("only hitsound with whistle selected", () => hitsoundEditor.SelectedKeys, () => Is.EquivalentTo(new[] { 500 }));
        }

        [Test]
        public void TestVolumeLine()
        {
            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddStep("right drag line from 100% to 50%", () =>
            {
                InputManager.MoveMouseTo(volumePosition(-50, 100));
                InputManager.PressButton(MouseButton.Right);
                InputManager.MoveMouseTo(volumePosition(300, 75));
                InputManager.MoveMouseTo(volumePosition(550, 50));
            });
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Right));
            AddAssert("first object keeps full volume", () => HitsoundSamples.GetVolume(EditorBeatmap.HitObjects[0].Samples), () => Is.EqualTo(100).Within(5));
            AddAssert("second object follows line", () => HitsoundSamples.GetVolume(EditorBeatmap.HitObjects[1].Samples), () => Is.EqualTo(55).Within(5));
        }

        private HitsoundEditor hitsoundEditor => Editor.ChildrenOfType<HitsoundEditor>().Single();

        private void clickLaneWith(Key modifier, HitsoundLane lane, double time)
        {
            InputManager.MoveMouseTo(lanePosition(lane, time));
            InputManager.PressKey(modifier);
            InputManager.Click(MouseButton.Left);
            InputManager.ReleaseKey(modifier);
        }

        private Vector2 lanePosition(HitsoundLane lane, double time)
        {
            var area = Editor.ChildrenOfType<HitsoundLaneArea>().Single();
            var lanes = hitsoundEditor.VisibleLanes;
            int index = lanes.ToList().IndexOf(lane);
            var timeline = Editor.ChildrenOfType<HitsoundTimeline>().Single();

            return area.ToScreenSpace(new Vector2(timeline.TimeToX(time, area.DrawWidth), (index + 0.5f) * area.DrawHeight / lanes.Count));
        }

        private Vector2 volumePosition(double time, float volume)
        {
            var area = Editor.ChildrenOfType<HitsoundVolumeArea>().Single();
            var timeline = Editor.ChildrenOfType<HitsoundTimeline>().Single();

            return area.ToScreenSpace(new Vector2(timeline.TimeToX(time, area.DrawWidth), (1 - volume / 100) * area.DrawHeight));
        }

        private void clickLane(HitsoundLane lane, MouseButton button)
        {
            var area = Editor.ChildrenOfType<HitsoundLaneArea>().Single();
            var lanes = Editor.ChildrenOfType<HitsoundEditor>().Single().VisibleLanes;
            int index = lanes.ToList().IndexOf(lane);

            // the current time is at the centre of the lanes.
            InputManager.MoveMouseTo(area.ToScreenSpace(new Vector2(area.DrawWidth / 2, (index + 0.5f) * area.DrawHeight / lanes.Count)));
            InputManager.Click(button);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void TestClickObjectStripSeeksTowardsClick(int direction)
        {
            double timeBefore = 0;

            AddStep("seek to 500", () => EditorClock.Seek(500));
            AddStep("click strip beside playhead", () =>
            {
                var strip = Editor.ChildrenOfType<HitsoundObjectStrip>().Single();
                timeBefore = EditorClock.CurrentTime;
                InputManager.MoveMouseTo(strip.ToScreenSpace(new Vector2(strip.DrawWidth / 2 + direction * 60, 4)));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("seeked in click direction", () => Math.Sign(Math.Round(EditorClock.CurrentTime - timeBefore)), () => Is.EqualTo(direction));
        }

        [Test]
        public void TestTimelinePartsLineUp()
        {
            AddUntilStep("hit objects line up with lanes", () => timelinePartsLineUp());
        }

        [Test]
        public void TestToolboxContractsWithSidebars()
        {
            AddStep("contract sidebars", () => config.SetValue(OsuSetting.EditorContractSidebars, true));
            AddUntilStep("toolbox contracted", () => toolbox.DrawWidth, () => Is.EqualTo(HitsoundToolbox.CONTRACTED_WIDTH).Within(1));
            AddUntilStep("hit objects line up with lanes", () => timelinePartsLineUp());

            double hoverStartTime = 0;

            AddStep("hover toolbox", () =>
            {
                InputManager.MoveMouseTo(toolbox);
                hoverStartTime = Time.Current;
            });
            AddUntilStep("wait past hover expansion delay", () => Time.Current - hoverStartTime > 500);
            AddAssert("toolbox stays contracted", () => toolbox.DrawWidth, () => Is.EqualTo(HitsoundToolbox.CONTRACTED_WIDTH).Within(1));

            AddStep("seek to second object", () => EditorClock.Seek(500));
            AddStep("click compact drum hitnormal button", () =>
            {
                InputManager.MoveMouseTo(visibleToggle(SlopHitsoundEditorStrings.BankDrum));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("hitnormal bank changed", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[1].Samples), () => Is.EqualTo(HitSampleInfo.BANK_DRUM));

            AddStep("click compact volume down button", () =>
            {
                InputManager.MoveMouseTo(toolbox.ChildrenOfType<HitsoundToggleButton>().First(b => isVisible(b) && b.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == "-")));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("volume lowered", () => HitsoundSamples.GetVolume(EditorBeatmap.HitObjects[1].Samples), () => Is.EqualTo(95));
        }

        [Test]
        public void TestSubLanes()
        {
            var softWhistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);
            var subLane = softWhistle.WithCustomIndex(2);

            AddAssert("no sub-lanes without custom samples", () => hitsoundEditor.GetSubLaneIndices(softWhistle), () => Is.Empty);

            AddStep("use custom index 2 for second hitsound", () => hitsoundEditor.SetCustomIndex(new[] { hitsoundEditor.Map.Columns[1] }, 2));
            AddAssert("sub-lane available", () => hitsoundEditor.GetSubLaneIndices(softWhistle), () => Is.EqualTo(new[] { 2 }));
            AddAssert("collapsed by default", () => !hitsoundEditor.VisibleLanes.Contains(subLane));

            AddStep("expand", () => hitsoundEditor.ToggleExpanded(softWhistle));
            AddUntilStep("sub-lane visible below lane", () => hitsoundEditor.VisibleLanes.ToList().IndexOf(subLane), () => Is.EqualTo(hitsoundEditor.VisibleLanes.ToList().IndexOf(softWhistle) + 1));
            AddAssert("second hitsound plays sub-lane", () => hitsoundEditor.Map.Columns[1].Has(subLane));
            AddAssert("but not other custom sample sets", () => !hitsoundEditor.Map.Columns[1].Has(softWhistle.WithCustomIndex(3)));

            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddStep("click sub-lane at first hitsound", () =>
            {
                InputManager.MoveMouseTo(lanePosition(subLane, 0));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("whistle added", () => hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_WHISTLE));
            AddAssert("from custom sample set", () => HitsoundSamples.GetCustomIndex(EditorBeatmap.HitObjects[0].Samples), () => Is.EqualTo(2));

            AddStep("collapse", () => hitsoundEditor.ToggleExpanded(softWhistle));
            AddUntilStep("sub-lane hidden", () => !hitsoundEditor.VisibleLanes.Contains(subLane));
        }

        [Test]
        public void TestDoubleClickLaneTogglesSubLanes()
        {
            var softWhistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);
            var subLane = softWhistle.WithCustomIndex(2);

            AddStep("double click lane without custom samples", () => doubleClickHeader(softWhistle));
            AddAssert("not expanded", () => hitsoundEditor.ExpandedLanes, () => Is.Empty);

            AddStep("use custom index 2 for second hitsound", () => hitsoundEditor.SetCustomIndex(new[] { hitsoundEditor.Map.Columns[1] }, 2));
            AddStep("double click lane", () => doubleClickHeader(softWhistle));
            AddUntilStep("sub-lane visible", () => hitsoundEditor.VisibleLanes.Contains(subLane));

            AddStep("double click lane again", () => doubleClickHeader(softWhistle));
            AddUntilStep("sub-lane hidden", () => !hitsoundEditor.VisibleLanes.Contains(subLane));
        }

        private void doubleClickHeader(HitsoundLane lane)
        {
            InputManager.MoveMouseTo(headerPosition(lane));
            InputManager.Click(MouseButton.Left);
            InputManager.Click(MouseButton.Left);
        }

        [Test]
        public void TestRenameSubLane()
        {
            var softWhistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);
            var subLane = softWhistle.WithCustomIndex(2);

            AddStep("use custom index 2 for second hitsound", () => hitsoundEditor.SetCustomIndex(new[] { hitsoundEditor.Map.Columns[1] }, 2));
            AddStep("expand", () => hitsoundEditor.ToggleExpanded(softWhistle));
            AddUntilStep("sub-lane visible", () => hitsoundEditor.VisibleLanes.Contains(subLane));

            AddStep("double click sub-lane header", () =>
            {
                InputManager.MoveMouseTo(headerPosition(subLane));
                InputManager.Click(MouseButton.Left);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("text box focused", () => renameTextBox?.HasFocus == true);

            AddStep("press W", () => InputManager.Key(Key.W));
            AddAssert("shortcut not triggered", () => !hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_WHISTLE));

            AddStep("enter name", () => renameTextBox!.Text = "Kick");
            AddStep("commit", () => InputManager.Key(Key.Enter));
            AddUntilStep("name shown", () => headerTexts.Contains("Kick"));
            AddUntilStep("text box removed", () => renameTextBox == null);
            AddAssert("saved in file of beatmap set", () => storage.Exists(HitsoundLaneNames.GetPath(EditorBeatmap.BeatmapInfo.BeatmapSet!.ID)));

            AddStep("right click sub-lane header", () =>
            {
                InputManager.MoveMouseTo(headerPosition(subLane));
                InputManager.Click(MouseButton.Right);
            });
            AddStep("click reset name", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<DrawableOsuMenuItem>().Single(i => i.Item.Text.Value.Equals(SlopHitsoundEditorStrings.ResetLaneName)));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("default name shown", () => !headerTexts.Contains("Kick") && headerTexts.Contains(HitsoundLaneHeaders.GetCustomIndexName(2).ToString()));
            AddAssert("file of beatmap set removed", () => !storage.Exists(HitsoundLaneNames.GetPath(EditorBeatmap.BeatmapInfo.BeatmapSet!.ID)));
        }

        [Test]
        public void TestToolsDontFollowPlayheadWhilePlaying()
        {
            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddUntilStep("editing at playhead", () => visibleTexts.Contains(SlopHitsoundEditorStrings.EditingAtPlayhead.ToString()));

            AddStep("play", () => EditorClock.Start());
            AddUntilStep("played past first object", () => EditorClock.CurrentTime > 150);
            AddAssert("still showing first object", () => visibleTexts.Contains(SlopHitsoundEditorStrings.EditingAtPlayhead.ToString()));

            AddStep("stop", () => EditorClock.Stop());
            AddUntilStep("updated after stopping", () => visibleTexts.Contains(SlopHitsoundEditorStrings.NothingSelected.ToString()));
        }

        private OsuTextBox? renameTextBox => Editor.ChildrenOfType<HitsoundLaneHeaders>().Single().ChildrenOfType<OsuTextBox>().SingleOrDefault();

        private IEnumerable<string> headerTexts => Editor.ChildrenOfType<HitsoundLaneHeaders>().Single().ChildrenOfType<SpriteText>().Select(t => t.Text.ToString());

        private IEnumerable<string> visibleTexts => toolbox.ChildrenOfType<SpriteText>().Where(isVisible).Select(t => t.Text.ToString());

        private Vector2 headerPosition(HitsoundLane lane)
        {
            var headers = Editor.ChildrenOfType<HitsoundLaneHeaders>().Single();
            var lanes = hitsoundEditor.VisibleLanes.ToList();

            return headers.ToScreenSpace(new Vector2(headers.DrawWidth / 2, (lanes.IndexOf(lane) + 0.5f) * headers.DrawHeight / lanes.Count));
        }

        [Test]
        public void TestSubLaneAdditionInHitsoundDifficulty()
        {
            var softClap = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP).WithCustomIndex(3);

            AddStep("enable hitsound difficulty mode", () => hitsoundEditor.HitsoundDifficultyMode.Value = true);
            AddStep("turn on clap of custom sample set 3", () => hitsoundEditor.SetLane(hitsoundEditor.Map.Columns[1], softClap, true));
            AddAssert("object created for it", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(3));
            AddAssert("existing object keeps its custom sample set", () => HitsoundSamples.GetCustomIndex(EditorBeatmap.HitObjects.First(h => hasSample(h, HitSampleInfo.HIT_WHISTLE)).Samples), () => Is.EqualTo(0));
            AddAssert("clap plays", () => hitsoundEditor.Map.Columns[1].Has(softClap));

            AddStep("turn off clap again", () => hitsoundEditor.SetLane(hitsoundEditor.Map.Columns[1], softClap, false));
            AddAssert("created object deleted", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("whistle kept", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
        }

        [Test]
        public void TestFrostedLanes()
        {
            AddAssert("opaque by default", () => frostedBackground.Alpha, () => Is.EqualTo(0));

            AddStep("enable frosted lanes", () => config.SetValue(OsuSetting.SlopHitsoundEditorFrostedLanes, true));
            AddAssert("frosted background shown", () => frostedBackground.Alpha, () => Is.EqualTo(1));

            AddStep("disable frosted lanes", () => config.SetValue(OsuSetting.SlopHitsoundEditorFrostedLanes, false));
            AddAssert("frosted background hidden", () => frostedBackground.Alpha, () => Is.EqualTo(0));
        }

        private FrostedPanelBackground frostedBackground => Editor.ChildrenOfType<HitsoundGrid>().Single().ChildrenOfType<FrostedPanelBackground>().Single();

        private HitsoundToggleButton visibleToggle(LocalisableString tooltip) => toolbox.ChildrenOfType<HitsoundToggleButton>().First(b => isVisible(b) && b.TooltipText.Equals(tooltip));

        /// <summary>
        /// Whether a drawable and all of its parents are present, as hidden content of the toolbox stays present itself.
        /// </summary>
        private static bool isVisible(Drawable drawable)
        {
            for (Drawable? d = drawable; d != null; d = d.Parent)
            {
                if (!d.IsPresent)
                    return false;
            }

            return true;
        }

        private HitsoundToolbox toolbox => Editor.ChildrenOfType<HitsoundToolbox>().Single();

        private bool timelinePartsLineUp()
        {
            var strip = Editor.ChildrenOfType<HitsoundObjectStrip>().Single().ScreenSpaceDrawQuad;
            var lanes = Editor.ChildrenOfType<HitsoundLaneArea>().Single().ScreenSpaceDrawQuad;

            return Math.Abs(strip.TopLeft.X - lanes.TopLeft.X) < 1 && Math.Abs(strip.TopRight.X - lanes.TopRight.X) < 1;
        }

        private void waitForScreen() => AddUntilStep("wait for hitsound screen", () => screen?.IsLoaded == true);

        private static bool hasSample(HitObject hitObject, string name) => hitObject.Samples.Any(s => s.Name == name);

        private static HitCircle createCircle(double time, string? addition = null, string bank = HitSampleInfo.BANK_NORMAL)
        {
            var samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, bank) };

            if (addition != null)
                samples.Add(new HitSampleInfo(addition, bank));

            return new HitCircle
            {
                StartTime = time,
                Position = new Vector2(256, 192),
                Samples = samples,
            };
        }
    }
}
