// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneBeatmapStatistics : TestSceneOsuEditor
    {
        [Test]
        public void TestStatisticsDisplayed()
        {
            AddUntilStep("difficulty calculated", () => Editor.ChildrenOfType<EditorBeatmapDifficulty>().Single().Info.Value, () => Is.Not.Null);

            AddAssert("max combo matches beatmap", () => Editor.ChildrenOfType<EditorBeatmapDifficulty>().Single().Info.Value!.MaxCombo, () => Is.GreaterThan(0));
            AddAssert("star rating positive", () => Editor.ChildrenOfType<EditorBeatmapDifficulty>().Single().Info.Value!.StarRating, () => Is.GreaterThan(0));

            AddUntilStep("timing displayed", () => statisticsText().Contains("BPM@4/4"));
            AddUntilStep("max combo displayed", () => statisticsText().Contains($"{Editor.ChildrenOfType<EditorBeatmapDifficulty>().Single().Info.Value!.MaxCombo}x"));
        }

        [Test]
        public void TestRecalculatedOnChange()
        {
            int initialCombo = 0;

            AddUntilStep("difficulty calculated", () => Editor.ChildrenOfType<EditorBeatmapDifficulty>().Single().Info.Value, () => Is.Not.Null);
            AddStep("store max combo", () => initialCombo = Editor.ChildrenOfType<EditorBeatmapDifficulty>().Single().Info.Value!.MaxCombo);

            AddStep("remove first object", () => EditorBeatmap.Remove(EditorBeatmap.HitObjects.First()));
            AddUntilStep("max combo decreased", () => Editor.ChildrenOfType<EditorBeatmapDifficulty>().Single().Info.Value!.MaxCombo, () => Is.LessThan(initialCombo));
        }

        /// <summary>
        /// The text displayed by the statistics inspector, without whitespace (as the text flow splits words into separate sprites).
        /// </summary>
        private string statisticsText() => string.Concat(Editor.ChildrenOfType<BeatmapStatisticsInspector>().Single().ChildrenOfType<OsuSpriteText>().Select(t => t.Text.ToString())).Replace(" ", string.Empty);
    }
}
