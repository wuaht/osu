// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.MapsetVerifier.UI.Overview
{
    /// <summary>
    /// The results of the analyses of the beatmap set (metadata, resources, objects, difficulty, audio and video).
    /// </summary>
    public partial class OverviewTab : VerifierTab
    {
        public const float NAVIGATION_WIDTH = 210;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Bindable<bool> advancedAudio = new Bindable<bool>();
        private readonly Bindable<string?> selectedDifficulty = new Bindable<string?>();

        private OverviewSection[] sections = null!;
        private FillFlowContainer<NavigationRow> navigation = null!;
        private OsuScrollContainer scroll = null!;
        private Container errorBanner = null!;
        private OsuTextFlowContainer errorText = null!;

        private bool shown;
        private string? lastCategory;
        private int displayedErrorCount = -1;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopMapsetVerifierShowAdvancedAudioAnalysis, advancedAudio);

            sections = new OverviewSection[]
            {
                new MetadataSection(),
                new ResourcesSection(),
                new StatisticsSection(),
                new SettingsSection(),
                new ColoursSection(),
                new ObjectsSection(selectedDifficulty),
                new DifficultySection(selectedDifficulty),
                new AudioSection(),
                new VideoSection(),
            };

            var sectionFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 24),
                Padding = new MarginPadding { Horizontal = 16, Top = 12, Bottom = 40 },
            };

            sectionFlow.Add(errorBanner = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 6,
                Alpha = 0,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = VerifierDisplay.WARNING,
                        Alpha = 0.15f,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = VerifierDisplay.WARNING,
                    },
                    errorText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = 14, Vertical = 8 },
                    },
                }
            });

            sectionFlow.AddRange(sections);

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, NAVIGATION_WIDTH),
                    new Dimension(GridSizeMode.Absolute, VerifierScreen.SPACING),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new VerifierPanel
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        Height = 30,
                                        Margin = new MarginPadding { Top = 12 },
                                        Padding = new MarginPadding { Horizontal = 12 },
                                        Child = new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = VerifierStrings.TabOverview,
                                            Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
                                        },
                                    },
                                    navigation = new FillFlowContainer<NavigationRow>
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                        Padding = new MarginPadding { Horizontal = 8, Top = 4, Bottom = 8 },
                                        ChildrenEnumerable = sections.Select(s => new NavigationRow(s, () => scroll.ScrollTo(s))),
                                    },
                                }
                            }
                        },
                        Empty(),
                        new VerifierPanel
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = scroll = new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = sectionFlow,
                            }
                        },
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedDifficulty.BindValueChanged(e =>
            {
                if (e.NewValue != null && Session.Results != null && Session.Results.Categories.Any(c => c.Name == e.NewValue))
                    Screen.SelectCategory(e.NewValue);
            });

            advancedAudio.BindValueChanged(e =>
            {
                if (e.NewValue && shown)
                    Session.RequestAdvancedAudio();
            });
        }

        public override void OnShown()
        {
            base.OnShown();

            shown = true;
            Session.RequestOverview();

            if (advancedAudio.Value)
                Session.RequestAdvancedAudio();
        }

        protected override void OnSessionChanged()
        {
            var overview = Session.Overview;

            updateSelectedDifficulty(overview);

            foreach (var section in sections)
                section.UpdateFrom(overview);

            int errorCount = overview?.Errors.Count ?? 0;

            if (errorCount != displayedErrorCount)
            {
                displayedErrorCount = errorCount;

                if (errorCount > 0)
                {
                    errorText.Text = VerifierStrings.OverviewFailed(string.Join(@" · ", overview!.Errors));
                    errorBanner.FadeIn(200, Easing.OutQuint);
                }
                else
                    errorBanner.FadeOut(200, Easing.OutQuint);
            }
        }

        private void updateSelectedDifficulty(VerifierOverview? overview)
        {
            var difficulties = overview?.Objects?.Difficulties.Select(d => d.Version).ToArray()
                               ?? overview?.Beatmap?.Statistics.Select(s => s.Version).ToArray()
                               ?? overview?.Metadata?.Difficulties.Select(d => d.Version).ToArray();

            string category = Session.SelectedCategory;

            // follows difficulties which were selected elsewhere (e.g. in the checks or by switching the difficulty of the editor).
            if (category != lastCategory)
            {
                lastCategory = category;

                if (category != VerifierSession.GENERAL && (difficulties == null || difficulties.Contains(category)))
                {
                    selectedDifficulty.Value = category;
                    return;
                }
            }

            if (difficulties != null && difficulties.Length > 0 && (selectedDifficulty.Value == null || !difficulties.Contains(selectedDifficulty.Value)))
                selectedDifficulty.Value = difficulties[0];
        }

        protected override void Update()
        {
            base.Update();

            // highlights the section at the top of the scroll container, or the last section if scrolled to the end.
            OverviewSection? current = null;

            if (scroll.IsScrolledToEnd(10))
                current = sections[^1];
            else
            {
                foreach (var section in sections)
                {
                    if (scroll.GetChildPosInContent(section) <= scroll.Current + 60)
                        current = section;
                }
            }

            current ??= sections[0];

            foreach (var row in navigation)
                row.Active = row.Section == current;
        }

        private partial class NavigationRow : OsuClickableContainer
        {
            public readonly OverviewSection Section;

            private bool active;

            public bool Active
            {
                get => active;
                set
                {
                    if (active == value)
                        return;

                    active = value;
                    updateState();
                }
            }

            private readonly Box background;
            private readonly Box bar;
            private readonly OsuSpriteText text;
            private readonly SpriteIcon icon;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public NavigationRow(OverviewSection section, Action onClick)
            {
                Section = section;
                Action = onClick;

                RelativeSizeAxes = Axes.X;
                Height = 32;
                Masking = true;
                CornerRadius = 6;

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                    bar = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Alpha = 0,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 0),
                        Padding = new MarginPadding { Left = 12 },
                        Children = new Drawable[]
                        {
                            icon = new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = section.Icon,
                                Size = new Vector2(13),
                            },
                            text = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = section.Title,
                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                            },
                        }
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                background.Colour = colourProvider.Background3;
                bar.Colour = colourProvider.Highlight1;
                updateState();
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
                background.FadeTo(active ? 1 : IsHovered ? 0.5f : 0, 150, Easing.OutQuint);
                bar.FadeTo(active ? 1 : 0, 150, Easing.OutQuint);
                text.FadeColour(active ? colourProvider.Content1 : colourProvider.Content2, 150, Easing.OutQuint);
                icon.FadeColour(active ? colourProvider.Highlight1 : colourProvider.Content2, 150, Easing.OutQuint);
            }
        }
    }
}
