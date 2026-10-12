// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    /// <summary>
    /// Dropping a .osu file of another difficulty of the beatmap set than the one which is open.
    /// </summary>
    [Category("slop")]
    public partial class TestSceneGuestDifficultyImportIntoSet : EditorTestScene
    {
        private const string guest_name = "Guest";
        private const string copy_name = "Guest (copy)";

        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override bool IsolateSavingFromDatabase => false;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null) => new DummyWorkingBeatmap(Audio, null);

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private string title = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("make beatmap unique", () =>
            {
                EditorBeatmap.Metadata.Title = title = Guid.NewGuid().ToString();
                EditorBeatmap.BeatmapInfo.DifficultyName = guest_name;
            });
            AddStep("add timing point", () => EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 }));
            AddStep("add hit object", () => EditorBeatmap.Add(new HitCircle { StartTime = 0 }));
            AddStep("set approach rate", () => EditorBeatmap.Difficulty.ApproachRate = 9);
            AddStep("save", () => Editor.Save());

            AddStep("create copy of difficulty", () => Editor.CreateNewDifficulty(new OsuRuleset().RulesetInfo));
            AddUntilStep("wait for dialog", () => DialogOverlay.CurrentDialog is CreateNewDifficultyDialog);
            AddStep("choose copy", () => DialogOverlay.CurrentDialog!.Buttons.ElementAt(1).TriggerClick());
            AddUntilStep("switched to copy", () => Editor.ReadyForUse && openDifficulty?.BeatmapInfo.DifficultyName == copy_name);
            AddStep("save", () => Editor.Save());
        }

        private EditorBeatmap? openDifficulty => Editor.ChildrenOfType<EditorBeatmap>().SingleOrDefault();

        private BeatmapSetInfo set => realm.Run(r => r.All<BeatmapSetInfo>().AsEnumerable().Single(s => !s.DeletePending && s.Metadata.Title == title).Detach());

        private IBeatmap savedDifficulty(string name) => beatmapManager.GetWorkingBeatmap(set.Beatmaps.Single(b => b.DifficultyName == name), true).Beatmap;

        /// <summary>
        /// Writes the open difficulty as the guest difficulty with an approach rate of 3 and another circle.
        /// </summary>
        private string writeGuestFile(string name = guest_name)
        {
            string text;

            using (var ms = new MemoryStream())
            {
                using (var sw = new StreamWriter(ms, Encoding.UTF8, 1024, true))
                    new LegacyBeatmapEncoder(openDifficulty!, null, null).Encode(sw);

                text = Encoding.UTF8.GetString(ms.ToArray());
            }

            Beatmap beatmap;

            using (var reader = new LineBufferedReader(new MemoryStream(Encoding.UTF8.GetBytes(text))))
                beatmap = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

            beatmap.BeatmapInfo.DifficultyName = name;
            beatmap.Difficulty.ApproachRate = 3;
            beatmap.HitObjects.Add(new HitCircle { StartTime = 1000 });

            string path = Path.Combine(Path.GetTempPath(), $"guest-{Guid.NewGuid()}.osu");

            using (var sw = new StreamWriter(path))
                new LegacyBeatmapEncoder(beatmap, null, null).Encode(sw);

            return path;
        }

        [Test]
        public void TestOtherDifficultyIsReplaced()
        {
            object? before = null;

            AddStep("drop guest difficulty", () =>
            {
                before = Editor;
                Editor.ImportGuestDifficulty(writeGuestFile());
            });
            AddUntilStep("editor reloaded", () => Editor != before && Editor.ReadyForUse && openDifficulty?.BeatmapInfo.DifficultyName == copy_name);

            AddAssert("open difficulty unchanged", () => openDifficulty!.HitObjects.Count, () => Is.EqualTo(1));
            AddAssert("open approach rate unchanged", () => openDifficulty!.Difficulty.ApproachRate, () => Is.EqualTo(9));

            AddAssert("guest difficulty replaced", () => savedDifficulty(guest_name).HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("guest approach rate replaced", () => savedDifficulty(guest_name).Difficulty.ApproachRate, () => Is.EqualTo(3));
            AddAssert("copy unchanged on disk", () => savedDifficulty(copy_name).HitObjects.Count, () => Is.EqualTo(1));
            AddAssert("set has two difficulties", () => set.Beatmaps.Count, () => Is.EqualTo(2));
            AddAssert("both difficulties still have their files", () => realm.Run(r =>
            {
                var s = r.All<BeatmapSetInfo>().AsEnumerable().Single(s => !s.DeletePending && s.Metadata.Title == title);
                return s.Beatmaps.All(b => s.Files.Any(f => f.File.Hash == b.Hash));
            }));
            AddAssert("editor's set contains the replaced difficulty", () =>
                openDifficulty!.BeatmapInfo.BeatmapSet!.Beatmaps.Single(b => b.DifficultyName == guest_name).Difficulty.ApproachRate, () => Is.EqualTo(3));
        }

        [Test]
        public void TestUnsavedChangesOfOpenDifficultyAreSaved()
        {
            AddStep("add hit object to open difficulty", () => openDifficulty!.Add(new HitCircle { StartTime = 2000 }));
            AddStep("drop guest difficulty", () => Editor.ImportGuestDifficulty(writeGuestFile()));
            AddUntilStep("guest difficulty replaced", () => savedDifficulty(guest_name).Difficulty.ApproachRate, () => Is.EqualTo(3));
            AddUntilStep("editor reloaded", () => Editor.ReadyForUse && openDifficulty?.BeatmapInfo.DifficultyName == copy_name);
            AddAssert("open difficulty kept its change", () => openDifficulty!.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("change saved", () => savedDifficulty(copy_name).HitObjects.Count, () => Is.EqualTo(2));
        }

        [Test]
        public void TestDifficultyNotInSetIsAdded()
        {
            object? before = null;

            AddStep("drop unknown difficulty", () =>
            {
                before = Editor;
                Editor.ImportGuestDifficulty(writeGuestFile("Unknown"));
            });
            AddUntilStep("editor reloaded", () => Editor != before && Editor.ReadyForUse && openDifficulty?.BeatmapInfo.DifficultyName == copy_name);

            AddAssert("open difficulty unchanged", () => openDifficulty!.HitObjects.Count, () => Is.EqualTo(1));
            AddAssert("set has three difficulties", () => set.Beatmaps.Count, () => Is.EqualTo(3));
            AddAssert("difficulty added", () => savedDifficulty("Unknown").HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("approach rate of added difficulty", () => savedDifficulty("Unknown").Difficulty.ApproachRate, () => Is.EqualTo(3));
            AddAssert("guest difficulty unchanged", () => savedDifficulty(guest_name).HitObjects.Count, () => Is.EqualTo(1));
            AddAssert("all difficulties have their files", () => realm.Run(r =>
            {
                var s = r.All<BeatmapSetInfo>().AsEnumerable().Single(s => !s.DeletePending && s.Metadata.Title == title);
                return s.Beatmaps.All(b => s.Files.Any(f => f.File.Hash == b.Hash));
            }));
            AddAssert("editor's set contains the added difficulty", () => openDifficulty!.BeatmapInfo.BeatmapSet!.Beatmaps.Any(b => b.DifficultyName == "Unknown"));
        }
    }
}
