// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Layout;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.MapsetVerifier.UI.Overview
{
    /// <summary>
    /// A rounded card which groups the content of a section of the overview.
    /// </summary>
    public partial class OverviewCard : CompositeDrawable
    {
        protected readonly FillFlowContainer Content;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Box background;

        public OverviewCard(LocalisableString title = default, LocalisableString subtitle = default)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 8;

            InternalChildren = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                Content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Padding = new MarginPadding(12),
                },
            };

            if (title != default)
            {
                Content.Add(new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = title,
                            Font = OsuFont.Default.With(size: 15, weight: FontWeight.Bold),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Text = subtitle,
                            Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                            Alpha = 0.6f,
                            Margin = new MarginPadding { Bottom = 1 },
                        },
                    }
                });
            }
        }

        public OverviewCard Add(params Drawable[] drawables)
        {
            Content.AddRange(drawables);
            return this;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            background.Colour = colourProvider.Background4;
        }
    }

    /// <summary>
    /// Labelled values laid out in columns.
    /// </summary>
    public partial class KeyValueGrid : FillFlowContainer
    {
        public KeyValueGrid(float columnWidth = 220)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(12, 10);

            this.columnWidth = columnWidth;
        }

        private readonly float columnWidth;

        public KeyValueGrid Add(LocalisableString key, LocalisableString value, Color4? colour = null, LocalisableString tooltip = default)
        {
            Add(new KeyValue(key, value, colour, tooltip) { Width = columnWidth });
            return this;
        }

        private partial class KeyValue : FillFlowContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            private readonly Color4? colour;
            private readonly OsuSpriteText valueText;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private readonly OsuSpriteText keyText;

            public KeyValue(LocalisableString key, LocalisableString value, Color4? colour, LocalisableString tooltip)
            {
                this.colour = colour;
                TooltipText = tooltip;

                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 2);

                Children = new Drawable[]
                {
                    keyText = new OsuSpriteText
                    {
                        Text = key,
                        Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                    },
                    valueText = new TruncatingSpriteText
                    {
                        RelativeSizeAxes = Axes.X,
                        ShowTooltip = false,
                        Text = value,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                keyText.Colour = colourProvider.Content2;
                valueText.Colour = colour ?? Color4.White;
            }
        }
    }

    /// <summary>
    /// A table with a column for each difficulty and a row for each field.
    /// </summary>
    public partial class DifficultyTable : CompositeDrawable
    {
        public const float LABEL_WIDTH = 170;
        public const float MIN_COLUMN_WIDTH = 120;
        public const float MAX_COLUMN_WIDTH = 300;
        public const float ROW_HEIGHT = 26;

        /// <summary>
        /// The height of the difficulty names above the rows, including the space below them.
        /// </summary>
        private const float HEADER_HEIGHT = 30 + 4;

        private readonly IReadOnlyList<(string Name, double? StarRating)> difficulties;
        private readonly List<Row> rows = new List<Row>();

        /// <summary>
        /// The cells of all columns, which share the width left by the labels.
        /// </summary>
        private readonly List<Container> columnCells = new List<Container>();

        private float columnWidth = MIN_COLUMN_WIDTH;

        private FillFlowContainer backgrounds = null!;
        private FillFlowContainer labels = null!;
        private FillFlowContainer flow = null!;
        private HorizontalScrollContainer scroll = null!;

        /// <summary>
        /// The space above the table which is taken by the scrollbar, if the columns don't fit.
        /// </summary>
        private float scrollbarSpace = -1;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public DifficultyTable(IReadOnlyList<(string Name, double? StarRating)> difficulties)
        {
            this.difficulties = difficulties;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        /// <summary>
        /// Adds a row of values, one for each difficulty.
        /// </summary>
        /// <param name="label">The name of the field.</param>
        /// <param name="values">The values, in the order of the difficulties.</param>
        /// <param name="highlightInconsistency">Whether to highlight the row if the values aren't all the same.</param>
        public DifficultyTable AddRow(LocalisableString label, IReadOnlyList<string?> values, bool highlightInconsistency = false)
        {
            rows.Add(new Row(label, values, highlightInconsistency));
            return this;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            backgrounds = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            };

            labels = new FillFlowContainer
            {
                Width = LABEL_WIDTH,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            };

            flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
            };

            var header = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Margin = new MarginPadding { Bottom = 4 },
            };

            foreach (var (name, starRating) in difficulties)
            {
                header.Add(createColumnCell().With(c =>
                {
                    c.Height = 30;
                    c.Children = new Drawable[]
                    {
                        new TruncatingSpriteText
                        {
                            Text = name,
                            RelativeSizeAxes = Axes.X,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                        },
                        new Circle
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = 3,
                            Colour = starRating == null ? colourProvider.Light4 : colours.ForStarDifficulty(starRating.Value),
                        },
                    };
                }));
            }

            flow.Add(header);

            for (int i = 0; i < rows.Count; i++)
            {
                bool inconsistent = rows[i].HighlightInconsistency && rows[i].Values.Distinct().Count() > 1;

                backgrounds.Add(createRowBackground(inconsistent, i % 2 == 0));
                labels.Add(createLabel(rows[i], inconsistent));
                flow.Add(createValues(rows[i]));
            }

            // the labels stay in place, so that the fields can still be told apart when scrolling through many difficulties.
            InternalChildren = new Drawable[]
            {
                backgrounds,
                labels,
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Left = LABEL_WIDTH },
                    // the scrollbar is above the difficulty names, so that it can be reached without scrolling to the end of long tables.
                    Child = scroll = new HorizontalScrollContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        ScrollbarAnchor = Anchor.TopLeft,
                        ScrollbarOverlapsContent = false,
                        Child = flow,
                    },
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            float space = flow.DrawWidth > scroll.DrawWidth + 1 ? scroll.ScrollbarSpace : 0;

            if (space != scrollbarSpace)
            {
                scrollbarSpace = space;

                backgrounds.Padding = labels.Padding = new MarginPadding { Top = space + HEADER_HEIGHT };
                scroll.Height = space + HEADER_HEIGHT + rows.Count * ROW_HEIGHT;
            }

            float width = Math.Clamp((DrawWidth - LABEL_WIDTH) / Math.Max(1, difficulties.Count), MIN_COLUMN_WIDTH, MAX_COLUMN_WIDTH);

            if (width == columnWidth)
                return;

            columnWidth = width;

            foreach (var cell in columnCells)
                cell.Width = width;
        }

        private Container createColumnCell()
        {
            var cell = new Container
            {
                Width = columnWidth,
                Height = ROW_HEIGHT,
                Padding = new MarginPadding { Right = 8 },
            };

            columnCells.Add(cell);
            return cell;
        }

        private Drawable createRowBackground(bool inconsistent, bool even) => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = ROW_HEIGHT,
            Masking = true,
            CornerRadius = 4,
            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = inconsistent ? VerifierDisplay.WARNING : colourProvider.Background3,
                Alpha = inconsistent ? 0.08f : even ? 0.6f : 0,
            },
        };

        private Drawable createLabel(Row row, bool inconsistent) => new Container
        {
            Width = LABEL_WIDTH,
            Height = ROW_HEIGHT,
            Padding = new MarginPadding { Horizontal = 8 },
            Child = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = row.Label,
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        Colour = inconsistent ? VerifierDisplay.WARNING : colourProvider.Content2,
                    },
                    new TooltipIcon(FontAwesome.Solid.ExclamationTriangle, VerifierStrings.Inconsistent)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(10),
                        Colour = VerifierDisplay.WARNING,
                        Alpha = inconsistent ? 1 : 0,
                    },
                }
            },
        };

        private Drawable createValues(Row row)
        {
            var cells = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
            };

            foreach (string? value in row.Values)
            {
                cells.Add(createColumnCell().With(c => c.Child = new TruncatingSpriteText
                    {
                        Text = string.IsNullOrEmpty(value) ? VerifierStrings.NotAvailable.ToString() : value,
                        RelativeSizeAxes = Axes.X,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = OsuFont.Default.With(size: 13),
                        Alpha = string.IsNullOrEmpty(value) ? 0.4f : 1,
                    }));
            }

            return cells;
        }

        private record Row(LocalisableString Label, IReadOnlyList<string?> Values, bool HighlightInconsistency);

        /// <summary>
        /// Scrolls the columns which don't fit, but leaves plain vertical scrolling to the overview, so that it isn't blocked by the tables.
        /// </summary>
        private partial class HorizontalScrollContainer : OsuScrollContainer
        {
            public HorizontalScrollContainer()
                : base(Direction.Horizontal)
            {
            }

            public float ScrollbarSpace => Scrollbar.Height + Scrollbar.Margin.Top;

            protected override bool OnScroll(ScrollEvent e)
            {
                if (e.ScrollDelta.X == 0 && !e.ShiftPressed)
                    return false;

                return base.OnScroll(e);
            }
        }
    }

    public partial class TooltipIcon : SpriteIcon, IHasTooltip
    {
        public LocalisableString TooltipText { get; }

        public TooltipIcon(IconUsage icon, LocalisableString tooltip)
        {
            Icon = icon;
            TooltipText = tooltip;
        }
    }

    /// <summary>
    /// Horizontal bars showing the share of each entry, e.g. how many objects are snapped to each divisor.
    /// </summary>
    public partial class ShareBars : FillFlowContainer
    {
        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly List<(LocalisableString Label, LocalisableString Value, double Share, Color4? Colour)> entries = new List<(LocalisableString, LocalisableString, double, Color4?)>();

        public ShareBars()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 4);
        }

        /// <param name="label">The name of the entry.</param>
        /// <param name="value">The text shown next to the bar.</param>
        /// <param name="share">The share of the entry, from 0 to 1.</param>
        /// <param name="colour">The colour of the bar.</param>
        public ShareBars AddEntry(LocalisableString label, LocalisableString value, double share, Color4? colour = null)
        {
            entries.Add((label, value, share, colour));
            return this;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            foreach (var (label, value, share, colour) in entries)
            {
                Add(new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 18,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 110),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 110),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                Text = label,
                                RelativeSizeAxes = Axes.X,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                                Colour = colourProvider.Content2,
                                Padding = new MarginPadding { Right = 8 },
                            },
                            new Container
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                                Height = 8,
                                Masking = true,
                                CornerRadius = 4,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colourProvider.Background6,
                                    },
                                    new Circle
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Width = (float)Math.Clamp(share, 0, 1),
                                        Colour = colour ?? colourProvider.Highlight1,
                                    },
                                }
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Text = value,
                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                            },
                        }
                    }
                });
            }
        }
    }

    /// <summary>
    /// Pills to select one of the difficulties of the beatmap set, for sections which show one difficulty at a time.
    /// </summary>
    public partial class DifficultyPills : FillFlowContainer
    {
        public DifficultyPills(IEnumerable<string> difficulties, Bindable<string?> current)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(6);

            foreach (string difficulty in difficulties)
                Add(new Pill(difficulty, current));
        }

        private partial class Pill : OsuClickableContainer
        {
            private readonly string difficulty;
            private readonly Bindable<string?> current = new Bindable<string?>();

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private Box background = null!;
            private OsuSpriteText text = null!;

            public Pill(string difficulty, Bindable<string?> current)
            {
                this.difficulty = difficulty;
                this.current.BindTo(current);

                AutoSizeAxes = Axes.X;
                Height = 26;
                Masking = true;
                CornerRadius = 13;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both },
                    text = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = difficulty,
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        Margin = new MarginPadding { Horizontal = 12 },
                    },
                };

                Action = () => current.Value = difficulty;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                current.BindValueChanged(_ => updateState(), true);
                FinishTransforms(true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                bool selected = current.Value == difficulty;

                background.FadeColour(selected ? colourProvider.Highlight1 : IsHovered ? colourProvider.Background2 : colourProvider.Background3, 150, Easing.OutQuint);
                text.FadeColour(selected ? colourProvider.Background6 : colourProvider.Content1, 150, Easing.OutQuint);
            }
        }
    }

    /// <summary>
    /// A line graph of values over the time of the beatmap.
    /// </summary>
    public partial class TimeGraph : CompositeDrawable, IHasTooltip
    {
        public readonly record struct Series(LocalisableString Name, IReadOnlyList<(double Time, double Value)> Points, Color4 Colour, bool Step = false);

        public LocalisableString TooltipText { get; private set; }

        private readonly IReadOnlyList<Series> series;
        private readonly double startTime;
        private readonly double endTime;
        private readonly double minValue;
        private readonly double maxValue;
        private readonly Func<double, string> formatValue;
        private readonly Func<double, string> formatPosition;

        private readonly Container pathContainer;
        private readonly Box background;
        private readonly Box hoverLine;
        private readonly LayoutValue pathCached = new LayoutValue(Invalidation.DrawSize);

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        /// <param name="series">The lines of the graph.</param>
        /// <param name="startTime">The time at the left of the graph.</param>
        /// <param name="endTime">The time at the right of the graph.</param>
        /// <param name="formatValue">How values are shown in the tooltip.</param>
        /// <param name="minValue">The value at the bottom of the graph, or the lowest value if null.</param>
        /// <param name="maxValue">The value at the top of the graph, or the highest value if null.</param>
        /// <param name="formatPosition">How positions on the horizontal axis are shown in the tooltip, as times by default.</param>
        public TimeGraph(IReadOnlyList<Series> series, double startTime, double endTime, Func<double, string> formatValue, double? minValue = 0, double? maxValue = null,
                         Func<double, string>? formatPosition = null)
        {
            this.formatPosition = formatPosition ?? VerifierDisplay.FormatTime;
            this.series = series;
            this.startTime = startTime;
            this.endTime = Math.Max(endTime, startTime + 1);
            this.formatValue = formatValue;

            var values = series.SelectMany(s => s.Points.Select(p => p.Value)).ToArray();

            this.minValue = minValue ?? (values.Length > 0 ? values.Min() : 0);
            this.maxValue = maxValue ?? (values.Length > 0 ? values.Max() : 1);

            if (this.maxValue - this.minValue < 1e-6)
                this.maxValue = this.minValue + 1;

            RelativeSizeAxes = Axes.X;
            Height = 120;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 6,
                    Child = background = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0.5f },
                },
                createGridLine(0.25f),
                createGridLine(0.5f),
                createGridLine(0.75f),
                pathContainer = new Container { RelativeSizeAxes = Axes.Both },
                hoverLine = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 1,
                    Alpha = 0,
                    Colour = Color4.White,
                },
            };

            AddLayout(pathCached);
        }

        private static Drawable createGridLine(float y) => new Box
        {
            RelativePositionAxes = Axes.Y,
            RelativeSizeAxes = Axes.X,
            Y = y,
            Height = 1,
            Alpha = 0.06f,
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            background.Colour = colourProvider.Background6;
        }

        protected override void Update()
        {
            base.Update();

            if (!pathCached.IsValid)
            {
                createPaths();
                pathCached.Validate();
            }
        }

        private const float path_radius = 1.25f;

        private float getX(double time) => (float)((time - startTime) / (endTime - startTime)) * DrawWidth;

        private float getY(double value) => (float)(1 - (value - minValue) / (maxValue - minValue)) * (DrawHeight - 2 * path_radius - 4) + path_radius + 2;

        private void createPaths()
        {
            pathContainer.Clear();

            if (DrawWidth <= 0)
                return;

            foreach (var s in series)
            {
                if (s.Points.Count == 0)
                    continue;

                var vertices = new List<Vector2>();

                for (int i = 0; i < s.Points.Count; i++)
                {
                    var point = s.Points[i];
                    float y = getY(Math.Clamp(point.Value, minValue, maxValue));

                    if (s.Step && i > 0)
                        vertices.Add(new Vector2(getX(point.Time), vertices[^1].Y));

                    vertices.Add(new Vector2(getX(point.Time), y));
                }

                // step series continue until the end of the beatmap.
                if (s.Step)
                    vertices.Add(new Vector2(DrawWidth, vertices[^1].Y));

                if (vertices.Count == 1)
                    vertices.Add(vertices[0] + new Vector2(1, 0));

                var min = new Vector2(vertices.Min(v => v.X), vertices.Min(v => v.Y));

                pathContainer.Add(new SmoothPath
                {
                    PathRadius = path_radius,
                    Colour = s.Colour,
                    Vertices = vertices.Select(v => v - min).ToArray(),
                    Position = min - new Vector2(path_radius),
                });
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverLine.FadeTo(0.3f, 100);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverLine.FadeOut(100);
            base.OnHoverLost(e);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            float x = Math.Clamp(e.MousePosition.X, 0, DrawWidth);
            hoverLine.X = x;

            double time = startTime + x / Math.Max(DrawWidth, 1) * (endTime - startTime);

            var lines = new List<string> { formatPosition(time) };

            foreach (var s in series)
            {
                if (s.Points.Count == 0)
                    continue;

                double value = valueAt(s, time);
                lines.Add(s.Name == default ? formatValue(value) : $@"{s.Name}: {formatValue(value)}");
            }

            TooltipText = string.Join('\n', lines);
            return base.OnMouseMove(e);
        }

        private static double valueAt(Series series, double time)
        {
            var points = series.Points;

            // the last point at or before the time, as most series are sampled in intervals or are steps.
            int low = 0, high = points.Count - 1, found = 0;

            while (low <= high)
            {
                int mid = (low + high) / 2;

                if (points[mid].Time <= time)
                {
                    found = mid;
                    low = mid + 1;
                }
                else
                    high = mid - 1;
            }

            return points[found].Value;
        }
    }

    /// <summary>
    /// The colours and names of the lines of a graph.
    /// </summary>
    public partial class GraphLegend : FillFlowContainer
    {
        public GraphLegend(IEnumerable<(LocalisableString Name, Color4 Colour)> entries)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Full;
            Spacing = new Vector2(14, 4);

            foreach (var (name, colour) in entries)
            {
                Add(new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        new Circle
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(12, 4),
                            Colour = colour,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = name,
                            Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                        },
                    }
                });
            }
        }
    }

    /// <summary>
    /// Shown in a section of the overview while its analysis is running, or if there's nothing to show.
    /// </summary>
    public partial class SectionPlaceholder : CompositeDrawable
    {
        public SectionPlaceholder(bool loading, LocalisableString text = default)
        {
            RelativeSizeAxes = Axes.X;
            Height = 80;

            InternalChildren = loading
                ? new Drawable[]
                {
                    new osu.Game.Graphics.UserInterface.LoadingSpinner
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(30),
                        State = { Value = Visibility.Visible },
                    }
                }
                : new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = text == default ? VerifierStrings.NoAnalysis : text,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                        Alpha = 0.6f,
                    }
                };
        }
    }
}
