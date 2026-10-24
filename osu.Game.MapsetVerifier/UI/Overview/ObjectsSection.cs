// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Server.Model.BeatmapAnalysis;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.MapsetVerifier.UI.Overview
{
    public partial class ObjectsSection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionObjects;
        public override IconUsage Icon => FontAwesome.Solid.Shapes;
        protected override int AnalysisIndex => 2;

        private const int max_timestamps = 40;

        private readonly Bindable<string?> selectedDifficulty;

        public ObjectsSection(Bindable<string?> selectedDifficulty)
        {
            this.selectedDifficulty = selectedDifficulty.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            selectedDifficulty.BindValueChanged(_ => Scheduler.AddOnce(Refresh));
        }

        protected override object? GetData(VerifierOverview overview) => overview.Objects;

        protected override string? GetError(object data) => ((ObjectsOverviewResult)data).Success ? null : ((ObjectsOverviewResult)data).ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            var result = (ObjectsOverviewResult)data;

            if (result.Difficulties.Count == 0)
            {
                yield return new OverviewCard().Add(new SectionPlaceholder(false));

                yield break;
            }

            var difficulty = result.Difficulties.FirstOrDefault(d => d.Version == selectedDifficulty.Value) ?? result.Difficulties[0];
            string version = difficulty.Version;

            yield return new DifficultyPills(result.Difficulties.Select(d => d.Version), selectedDifficulty);

            yield return new OverviewCard(VerifierStrings.Timeline).Add(
                new KeyValueGrid(180)
                    .Add(VerifierStrings.Objects, difficulty.ObjectCount.ToString())
                    .Add(VerifierStrings.Edges, difficulty.EdgeCount.ToString())
                    .Add(VerifierStrings.Unsnapped, VerifierStrings.UnsnappedPercentage(difficulty.UnsnappedCount, $@"{Format(difficulty.UnsnappedPercentage, @"0.##")}%"),
                        difficulty.UnsnappedCount > 0 ? VerifierDisplay.WARNING : null)
                    .Add(VerifierStrings.Breaks, difficulty.BreakPeriods.Count.ToString()),
                new ObjectTimeline(difficulty, result.StartTimeMs, result.EndTimeMs));

            var snappings = new ShareBars();

            foreach (var snapping in difficulty.Snappings.Where(s => s.Count > 0))
                snappings.AddEntry(snapping.Label, $@"{snapping.Count} ({Format(snapping.Percentage, @"0.#")}%)", snapping.Percentage / 100, snapColour(snapping.Divisor));

            var types = new ShareBars();

            foreach (var type in difficulty.ObjectTypes.Where(t => t.Count > 0))
                types.AddEntry(type.Label, $@"{type.Count} ({Format(type.Percentage, @"0.#")}%)", type.Percentage / 100);

            yield return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[] { new Dimension(), new Dimension(GridSizeMode.Absolute, 8), new Dimension() },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new OverviewCard(VerifierStrings.Snappings).Add(difficulty.Snappings.Any(s => s.Count > 0) ? snappings : new SectionPlaceholder(false)),
                        Empty(),
                        new OverviewCard(VerifierStrings.ObjectTypes).Add(difficulty.ObjectTypes.Any(t => t.Count > 0) ? types : new SectionPlaceholder(false)),
                    }
                }
            };

            if (difficulty.UnsnappedEdgeTimesMs.Count > 0)
            {
                yield return new OverviewCard(VerifierStrings.Unsnapped, difficulty.UnsnappedEdgeTimesMs.Count.ToString())
                    .Add(new IssueTextFlow(formatTimestamps(difficulty.UnsnappedEdgeTimesMs), version, 13));
            }

            if (difficulty.HitsoundGapPeriods.Count > 0)
            {
                yield return new OverviewCard(VerifierStrings.HitsoundGaps, difficulty.HitsoundGapPeriods.Count.ToString())
                    .Add(new IssueTextFlow(string.Join(@", ", difficulty.HitsoundGapPeriods.Take(max_timestamps)
                                                                   .Select(g => $@"{VerifierDisplay.FormatTime(g.StartTimeMs)} - {VerifierDisplay.FormatTime(g.EndTimeMs)}")),
                        version, 13));
            }

            if (difficulty.TimingSegments.Count > 0)
            {
                var timing = difficulty.TimingSegments.Take(100).ToArray();

                var segments = new DifficultyTable(timing.Select(s => (VerifierDisplay.FormatTime(s.OffsetMs), (double?)null)).ToArray())
                               .AddRow(VerifierStrings.Bpm, timing.Select(s => (string?)Format(s.Bpm, @"0.###")).ToArray())
                               .AddRow(VerifierStrings.Meter, timing.Select(s => (string?)$@"{s.Meter}/4").ToArray())
                               .AddRow(VerifierStrings.Sampleset, timing.Select(s => (string?)(s.CustomIndex > 0 ? $@"{s.Sampleset} ({s.CustomIndex})" : s.Sampleset))
                                                                           .ToArray())
                               .AddRow(VerifierStrings.Duration, timing.Select(s => (string?)VerifierDisplay.FormatTime(s.EndTimeMs - s.StartTimeMs)).ToArray());

                yield return new OverviewCard(VerifierStrings.TimingSegments, difficulty.TimingSegments.Count.ToString()).Add(segments);
            }

            if (difficulty.ColumnUsage is List<ObjectsColumnUsage> columns && columns.Count > 0)
            {
                var usage = new ShareBars();
                double max = columns.Max(c => c.Percentage);

                foreach (var column in columns)
                    usage.AddEntry($@"{column.Column + 1}", $@"{column.NoteCount} + {column.HoldNoteCount} ({Format(column.Percentage, @"0.#")}%)", max > 0 ? column.Percentage / max : 0);

                yield return new OverviewCard(VerifierStrings.ColumnUsage).Add(usage);
            }
        }

        private static string formatTimestamps(IReadOnlyCollection<double> times)
        {
            string text = string.Join(@", ", times.Take(max_timestamps).Select(VerifierDisplay.FormatTime));
            return times.Count > max_timestamps ? text + @", …" : text;
        }

        /// <summary>
        /// The colours of the beat snap divisors as in the editor.
        /// </summary>
        private Color4? snapColour(int divisor)
        {
            switch (divisor)
            {
                case 1: return Color4.White;

                case 2: return Colours.Red;

                case 3: return Colours.Purple;

                case 4: return Colours.Blue;

                case 6: return Colours.YellowDark;

                case 8: return Colours.Yellow;

                case 12: return Colours.YellowDarker;

                case 16: return Colours.PurpleDark;

                default: return null;
            }
        }

        /// <summary>
        /// The density of objects over the length of the beatmap set, in the combo colours of the objects.
        /// Clicking it shows the clicked position in the compose screen.
        /// </summary>
        private partial class ObjectTimeline : CompositeDrawable, IHasTooltip
        {
            private const int bin_count = 240;
            private const int max_ticks = 300;

            public LocalisableString TooltipText { get; private set; }

            private readonly ObjectsOverviewDifficulty difficulty;
            private readonly double startTime;
            private readonly double endTime;

            private readonly int[] counts = new int[bin_count];

            private Box hoverLine = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [Resolved]
            private VerifierScreen screen { get; set; } = null!;

            public ObjectTimeline(ObjectsOverviewDifficulty difficulty, double startTime, double endTime)
            {
                this.difficulty = difficulty;
                this.startTime = startTime;
                this.endTime = Math.Max(endTime, startTime + 1);

                RelativeSizeAxes = Axes.X;
                Height = 64;
                Masking = true;
                CornerRadius = 4;
            }

            private float position(double time) => (float)Math.Clamp((time - startTime) / (endTime - startTime), 0, 1);

            [BackgroundDependencyLoader]
            private void load()
            {
                var colours = new Color4?[bin_count];

                foreach (var obj in difficulty.TimelineObjects)
                {
                    int bin = Math.Min((int)(position(obj.StartTimeMs) * bin_count), bin_count - 1);
                    counts[bin]++;

                    if (colours[bin] == null && obj.ComboColourHex != null && Colour4.TryParseHex(obj.ComboColourHex, out var colour))
                        colours[bin] = colour;
                }

                int maxCount = Math.Max(1, counts.Max());

                var periods = new Container { RelativeSizeAxes = Axes.Both };

                foreach (var period in difficulty.BreakPeriods)
                    periods.Add(createPeriod(period.StartTimeMs, period.EndTimeMs, colourProvider.Light4, 0.12f));

                foreach (var period in difficulty.HitsoundGapPeriods)
                    periods.Add(createPeriod(period.StartTimeMs, period.EndTimeMs, VerifierDisplay.CHANGED, 0.15f));

                var bars = new Container { RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Top = 10 } };

                for (int i = 0; i < bin_count; i++)
                {
                    if (counts[i] == 0)
                        continue;

                    bars.Add(new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.Both,
                        X = (float)i / bin_count,
                        Width = 1f / bin_count * 0.8f,
                        Height = 0.1f + 0.9f * counts[i] / maxCount,
                        Colour = colours[i] ?? colourProvider.Highlight1,
                        Alpha = 0.85f,
                    });
                }

                var ticks = new Container { RelativeSizeAxes = Axes.Both };

                foreach (double time in difficulty.UnsnappedEdgeTimesMs.Take(max_ticks))
                {
                    ticks.Add(new Box
                    {
                        RelativePositionAxes = Axes.X,
                        X = position(time),
                        Origin = Anchor.TopCentre,
                        Size = new Vector2(2, 7),
                        Colour = VerifierDisplay.PROBLEM,
                    });
                }

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                    },
                    periods,
                    bars,
                    ticks,
                    hoverLine = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        RelativePositionAxes = Axes.X,
                        Origin = Anchor.TopCentre,
                        Width = 1.5f,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                };
            }

            private Drawable createPeriod(double start, double end, Color4 colour, float alpha) => new Box
            {
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.X,
                X = position(start),
                Width = Math.Max(position(end) - position(start), 0.002f),
                Colour = colour,
                Alpha = alpha,
            };

            private double timeAt(Vector2 screenSpacePosition)
            {
                float x = Math.Clamp(ToLocalSpace(screenSpacePosition).X / DrawWidth, 0, 1);
                return startTime + x * (endTime - startTime);
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverLine.FadeTo(0.6f, 100, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverLine.FadeOut(200, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            protected override bool OnMouseMove(MouseMoveEvent e)
            {
                double time = timeAt(e.ScreenSpaceMousePosition);
                int bin = Math.Min((int)(position(time) * bin_count), bin_count - 1);

                hoverLine.X = position(time);
                TooltipText = $@"{VerifierDisplay.FormatTime(time)}{'\n'}{VerifierStrings.Objects}: {counts[bin]}";

                return base.OnMouseMove(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                screen.ShowTimestamp(VerifierDisplay.FormatTime(timeAt(e.ScreenSpaceMousePosition)), difficulty.Version);
                return true;
            }
        }
    }
}
