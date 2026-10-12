// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneGuestDifficultyImport : EditorTestScene
    {
        private const string resource_name = "Soleily - Renatus (Gamu) [Insane].osu";

        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            using var reader = new LineBufferedReader(TestResources.OpenResource(resource_name));
            return osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
        }

        private static string originalText
        {
            get
            {
                using var reader = new StreamReader(TestResources.OpenResource(resource_name));
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// The stable file with a changed approach rate, a removed circle and an added circle with a whistle, and extra trailing lines.
        /// </summary>
        private static string updatedText => originalText
                                             .Replace("ApproachRate:9", "ApproachRate:7")
                                             .Replace("304,56,1285,1,8,0:0:0:0:\r\n", string.Empty)
                                             .Replace("[HitObjects]\r\n", "[HitObjects]\r\n256,192,1300,1,2,0:0:0:0:\r\n")
                                         + "\r\n\r\n";

        private string writeFile(string text)
        {
            string path = Path.Combine(Path.GetTempPath(), $"guest-{Guid.NewGuid()}.osu");
            File.WriteAllText(path, text);
            return path;
        }

        private int originalCount;

        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("store object count", () => originalCount = EditorBeatmap.HitObjects.Count);
        }

        [Test]
        public void TestImportUpdatesDifficulty()
        {
            AddStep("drop updated file", () => Editor.ImportGuestDifficulty(writeFile(updatedText)));

            AddAssert("approach rate updated", () => EditorBeatmap.Difficulty.ApproachRate, () => Is.EqualTo(7));
            AddAssert("same object count", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(originalCount));
            AddAssert("removed circle gone", () => EditorBeatmap.HitObjects.Any(h => h.StartTime == 1285), () => Is.False);
            AddAssert("added circle with whistle", () => EditorBeatmap.HitObjects.Single(h => h.StartTime == 1300).Samples.Any(s => s.Name == osu.Game.Audio.HitSampleInfo.HIT_WHISTLE));

            AddStep("undo", () => Editor.Undo());
            AddAssert("approach rate restored", () => EditorBeatmap.Difficulty.ApproachRate, () => Is.EqualTo(9));
            AddAssert("removed circle back", () => EditorBeatmap.HitObjects.Any(h => h.StartTime == 1285));
            AddAssert("added circle gone", () => EditorBeatmap.HitObjects.Any(h => h.StartTime == 1300), () => Is.False);
        }

        [Test]
        public void TestUnchangedFileChangesNothing()
        {
            AddStep("drop original file", () => Editor.ImportGuestDifficulty(writeFile(originalText)));
            AddAssert("same objects", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(originalCount));
            AddAssert("no unsaved changes", () => Editor.HasUnsavedChanges, () => Is.False);
        }

        [Test]
        public void TestOtherSetIsRejected()
        {
            AddStep("drop file of other song", () => Editor.ImportGuestDifficulty(writeFile(updatedText.Replace("Title:Renatus", "Title:Other").Replace("BeatmapSetID:241526", "BeatmapSetID:-1"))));
            AddAssert("approach rate unchanged", () => EditorBeatmap.Difficulty.ApproachRate, () => Is.EqualTo(9));
            AddAssert("objects unchanged", () => EditorBeatmap.HitObjects.Any(h => h.StartTime == 1285));
        }

        [Test]
        public void TestOtherRulesetIsRejected()
        {
            AddStep("drop osu!mania file", () => Editor.ImportGuestDifficulty(writeFile(updatedText.Replace("Mode: 0", "Mode: 3"))));
            AddAssert("approach rate unchanged", () => EditorBeatmap.Difficulty.ApproachRate, () => Is.EqualTo(9));
        }

        [Test]
        public void TestOtherDifficultyNameIsConfirmed()
        {
            AddStep("drop file of other difficulty", () => Editor.ImportGuestDifficulty(writeFile(updatedText.Replace("Version:Insane", "Version:Hard"))));
            AddUntilStep("confirmation shown", () => DialogOverlay.CurrentDialog is GuestDifficultyReplaceDialog);
            AddAssert("not applied yet", () => EditorBeatmap.Difficulty.ApproachRate, () => Is.EqualTo(9));

            AddStep("hold confirm", () =>
            {
                InputManager.MoveMouseTo(DialogOverlay.CurrentDialog!.ChildrenOfType<PopupDialogDangerousButton>().Single());
                InputManager.PressButton(MouseButton.Left);
            });
            AddUntilStep("dialog closed", () => DialogOverlay.CurrentDialog == null);
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("applied", () => EditorBeatmap.Difficulty.ApproachRate, () => Is.EqualTo(7));
            AddAssert("difficulty name kept", () => EditorBeatmap.BeatmapInfo.DifficultyName, () => Is.EqualTo("Insane"));
        }
    }
}
