// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneStrainGraph : TestSceneOsuEditor
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private EditorBeatmapDifficultyInfo? info => Editor.ChildrenOfType<EditorBeatmapDifficulty>().Single().Info.Value;

        [Test]
        public void TestStrainsCoverBeatmap()
        {
            AddUntilStep("difficulty calculated", () => info, () => Is.Not.Null);

            AddAssert("strains cover all objects", () => info!.Strains.Length * EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH,
                () => Is.GreaterThan(EditorBeatmap.HitObjects.Max(h => h.StartTime)));
            AddAssert("strains relative to hardest section", () => info!.Strains.Max(), () => Is.EqualTo(1).Within(0.0001));
            AddAssert("strains not negative", () => info!.Strains.All(s => s >= 0));

            // strains are only located where there are objects.
            AddAssert("no strain before first object", () => info!.Strains
                                                                   .Take((int)(EditorBeatmap.HitObjects.Min(h => h.StartTime) / EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH))
                                                                   .All(s => s == 0));
        }

        [Test]
        public void TestGraphVisibility()
        {
            AddStep("disable strains", () => config.SetValue(OsuSetting.SlopEditorShowDifficultyStrains, false));
            AddUntilStep("graph hidden", () => Editor.ChildrenOfType<StrainPart>().Single().Alpha, () => Is.EqualTo(0));

            AddStep("enable strains", () => config.SetValue(OsuSetting.SlopEditorShowDifficultyStrains, true));
            AddUntilStep("graph shown", () => Editor.ChildrenOfType<StrainPart>().Single().Alpha, () => Is.EqualTo(1));

            AddStep("disable strains", () => config.SetValue(OsuSetting.SlopEditorShowDifficultyStrains, false));
        }
    }
}
