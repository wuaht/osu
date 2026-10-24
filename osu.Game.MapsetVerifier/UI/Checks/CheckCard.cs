// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Framework.Objects;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
using osu.Game.Screens.Edit.Nominators;
using osuTK;

namespace osu.Game.MapsetVerifier.UI.Checks
{
    /// <summary>
    /// The issues which a check found in a category.
    /// </summary>
    public partial class CheckCard : CompositeDrawable
    {
        /// <summary>
        /// How many issues are shown before the rest needs to be expanded.
        /// </summary>
        private const int collapsed_issue_count = 5;

        public bool Expanded;

        public Action<bool>? ExpandedChanged;

        [Resolved]
        private IssueFilter filter { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly string category;
        private readonly int checkId;
        private readonly string checkName;
        private readonly List<VerifierIssue> issues;
        private readonly bool hasMinorIssues;

        private FillFlowContainer issueFlow = null!;
        private Issue.Level highestLevel;
        private ShowMoreButton showMoreButton = null!;

        /// <param name="category">The category of the issues.</param>
        /// <param name="checkId">The check which found the issues.</param>
        /// <param name="checkName">The name of the check.</param>
        /// <param name="issues">The issues which are shown.</param>
        /// <param name="hasMinorIssues">Whether the check found negligible issues, even if they are hidden.</param>
        public CheckCard(string category, int checkId, string checkName, List<VerifierIssue> issues, bool hasMinorIssues)
        {
            this.category = category;
            this.checkId = checkId;
            this.checkName = checkName;
            this.issues = issues;
            this.hasMinorIssues = hasMinorIssues;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 8;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            highestLevel = issues.MaxBy(i => VerifierDisplay.GetSeverity(i.Level))!.Level;
            var levelColour = VerifierDisplay.GetColour(highestLevel);

            bool minorHidden = filter.HiddenMinorChecks.Contains(checkId);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 3,
                    Colour = levelColour,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = 3, Bottom = 6 },
                    Children = new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 38,
                            Padding = new MarginPadding { Left = 12, Right = 6 },
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.Absolute, 10),
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.Absolute, 8),
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            Content = new[]
                            {
                                new[]
                                {
                                    VerifierDisplay.CreateIcon(highestLevel, 16).With(i =>
                                    {
                                        i.Anchor = Anchor.CentreLeft;
                                        i.Origin = Anchor.CentreLeft;
                                    }),
                                    Empty(),
                                    new TruncatingSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        MaxWidth = 600,
                                        Text = checkName,
                                        Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
                                        ShowTooltip = true,
                                    },
                                    Empty(),
                                    new NominatorTag(issues.Count.ToString(), levelColour, textSize: 11)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                    },
                                    Empty(),
                                    hasMinorIssues
                                        ? new IconButton
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Size = new Vector2(30),
                                            IconScale = new Vector2(0.7f),
                                            Icon = minorHidden ? FontAwesome.Solid.EyeSlash : FontAwesome.Solid.Eye,
                                            TooltipText = minorHidden ? VerifierStrings.ShowNegligibleOfCheck : VerifierStrings.HideNegligibleOfCheck,
                                            Action = () => filter.SetMinorHidden(checkId, !minorHidden),
                                        }
                                        : Empty(),
                                }
                            }
                        },
                        issueFlow = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Horizontal = 6 },
                        },
                        showMoreButton = new ShowMoreButton
                        {
                            Margin = new MarginPadding { Left = 38, Top = 4 },
                            Action = () =>
                            {
                                Expanded = !Expanded;
                                ExpandedChanged?.Invoke(Expanded);
                                updateIssues();
                            },
                        },
                    }
                },
            };

            updateIssues();
        }

        private void updateIssues()
        {
            int count = Expanded ? issues.Count : Math.Min(issues.Count, collapsed_issue_count);

            // issues which were shown already are kept, such that expanding doesn't recreate them.
            while (issueFlow.Count > count)
                issueFlow.Remove(issueFlow[^1], true);

            for (int i = issueFlow.Count; i < count; i++)
                issueFlow.Add(new IssueRow(issues[i], category, highestLevel));

            int hidden = issues.Count - collapsed_issue_count;

            showMoreButton.Alpha = hidden > 0 ? 1 : 0;
            showMoreButton.Text = Expanded ? VerifierStrings.ShowLess : VerifierStrings.ShowMore(hidden);
        }

        private partial class IssueRow : OsuClickableContainer
        {
            [Resolved]
            private IssueFilter filter { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private readonly VerifierIssue issue;
            private readonly string category;
            private readonly Issue.Level cardLevel;

            private readonly Bindable<bool> showDetails = new Bindable<bool>();
            private readonly IBindable<(VerifierIssue Issue, string Category)?> selectedIssue = new Bindable<(VerifierIssue Issue, string Category)?>();

            private Box background = null!;

            public IssueRow(VerifierIssue issue, string category, Issue.Level cardLevel)
            {
                this.issue = issue;
                this.category = category;
                this.cardLevel = cardLevel;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                config.BindWith(OsuSetting.SlopMapsetVerifierShowIssueDetails, showDetails);

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                        Alpha = 0,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = 6, Vertical = 4 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 26),
                            new Dimension(),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                // the level of the card is shown already, so only issues of other levels stand out.
                                VerifierDisplay.CreateIcon(issue.Level, 11).With(i =>
                                {
                                    i.Margin = new MarginPadding { Left = 2.5f, Top = 3 };
                                    i.Alpha = issue.Level == cardLevel ? 0.35f : 1;
                                }),
                                new IssueTextFlow(issue.Message, category),
                            }
                        }
                    },
                };

                Action = () =>
                {
                    if (!showDetails.Value)
                        return;

                    bool alreadySelected = selectedIssue.Value is var (selected, _) && selected == issue;
                    filter.SelectedIssue.Value = alreadySelected ? null : (issue, category);
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                selectedIssue.BindTo(filter.SelectedIssue);
                selectedIssue.BindValueChanged(_ => updateBackground());
                showDetails.BindValueChanged(_ => updateBackground(), true);
            }

            private bool isSelected => selectedIssue.Value is var (selected, _) && selected == issue;

            private void updateBackground()
            {
                float alpha = isSelected ? 1 : IsHovered && showDetails.Value ? 0.6f : 0;
                background.FadeTo(alpha, 150, Easing.OutQuint);
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

        private partial class ShowMoreButton : OsuHoverContainer
        {
            private readonly OsuSpriteText text;

            public LocalisableString Text
            {
                set => text.Text = value;
            }

            public ShowMoreButton()
            {
                AutoSizeAxes = Axes.Both;

                Child = text = new OsuSpriteText
                {
                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                IdleColour = colourProvider.Light2;
                HoverColour = colourProvider.Content1;
            }
        }
    }
}
