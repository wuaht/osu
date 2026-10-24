// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Framework.Objects;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;
using MvBeatmap = MapsetVerifier.Parser.Objects.Beatmap;

namespace osu.Game.MapsetVerifier.UI
{
    /// <summary>
    /// The beatmap set, the state of the checks and the tabs of the verify screen.
    /// </summary>
    public partial class VerifierHeader : CompositeDrawable
    {
        [Resolved]
        private VerifierScreen screen { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> working { get; set; } = null!;

        private OsuSpriteText title = null!;
        private OsuSpriteText artist = null!;
        private FillFlowContainer tags = null!;
        private OsuSpriteText statusText = null!;
        private FillFlowContainer levelCounts = null!;
        private Container progressBar = null!;
        private Box progressFill = null!;
        private LoadingSpinner spinner = null!;

        private string? displayedTags;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            var tabFlow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Margin = new MarginPadding { Top = 12 },
            };

            foreach (var type in Enum.GetValues<VerifierTabType>())
                tabFlow.Add(new TabPill(type));

            InternalChild = new VerifierPanel(Axes.Y)
            {
                RelativeSizeAxes = Axes.X,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.3f,
                        Child = new BeatmapBackgroundSprite(working.Value)
                        {
                            RelativeSizeAxes = Axes.Both,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            FillMode = FillMode.Fill,
                        },
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.6f), Color4.Black.Opacity(0.1f)),
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = 18, Top = 12, Bottom = 12 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 24),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 3),
                                    Children = new Drawable[]
                                    {
                                        title = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = OsuFont.Default.With(size: 22, weight: FontWeight.Bold),
                                        },
                                        artist = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = OsuFont.Default.With(size: 14),
                                        },
                                        tags = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Full,
                                            Spacing = new Vector2(5),
                                            Margin = new MarginPadding { Top = 5 },
                                        },
                                        tabFlow,
                                    }
                                },
                                Empty(),
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 8),
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(4, 0),
                                            Children = new Drawable[]
                                            {
                                                spinner = new LoadingSpinner
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Size = new Vector2(18),
                                                    Margin = new MarginPadding { Right = 6 },
                                                },
                                                statusText = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Font = OsuFont.Default.With(size: 14),
                                                    Alpha = 0.8f,
                                                    Margin = new MarginPadding { Right = 6 },
                                                },
                                                new IconButton
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Icon = FontAwesome.Solid.SyncAlt,
                                                    TooltipText = VerifierStrings.RefreshTooltip,
                                                    Action = () => screen.Check(true),
                                                    Size = new Vector2(30),
                                                    IconScale = new Vector2(0.8f),
                                                },
                                            }
                                        },
                                        levelCounts = new FillFlowContainer
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(5, 0),
                                        },
                                        progressBar = new Container
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            Width = 200,
                                            Height = 6,
                                            Masking = true,
                                            CornerRadius = 3,
                                            Children = new Drawable[]
                                            {
                                                new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = colourProvider.Background6,
                                                },
                                                progressFill = new Box
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Colour = colourProvider.Highlight1,
                                                    Width = 0,
                                                },
                                            }
                                        },
                                    }
                                },
                            }
                        }
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screen.SessionChanged += updateDisplay;
            updateDisplay();
        }

        private void updateDisplay()
        {
            var session = screen.Session;
            var metadata = working.Value.BeatmapInfo.Metadata;

            title.Text = new RomanisableString(metadata.TitleUnicode, metadata.Title);
            artist.Text = VerifierStrings.ByArtistMappedBy(metadata.Artist, metadata.Author.Username);

            var results = session.Results;

            updateTags(results);

            switch (session.State)
            {
                case VerifierRunState.Running:
                    spinner.Show();

                    var progress = session.Progress;

                    statusText.Text = progress == null || progress.Value.Total == 0
                        ? VerifierStrings.Checking
                        : VerifierStrings.CheckingProgress(progress.Value.Completed, progress.Value.Total);

                    progressBar.FadeIn(200, Easing.OutQuint);

                    if (progress != null && progress.Value.Total > 0)
                        progressFill.ResizeWidthTo((float)progress.Value.Completed / progress.Value.Total, 200, Easing.OutQuint);
                    else
                        progressFill.Width = 0;

                    break;

                case VerifierRunState.Failed:
                    spinner.Hide();
                    statusText.Text = VerifierStrings.CheckFailed;
                    progressBar.FadeOut(200, Easing.OutQuint);
                    break;

                case VerifierRunState.Ready:
                    spinner.Hide();
                    statusText.Text = VerifierStrings.CheckedAt(results?.RunAt.ToString(@"HH:mm:ss") ?? string.Empty);
                    progressBar.FadeOut(200, Easing.OutQuint);
                    break;

                default:
                    spinner.Hide();
                    statusText.Text = VerifierStrings.NotRunYet;
                    progressBar.FadeOut(200, Easing.OutQuint);
                    break;
            }

            levelCounts.Clear();

            if (results == null)
                return;

            foreach (var level in new[] { Issue.Level.Problem, Issue.Level.Warning, Issue.Level.Minor })
            {
                int count = results.Categories.Sum(c => c.Issues.Count(i => i.Level == level));

                levelCounts.Add(new NominatorTag(count.ToString(), VerifierDisplay.GetColour(level), new SpriteIcon { Icon = VerifierDisplay.GetIcon(level), Size = new Vector2(11) }, 12)
                {
                    TooltipText = VerifierDisplay.GetName(level),
                    Alpha = count > 0 ? 1 : 0.4f,
                });
            }
        }

        private void updateTags(VerifierResults? results)
        {
            var difficulties = results?.Categories.Where(c => !c.IsGeneral).ToArray() ?? Array.Empty<VerifierCategory>();
            var modes = difficulties.Select(c => c.Mode).OfType<MvBeatmap.Mode>().Distinct().OrderBy(m => m).ToArray();
            int difficultyCount = difficulties.Length > 0 ? difficulties.Length : working.Value.BeatmapSetInfo?.Beatmaps.Count ?? 0;

            string key = $@"{string.Join(',', modes)}|{difficultyCount}";

            if (key == displayedTags)
                return;

            displayedTags = key;
            tags.Clear();

            foreach (var mode in modes)
                tags.Add(new NominatorTag(VerifierDisplay.GetName(mode), Color4.White, VerifierDisplay.CreateIcon(rulesets, mode, 11), 12));

            tags.Add(new NominatorTag(VerifierStrings.DifficultyCount(difficultyCount), Color4.White, textSize: 12));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            screen.SessionChanged -= updateDisplay;
        }

        private partial class TabPill : OsuClickableContainer
        {
            [Resolved]
            private VerifierScreen screen { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private readonly VerifierTabType type;

            private Box background = null!;
            private OsuSpriteText text = null!;
            private FillFlowContainer content = null!;

            public TabPill(VerifierTabType type)
            {
                this.type = type;

                // must be set before the base class loads, as it sizes its content according to it.
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 15;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    content = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(7, 0),
                        Padding = new MarginPadding { Horizontal = 14, Vertical = 7 },
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = getIcon(type),
                                Size = new Vector2(12),
                            },
                            text = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = getName(type),
                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                            },
                        }
                    },
                };

                Action = () => screen.CurrentTab.Value = type;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                screen.CurrentTab.BindValueChanged(_ => updateState(), true);
                FinishTransforms(true);
            }

            protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(osu.Framework.Input.Events.HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                bool selected = screen.CurrentTab.Value == type;

                background.FadeColour(selected ? colourProvider.Highlight1 : IsHovered ? colourProvider.Background2 : colourProvider.Background4, 200, Easing.OutQuint);
                background.FadeTo(selected ? 1 : 0.8f, 200, Easing.OutQuint);
                content.FadeColour(selected ? colourProvider.Background6 : Color4.White, 200, Easing.OutQuint);
                text.Font = text.Font.With(weight: selected ? FontWeight.Bold : FontWeight.SemiBold);
            }

            private static IconUsage getIcon(VerifierTabType type)
            {
                switch (type)
                {
                    case VerifierTabType.Checks:
                        return FontAwesome.Solid.CheckDouble;

                    case VerifierTabType.Documentation:
                        return FontAwesome.Solid.Book;

                    case VerifierTabType.Snapshots:
                        return FontAwesome.Solid.History;

                    default:
                        return FontAwesome.Solid.ChartBar;
                }
            }

            private static LocalisableString getName(VerifierTabType type)
            {
                switch (type)
                {
                    case VerifierTabType.Checks:
                        return VerifierStrings.TabChecks;

                    case VerifierTabType.Documentation:
                        return VerifierStrings.TabDocumentation;

                    case VerifierTabType.Snapshots:
                        return VerifierStrings.TabSnapshots;

                    default:
                        return VerifierStrings.TabOverview;
                }
            }
        }
    }
}
