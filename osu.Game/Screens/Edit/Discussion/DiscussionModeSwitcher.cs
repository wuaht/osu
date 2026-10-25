// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The tabs of the pages of the discussion, like the mode switcher of the website.
    /// </summary>
    public partial class DiscussionModeSwitcher : DiscussionSection
    {
        private FillFlowContainer tabs = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new NominatorsPanel(Axes.Y)
            {
                RelativeSizeAxes = Axes.X,
                Child = tabs = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(24, 0),
                    Padding = new MarginPadding { Horizontal = 20 },
                },
            };
        }

        protected override void UpdateDisplay(DiscussionsState state)
        {
            tabs.Clear();

            foreach (var page in new[] { DiscussionPage.Reviews, DiscussionPage.GeneralAll, DiscussionPage.General, DiscussionPage.Timeline, DiscussionPage.Events })
            {
                var p = page;
                int? count = page == DiscussionPage.Events ? null : state.DiscussionsForSelectedUserByMode[page].Count;

                tabs.Add(new ModeTab(pageName(state, page), count, state.View.Page == page)
                {
                    Action = () => state.ChangeDiscussionPage(p),
                });
            }
        }

        private static LocalisableString pageName(DiscussionsState state, DiscussionPage page)
        {
            switch (page)
            {
                case DiscussionPage.Reviews:
                    return SlopDiscussionStrings.ModeReviews;

                case DiscussionPage.GeneralAll:
                    return SlopDiscussionStrings.ModeGeneral(SlopDiscussionStrings.ScopeAllDifficulties.ToString());

                case DiscussionPage.General:
                    return SlopDiscussionStrings.ModeGeneral(state.CurrentBeatmap != null ? $@"({state.CurrentBeatmap.Version})" : string.Empty);

                case DiscussionPage.Timeline:
                    return SlopDiscussionStrings.ModeTimeline;

                default:
                    return SlopDiscussionStrings.ModeEvents;
            }
        }

        private partial class ModeTab : OsuClickableContainer
        {
            private readonly LocalisableString text;
            private readonly int? count;
            private readonly bool selected;

            private FillFlowContainer content = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ModeTab(LocalisableString text, int? count, bool selected)
            {
                this.text = text;
                this.count = count;
                this.selected = selected;

                AutoSizeAxes = Axes.X;
                Height = 50;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    content = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = text,
                                Font = OsuFont.Default.With(size: 15, weight: selected ? FontWeight.Bold : FontWeight.SemiBold),
                            },
                            new CircularContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.X,
                                Height = 18,
                                Masking = true,
                                Alpha = count != null ? 1 : 0,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = selected ? colourProvider.Highlight1 : colourProvider.Background3,
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Margin = new MarginPadding { Horizontal = 7 },
                                        Text = DiscussionDisplay.FormatNumber(count ?? 0),
                                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                                        Colour = selected ? colourProvider.Background6 : Color4.White,
                                    },
                                }
                            },
                        }
                    },
                    new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 3,
                        Colour = colourProvider.Highlight1,
                        Alpha = selected ? 1 : 0,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateColour();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateColour();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateColour();
                base.OnHoverLost(e);
            }

            private void updateColour() => content.FadeColour(selected || IsHovered ? Color4.White : colourProvider.Content2, 100, Easing.OutQuint);
        }
    }
}
