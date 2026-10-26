// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// A discussion with its posts, like a discussion on the website.
    /// </summary>
    public partial class DrawableDiscussion : CompositeDrawable
    {
        public readonly BeatmapDiscussion Discussion;

        private readonly DiscussionsState state;

        private Container replies = null!;
        private SpriteIcon collapseIcon = null!;

        [Resolved]
        private DiscussionScreen screen { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public DrawableDiscussion(DiscussionsState state, BeatmapDiscussion discussion)
        {
            this.state = state;
            Discussion = discussion;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var view = state.View;
            bool highlighted = view.HighlightedDiscussionId == Discussion.Id;

            var posts = Discussion.Posts.ToList();
            var startingPost = posts.FirstOrDefault();
            var replyPosts = posts.Skip(1).ToList();

            // replies of other users are hidden when only showing the posts of the selected users.
            int hiddenReplies = 0;

            if (view.SelectedUserIds.Count > 0 && !view.ShowOtherReplies)
            {
                hiddenReplies = replyPosts.Count(p => !view.SelectedUserIds.Contains(p.UserId));
                replyPosts = replyPosts.Where(p => view.SelectedUserIds.Contains(p.UserId)).ToList();
            }

            var typeColour = DiscussionDisplay.TypeColour(Discussion.MessageType, colourProvider);

            var replyFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            };

            foreach (var post in replyPosts)
                replyFlow.Add(new DrawableDiscussionPost(state, Discussion, post, false));

            if (hiddenReplies > 0)
            {
                string count = DiscussionDisplay.FormatNumber(hiddenReplies);

                replyFlow.Add(new OsuSpriteText
                {
                    Margin = new MarginPadding { Horizontal = 20, Vertical = 8 },
                    Text = hiddenReplies == 1 ? SlopDiscussionStrings.HiddenRepliesOne(count) : SlopDiscussionStrings.HiddenRepliesMany(count),
                    Font = OsuFont.Default.With(size: 12, italics: true),
                    Colour = colourProvider.Foreground1,
                });
            }

            if (!state.BeatmapSet.DiscussionLocked)
            {
                replyFlow.Add(new ReplyBox(Discussion));
            }

            Masking = true;
            CornerRadius = 10;
            BorderThickness = highlighted ? 2 : 0;
            BorderColour = colourProvider.Highlight1;

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
                    Width = 4,
                    Colour = typeColour,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = 4 },
                    Children = new Drawable[]
                    {
                        createTopBar(typeColour),
                        startingPost != null ? new DrawableDiscussionPost(state, Discussion, startingPost, true) : Empty(),
                        replies = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colourProvider.Background5,
                                },
                                replyFlow,
                            }
                        },
                    }
                },
            };

            updateCollapsed(false);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (state.View.HighlightedDiscussionId == Discussion.Id)
                Schedule(() => this.FlashColour(colourProvider.Light1.Opacity(0.6f), 1000, Easing.OutQuint));
        }

        private Drawable createTopBar(Color4 typeColour)
        {
            var left = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(10, 0),
                Children = new Drawable[]
                {
                    new TypeIcon(Discussion, typeColour)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                }
            };

            if (Discussion.Timestamp is int timestamp)
            {
                string text = DiscussionDisplay.FormatTimestamp(timestamp);

                left.Add(new DiscussionTextButton(text, FontAwesome.Regular.Clock, 14)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    TooltipText = SlopDiscussionStrings.ShowInEditor,
                    AccentColour = colourProvider.Highlight1,
                    Action = () => screen.ShowTimestamp(text, Discussion.BeatmapId),
                });
            }
            else if (Discussion.MessageType != @"review")
            {
                left.Add(new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = Discussion.BeatmapId == null ? SlopDiscussionStrings.AllDifficulties : SlopDiscussionStrings.TimestampGeneral,
                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                    Colour = colourProvider.Content2,
                });
            }

            // discussions about a difficulty show it, since timestamps refer to it.
            if (Discussion.BeatmapId is int beatmapId && state.Beatmaps.GetValueOrDefault(beatmapId) is DiscussionBeatmap beatmap && state.View.Page != DiscussionPage.Timeline
                && state.View.Page != DiscussionPage.General)
            {
                left.Add(new DiscussionPill(beatmap.Version, colourProvider.Background6, colourProvider.Content2)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                });
            }

            if (state.IsUnread(Discussion))
            {
                left.Add(new Circle
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(6),
                    Colour = colourProvider.Highlight1,
                });
            }

            var right = new FillFlowContainer
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(15, 0),
            };

            // like the website, every discussion can be voted on, except the own one and deleted or locked ones.
            bool locked = Discussion.DeletedAt != null || (state.BeatmapSet.DiscussionLocked && !screen.IsCurrentUserModerator);
            bool canVote = screen.IsSignedIn && Discussion.UserId != state.CurrentUserId && !locked;

            right.Add(new VoteButton(state, Discussion, true)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Action = () => vote(true),
                Enabled = { Value = canVote },
            });

            right.Add(new VoteButton(state, Discussion, false)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Action = () => vote(false),
                Enabled = { Value = canVote },
            });

            right.Add(new OsuClickableContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(24),
                Action = toggleCollapsed,
                Child = collapseIcon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Icon = FontAwesome.Solid.ChevronDown,
                    Colour = colourProvider.Content2,
                },
            });

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 40,
                Padding = new MarginPadding { Horizontal = 20 },
                Children = new Drawable[] { left, right },
            };
        }

        private void vote(bool up)
        {
            int score = Discussion.CurrentUserAttributes?.VoteScore ?? 0;

            // voting the same way again takes back the vote.
            if (up)
                screen.Vote(Discussion, score > 0 ? 0 : 1);
            else
                screen.Vote(Discussion, score < 0 ? 0 : -1);
        }

        private void toggleCollapsed()
        {
            // collapsing is only a local change, so the discussions aren't shown again.
            state.View.Collapsed[Discussion.Id] = !state.View.IsCollapsed(Discussion.Id);
            updateCollapsed(true);
        }

        private void updateCollapsed(bool animate)
        {
            bool collapsed = state.View.IsCollapsed(Discussion.Id);
            double duration = animate ? 200 : 0;

            replies.FadeTo(collapsed ? 0 : 1, duration, Easing.OutQuint);

            if (collapsed)
                replies.ResizeHeightTo(0, duration, Easing.OutQuint).OnComplete(r => r.AutoSizeAxes = Axes.None);
            else
            {
                replies.ClearTransforms(targetMember: nameof(Height));
                replies.AutoSizeAxes = Axes.Y;
            }

            collapseIcon.RotateTo(collapsed ? 0 : 180, duration, Easing.OutQuint);
        }

        /// <summary>
        /// The icon of the type of a discussion, or a check if it's resolved.
        /// </summary>
        private partial class TypeIcon : CompositeDrawable, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public TypeIcon(BeatmapDiscussion discussion, Color4 colour)
            {
                TooltipText = DiscussionDisplay.TypeName(discussion.MessageType);

                AutoSizeAxes = Axes.Both;

                var icons = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5, 0),
                    Child = new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(16),
                        Icon = DiscussionDisplay.TypeIcon(discussion.MessageType),
                        Colour = colour,
                    },
                };

                if (discussion.CanBeResolved && discussion.Resolved)
                {
                    icons.Add(new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(14),
                        Icon = DiscussionDisplay.ResolvedIcon,
                        Colour = DiscussionDisplay.GREEN,
                    });
                }

                InternalChild = icons;
            }
        }

        /// <summary>
        /// The votes of a discussion, with the latest voters as tooltip. Voting is done on the website.
        /// </summary>
        private partial class ReplyBox : CompositeDrawable
        {
            private readonly BeatmapDiscussion discussion;

            private DiscussionTextButton respondButton = null!;
            private FillFlowContainer form = null!;
            private DiscussionTextEditor textBox = null!;

            [Resolved]
            private DiscussionScreen screen { get; set; } = null!;

            public ReplyBox(BeatmapDiscussion discussion)
            {
                this.discussion = discussion;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Padding = new MarginPadding { Horizontal = 20, Vertical = 10 };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var buttons = new FillFlowContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Children = new Drawable[]
                    {
                        new RoundedButton
                        {
                            Width = 120,
                            Height = 30,
                            Text = WebCommonStrings.ButtonsCancel,
                            Action = () => setExpanded(false),
                        },
                    }
                };

                var attributes = discussion.CurrentUserAttributes;

                // like the website, replying can resolve or reopen the discussion at the same time.
                if (discussion.CanBeResolved && !discussion.Resolved && attributes?.CanResolve == true)
                    buttons.Add(createButton(SlopDiscussionStrings.ReplyAndResolve, true));
                else if (discussion.CanBeResolved && discussion.Resolved && attributes?.CanReopen == true)
                    buttons.Add(createButton(SlopDiscussionStrings.ReplyAndReopen, false));

                buttons.Add(createButton(SlopDiscussionStrings.Reply, null));

                InternalChildren = new Drawable[]
                {
                    respondButton = new DiscussionTextButton(SlopDiscussionStrings.Respond, FontAwesome.Solid.Reply)
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Action = () => setExpanded(true),
                    },
                    form = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            textBox = new DiscussionTextEditor(SlopDiscussionStrings.ReplyPlaceholder),
                            buttons,
                        }
                    },
                };

                textBox.Committed += () => reply(null);
            }

            private RoundedButton createButton(LocalisableString text, bool? resolved) => new RoundedButton
            {
                Width = 150,
                Height = 30,
                Text = text,
                BackgroundColour = DiscussionDisplay.BLUE_DARKER,
                Action = () => reply(resolved),
            };

            private void setExpanded(bool expanded)
            {
                respondButton.Alpha = expanded ? 0 : 1;
                form.Alpha = expanded ? 1 : 0;

                if (expanded)
                    Schedule(textBox.TakeFocus);
            }

            private void reply(bool? resolved)
            {
                string message = textBox.Text.Trim();

                if (message.Length == 0)
                    return;

                screen.Reply(discussion, message, resolved, () =>
                {
                    textBox.Text = string.Empty;
                    setExpanded(false);
                });
            }
        }

        private partial class VoteButton : OsuClickableContainer
        {
            private const int max_voters = 15;

            private readonly bool up;
            private readonly int count;
            private readonly bool voted;

            private FillFlowContainer content = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public VoteButton(DiscussionsState state, BeatmapDiscussion discussion, bool up)
            {
                this.up = up;

                count = (up ? discussion.Votes?.Up : discussion.Votes?.Down) ?? 0;

                int score = discussion.CurrentUserAttributes?.VoteScore ?? 0;
                voted = up ? score > 0 : score < 0;

                var voters = (up ? discussion.Votes?.Voters?.Up : discussion.Votes?.Voters?.Down) ?? new List<int>();
                var names = voters.Take(max_voters).Select(id => state.GetUser(id)?.Username).Where(n => n != null).ToList();

                if (names.Count == 0)
                    TooltipText = up ? SlopDiscussionStrings.VotesNoneUp : SlopDiscussionStrings.VotesNoneDown;
                else
                    TooltipText = $"{(up ? SlopDiscussionStrings.VotesLatestUp : SlopDiscussionStrings.VotesLatestDown)}\n{string.Join(@", ", names)}";

                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = content = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(14),
                            Icon = up ? FontAwesome.Solid.ThumbsUp : FontAwesome.Solid.ThumbsDown,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = DiscussionDisplay.FormatNumber(count),
                            Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                        },
                    }
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

            private void updateColour()
            {
                Color4 colour = voted ? (up ? DiscussionDisplay.GREEN : DiscussionDisplay.RED_1) : colourProvider.Content2;
                content.FadeColour(IsHovered && Enabled.Value ? colour.Lighten(0.3f) : colour, 100, Easing.OutQuint);
                content.FadeTo(Enabled.Value || voted ? 1 : 0.5f, 100, Easing.OutQuint);
            }
        }
    }

    /// <summary>
    /// A post of a discussion, like a post of a discussion on the website.
    /// </summary>
    public partial class DrawableDiscussionPost : CompositeDrawable
    {
        private readonly DiscussionsState state;
        private readonly BeatmapDiscussion discussion;
        private readonly BeatmapDiscussionPost post;
        private readonly bool isStartingPost;

        private Box unreadMarker = null!;

        [Resolved]
        private DiscussionScreen screen { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        public DrawableDiscussionPost(DiscussionsState state, BeatmapDiscussion discussion, BeatmapDiscussionPost post, bool isStartingPost)
        {
            this.state = state;
            this.discussion = discussion;
            this.post = post;
            this.isStartingPost = isStartingPost;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            bool unread = !state.View.ReadPostIds.Contains(post.Id);

            if (post.System)
            {
                InternalChildren = new Drawable[]
                {
                    unreadMarker = createUnreadMarker(unread),
                    createSystemPost(),
                };
                return;
            }

            var user = state.GetUser(post.UserId);
            var group = DiscussionDisplay.BadgeGroup(state, user, discussion);

            var userCard = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new UpdateableAvatar(user?.ToAPIUser(), isInteractive: user != null && !user.IsDeleted, showGuestOnNull: false)
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Size = new Vector2(isStartingPost ? 50 : 40),
                        Masking = true,
                        CornerRadius = 8,
                    },
                    createUserLink(group),
                }
            };

            if (group != null)
            {
                userCard.Add(new DiscussionGroupBadge(group)
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                });
            }

            InternalChildren = new Drawable[]
            {
                unreadMarker = createUnreadMarker(unread),
                new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 20, Vertical = 10 },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 110),
                        new Dimension(GridSizeMode.Absolute, 20),
                        new Dimension(),
                    },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new[]
                        {
                            userCard,
                            Empty(),
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 8),
                                Children = new[]
                                {
                                    createMessage(),
                                    createFooter(),
                                }
                            },
                        }
                    }
                },
            };

            if (post.DeletedAt != null)
                Alpha = 0.5f;
        }

        private Box createUnreadMarker(bool unread) => new Box
        {
            RelativeSizeAxes = Axes.Y,
            Width = 3,
            Colour = colourProvider.Highlight1,
            Alpha = unread ? 1 : 0,
        };

        protected override bool OnHover(HoverEvent e)
        {
            // reading a post is only a local change, so the discussions aren't shown again.
            if (state.View.ReadPostIds.Add(post.Id))
                unreadMarker.FadeOut(500, Easing.OutQuint);

            return base.OnHover(e);
        }

        private Drawable createUserLink(osu.Game.Online.API.Requests.Responses.APIUserGroup? group)
        {
            var flow = new LinkFlowContainer(s => s.Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                TextAnchor = Anchor.TopCentre,
                Colour = DiscussionDisplay.GroupColour(group, colourProvider, Color4.White),
            };

            DiscussionLinks.AddUser(flow, state, post.UserId);
            return flow;
        }

        private Drawable createSystemPost()
        {
            var flow = new LinkFlowContainer(s =>
            {
                s.Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold);
                s.Colour = colourProvider.Content2;
            })
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Horizontal = 20, Vertical = 8 },
            };

            bool resolved = post.Message?.Type == JTokenType.Object && post.Message[@"value"]?.Value<bool>() == true;

            flow.AddIcon(resolved ? DiscussionDisplay.ResolvedIcon : FontAwesome.Solid.Undo, s =>
            {
                s.Font = s.Font.With(size: 12);
                s.Colour = resolved ? DiscussionDisplay.GREEN : DiscussionDisplay.ORANGE_1;
            });
            flow.AddText(@"  ");

            DiscussionDisplay.AddPattern(flow, resolved ? SlopDiscussionStrings.SystemResolved : SlopDiscussionStrings.SystemReopened, new Dictionary<string, Action<LinkFlowContainer>>
            {
                [@"0"] = f => DiscussionLinks.AddUser(f, state, post.UserId),
            });

            flow.AddText(@" · ");
            flow.AddArbitraryDrawable(new DrawableDate(post.CreatedAt, 12, false) { Colour = colourProvider.Foreground1 });

            return flow;
        }

        private Drawable createMessage()
        {
            if (isStartingPost && discussion.MessageType == @"review")
                return new ReviewMessage(state, discussion, post.MessageText);

            return new DiscussionMarkdown(post.MessageText, state.BeatmapSet.Id, discussion.BeatmapId, 14);
        }

        private Drawable createFooter()
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(12, 4),
                Child = new DrawableDate(post.CreatedAt, 12, false)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Colour = colourProvider.Foreground1,
                },
            };

            if (post.DeletedAt is DateTimeOffset deletedAt)
                flow.Add(createByText(SlopDiscussionStrings.DeletedBy, post.DeletedById, deletedAt));
            else if (post.LastEditorId != null && post.UpdatedAt is DateTimeOffset updatedAt && updatedAt > post.CreatedAt)
                flow.Add(createByText(SlopDiscussionStrings.EditedBy, post.LastEditorId, updatedAt));

            if (isStartingPost && discussion.KudosuDenied)
            {
                flow.Add(new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = SlopDiscussionStrings.KudosuDenied,
                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                    Colour = DiscussionDisplay.RED_1,
                });
            }

            // discussions of a review link to the review.
            if (isStartingPost && state.GetDiscussion(discussion.ParentId) is BeatmapDiscussion parent && parent.MessageType == @"review")
            {
                flow.Add(new DiscussionTextButton(SlopDiscussionStrings.GoToParent, FontAwesome.Solid.Link, 12)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Action = () => showDiscussion(parent.Id),
                });
            }

            DiscussionTextButton permalink = null!;

            flow.Add(permalink = new DiscussionTextButton(SlopDiscussionStrings.Permalink, FontAwesome.Solid.Link, 12)
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            });

            permalink.Action = () =>
            {
                clipboard.SetText(DiscussionDisplay.DiscussionUrl(screen.WebsiteUrl, discussion, isStartingPost ? null : post));

                permalink.Text = SlopDiscussionStrings.PermalinkCopied;
                permalink.Delay(2000).Schedule(() => permalink.Text = SlopDiscussionStrings.Permalink);
            };

            bool isOwnPost = state.CurrentUserId != null && post.UserId == state.CurrentUserId;
            bool isModerator = screen.IsCurrentUserModerator;

            // like the website, own posts can be edited and deleted, and moderators can do so with every post.
            if (post.DeletedAt == null && (isModerator || (isOwnPost && !state.BeatmapSet.DiscussionLocked)))
            {
                // reviews are edited as a document, which only the website can do.
                if (!(isStartingPost && discussion.MessageType == @"review"))
                {
                    flow.Add(new DiscussionTextButton(SlopDiscussionStrings.PostEdit, null, 12)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Action = editPost,
                    });
                }

                // starting posts can only be deleted until the discussion got replies, unless being a moderator.
                if (!isStartingPost || isModerator || discussion.CurrentUserAttributes?.CanDestroy == true)
                {
                    flow.Add(new DiscussionTextButton(SlopDiscussionStrings.PostDelete, null, 12)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Action = () => screen.DeletePost(discussion, post),
                    });
                }
            }

            if (post.DeletedAt != null && isModerator)
            {
                flow.Add(new DiscussionTextButton(SlopDiscussionStrings.PostRestore, null, 12)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Action = () => screen.RestorePost(discussion, post),
                });
            }

            if (isStartingPost && discussion.CanGrantKudosu && discussion.CurrentUserAttributes?.CanModerateKudosu == true)
            {
                flow.Add(new DiscussionTextButton(discussion.KudosuDenied ? SlopDiscussionStrings.AllowKudosu : SlopDiscussionStrings.DenyKudosu, null, 12)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Action = () =>
                    {
                        if (discussion.KudosuDenied)
                            screen.AllowKudosu(discussion);
                        else
                            screen.DenyKudosu(discussion);
                    },
                });
            }

            return flow;
        }

        private Drawable createByText(TranslatableString pattern, int? userId, DateTimeOffset date)
        {
            var flow = new LinkFlowContainer(s =>
            {
                s.Font = OsuFont.Default.With(size: 12);
                s.Colour = colourProvider.Foreground1;
            })
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
            };

            DiscussionDisplay.AddPattern(flow, pattern, new Dictionary<string, Action<LinkFlowContainer>>
            {
                [@"0"] = f => DiscussionLinks.AddUser(f, state, userId, s => s.Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold)),
                [@"1"] = f => f.AddArbitraryDrawable(new DrawableDate(date, 12, false)),
            });

            return flow;
        }

        private void editPost()
        {
            screen.ShowPrompt(new DiscussionTextPrompt(SlopDiscussionStrings.EditTitle, default, SlopDiscussionStrings.ReplyPlaceholder, SlopDiscussionStrings.EditButton,
                text => screen.RunInPrompt(session => screen.EditPost(session, post, text)), post.MessageText, multiline: true));
        }

        private void showDiscussion(int discussionId)
        {
            state.View.ScrollToHighlighted = true;

            if (!state.ChangeToDiscussion(discussionId))
                state.View.ScrollToHighlighted = false;
        }

        /// <summary>
        /// The message of a review, made of paragraphs and discussions embedded in it, like a review on the website.
        /// </summary>
        private partial class ReviewMessage : FillFlowContainer
        {
            private readonly DiscussionsState state;
            private readonly BeatmapDiscussion review;
            private readonly string message;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ReviewMessage(DiscussionsState state, BeatmapDiscussion review, string message)
            {
                this.state = state;
                this.review = review;
                this.message = message;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 10);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                JArray blocks;

                try
                {
                    blocks = JArray.Parse(message);
                }
                catch
                {
                    Add(createError(SlopDiscussionStrings.ReviewParseError));
                    return;
                }

                foreach (var block in blocks.OfType<JObject>())
                {
                    switch (block[@"type"]?.Value<string>())
                    {
                        case @"paragraph":
                            Add(new DiscussionMarkdown(block[@"text"]?.Value<string>() ?? string.Empty, state.BeatmapSet.Id, review.BeatmapId, 14));
                            break;

                        case @"embed":
                            int? id = block[@"discussion_id"]?.Type == JTokenType.Integer ? block[@"discussion_id"]!.Value<int>() : null;
                            var embedded = state.GetDiscussion(id);

                            Add(embedded == null || embedded.DeletedAt != null ? createError(SlopDiscussionStrings.EmbedMissing) : new ReviewEmbed(state, embedded));
                            break;
                    }
                }
            }

            private Drawable createError(LocalisableString text) => new OsuSpriteText
            {
                Text = text,
                Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                Colour = colourProvider.Foreground1,
            };
        }

        /// <summary>
        /// A discussion embedded in a review, see <c>review-post-embed</c> on the website.
        /// </summary>
        private partial class ReviewEmbed : CompositeDrawable
        {
            private readonly DiscussionsState state;
            private readonly BeatmapDiscussion discussion;

            [Resolved]
            private DiscussionScreen screen { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ReviewEmbed(DiscussionsState state, BeatmapDiscussion discussion)
            {
                this.state = state;
                this.discussion = discussion;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var typeColour = DiscussionDisplay.TypeColour(discussion.MessageType, colourProvider);

                var header = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Child = new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(13),
                        Icon = discussion.CanBeResolved && discussion.Resolved ? DiscussionDisplay.ResolvedIcon : DiscussionDisplay.TypeIcon(discussion.MessageType),
                        Colour = discussion.CanBeResolved && discussion.Resolved ? DiscussionDisplay.GREEN : typeColour,
                    },
                };

                if (discussion.Timestamp is int timestamp)
                {
                    string text = DiscussionDisplay.FormatTimestamp(timestamp);

                    header.Add(new DiscussionTextButton(text, null, 12)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        TooltipText = SlopDiscussionStrings.ShowInEditor,
                        AccentColour = colourProvider.Highlight1,
                        Action = () => screen.ShowTimestamp(text, discussion.BeatmapId),
                    });
                }

                header.Add(new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = discussion.BeatmapId is int beatmapId && state.Beatmaps.GetValueOrDefault(beatmapId) is DiscussionBeatmap beatmap
                        ? beatmap.Version
                        : SlopDiscussionStrings.AllDifficulties,
                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                    Colour = colourProvider.Content2,
                });

                header.Add(new DiscussionTextButton(SlopDiscussionStrings.GoToChild, FontAwesome.Solid.Link, 12)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Action = () =>
                    {
                        state.View.ScrollToHighlighted = true;

                        if (!state.ChangeToDiscussion(discussion.Id))
                            state.View.ScrollToHighlighted = false;
                    },
                });

                Masking = true;
                CornerRadius = 6;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = typeColour,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 6),
                        Padding = new MarginPadding { Left = 15, Right = 12, Vertical = 10 },
                        Children = new Drawable[]
                        {
                            header,
                            new DiscussionMarkdown(discussion.StartingPost?.MessageText ?? string.Empty, state.BeatmapSet.Id, discussion.BeatmapId, 13),
                        }
                    },
                };
            }
        }
    }
}
