// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Screens.Edit.Nominators;
using osuTK;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The discussions of the selected page, like the discussions of the website.
    /// </summary>
    public partial class DiscussionList : DiscussionSection
    {
        private FillFlowContainer sortButtons = null!;
        private DiscussionTextButton collapseAll = null!;
        private DiscussionTextButton expandAll = null!;
        private FillFlowContainer<DrawableDiscussion> discussions = null!;
        private OsuSpriteText empty = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new NominatorsPanel(Axes.Y)
            {
                RelativeSizeAxes = Axes.X,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(20),
                    Spacing = new Vector2(0, 15),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new DiscussionTitle(SlopDiscussionStrings.DiscussionsTitle)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(20, 0),
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(10, 0),
                                            Children = new Drawable[]
                                            {
                                                new OsuSpriteText
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Text = SlopDiscussionStrings.Sort,
                                                    Font = OsuFont.Default.With(size: 13),
                                                    Colour = ColourProvider.Foreground1,
                                                },
                                                sortButtons = new FillFlowContainer
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(10, 0),
                                                },
                                            }
                                        },
                                        collapseAll = new DiscussionTextButton(SlopDiscussionStrings.CollapseAll, FontAwesome.Solid.AngleUp)
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                        expandAll = new DiscussionTextButton(SlopDiscussionStrings.ExpandAll, FontAwesome.Solid.AngleDown)
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                    }
                                },
                            }
                        },
                        empty = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Margin = new MarginPadding { Vertical = 20 },
                            Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                            Colour = ColourProvider.Content2,
                        },
                        discussions = new FillFlowContainer<DrawableDiscussion>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 10),
                        },
                    }
                }
            };

            collapseAll.Action = () => State?.SetAllCollapsed(true);
            expandAll.Action = () => State?.SetAllCollapsed(false);
        }

        protected override void UpdateDisplay(DiscussionsState state)
        {
            var view = state.View;

            Alpha = view.Page == DiscussionPage.Events ? 0 : 1;

            if (view.Page == DiscussionPage.Events)
            {
                discussions.Clear();
                return;
            }

            sortButtons.Clear();

            foreach (var sort in new[] { DiscussionSort.CreatedAt, DiscussionSort.Timeline, DiscussionSort.UpdatedAt })
            {
                // sorting by the timeline only makes sense for discussions which have a timestamp.
                if (sort == DiscussionSort.Timeline && view.Page != DiscussionPage.Timeline)
                    continue;

                var s = sort;

                sortButtons.Add(new DiscussionTextButton(sortName(sort))
                {
                    Selected = view.CurrentSort == sort,
                    Action = () => state.ChangeSort(s),
                });
            }

            var sorted = state.SortedDiscussions;

            bool filtered = view.Filter != DiscussionFilter.Total || view.SelectedUserIds.Count > 0;

            empty.Text = filtered ? SlopDiscussionStrings.EmptyHidden : SlopDiscussionStrings.Empty;
            empty.Alpha = sorted.Count == 0 ? 1 : 0;

            collapseAll.Alpha = expandAll.Alpha = sorted.Count == 0 ? 0 : 1;

            discussions.ChildrenEnumerable = sorted.Select(d => new DrawableDiscussion(state, d));

            if (view.ScrollToHighlighted)
            {
                view.ScrollToHighlighted = false;

                var highlighted = discussions.FirstOrDefault(d => d.Discussion.Id == view.HighlightedDiscussionId);

                // wait for the layout of the discussions before scrolling to the highlighted one.
                if (highlighted != null)
                    Schedule(() => ScheduleAfterChildren(() => Screen.ScrollIntoView(highlighted)));
            }
        }

        private static osu.Framework.Localisation.LocalisableString sortName(DiscussionSort sort)
        {
            switch (sort)
            {
                case DiscussionSort.CreatedAt:
                    return SlopDiscussionStrings.SortCreatedAt;

                case DiscussionSort.Timeline:
                    return SlopDiscussionStrings.SortTimeline;

                default:
                    return SlopDiscussionStrings.SortUpdatedAt;
            }
        }
    }
}
