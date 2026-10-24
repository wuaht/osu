// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Server.Model.BeatmapAnalysis;
using MapsetVerifier.Server.Model.MetadataAnalysis;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.MapsetVerifier.UI.Overview
{
    public partial class MetadataSection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionMetadata;
        public override IconUsage Icon => FontAwesome.Solid.Tags;
        protected override int AnalysisIndex => 0;

        protected override object? GetData(VerifierOverview overview) => overview.Metadata;

        protected override string? GetError(object data) => ((MetadataAnalysisResult)data).Success ? null : ((MetadataAnalysisResult)data).ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            var difficulties = ((MetadataAnalysisResult)data).Difficulties;

            var table = new DifficultyTable(difficulties.Select(d => (d.Version, d.StarRating)).ToArray())
                        .AddRow(VerifierStrings.Artist, difficulties.Select(d => (string?)d.Artist).ToArray(), true)
                        .AddRow(VerifierStrings.ArtistUnicode, difficulties.Select(d => (string?)d.ArtistUnicode).ToArray(), true)
                        .AddRow(VerifierStrings.Title, difficulties.Select(d => (string?)d.Title).ToArray(), true)
                        .AddRow(VerifierStrings.TitleUnicode, difficulties.Select(d => (string?)d.TitleUnicode).ToArray(), true)
                        .AddRow(VerifierStrings.Creator, difficulties.Select(d => (string?)d.Creator).ToArray(), true)
                        .AddRow(VerifierStrings.Source, difficulties.Select(d => (string?)d.Source).ToArray(), true)
                        .AddRow(VerifierStrings.Tags, difficulties.Select(d => (string?)d.Tags).ToArray(), true)
                        .AddRow(VerifierStrings.BeatmapId, difficulties.Select(d => d.BeatmapId?.ToString()).ToArray());

            yield return new OverviewCard().Add(table);
        }
    }

    public partial class ResourcesSection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionResources;
        public override IconUsage Icon => FontAwesome.Solid.FolderOpen;
        protected override int AnalysisIndex => 0;

        private const int max_timestamps = 12;

        protected override object? GetData(VerifierOverview overview) => overview.Metadata;

        protected override string? GetError(object data) => ((MetadataAnalysisResult)data).Success ? null : ((MetadataAnalysisResult)data).ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            var resources = ((MetadataAnalysisResult)data).Resources;

            var audio = new OverviewCard(VerifierStrings.AudioFile, VerifierStrings.TotalSize(resources.TotalFolderSizeFormatted));

            if (resources.AudioFile is AudioFileInfo audioFile)
            {
                audio.Add(new KeyValueGrid()
                          .Add(VerifierStrings.FileName, audioFile.FileName)
                          .Add(VerifierStrings.Format, audioFile.Format)
                          .Add(VerifierStrings.Size, audioFile.FileSizeFormatted)
                          .Add(VerifierStrings.Duration, audioFile.DurationFormatted)
                          .Add(VerifierStrings.Bitrate, VerifierStrings.BitrateValue(Format(audioFile.AverageBitrate, @"0"))));
            }
            else
                audio.Add(new SectionPlaceholder(false));

            yield return audio;

            var backgrounds = new OverviewCard(VerifierStrings.Backgrounds);

            if (resources.Backgrounds.Count == 0)
                backgrounds.Add(new SectionPlaceholder(false, VerifierStrings.NoBackgrounds));

            foreach (var background in resources.Backgrounds)
            {
                backgrounds.Add(new FileRow(background.FileName, background.UsedByDifficulties,
                    (VerifierStrings.Resolution, background.Resolution),
                    (VerifierStrings.Size, background.FileSizeFormatted)));
            }

            yield return backgrounds;

            if (resources.Videos.Count > 0)
            {
                var videos = new OverviewCard(VerifierStrings.Videos);

                foreach (var video in resources.Videos)
                {
                    videos.Add(new FileRow(video.FileName, video.UsedByDifficulties,
                        (VerifierStrings.Resolution, video.Resolution),
                        (VerifierStrings.Duration, video.DurationFormatted),
                        (VerifierStrings.Offset, $@"{video.OffsetMs} ms"),
                        (VerifierStrings.Size, video.FileSizeFormatted)));
                }

                yield return videos;
            }

            var hitSounds = new OverviewCard(VerifierStrings.HitSounds, resources.HitSounds.Count > 0 ? resources.HitSounds.Count.ToString() : default(LocalisableString));

            if (resources.HitSounds.Count == 0)
                hitSounds.Add(new SectionPlaceholder(false, VerifierStrings.NoHitSounds));

            foreach (var hitSound in resources.HitSounds)
            {
                var row = new FileRow(hitSound.FileName, Array.Empty<string>(),
                    (VerifierStrings.Format, hitSound.Format),
                    (VerifierStrings.Size, hitSound.FileSizeFormatted),
                    (VerifierStrings.Duration, $@"{Format(hitSound.DurationMs, @"0")} ms"),
                    (VerifierStrings.Usages, hitSound.TotalUsageCount.ToString()));

                foreach (var usage in hitSound.UsagePerDifficulty.Where(u => u.UsageCount > 0))
                {
                    string timestamps = string.Join(@", ", usage.Timestamps.Take(max_timestamps));

                    if (usage.Timestamps.Count > max_timestamps)
                        timestamps += @", …";

                    row.AddDetail(new IssueTextFlow($@"{usage.Version} ({usage.UsageCount}): {timestamps}", usage.Version, 12));
                }

                hitSounds.Add(row);
            }

            yield return hitSounds;

            var storyboard = resources.Storyboard;
            var storyboardCard = new OverviewCard(VerifierStrings.Storyboard);

            storyboardCard.Add(new KeyValueGrid(300)
                .Add(VerifierStrings.OsbFile,
                    storyboard.HasOsb ? storyboard.OsbFileName + (storyboard.OsbIsUsed ? string.Empty : $@" ({VerifierStrings.OsbUnused})") : VerifierStrings.NoOsb.ToString(),
                    storyboard.HasOsb && !storyboard.OsbIsUsed ? VerifierDisplay.WARNING : null));

            var difficulties = storyboard.DifficultySpecificStoryboards;

            if (difficulties.Count > 0)
            {
                storyboardCard.Add(new DifficultyTable(difficulties.Select(d => (d.Version, (double?)null)).ToArray())
                                   .AddRow(VerifierStrings.Storyboard, difficulties.Select(d => (string?)YesNo(d.HasStoryboard)).ToArray())
                                   .AddRow(VerifierStrings.Sprites, difficulties.Select(d => (string?)d.SpriteCount.ToString()).ToArray())
                                   .AddRow(VerifierStrings.Animations, difficulties.Select(d => (string?)d.AnimationCount.ToString()).ToArray())
                                   .AddRow(VerifierStrings.Samples, difficulties.Select(d => (string?)d.SampleCount.ToString()).ToArray()));
            }

            yield return storyboardCard;
        }

        /// <summary>
        /// A file of the beatmap set with its properties and the difficulties using it.
        /// </summary>
        private partial class FileRow : CompositeDrawable
        {
            private readonly FillFlowContainer details;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private readonly Box background;

            public FileRow(string fileName, IReadOnlyCollection<string> usedBy, params (LocalisableString Key, string Value)[] properties)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 6;

                var propertyFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(14, 2),
                };

                foreach (var (key, value) in properties)
                {
                    propertyFlow.Add(new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4, 0),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Text = key,
                                Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                                Alpha = 0.6f,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Text = value,
                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                            },
                        }
                    });
                }

                if (usedBy.Count > 0)
                {
                    propertyFlow.Add(new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(4, 0),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Text = VerifierStrings.UsedBy,
                                Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                                Alpha = 0.6f,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Text = string.Join(@", ", usedBy),
                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                            },
                        }
                    });
                }

                InternalChildren = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Padding = new MarginPadding { Horizontal = 10, Vertical = 7 },
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(7, 0),
                                Children = new Drawable[]
                                {
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Icon = FontAwesome.Regular.File,
                                        Size = new Vector2(11),
                                        Alpha = 0.7f,
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = fileName,
                                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                                    },
                                }
                            },
                            propertyFlow,
                            details = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 2),
                            },
                        }
                    },
                };
            }

            public void AddDetail(Drawable drawable) => details.Add(drawable);

            [BackgroundDependencyLoader]
            private void load()
            {
                background.Colour = colourProvider.Background3;
                background.Alpha = 0.6f;
            }
        }
    }

    public partial class StatisticsSection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionStatistics;
        public override IconUsage Icon => FontAwesome.Solid.ChartBar;
        protected override int AnalysisIndex => 1;

        protected override object? GetData(VerifierOverview overview) => overview.Beatmap;

        protected override string? GetError(object data) => ((BeatmapAnalysisResult)data).Success ? null : ((BeatmapAnalysisResult)data).ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            var statistics = ((BeatmapAnalysisResult)data).Statistics;

            var table = new DifficultyTable(statistics.Select(s => (s.Version, s.StarRating)).ToArray())
                .AddRow(VerifierStrings.Circles, statistics.Select(s => (string?)s.CircleCount.ToString()).ToArray());

            if (statistics.Any(s => s.SliderCount != null))
                table.AddRow(VerifierStrings.Sliders, statistics.Select(s => s.SliderCount?.ToString()).ToArray());

            if (statistics.Any(s => s.SpinnerCount != null))
                table.AddRow(VerifierStrings.Spinners, statistics.Select(s => s.SpinnerCount?.ToString()).ToArray());

            if (statistics.Any(s => s.HoldNoteCount != null))
            {
                table.AddRow(VerifierStrings.HoldNotes, statistics.Select(s => s.HoldNoteCount?.ToString()).ToArray());
                table.AddRow(VerifierStrings.Columns, statistics.Select(s => s.HoldNoteCount == null ? null : s.ColumnCount.ToString()).ToArray());
            }

            table.AddRow(VerifierStrings.NewCombos, statistics.Select(s => (string?)s.NewComboCount.ToString()).ToArray())
                 .AddRow(VerifierStrings.Breaks, statistics.Select(s => (string?)s.BreakCount.ToString()).ToArray())
                 .AddRow(VerifierStrings.UninheritedLines, statistics.Select(s => (string?)s.UninheritedLineCount.ToString()).ToArray())
                 .AddRow(VerifierStrings.InheritedLines, statistics.Select(s => (string?)s.InheritedLineCount.ToString()).ToArray())
                 .AddRow(VerifierStrings.KiaiTime, statistics.Select(s => (string?)s.KiaiTimeFormatted).ToArray())
                 .AddRow(VerifierStrings.DrainTime, statistics.Select(s => (string?)s.DrainTimeFormatted).ToArray())
                 .AddRow(VerifierStrings.PlayTime, statistics.Select(s => (string?)s.PlayTimeFormatted).ToArray());

            yield return new OverviewCard().Add(table);
        }
    }

    public partial class SettingsSection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionSettings;
        public override IconUsage Icon => FontAwesome.Solid.SlidersH;
        protected override int AnalysisIndex => 1;

        protected override object? GetData(VerifierOverview overview) => overview.Beatmap;

        protected override string? GetError(object data) => ((BeatmapAnalysisResult)data).Success ? null : ((BeatmapAnalysisResult)data).ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            var result = (BeatmapAnalysisResult)data;
            var general = result.GeneralSettings;
            var difficulty = result.DifficultySettings;

            double? starRating(string version) => result.Statistics.FirstOrDefault(s => s.Version == version)?.StarRating;

            yield return new OverviewCard(VerifierStrings.GeneralSettings).Add(
                new DifficultyTable(general.Select(g => (g.Version, starRating(g.Version))).ToArray())
                    .AddRow(VerifierStrings.AudioFile, general.Select(g => (string?)g.AudioFileName).ToArray(), true)
                    .AddRow(VerifierStrings.AudioLeadIn, general.Select(g => (string?)$@"{Format(g.AudioLeadIn)} ms").ToArray())
                    .AddRow(VerifierStrings.StackLeniency, general.Select(g => g.StackLeniency).ToArray())
                    .AddRow(VerifierStrings.Countdown, general.Select(g => (string?)(g.HasCountdown ? g.CountdownSpeed ?? YesNo(true) : YesNo(false))).ToArray())
                    .AddRow(VerifierStrings.CountdownOffset, general.Select(g => g.CountdownOffset?.ToString()).ToArray())
                    .AddRow(VerifierStrings.LetterboxInBreaks, general.Select(g => (string?)YesNo(g.LetterboxInBreaks)).ToArray(), true)
                    .AddRow(VerifierStrings.WidescreenStoryboard, general.Select(g => (string?)YesNo(g.WidescreenStoryboard)).ToArray(), true)
                    .AddRow(VerifierStrings.PreviewTime, general.Select(g => (string?)g.PreviewTimeFormatted).ToArray(), true)
                    .AddRow(VerifierStrings.UseSkinSprites, general.Select(g => g.UseSkinSprites).ToArray(), true)
                    .AddRow(VerifierStrings.SkinPreference, general.Select(g => (string?)g.SkinPreference).ToArray(), true)
                    .AddRow(VerifierStrings.EpilepsyWarning, general.Select(g => g.EpilepsyWarning).ToArray(), true));

            yield return new OverviewCard(VerifierStrings.DifficultySettings).Add(
                new DifficultyTable(difficulty.Select(d => (d.Version, starRating(d.Version))).ToArray())
                    .AddRow(VerifierStrings.HpDrain, difficulty.Select(d => (string?)Format(d.HpDrain, @"0.#")).ToArray())
                    .AddRow(VerifierStrings.CircleSize, difficulty.Select(d => d.CircleSize).ToArray())
                    .AddRow(VerifierStrings.OverallDifficulty, difficulty.Select(d => (string?)Format(d.OverallDifficulty, @"0.#")).ToArray())
                    .AddRow(VerifierStrings.ApproachRate, difficulty.Select(d => d.ApproachRate).ToArray())
                    .AddRow(VerifierStrings.SliderTickRate, difficulty.Select(d => d.SliderTickRate).ToArray())
                    .AddRow(VerifierStrings.SliderVelocity, difficulty.Select(d => d.SliderVelocity).ToArray()));
        }
    }

    public partial class ColoursSection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionColours;
        public override IconUsage Icon => FontAwesome.Solid.Palette;
        protected override int AnalysisIndex => 0;

        protected override object? GetData(VerifierOverview overview) => overview.Metadata;

        protected override string? GetError(object data) => ((MetadataAnalysisResult)data).Success ? null : ((MetadataAnalysisResult)data).ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            foreach (var settings in ((MetadataAnalysisResult)data).ColourSettings)
            {
                var card = new OverviewCard(settings.Version);

                if (!settings.IsApplicable)
                {
                    card.Add(new OsuSpriteText
                    {
                        Text = VerifierStrings.NotApplicable,
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                        Alpha = 0.6f,
                    });

                    yield return card;

                    continue;
                }

                var swatches = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Full,
                    Spacing = new Vector2(10),
                };

                if (settings.ComboColours.Count == 0)
                {
                    swatches.Add(new OsuSpriteText
                    {
                        Text = VerifierStrings.NoComboColours,
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                        Alpha = 0.6f,
                    });
                }

                foreach (var colour in settings.ComboColours)
                    swatches.Add(new Swatch($@"{colour.Index}", colour.Hex, colour.HspLuminosity, colour.LuminosityWarning));

                if (settings.SliderBorder is ColourInfo border)
                    swatches.Add(new Swatch(VerifierStrings.SliderBorder.ToString(), border.Hex, border.HspLuminosity, border.LuminosityWarning));

                if (settings.SliderTrack is ColourInfo track)
                    swatches.Add(new Swatch(VerifierStrings.SliderTrack.ToString(), track.Hex, track.HspLuminosity, track.LuminosityWarning));

                card.Add(swatches);

                yield return card;
            }
        }

        private partial class Swatch : FillFlowContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public Swatch(string label, string hex, float luminosity, string warning)
            {
                bool hasWarning = !string.IsNullOrEmpty(warning);

                TooltipText = $@"{hex}{'\n'}{VerifierStrings.Luminosity}: {luminosity:0.#}" + (hasWarning ? $@"{'\n'}{warning}" : string.Empty);

                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 4);

                Color4 colour = Color4Extensions.FromHex(hex.TrimStart('#'));

                Children = new Drawable[]
                {
                    new Container
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Size = new Vector2(40, 30),
                        Masking = true,
                        CornerRadius = 6,
                        BorderThickness = hasWarning ? 2 : 0,
                        BorderColour = VerifierDisplay.WARNING,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colour,
                            },
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.ExclamationTriangle,
                                Size = new Vector2(12),
                                Colour = luminosity > 128 ? Color4.Black : Color4.White,
                                Alpha = hasWarning ? 0.8f : 0,
                            },
                        }
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = label,
                        Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = hex,
                        Font = OsuFont.Default.With(size: 10, weight: FontWeight.SemiBold),
                        Alpha = 0.6f,
                    },
                };
            }
        }
    }
}
