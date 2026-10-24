// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Framework.Objects;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.MapsetVerifier.UI.Checks
{
    /// <summary>
    /// The categories of issues (general and each difficulty), of which one is shown.
    /// Selecting a difficulty also switches the editor to it.
    /// </summary>
    public partial class DifficultyPicker : CompositeDrawable
    {
        [Resolved]
        private VerifierScreen screen { get; set; } = null!;

        private OsuScrollContainer scroll = null!;
        private FillFlowContainer<DifficultyButton> buttonFlow = null!;

        private string? displayedSelection;

        private string[] displayedCategories = Array.Empty<string>();

        public DifficultyPicker()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new VerifierPanel(Axes.Y)
            {
                RelativeSizeAxes = Axes.X,
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(8),
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new IconButton
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = FontAwesome.Solid.ChevronLeft,
                                Size = new Vector2(30),
                                IconScale = new Vector2(0.8f),
                                TooltipText = VerifierStrings.PreviousDifficulty,
                                Action = () => screen.SelectAdjacentCategory(-1),
                            },
                            scroll = new OsuScrollContainer(Direction.Horizontal)
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = DifficultyButton.HEIGHT,
                                ScrollbarOverlapsContent = false,
                                ScrollbarVisible = false,
                                Child = buttonFlow = new FillFlowContainer<DifficultyButton>
                                {
                                    AutoSizeAxes = Axes.X,
                                    RelativeSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(6, 0),
                                    Padding = new MarginPadding { Horizontal = 4 },
                                }
                            },
                            new IconButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Icon = FontAwesome.Solid.ChevronRight,
                                Size = new Vector2(30),
                                IconScale = new Vector2(0.8f),
                                TooltipText = VerifierStrings.NextDifficulty,
                                Action = () => screen.SelectAdjacentCategory(1),
                            },
                        }
                    }
                }
            };
        }

        public void UpdateFrom(VerifierSession session)
        {
            var categories = session.Results?.Categories;

            if (categories == null)
            {
                // nothing to show until the first check run finished, but the general category is always there.
                if (displayedCategories.Length != 1 || buttonFlow.Count == 0)
                {
                    buttonFlow.Clear();
                    buttonFlow.Add(new DifficultyButton(VerifierSession.GENERAL));
                    displayedCategories = new[] { VerifierSession.GENERAL };
                }
            }
            else
            {
                string[] names = categories.Select(c => c.Name).ToArray();

                if (!names.SequenceEqual(displayedCategories))
                {
                    buttonFlow.Clear();
                    buttonFlow.AddRange(names.Select(n => new DifficultyButton(n)));
                    displayedCategories = names;
                }
            }

            foreach (var button in buttonFlow)
                button.UpdateFrom(session.Results?.Get(button.Category), session.SelectedCategory == button.Category);

            if (displayedSelection != session.SelectedCategory)
            {
                displayedSelection = session.SelectedCategory;

                var selectedButton = buttonFlow.FirstOrDefault(b => b.Category == session.SelectedCategory);

                // the buttons are laid out on the next frame, by which they may have been replaced already.
                if (selectedButton != null)
                {
                    ScheduleAfterChildren(() =>
                    {
                        if (selectedButton.IsLoaded && selectedButton.Parent == buttonFlow)
                            scroll.ScrollIntoView(selectedButton);
                    });
                }
            }
        }

        private partial class DifficultyButton : OsuClickableContainer
        {
            public const float HEIGHT = 44;

            public readonly string Category;

            [Resolved]
            private VerifierScreen screen { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [Resolved]
            private RulesetStore rulesets { get; set; } = null!;

            private readonly Bindable<bool> useModeNames = new Bindable<bool>();

            private Box background = null!;
            private Box levelBar = null!;
            private Container iconContainer = null!;
            private OsuSpriteText nameText = null!;
            private OsuSpriteText detailsText = null!;
            private Container issueIconContainer = null!;

            private VerifierCategory? category;
            private bool selected;

            private Issue.Level? displayedHighestLevel;
            private global::MapsetVerifier.Parser.Objects.Beatmap.Mode? displayedMode;

            public DifficultyButton(string category)
            {
                Category = category;

                AutoSizeAxes = Axes.X;
                Height = HEIGHT;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                config.BindWith(OsuSetting.SlopMapsetVerifierShowGamemodeDifficultyNames, useModeNames);

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    levelBar = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 4,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.X,
                        RelativeSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 0),
                        Padding = new MarginPadding { Left = 14, Right = 12 },
                        Children = new Drawable[]
                        {
                            iconContainer = new Container
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(18),
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    nameText = new TruncatingSpriteText
                                    {
                                        MaxWidth = 220,
                                        Text = Category == VerifierSession.GENERAL ? VerifierStrings.General : Category,
                                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                                    },
                                    detailsText = new OsuSpriteText
                                    {
                                        Font = OsuFont.Default.With(size: 11),
                                        Alpha = 0.7f,
                                    },
                                }
                            },
                            issueIconContainer = new Container
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(14),
                            },
                        }
                    },
                };

                Action = () => screen.SelectCategory(Category);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                useModeNames.BindValueChanged(_ => updateDisplay());
                updateDisplay();
            }

            public void UpdateFrom(VerifierCategory? category, bool selected)
            {
                bool selectionChanged = this.selected != selected;

                this.category = category;
                this.selected = selected;

                if (!IsLoaded)
                    return;

                updateDisplay();

                if (selectionChanged && selected)
                    this.FlashColour(Color4.White.Opacity(0.2f), 300, Easing.OutQuint);
            }

            private void updateDisplay()
            {
                bool isGeneral = Category == VerifierSession.GENERAL;

                Color4 levelColour = category?.Level is global::MapsetVerifier.Parser.Objects.Beatmap.Difficulty level
                    ? VerifierDisplay.GetColour(level)
                    : colourProvider.Highlight1;

                levelBar.Colour = levelColour;
                levelBar.FadeTo(selected ? 1 : 0.5f, 200, Easing.OutQuint);

                updateBackground();

                var mode = category?.Mode;

                if (iconContainer.Count == 0 || mode != displayedMode)
                {
                    displayedMode = mode;
                    iconContainer.Child = isGeneral || mode == null
                        ? new SpriteIcon { RelativeSizeAxes = Axes.Both, Icon = FontAwesome.Solid.LayerGroup }
                        : VerifierDisplay.CreateIcon(rulesets, mode.Value, 18);
                }

                iconContainer.Colour = selected ? Color4.White : colourProvider.Content2;
                nameText.Colour = selected ? Color4.White : colourProvider.Content1;

                detailsText.Text = getDetails();

                var highestLevel = category?.HighestLevel;

                if (highestLevel != displayedHighestLevel)
                {
                    displayedHighestLevel = highestLevel;
                    issueIconContainer.Child = highestLevel == null
                        ? Empty()
                        : VerifierDisplay.CreateIcon(highestLevel.Value, 14);
                }
            }

            private LocalisableString getDetails()
            {
                if (category == null)
                    return string.Empty;

                if (category.IsGeneral)
                    return category.Issues.Count == 1 ? VerifierStrings.SingleIssue : VerifierStrings.IssueCount(category.Issues.Count);

                string level = category.Level is global::MapsetVerifier.Parser.Objects.Beatmap.Difficulty l
                    ? VerifierDisplay.GetName(l, category.Mode, useModeNames.Value)
                    : string.Empty;

                if (category.StarRating is double starRating)
                    return LocalisableString.Interpolate($"{level} · {starRating:0.00}★");

                return level;
            }

            private void updateBackground()
            {
                if (selected)
                    background.FadeColour(colourProvider.Background2, 200, Easing.OutQuint);
                else if (IsHovered)
                    background.FadeColour(colourProvider.Background3, 200, Easing.OutQuint);
                else
                    background.FadeColour(colourProvider.Background4, 200, Easing.OutQuint);
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateBackground();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateBackground();
                base.OnHoverLost(e);
            }
        }
    }
}
