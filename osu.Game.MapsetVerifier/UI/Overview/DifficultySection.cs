// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Server.Model.BeatmapAnalysis;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osuTK.Graphics;

namespace osu.Game.MapsetVerifier.UI.Overview
{
    public partial class DifficultySection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionDifficulty;
        public override IconUsage Icon => FontAwesome.Solid.ChartLine;
        protected override int AnalysisIndex => 5;

        private readonly Bindable<string?> selectedDifficulty;
        private readonly Bindable<MapsetVerifierStrainDisplayMode> displayMode = new Bindable<MapsetVerifierStrainDisplayMode>();

        public DifficultySection(Bindable<string?> selectedDifficulty)
        {
            this.selectedDifficulty = selectedDifficulty.GetBoundCopy();
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopMapsetVerifierStrainDisplayMode, displayMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedDifficulty.BindValueChanged(_ => Scheduler.AddOnce(Refresh));
            displayMode.BindValueChanged(_ => Scheduler.AddOnce(Refresh));
        }

        protected override object? GetData(VerifierOverview overview) => overview.Difficulty;

        protected override string? GetError(object data) => ((DifficultyOverviewResult)data).Success ? null : ((DifficultyOverviewResult)data).ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            var result = (DifficultyOverviewResult)data;

            if (result.Difficulties.Count == 0)
            {
                yield return new OverviewCard().Add(new SectionPlaceholder(false));

                yield break;
            }

            var difficulty = result.Difficulties.FirstOrDefault(d => d.Version == selectedDifficulty.Value) ?? result.Difficulties[0];

            var allSamples = result.Difficulties.SelectMany(d => d.StarRatingSamples.Concat(d.SliderVelocitySamples).Concat(d.VolumeSamples)
                                                                   .Concat(d.Skills.SelectMany(s => s.StrainSamples))).ToArray();

            double startTime = allSamples.Length > 0 ? Math.Min(0, allSamples.Min(s => s.TimeMs)) : 0;
            double endTime = allSamples.Length > 0 ? allSamples.Max(s => s.TimeMs) : 1;

            yield return new DifficultyPills(result.Difficulties.Select(d => d.Version), selectedDifficulty);

            if (displayMode.Value != MapsetVerifierStrainDisplayMode.Strain)
            {
                var series = result.Difficulties
                                   .Where(d => d.StarRatingSamples.Count > 0)
                                   .OrderBy(d => d.StarRating)
                                   .Select(d => new TimeGraph.Series(d.Version, points(d.StarRatingSamples), Colours.ForStarDifficulty(d.StarRating)))
                                   .ToArray();

                yield return new OverviewCard(VerifierStrings.StarRatingGraph).Add(
                    series.Length > 0 ? new TimeGraph(series, startTime, endTime, v => $@"{Format(v)}★") : new SectionPlaceholder(false),
                    new GraphLegend(result.Difficulties.OrderBy(d => d.StarRating)
                                          .Select(d => ((LocalisableString)$@"{d.Version} ({Format(d.StarRating)}★)", Colours.ForStarDifficulty(d.StarRating)))));
            }

            if (displayMode.Value != MapsetVerifierStrainDisplayMode.StarRating)
            {
                var skillColours = new[] { Colours.Blue, Colours.Pink, Colours.Yellow, Colours.Green, Colours.Purple, Colours.Orange1 };

                var series = difficulty.Skills
                                       .Where(s => s.StrainSamples.Count > 0)
                                       .Select((s, i) => new TimeGraph.Series(s.SkillName, points(s.StrainSamples), skillColours[i % skillColours.Length]))
                                       .ToArray();

                yield return new OverviewCard(VerifierStrings.StrainGraph, $@"{difficulty.Version} · {difficulty.DifficultyLevel}").Add(
                    series.Length > 0 ? new TimeGraph(series, startTime, endTime, v => Format(v)) : new SectionPlaceholder(false),
                    new GraphLegend(difficulty.Skills.Select((s, i) => ((LocalisableString)$@"{s.SkillName} ({Format(s.DifficultyValue)})", skillColours[i % skillColours.Length]))));
            }

            if (difficulty.SliderVelocitySamples.Count > 0)
            {
                yield return new OverviewCard(VerifierStrings.SliderVelocityGraph, difficulty.Version).Add(
                    new TimeGraph(new[] { new TimeGraph.Series(VerifierStrings.SliderVelocity, points(difficulty.SliderVelocitySamples), Colours.Lime1, true) },
                        startTime, endTime, v => $@"{Format(v)}x"));
            }

            if (difficulty.VolumeSamples.Count > 0)
            {
                yield return new OverviewCard(VerifierStrings.VolumeGraph, difficulty.Version).Add(
                    new TimeGraph(new[] { new TimeGraph.Series(VerifierStrings.VolumeGraph, points(difficulty.VolumeSamples), Colours.Orange1, true) },
                        startTime, endTime, v => $@"{Format(v, @"0")}%", 0, 100));
            }
        }

        private static IReadOnlyList<(double Time, double Value)> points(IEnumerable<DifficultySamplePoint> samples) => samples.Select(s => (s.TimeMs, s.Value)).ToArray();
    }
}
