// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit.Submission;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneSubmissionDifficultySelection : OsuManualInputManagerTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private BeatmapSubmissionSettings settings = null!;
        private BeatmapSetInfo set = null!;

        private void createScreen(params string[] difficultyNames)
        {
            AddStep("create screen", () =>
            {
                var working = CreateWorkingBeatmap(new OsuRuleset().RulesetInfo);
                set = working.BeatmapSetInfo;
                set.Beatmaps.Clear();

                for (int i = 0; i < difficultyNames.Length; i++)
                {
                    set.Beatmaps.Add(new BeatmapInfo(new OsuRuleset().RulesetInfo, new BeatmapDifficulty(), new BeatmapMetadata())
                    {
                        DifficultyName = difficultyNames[i],
                        StarRating = i,
                        BeatmapSet = set,
                    });
                }

                Beatmap.Value = working;

                settings = new BeatmapSubmissionSettings();
                settings.ExcludeHitsoundDifficulties(config, Beatmap.Value.BeatmapSetInfo.Beatmaps);

                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(BeatmapSubmissionSettings), settings) },
                    Child = new ScreenStack(new ScreenSubmissionSettings()) { RelativeSizeAxes = Axes.Both },
                };
            });
            AddUntilStep("wait for checkboxes", () => checkbox(difficultyNames[0]) != null);
        }

        private FormCheckBox? checkbox(string name) => this.ChildrenOfType<FormCheckBox>().FirstOrDefault(c => c.Caption.ToString().StartsWith(name, StringComparison.Ordinal));

        private bool excluded(string name) => settings.ExcludedBeatmaps.Any(b => b.DifficultyName == name);

        private void click(string name) => AddStep($"click {name}", () =>
        {
            InputManager.MoveMouseTo(checkbox(name)!);
            InputManager.Click(MouseButton.Left);
        });

        [Test]
        public void TestHitsoundDifficultyNotUploadedByDefault()
        {
            createScreen("Easy", "Hard", "Hitsounds");

            AddAssert("easy uploaded", () => checkbox("Easy")!.Current.Value);
            AddAssert("hitsounds not uploaded", () => checkbox("Hitsounds")!.Current.Value, () => Is.False);
            AddAssert("hitsounds excluded", () => excluded("Hitsounds"));

            click("Hitsounds");
            AddAssert("hitsounds included", () => excluded("Hitsounds"), () => Is.False);

            click("Easy");
            AddAssert("easy excluded", () => excluded("Easy"));
            AddAssert("easy unchecked", () => checkbox("Easy")!.Current.Value, () => Is.False);
        }

        [Test]
        public void TestLastDifficultyCannotBeUnchecked()
        {
            createScreen("Easy", "Hard", "Hitsounds");

            click("Easy");
            AddAssert("hard is the only uploaded one and locked", () => checkbox("Hard")!.Current.Disabled);

            click("Hard");
            AddAssert("hard still uploaded", () => excluded("Hard"), () => Is.False);

            click("Easy");
            AddAssert("hard unlocked again", () => checkbox("Hard")!.Current.Disabled, () => Is.False);
        }

        [Test]
        public void TestOnlyHitsoundDifficultyIsUploaded()
        {
            createScreen("Hitsounds");

            AddAssert("nothing excluded", () => settings.ExcludedBeatmaps, () => Is.Empty);
            AddAssert("checked and locked", () => checkbox("Hitsounds")!.Current.Value && checkbox("Hitsounds")!.Current.Disabled);
        }
    }
}
