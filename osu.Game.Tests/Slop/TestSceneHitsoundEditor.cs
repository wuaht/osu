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
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Rulesets.Edit;
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
        public void TestMoveSelectionToOtherLanes()
        {
            HitsoundEditor hitsoundEditor = null!;

            AddStep("show all lanes", () =>
            {
                hitsoundEditor = screen!.ChildrenOfType<HitsoundEditor>().Single();
                hitsoundEditor.HideUnusedLanes.Value = false;
            });

            AddStep("select first hitnormal", () => hitsoundEditor.SetSelection(new[] { 0 }, lanes: new[] { new HitsoundLane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL) }));
            AddStep("move to soft hitnormal", () => hitsoundEditor.MoveSelectionLanes(4));
            AddAssert("hitnormal moved to soft", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[0].Samples), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("selection followed", () => hitsoundEditor.GetSelectedLanes(0), () => Is.EquivalentTo(new[] { new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_NORMAL) }));

            AddStep("press Ctrl+Shift+Down", () => pressWithControlShift(Key.Down));
            AddAssert("hitnormal moved a bank to drum", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[0].Samples), () => Is.EqualTo(HitSampleInfo.BANK_DRUM));
            AddAssert("no addition added", () => !EditorBeatmap.HitObjects[0].Samples.Any(HitsoundSamples.IsAddition));

            AddStep("press Ctrl+Shift+Up", () => pressWithControlShift(Key.Up));
            AddAssert("hitnormal moved back a bank", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[0].Samples), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));

            AddStep("press Ctrl+Down", () => pressWithControl(Key.Down));
            AddAssert("hitnormal alone moved one lane into soft whistle", () => EditorBeatmap.HitObjects[0].Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE && s.Bank == HitSampleInfo.BANK_SOFT));
            AddStep("undo", () => Editor.Undo());

            AddStep("select whole second hitsound", () => hitsoundEditor.SetSelection(new[] { 500 }));
            AddStep("press Ctrl+Down", () => pressWithControl(Key.Down));
            AddAssert("whistle moved one lane to finish", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_FINISH) && !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
            AddAssert("hitnormal stayed", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[1].Samples), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddStep("undo", () => Editor.Undo());

            AddStep("select second whistle", () => hitsoundEditor.SetSelection(new[] { 500 }, lanes: new[] { new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE) }));
            AddStep("move whistle to normal bank", () => hitsoundEditor.MoveSelectionBanks(-1));
            AddAssert("whistle is normal", () => EditorBeatmap.HitObjects[1].Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE && s.Bank == HitSampleInfo.BANK_NORMAL));
            AddAssert("selection followed to normal whistle", () => hitsoundEditor.GetSelectedLanes(500), () => Is.EquivalentTo(new[] { new HitsoundLane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_WHISTLE) }));
            AddStep("move whistle back", () => hitsoundEditor.MoveSelectionBanks(1));

            AddStep("select second whistle", () => hitsoundEditor.SetSelection(new[] { 500 }, lanes: new[] { new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE) }));
            AddStep("move to normal hitnormal", () => hitsoundEditor.MoveSelectionLanes(-5));
            AddAssert("whistle removed", () => !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
            AddAssert("whistle became normal hitnormal", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[1].Samples), () => Is.EqualTo(HitSampleInfo.BANK_NORMAL));

            AddStep("undo", () => Editor.Undo());
            AddAssert("whistle restored", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
            AddAssert("hitnormal restored", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[1].Samples), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));

            void pressWithControl(Key key)
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(key);
                InputManager.ReleaseKey(Key.ControlLeft);
            }

            void pressWithControlShift(Key key)
            {
                InputManager.PressKey(Key.ShiftLeft);
                pressWithControl(key);
                InputManager.ReleaseKey(Key.ShiftLeft);
            }
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
                var hitsoundEditor = screen!.ChildrenOfType<HitsoundEditor>().Single();
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

            AddAssert("hitsound difficulty mode enabled", () => screen!.ChildrenOfType<HitsoundEditor>().Single().HitsoundDifficultyMode.Value);
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
            AddUntilStep("copy finished", () => HitsoundDifficultySync.Running?.IsCompleted != false);

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
        public void TestCopyToSeveralDifficultiesKeepsTheirFiles()
        {
            AddStep("create copy of difficulty", () => Editor.CreateNewDifficulty(new OsuRuleset().RulesetInfo));
            AddUntilStep("wait for dialog", () => DialogOverlay.CurrentDialog is CreateNewDifficultyDialog);
            AddStep("choose copy", () => DialogOverlay.CurrentDialog!.Buttons.ElementAt(1).TriggerClick());
            AddUntilStep("switched to copy", () => Editor.ReadyForUse && Editor.ChildrenOfType<EditorBeatmap>().SingleOrDefault()?.BeatmapInfo.DifficultyName.EndsWith("(copy)", StringComparison.Ordinal) == true);
            AddStep("save", () => Editor.Save());
            AddStep("switch to hitsound screen", () => Editor.Mode.Value = EditorScreenMode.Hitsound);
            waitForScreen();

            createHitsoundDifficulty();

            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddStep("press E", () => InputManager.Key(Key.E));

            AddStep("copy to other difficulties", () => screen!.CopyToDifficulties(screen.GetOtherDifficulties(), new HitsoundCopyOptions()));
            AddUntilStep("wait for confirmation", () => DialogOverlay.CurrentDialog is HitsoundCopyConfirmationDialog);
            AddStep("confirm", () => DialogOverlay.CurrentDialog!.PerformAction<PopupDialogDangerousButton>());
            AddUntilStep("copy finished", () => HitsoundDifficultySync.Running?.IsCompleted != false);
            AddUntilStep("editor reloaded", () => Editor.ReadyForUse && Editor.ChildrenOfType<EditorBeatmap>().SingleOrDefault()?.BeatmapInfo.DifficultyName == "Hitsounds");

            AddAssert("every difficulty still has its file and objects", () =>
            {
                var set = beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!.BeatmapSet!;

                return set.Beatmaps.Count == 3
                       && set.Beatmaps.All(b => b.Path != null && beatmapManager.GetWorkingBeatmap(b, true).Beatmap.HitObjects.Count == 2);
            });

            AddAssert("finish copied to both difficulties", () =>
            {
                var set = beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!.BeatmapSet!;

                return set.Beatmaps.Where(b => b.DifficultyName != "Hitsounds")
                          .All(b => hasSample(beatmapManager.GetWorkingBeatmap(b, true).Beatmap.HitObjects[0], HitSampleInfo.HIT_FINISH));
            });
        }

        [Test]
        public void TestHitsoundsSyncedWhenLeavingHitsoundDifficulty()
        {
            var errors = new List<string>();

            void onEntry(LogEntry e)
            {
                if (e.Level == LogLevel.Error)
                    errors.Add(e.Message);
            }

            AddStep("enable sync", () => config.SetValue(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, true));
            AddStep("listen for errors", () => Logger.NewEntry += onEntry);

            createHitsoundDifficulty();

            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddStep("press E", () => InputManager.Key(Key.E));
            AddStep("press Ctrl+S", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.S);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddStep("switch to first difficulty", () => screen!.SwitchToDifficulty(beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!));
            AddUntilStep("first difficulty loaded", () => Editor.ReadyForUse && EditorBeatmap.BeatmapInfo.DifficultyName == firstDifficultyName);

            AddAssert("loaded with the synced finish", () => hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_FINISH));
            AddAssert("finish saved", () =>
            {
                var info = beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!;
                return hasSample(beatmapManager.GetWorkingBeatmap(info, true).Beatmap.HitObjects[0], HitSampleInfo.HIT_FINISH);
            });
            AddStep("stop listening for errors", () => Logger.NewEntry -= onEntry);
            AddAssert("no errors", () => errors, () => Is.Empty);
        }

        [Test]
        public void TestSyncedVolumesSurviveSaving()
        {
            AddStep("enable sync", () => config.SetValue(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, true));

            // the file of the first difficulty then has a sample point for the second volume, which the copied volumes must replace.
            AddStep("set different volumes", () =>
            {
                var hitsoundEditor = screen!.ChildrenOfType<HitsoundEditor>().Single();
                hitsoundEditor.SetVolumes(new[] { (hitsoundEditor.Map.Columns[0], 100), (hitsoundEditor.Map.Columns[1], 50) });
            });
            AddStep("save", () => Editor.Save());

            createHitsoundDifficulty();

            AddStep("set same volume everywhere", () =>
            {
                var hitsoundEditor = screen!.ChildrenOfType<HitsoundEditor>().Single();
                hitsoundEditor.SetVolume(hitsoundEditor.Map.Columns.ToList(), 70);
            });
            AddStep("save", () => Editor.Save());

            AddStep("switch to first difficulty", () => screen!.SwitchToDifficulty(beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!));
            AddUntilStep("first difficulty loaded", () => Editor.ReadyForUse && EditorBeatmap.BeatmapInfo.DifficultyName == firstDifficultyName);

            AddAssert("volumes copied", () => EditorBeatmap.HitObjects.Select(h => HitsoundSamples.GetVolume(h.Samples)), () => Is.EqualTo(new[] { 70, 70 }));
        }

        [Test]
        public void TestCopyToSelectedIsNotOverriddenBySync()
        {
            AddStep("enable sync", () => config.SetValue(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, true));

            createHitsoundDifficulty();

            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddStep("press E", () => InputManager.Key(Key.E));

            AddStep("copy everything but additions", () => screen!.CopyToDifficulties(screen.GetOtherDifficulties(), new HitsoundCopyOptions { CopyAdditions = false }));
            AddUntilStep("wait for confirmation", () => DialogOverlay.CurrentDialog is HitsoundCopyConfirmationDialog);
            AddStep("hold confirm", () =>
            {
                InputManager.MoveMouseTo(DialogOverlay.CurrentDialog!.ChildrenOfType<PopupDialogDangerousButton>().Single());
                InputManager.PressButton(MouseButton.Left);
            });
            AddUntilStep("dialog closed", () => DialogOverlay.CurrentDialog == null);
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
            AddUntilStep("copy finished", () => HitsoundDifficultySync.Running?.IsCompleted != false);
            AddUntilStep("editor reloaded", () => Editor.ReadyForUse && EditorBeatmap.BeatmapInfo.DifficultyName == "Hitsounds");
            AddWaitStep("wait for any sync", 10);

            AddAssert("finish not copied", () =>
            {
                var info = beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!;
                return !hasSample(beatmapManager.GetWorkingBeatmap(info, true).Beatmap.HitObjects[0], HitSampleInfo.HIT_FINISH);
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestHitnormalAndAdditionOfSameBankShareObject(bool additionFirst)
        {
            var softNormal = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_NORMAL);
            var softWhistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);

            createHitsoundDifficulty();

            AddStep("show all lanes", () => hitsoundEditor.HideUnusedLanes.Value = false);
            AddStep("seek to empty time", () => EditorClock.Seek(1000));
            AddWaitStep("wait for seek", 2);

            AddStep("click first lane", () => clickLane(additionFirst ? softWhistle : softNormal, MouseButton.Left));
            AddAssert("one object created", () => objectsAt(1000).Count(), () => Is.EqualTo(1));
            AddStep("click second lane", () => clickLane(additionFirst ? softNormal : softWhistle, MouseButton.Left));

            AddAssert("still one object", () => objectsAt(1000).Count(), () => Is.EqualTo(1));
            AddAssert("soft hitnormal", () => HitsoundSamples.GetNormalBank(objectsAt(1000).Single().Samples), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("soft whistle", () => objectsAt(1000).Single().Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE && s.Bank == HitSampleInfo.BANK_SOFT));
        }

        [Test]
        public void TestPastedObjectGetsHitsoundsOfHitsoundDifficulty()
        {
            AddStep("enable sync", () => config.SetValue(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, true));

            createHitsoundDifficulty();

            AddStep("seek to second object", () => EditorClock.Seek(500));
            AddStep("press E", () => InputManager.Key(Key.E));
            AddStep("save", () => Editor.Save());

            AddStep("switch to first difficulty", () => screen!.SwitchToDifficulty(beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!));
            AddUntilStep("first difficulty loaded", () => Editor.ReadyForUse && EditorBeatmap.BeatmapInfo.DifficultyName == firstDifficultyName);

            AddStep("switch to compose", () => InputManager.Key(Key.F1));
            AddUntilStep("compose loaded", () => Editor.ChildrenOfType<HitObjectComposer>().SingleOrDefault()?.IsLoaded == true);

            AddStep("give first object a clap", () =>
            {
                var first = EditorBeatmap.HitObjects[0];
                first.Samples = first.Samples.Append(new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM)).ToList();
                EditorBeatmap.Update(first);
            });
            AddStep("delete second object", () => EditorBeatmap.Remove(EditorBeatmap.HitObjects[1]));

            AddStep("select first object", () => EditorBeatmap.SelectedHitObjects.Add(EditorBeatmap.HitObjects[0]));
            AddStep("press Ctrl+C", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.C);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddStep("seek to second hitsound", () => EditorClock.Seek(500));
            AddStep("press Ctrl+V", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.V);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("pasted", () => objectsAt(500).Count(), () => Is.EqualTo(1));
            AddAssert("clap of pasted object removed", () => !hasSample(objectsAt(500).Single(), HitSampleInfo.HIT_CLAP));
            AddAssert("finish of hitsound difficulty", () => hasSample(objectsAt(500).Single(), HitSampleInfo.HIT_FINISH));
            AddAssert("soft whistle of hitsound difficulty", () => objectsAt(500).Single().Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE && s.Bank == HitSampleInfo.BANK_SOFT));
        }

        [Test]
        public void TestPastedObjectWithoutHitsoundInHitsoundDifficultyLosesAdditions()
        {
            AddStep("enable sync", () => config.SetValue(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, true));

            createHitsoundDifficulty();

            AddStep("switch to first difficulty", () => screen!.SwitchToDifficulty(beatmapManager.QueryBeatmap(b => b.DifficultyName == firstDifficultyName)!));
            AddUntilStep("first difficulty loaded", () => Editor.ReadyForUse && EditorBeatmap.BeatmapInfo.DifficultyName == firstDifficultyName);

            AddStep("switch to compose", () => InputManager.Key(Key.F1));
            AddUntilStep("compose loaded", () => Editor.ChildrenOfType<HitObjectComposer>().SingleOrDefault()?.IsLoaded == true);

            AddStep("give first object a clap", () =>
            {
                var first = EditorBeatmap.HitObjects[0];
                first.Samples = first.Samples.Append(new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM)).ToList();
                EditorBeatmap.Update(first);
            });

            AddStep("select first object", () => EditorBeatmap.SelectedHitObjects.Add(EditorBeatmap.HitObjects[0]));
            AddStep("press Ctrl+C", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.C);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            // the hitsound difficulty only has hitsounds at 0 and 500.
            AddStep("seek to time without hitsound", () => EditorClock.Seek(1000));
            AddStep("press Ctrl+V", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.V);
                InputManager.ReleaseKey(Key.ControlLeft);
            });

            AddUntilStep("pasted", () => objectsAt(1000).Count(), () => Is.EqualTo(1));
            AddAssert("clap of pasted object removed", () => !hasSample(objectsAt(1000).Single(), HitSampleInfo.HIT_CLAP));
            AddAssert("clap of copied object kept", () => hasSample(objectsAt(0).Single(), HitSampleInfo.HIT_CLAP));
        }

        [Test]
        public void TestTimestampKeepsHitsoundScreen()
        {
            AddStep("seek to start", () => EditorClock.Seek(0));
            AddStep("open link to second object", () => Editor.HandleTimestamp("00:00:500 (2) - "));

            AddAssert("still on hitsound screen", () => Editor.Mode.Value, () => Is.EqualTo(EditorScreenMode.Hitsound));
            AddUntilStep("seeked to second object", () => EditorClock.CurrentTime, () => Is.EqualTo(500).Within(1));

            AddStep("click time display", () =>
            {
                InputManager.MoveMouseTo(Editor.ChildrenOfType<TimeInfoContainer>().Single().ChildrenOfType<OsuSpriteText>().First(t => t.Text.ToString().Contains(':')));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("time box shown", () => Editor.ChildrenOfType<TimeInfoContainer>().Single().ChildrenOfType<OsuTextBox>().Single().HasFocus);
            AddStep("enter timestamp with selection", () => Editor.ChildrenOfType<TimeInfoContainer>().Single().ChildrenOfType<OsuTextBox>().Single().Text = "00:00:000 (1) -");
            AddStep("press Enter", () => InputManager.Key(Key.Enter));
            AddAssert("still on hitsound screen", () => Editor.Mode.Value, () => Is.EqualTo(EditorScreenMode.Hitsound));
            AddUntilStep("seeked to first object", () => EditorClock.CurrentTime, () => Is.EqualTo(0).Within(1));

            AddStep("switch to compose", () => InputManager.Key(Key.F1));
            AddUntilStep("compose loaded", () => Editor.ChildrenOfType<HitObjectComposer>().SingleOrDefault()?.IsLoaded == true);
            AddStep("open link to second object", () => Editor.HandleTimestamp("00:00:500 (2) - "));
            AddAssert("second object selected", () => EditorBeatmap.SelectedHitObjects, () => Is.EqualTo(new[] { EditorBeatmap.HitObjects[1] }));
        }

        private void createHitsoundDifficulty()
        {
            AddStep("create hitsound difficulty", () => screen!.CreateHitsoundDifficulty(HitSampleInfo.BANK_SOFT, 60, false));
            AddUntilStep("switched to hitsound difficulty", () => Editor.ChildrenOfType<EditorBeatmap>().SingleOrDefault()?.BeatmapInfo.DifficultyName == "Hitsounds");
            AddUntilStep("wait for editor", () => Editor.ReadyForUse);
            waitForScreen();
        }

        [Test]
        public void TestPianoRollClicks()
        {
            var whistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);
            var clap = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP);

            AddStep("seek to second object", () => EditorClock.Seek(500));

            AddStep("left click whistle", () => clickLane(whistle, MouseButton.Left));
            AddAssert("hitsound selected", () => screen!.ChildrenOfType<HitsoundEditor>().Single().IsSelected(500));
            AddAssert("whistle kept", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));

            AddStep("left click whistle again", () => clickLane(whistle, MouseButton.Left));
            AddAssert("hitsound deselected", () => !screen!.ChildrenOfType<HitsoundEditor>().Single().IsSelected(500));

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
            AddAssert("only selected in whistle lane", () => hitsoundEditor.GetSelectedLanes(500), () => Is.EquivalentTo(new[] { whistle }));

            AddStep("add clap to selected hitsound", () => hitsoundEditor.ToggleAddition(hitsoundEditor.SelectedColumns, HitSampleInfo.HIT_CLAP));
            AddStep("delete", () => InputManager.Key(Key.Delete));
            AddAssert("whistle removed", () => !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
            AddAssert("clap kept", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_CLAP));
        }

        [Test]
        public void TestPastingAdditionsKeepsHitnormal()
        {
            var whistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);

            AddStep("seek between objects", () => EditorClock.Seek(250));
            AddStep("select whistle lane of second hitsound", () =>
            {
                InputManager.MoveMouseTo(lanePosition(whistle, 300));
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(lanePosition(whistle, 450));
                InputManager.MoveMouseTo(lanePosition(whistle, 700));
                InputManager.ReleaseButton(MouseButton.Left);
            });
            AddStep("copy", () => screen!.Copy());
            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddStep("paste", () => screen!.Paste());

            AddAssert("whistle added", () => EditorBeatmap.HitObjects[0].Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE && s.Bank == HitSampleInfo.BANK_SOFT));
            AddAssert("hitnormal bank kept", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[0].Samples), () => Is.EqualTo(HitSampleInfo.BANK_NORMAL));
        }

        [Test]
        public void TestPastingHitnormalLaneChangesHitnormal()
        {
            var softNormal = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_NORMAL);
            var softWhistle = new HitsoundLane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE);

            AddStep("select hitnormal and whistle lanes of second hitsound", () => hitsoundEditor.SetSelection(new[] { 500 }, lanes: new[] { softNormal, softWhistle }));
            AddStep("copy", () => screen!.Copy());
            AddStep("seek to first object", () => EditorClock.Seek(0));
            AddStep("paste", () => screen!.Paste());

            AddAssert("hitnormal bank pasted", () => HitsoundSamples.GetNormalBank(EditorBeatmap.HitObjects[0].Samples), () => Is.EqualTo(HitSampleInfo.BANK_SOFT));
            AddAssert("whistle pasted", () => hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_WHISTLE));
        }

        [Test]
        public void TestPastingRectangleSelectionOnlyAddsCopiedAdditions()
        {
            AddStep("select first hitsound in all lanes", () => hitsoundEditor.SetSelection(new[] { 0 }, lanes: HitsoundLane.ALL));
            AddStep("copy", () => screen!.Copy());
            AddStep("seek to second object", () => EditorClock.Seek(500));
            AddStep("paste", () => screen!.Paste());

            AddAssert("whistle kept", () => hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_WHISTLE));
            AddAssert("no finish added", () => !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_FINISH));
            AddAssert("no clap added", () => !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_CLAP));
        }

        [Test]
        public void TestPatternChangesAreSyncedToAllPlaces()
        {
            AddStep("add objects for second place", () => EditorBeatmap.AddRange(new[] { createCircle(1000), createCircle(1500) }));
            AddStep("select first two hitsounds", () => hitsoundEditor.SetSelection(new[] { 0, 500 }));
            AddStep("create pattern", () => screen!.CreatePatternFromSelection());
            AddStep("seek to third object", () => EditorClock.Seek(1000));
            AddStep("place pattern", () => screen!.PlacePattern(patterns.Patterns.Single()));

            AddAssert("pattern used twice", () => patterns.Instances.Count, () => Is.EqualTo(2));
            AddAssert("whistle placed", () => hasSample(EditorBeatmap.HitObjects[3], HitSampleInfo.HIT_WHISTLE));

            AddStep("add clap at first place", () => hitsoundEditor.ToggleAddition(new[] { hitsoundEditor.Map.GetColumn(0)! }, HitSampleInfo.HIT_CLAP));
            AddAssert("clap synced to second place", () => hasSample(EditorBeatmap.HitObjects[2], HitSampleInfo.HIT_CLAP));

            AddStep("undo", () => Editor.Undo());
            AddAssert("clap removed at first place", () => !hasSample(EditorBeatmap.HitObjects[0], HitSampleInfo.HIT_CLAP));
            AddAssert("clap removed at second place", () => !hasSample(EditorBeatmap.HitObjects[2], HitSampleInfo.HIT_CLAP));

            AddStep("delete pattern", () => patterns.Delete(patterns.Patterns.Single()));
            AddAssert("hitsounds kept", () => hasSample(EditorBeatmap.HitObjects[3], HitSampleInfo.HIT_WHISTLE));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDeletingObjectOutsideHitsoundScreenIsSyncedToAllPlaces(bool deleteAsRange)
        {
            AddStep("add objects for second place", () => EditorBeatmap.AddRange(new[] { createCircle(1000), createCircle(1500) }));
            AddStep("enable hitsound difficulty mode", () => hitsoundEditor.HitsoundDifficultyMode.Value = true);
            AddStep("select first two hitsounds", () => hitsoundEditor.SetSelection(new[] { 0, 500 }));
            AddStep("create pattern", () => screen!.CreatePatternFromSelection());
            AddStep("seek to second place", () => EditorClock.Seek(1000));
            AddStep("place pattern", () => screen!.PlacePattern(patterns.Patterns.Single()));

            // like deleting the object in the compose screen.
            AddStep("delete object at second place", () =>
            {
                var hitObject = objectsAt(1500).Single();

                if (deleteAsRange)
                    EditorBeatmap.RemoveRange(new[] { hitObject });
                else
                    EditorBeatmap.Remove(hitObject);
            });
            AddUntilStep("object deleted at first place", () => objectsAt(500), () => Is.Empty);

            AddStep("undo", () => Editor.Undo());
            AddUntilStep("objects back at both places", () => objectsAt(500).Count() == 1 && objectsAt(1500).Count() == 1);
        }

        [Test]
        public void TestPatternKeepsCustomSampleSetsOfEachObject()
        {
            AddStep("stack objects with an addition from custom sample set 2", () =>
            {
                var carrier = createCircle(0);
                carrier.Samples = new List<HitSampleInfo>
                {
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL, suffix: "2", useBeatmapSamples: true),
                    new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_SOFT, suffix: "2", useBeatmapSamples: true),
                };

                EditorBeatmap.AddRange(new HitObject[] { carrier, createCircle(1000), createCircle(1000), createCircle(1500) });
            });
            AddStep("select first two hitsounds", () => hitsoundEditor.SetSelection(new[] { 0, 500 }));
            AddStep("create pattern", () => screen!.CreatePatternFromSelection());
            AddStep("seek to second place", () => EditorClock.Seek(1000));
            AddStep("place pattern", () => screen!.PlacePattern(patterns.Patterns.Single()));

            AddAssert("clap placed from custom sample set 2", () => objectsAt(1000).Any(h => h.Samples.Any(s => s.Name == HitSampleInfo.HIT_CLAP && s.Suffix == "2")));
            AddAssert("other object keeps the default samples", () => objectsAt(1000).Any(h => HitsoundSamples.GetCustomIndex(h.Samples) == 0));
        }

        [Test]
        public void TestMakePatternUnique()
        {
            AddStep("add objects for second place", () => EditorBeatmap.AddRange(new[] { createCircle(1000), createCircle(1500) }));
            AddStep("select first two hitsounds", () => hitsoundEditor.SetSelection(new[] { 0, 500 }));
            AddStep("create pattern", () => screen!.CreatePatternFromSelection());
            AddStep("seek to second place", () => EditorClock.Seek(1000));
            AddStep("place pattern", () => screen!.PlacePattern(patterns.Patterns.Single()));

            AddStep("make second place unique", () => screen!.MakePatternUnique(patterns.Occurrences.Single(o => o.StartTime == 1000)));
            AddAssert("two patterns", () => patterns.Patterns.Count, () => Is.EqualTo(2));
            AddAssert("each used once", () => patterns.Patterns.All(p => patterns.GetOccurrences(p).Count() == 1));

            AddStep("add clap at second place", () => hitsoundEditor.ToggleAddition(new[] { hitsoundEditor.Map.GetColumn(1000)! }, HitSampleInfo.HIT_CLAP));
            AddAssert("first place unchanged", () => !hasSample(objectsAt(0).Single(), HitSampleInfo.HIT_CLAP));

            AddStep("undo clap", () => Editor.Undo());
            AddStep("undo make unique", () => Editor.Undo());
            AddAssert("one pattern again", () => patterns.Patterns.Count, () => Is.EqualTo(1));
            AddAssert("used at both places", () => patterns.GetOccurrences(patterns.Patterns.Single()).Count(), () => Is.EqualTo(2));

            AddStep("redo make unique", () => Editor.Redo());
            AddAssert("two patterns again", () => patterns.Patterns.Count, () => Is.EqualTo(2));
        }

        [Test]
        public void TestSubPatternIsUsedInAllPlacesOfItsPattern()
        {
            AddStep("add objects for second place", () => EditorBeatmap.AddRange(new[] { createCircle(1000), createCircle(1500) }));
            AddStep("select first two hitsounds", () => hitsoundEditor.SetSelection(new[] { 0, 500 }));
            AddStep("create pattern", () => screen!.CreatePatternFromSelection());
            AddStep("seek to second place", () => EditorClock.Seek(1000));
            AddStep("place pattern", () => screen!.PlacePattern(patterns.Patterns.Single()));

            AddStep("select second hitsound", () => hitsoundEditor.SetSelection(new[] { 500 }));
            AddStep("create sub-pattern", () => screen!.CreatePatternFromSelection());
            AddAssert("sub-pattern used in both places", () => patterns.Occurrences.Where(o => o.Depth == 1).Select(o => o.StartTime), () => Is.EqualTo(new double[] { 500, 1500 }));

            AddStep("add finish in second place of sub-pattern", () => hitsoundEditor.ToggleAddition(new[] { hitsoundEditor.Map.GetColumn(1500)! }, HitSampleInfo.HIT_FINISH));
            AddAssert("finish synced to first place", () => hasSample(objectsAt(500).Single(), HitSampleInfo.HIT_FINISH));

            AddStep("try placing pattern inside itself", () => EditorClock.Seek(0));
            AddAssert("pattern can't contain itself", () => hitsoundEditor.PlacePattern(patterns.Patterns.First(p => p.SubPatterns.Count > 0), 0), () => Is.Null);
        }

        [Test]
        public void TestMoveAndRemovePattern()
        {
            AddStep("add objects for second place", () => EditorBeatmap.AddRange(new[] { createCircle(1000), createCircle(1500) }));
            AddStep("select first two hitsounds", () => hitsoundEditor.SetSelection(new[] { 0, 500 }));
            AddStep("create pattern", () => screen!.CreatePatternFromSelection());

            AddStep("move pattern to second place", () => screen!.MovePatternOccurrence(patterns.Occurrences.Single(), 1000));
            AddAssert("pattern moved", () => patterns.Instances.Single().StartTime, () => Is.EqualTo(1000));
            AddAssert("whistle moved along", () => hasSample(objectsAt(1500).Single(), HitSampleInfo.HIT_WHISTLE));
            AddAssert("whistle removed at old place", () => !hasSample(objectsAt(500).Single(), HitSampleInfo.HIT_WHISTLE));

            AddStep("remove pattern from here", () => screen!.RemovePatternOccurrence(patterns.Occurrences.Single(), keepHitsounds: false));
            AddAssert("pattern not used anymore", () => patterns.Occurrences.Count, () => Is.EqualTo(0));
            AddAssert("its hitsounds removed", () => !hasSample(objectsAt(1500).Single(), HitSampleInfo.HIT_WHISTLE));

            AddStep("undo", () => Editor.Undo());
            AddAssert("pattern and hitsounds back", () => patterns.Occurrences.Count == 1 && hasSample(objectsAt(1500).Single(), HitSampleInfo.HIT_WHISTLE));
        }

        private IEnumerable<HitObject> objectsAt(double time) => EditorBeatmap.HitObjects.Where(h => Math.Abs(h.StartTime - time) < 1);

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

        private HitsoundEditor hitsoundEditor => screen!.ChildrenOfType<HitsoundEditor>().Single();

        private HitsoundPatterns patterns => screen!.Patterns;

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
            var lanes = screen!.ChildrenOfType<HitsoundEditor>().Single().VisibleLanes;
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

            AddStep("enable expanding on hover", () => config.SetValue(OsuSetting.SlopHitsoundEditorToolboxExpandOnHover, true));
            AddStep("hover toolbox", () => InputManager.MoveMouseTo(toolbox));
            AddUntilStep("toolbox expanded", () => toolbox.DrawWidth, () => Is.EqualTo(HitsoundToolbox.EXPANDED_WIDTH).Within(1));

            AddStep("move mouse away", () => InputManager.MoveMouseTo(Content.ScreenSpaceDrawQuad.TopLeft + new Vector2(10)));
            AddUntilStep("toolbox contracted", () => toolbox.DrawWidth, () => Is.EqualTo(HitsoundToolbox.CONTRACTED_WIDTH).Within(1));

            double hoverStartTime = 0;

            AddStep("disable expanding on hover", () => config.SetValue(OsuSetting.SlopHitsoundEditorToolboxExpandOnHover, false));
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
            AddAssert("no object stacked for it", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("existing object switches custom sample set", () => HitsoundSamples.GetCustomIndex(EditorBeatmap.HitObjects[1].Samples), () => Is.EqualTo(3));
            AddAssert("clap plays", () => hitsoundEditor.Map.Columns[1].Has(softClap));

            AddStep("turn off clap again", () => hitsoundEditor.SetLane(hitsoundEditor.Map.Columns[1], softClap, false));
            AddAssert("object kept", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("clap removed", () => !hasSample(EditorBeatmap.HitObjects[1], HitSampleInfo.HIT_CLAP));
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
