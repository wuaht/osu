// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Nominators;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The user filter and the counters of the discussion filters, like the bottom of the header of the website.
    /// </summary>
    public partial class DiscussionToolbar : DiscussionSection
    {
        private UserFilterButton userFilter = null!;
        private FillFlowContainer replyOptions = null!;
        private OsuCheckbox includeReplies = null!;
        private OsuCheckbox showOtherReplies = null!;
        private FillFlowContainer filters = null!;

        private bool updatingCheckboxes;

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
                    Spacing = new Vector2(0, 10),
                    Padding = new MarginPadding { Horizontal = 20, Vertical = 12 },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                userFilter = new UserFilterButton
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                filters = new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(4, 0),
                                },
                            }
                        },
                        replyOptions = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(30, 0),
                            Children = new Drawable[]
                            {
                                includeReplies = new OsuCheckbox(false)
                                {
                                    Width = 220,
                                    LabelText = SlopDiscussionStrings.IncludeReplies,
                                },
                                showOtherReplies = new OsuCheckbox(false)
                                {
                                    Width = 220,
                                    LabelText = SlopDiscussionStrings.ShowOtherReplies,
                                },
                            }
                        },
                    }
                }
            };

            includeReplies.Current.BindValueChanged(v =>
            {
                if (updatingCheckboxes || State == null)
                    return;

                State.View.RepliesIncludeSelectedUsers = v.NewValue;
                State.NotifyChanged();
            });

            showOtherReplies.Current.BindValueChanged(v =>
            {
                if (updatingCheckboxes || State == null)
                    return;

                State.View.ShowOtherReplies = v.NewValue;
                State.NotifyChanged();
            });
        }

        protected override void UpdateDisplay(DiscussionsState state)
        {
            userFilter.State = state;
            userFilter.UpdateText();

            bool hasSelectedUsers = state.View.SelectedUserIds.Count > 0;

            updatingCheckboxes = true;
            includeReplies.Current.Value = state.View.RepliesIncludeSelectedUsers;
            showOtherReplies.Current.Value = state.View.ShowOtherReplies;
            updatingCheckboxes = false;

            replyOptions.Alpha = hasSelectedUsers ? 1 : 0;

            filters.Clear();

            foreach (var filter in Enum.GetValues<DiscussionFilter>())
            {
                // the hype filter is only reachable from the hype train on the website.
                if (filter == DiscussionFilter.Hype)
                    continue;

                if (filter == DiscussionFilter.Mine && state.CurrentUserId == null)
                    continue;

                var f = filter;

                filters.Add(new FilterCounter(filter, state.DiscussionsByFilter[filter].Count, state.View.Page != DiscussionPage.Events && state.View.Filter == filter)
                {
                    Action = () => state.ChangeFilter(f),
                });
            }
        }

        private partial class FilterCounter : OsuClickableContainer
        {
            private readonly DiscussionFilter filter;
            private readonly int count;
            private readonly bool selected;

            private Box background = null!;
            private Box stripe = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public FilterCounter(DiscussionFilter filter, int count, bool selected)
            {
                this.filter = filter;
                this.count = count;
                this.selected = selected;

                Size = new Vector2(78, 48);
                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var colour = DiscussionDisplay.FilterColour(filter, colourProvider);

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = selected ? colourProvider.Background3 : colourProvider.Background4,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = DiscussionDisplay.FormatNumber(count),
                                Font = OsuFont.Default.With(size: 18, weight: FontWeight.Light),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = DiscussionDisplay.FilterName(filter),
                                Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                                Colour = colourProvider.Content2,
                            },
                        }
                    },
                    stripe = new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = selected ? 4 : 2,
                        Colour = colour,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!selected)
                {
                    background.FadeColour(colourProvider.Background3, 100, Easing.OutQuint);
                    stripe.ResizeHeightTo(4, 100, Easing.OutQuint);
                }

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!selected)
                {
                    background.FadeColour(colourProvider.Background4, 100, Easing.OutQuint);
                    stripe.ResizeHeightTo(2, 100, Easing.OutQuint);
                }

                base.OnHoverLost(e);
            }
        }

        private partial class UserFilterButton : OsuClickableContainer, IHasPopover
        {
            public DiscussionsState? State;

            private Box background = null!;
            private OsuSpriteText text = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public UserFilterButton()
            {
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8, 0),
                        Padding = new MarginPadding { Horizontal = 12, Vertical = 8 },
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = FontAwesome.Solid.UserFriends,
                                Size = new Vector2(13),
                                Colour = colourProvider.Content2,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = SlopDiscussionStrings.UserFilterLabel,
                                Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                                Colour = colourProvider.Content2,
                            },
                            text = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                            },
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = FontAwesome.Solid.ChevronDown,
                                Size = new Vector2(10),
                            },
                        }
                    },
                };

                Action = this.ShowPopover;
            }

            public void UpdateText()
            {
                if (State == null)
                    return;

                var selected = State.View.SelectedUserIds;

                if (selected.Count == 0)
                    text.Text = SlopDiscussionStrings.UserFilterEveryone;
                else if (selected.Count == 1)
                    text.Text = State.GetUser(selected.First())?.Username ?? SlopDiscussionStrings.UnknownUser.ToString();
                else
                    text.Text = SlopDiscussionStrings.UserFilterSelected(selected.Count);
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeColour(colourProvider.Background3, 100, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(colourProvider.Background4, 100, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            public Popover? GetPopover() => State == null ? null : new UserFilterPopover(State);
        }

        private partial class UserFilterPopover : OsuPopover
        {
            private readonly DiscussionsState state;

            private FillFlowContainer list = null!;

            [Resolved]
            private DiscussionScreen screen { get; set; } = null!;

            public UserFilterPopover(DiscussionsState state)
            {
                this.state = state;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new OsuScrollContainer
                {
                    Width = 260,
                    Height = 320,
                    Child = list = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                state.Changed += updateList;
                updateList();
            }

            private void updateList() => Scheduler.AddOnce(() =>
            {
                list.Clear();

                list.Add(new UserRow(null, SlopDiscussionStrings.UserFilterEveryone.ToString(), state.View.SelectedUserIds.Count == 0)
                {
                    Action = () => state.ToggleUser(null),
                });

                // the authors of the discussions of the page, see the user filter of the website.
                var userIds = new List<int>();

                if (state.View.Page != DiscussionPage.Events)
                {
                    foreach (var discussion in state.DiscussionsByMode[state.View.Page])
                    {
                        if (!userIds.Contains(discussion.UserId))
                            userIds.Add(discussion.UserId);
                    }
                }

                foreach (int id in state.View.SelectedUserIds)
                {
                    if (!userIds.Contains(id))
                        userIds.Add(id);
                }

                foreach (var user in userIds.Select(id => state.GetUser(id)).OfType<DiscussionUser>().OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase))
                {
                    int id = user.Id;

                    list.Add(new UserRow(user, user.IsDeleted ? SlopDiscussionStrings.DeletedUser.ToString() : user.Username, state.View.SelectedUserIds.Contains(id))
                    {
                        Action = () => state.ToggleUser(id),
                    });
                }

                if (state.View.SelectedUserIds.Count == 1)
                {
                    int selectedId = state.View.SelectedUserIds.First();

                    list.Add(new DiscussionTextButton(SlopDiscussionStrings.ModdingHistory, FontAwesome.Solid.History, 12)
                    {
                        Margin = new MarginPadding { Horizontal = 8, Vertical = 6 },
                        Action = () => screen.OpenOnWebsite(DiscussionDisplay.ModdingHistoryUrl(screen.WebsiteUrl, selectedId)),
                    });
                }
            });

            protected override void Dispose(bool isDisposing)
            {
                state.Changed -= updateList;
                base.Dispose(isDisposing);
            }
        }

        private partial class UserRow : OsuClickableContainer
        {
            private readonly DiscussionUser? user;
            private readonly string name;
            private readonly bool selected;

            private Box background = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public UserRow(DiscussionUser? user, string name, bool selected)
            {
                this.user = user;
                this.name = name;
                this.selected = selected;

                RelativeSizeAxes = Axes.X;
                Height = 32;
                Masking = true;
                CornerRadius = 4;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                        Alpha = selected ? 1 : 0,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8, 0),
                        Padding = new MarginPadding { Horizontal = 8 },
                        Children = new Drawable[]
                        {
                            user != null
                                ? new UpdateableAvatar(user.ToAPIUser(), false)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(22),
                                    Masking = true,
                                    CornerRadius = 4,
                                }
                                : new SpriteIcon
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Icon = FontAwesome.Solid.Users,
                                    Size = new Vector2(16),
                                    Margin = new MarginPadding { Horizontal = 3 },
                                },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = name,
                                Font = OsuFont.Default.With(size: 14, weight: selected ? FontWeight.Bold : FontWeight.Regular),
                                Colour = user?.ProfileColour != null ? Color4Extensions.FromHex(user.ProfileColour) : Color4.White,
                            },
                        }
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Icon = FontAwesome.Solid.Check,
                        Size = new Vector2(12),
                        Margin = new MarginPadding { Right = 10 },
                        Colour = colourProvider.Highlight1,
                        Alpha = selected ? 1 : 0,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeTo(1, 100, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!selected)
                    background.FadeTo(0, 100, Easing.OutQuint);

                base.OnHoverLost(e);
            }
        }
    }
}
