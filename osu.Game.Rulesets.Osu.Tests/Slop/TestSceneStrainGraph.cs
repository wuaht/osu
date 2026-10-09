// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osuTK;

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

            AddAssert("strains cover all objects", () => info!.StrainsStartTime + info.Strains.Length * EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH,
                () => Is.GreaterThanOrEqualTo(EditorBeatmap.HitObjects.Max(h => h.StartTime)));
            AddAssert("strains relative to hardest section", () => info!.Strains.Max(), () => Is.EqualTo(1).Within(0.0001));
            AddAssert("strains not negative", () => info!.Strains.All(s => s >= 0));

            // like in difficulty calculation, the first section ends at the first multiple of the section length after the first object with difficulty.
            AddAssert("strains start at first object", () => info!.StrainsStartTime,
                () => Is.InRange(firstObjectTime() - EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH, firstObjectTime()));
        }

        [Test]
        public void TestGraphMatchesObjectTimes()
        {
            AddStep("enable strains", () => config.SetValue(OsuSetting.SlopEditorShowDifficultyStrains, true));
            AddUntilStep("difficulty calculated", () => info, () => Is.Not.Null);
            AddUntilStep("graph has columns", () => graph.Columns.Count, () => Is.GreaterThan(0));

            // the strain sections are 400ms long, so the graph may start up to one section before the first difficulty object and end up to one section after the last one.
            AddAssert("graph starts at first objects", () => columnTime(firstNonZero()), () => Is.InRange(firstObjectTime() - EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH - columnLength(), firstObjectTime() + columnLength()));
            AddAssert("graph ends at last objects", () => columnTime(lastNonZero() + 1), () => Is.InRange(lastObjectTime() - columnLength(), lastObjectTime() + EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH + columnLength()));
            AddAssert("graph spans timeline width", () => graph.DrawWidth, () => Is.EqualTo(Editor.ChildrenOfType<StrainPart>().Single().DrawWidth).Within(0.01));

            AddStep("disable strains", () => config.SetValue(OsuSetting.SlopEditorShowDifficultyStrains, false));
        }

        private StrainPart.StrainGraph graph => Editor.ChildrenOfType<StrainPart.StrainGraph>().First();

        private double trackLength => Editor.ChildrenOfType<EditorClock>().First().TrackLength;

        private double columnLength() => trackLength / graph.DrawWidth;

        private double columnTime(int column) => column * columnLength();

        private int firstNonZero() => graph.Columns.ToList().FindIndex(c => c > 0);

        private int lastNonZero() => graph.Columns.ToList().FindLastIndex(c => c > 0);

        // the first object doesn't have a difficulty object in osu!, so strain starts at the second one.
        private double firstObjectTime() => EditorBeatmap.HitObjects[1].StartTime;

        private double lastObjectTime() => EditorBeatmap.HitObjects.Max(h => h.StartTime);

        [Test]
        public void TestNoGapDuringLongSlider()
        {
            const double slider_start = 3000;
            const double after_slider = 6000;

            AddStep("create beatmap with long slider", () =>
            {
                EditorBeatmap.Clear();

                for (int i = 0; i < 8; i++)
                    EditorBeatmap.Add(new HitCircle { StartTime = 1000 + i * 250, Position = new Vector2(100 + (i % 2) * 200, 200) });

                EditorBeatmap.Add(new Slider
                {
                    StartTime = slider_start,
                    Position = new Vector2(100, 100),
                    Path = new SliderPath(new[]
                    {
                        new PathControlPoint(Vector2.Zero, PathType.LINEAR),
                        new PathControlPoint(new Vector2(300, 0)),
                    }),
                    RepeatCount = 4,
                });

                EditorBeatmap.Add(new HitCircle { StartTime = after_slider, Position = new Vector2(300, 300) });
            });

            AddUntilStep("strains recalculated", () => info?.StrainsStartTime + info?.Strains.Length * EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH, () => Is.GreaterThanOrEqualTo(after_slider));

            AddAssert("strain throughout the slider", () => info!.Strains
                                                               .Skip((int)((slider_start - info.StrainsStartTime) / EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH))
                                                               .Take((int)((after_slider - slider_start) / EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH))
                                                               .All(s => s > 0));
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
