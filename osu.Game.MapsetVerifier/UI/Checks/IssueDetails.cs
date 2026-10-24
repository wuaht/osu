// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;
using osu.Game.Screens.Edit.Nominators;
using osuTK;

namespace osu.Game.MapsetVerifier.UI.Checks
{
    /// <summary>
    /// The details of an issue: the ranking criteria it refers to, and the documentation of its check.
    /// </summary>
    public partial class IssueDetails : CompositeDrawable
    {
        [Resolved]
        private IssueFilter filter { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        private OsuScrollContainer scroll = null!;
        private FillFlowContainer content = null!;

        private VerifierIssue? displayedIssue;

        public IssueDetails()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new VerifierPanel
            {
                RelativeSizeAxes = Axes.Both,
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                Margin = new MarginPadding { Top = 12 },
                                Padding = new MarginPadding { Horizontal = 12 },
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = VerifierStrings.IssueDetails,
                                        Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Icon = FontAwesome.Solid.Times,
                                        Size = new Vector2(30),
                                        IconScale = new Vector2(0.8f),
                                        TooltipText = VerifierStrings.CloseDetails,
                                        Action = () => filter.SelectedIssue.Value = null,
                                    },
                                }
                            },
                        },
                        new Drawable[]
                        {
                            scroll = new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = content = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 10),
                                    Padding = new MarginPadding { Horizontal = 12, Top = 8, Bottom = 12 },
                                }
                            },
                        },
                    }
                }
            };
        }

        public void Show(VerifierIssue issue, string category)
        {
            if (issue == displayedIssue)
                return;

            displayedIssue = issue;

            var colour = VerifierDisplay.GetColour(issue.Level);

            content.Clear();
            content.AddRange(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 6,
                    Children = new Drawable[]
                    {
                        new osu.Framework.Graphics.Shapes.Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                        new osu.Framework.Graphics.Shapes.Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 3,
                            Colour = colour,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 6),
                            Padding = new MarginPadding { Left = 13, Right = 10, Vertical = 10 },
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(6),
                                    Children = new Drawable[]
                                    {
                                        new NominatorTag(VerifierDisplay.GetName(issue.Level), colour, new SpriteIcon { Icon = VerifierDisplay.GetIcon(issue.Level), Size = new Vector2(9), Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft }, 10),
                                        new NominatorTag(category == VerifierSession.GENERAL ? VerifierStrings.General : category, colourProvider.Highlight1, textSize: 10),
                                    }
                                },
                                new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = issue.CheckName,
                                },
                                new IssueTextFlow(issue.Message, category, 13),
                            }
                        },
                    }
                },
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30,
                    Text = VerifierStrings.Copy,
                    BackgroundColour = colourProvider.Background3,
                    Action = () =>
                    {
                        clipboard.SetText(VerifierDisplay.FormatNumbers(issue.Message));
                        onScreenDisplay?.Display(new CopiedToClipboardToast());
                    },
                },
                new RuleStatementList(issue.RuleIds),
                new VerifierSubtitle(VerifierStrings.CheckDescription) { Margin = new MarginPadding { Top = 4 } },
                new CheckDocumentation(issue.CheckId),
            });

            scroll.ScrollToStart(false);
        }
    }
}
