// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The history of the beatmap set, like the history page of the discussion on the website.
    /// </summary>
    public partial class DiscussionEventsList : DiscussionSection
    {
        private FillFlowContainer events = null!;
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
                    Spacing = new Vector2(0, 10),
                    Children = new Drawable[]
                    {
                        new DiscussionTitle(SlopDiscussionStrings.ModeEvents),
                        empty = new OsuSpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Margin = new MarginPadding { Vertical = 20 },
                            Text = SlopDiscussionStrings.EventsEmpty,
                            Font = OsuFont.Default.With(size: 16, weight: FontWeight.SemiBold),
                            Colour = ColourProvider.Content2,
                        },
                        events = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 2),
                        },
                    }
                }
            };
        }

        protected override void UpdateDisplay(DiscussionsState state)
        {
            bool visible = state.View.Page == DiscussionPage.Events;

            Alpha = visible ? 1 : 0;

            if (!visible)
            {
                events.Clear();
                return;
            }

            var sorted = state.BeatmapSet.Events.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).ToList();

            empty.Alpha = sorted.Count == 0 ? 1 : 0;
            events.ChildrenEnumerable = sorted.Select(e => new DrawableEvent(state, e));
        }

        /// <summary>
        /// An event of the beatmap set, see <c>beatmapset-event</c> on the website.
        /// </summary>
        private partial class DrawableEvent : CompositeDrawable
        {
            private readonly DiscussionsState state;
            private readonly BeatmapSetEvent beatmapSetEvent;

            [Resolved]
            private DiscussionScreen screen { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public DrawableEvent(DiscussionsState state, BeatmapSetEvent beatmapSetEvent)
            {
                this.state = state;
                this.beatmapSetEvent = beatmapSetEvent;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var (icon, colour) = iconOf(beatmapSetEvent.Type);

                var message = new LinkFlowContainer(s => s.Font = OsuFont.Default.With(size: 14))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                };

                addMessage(message);

                Masking = true;
                CornerRadius = 6;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = 15, Vertical = 8 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 130),
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new DrawableDate(beatmapSetEvent.CreatedAt, 13, false)
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Colour = colourProvider.Content2,
                                },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(16),
                                    Icon = icon,
                                    Colour = colour,
                                },
                                message,
                            }
                        }
                    },
                };
            }

            private void addMessage(LinkFlowContainer flow)
            {
                var comment = beatmapSetEvent.Comment as JObject;

                string type = beatmapSetEvent.Type;

                int? discussionId = beatmapSetEvent.CommentDiscussionId ?? beatmapSetEvent.DiscussionId;
                var discussion = state.GetDiscussion(discussionId);

                var parts = new Dictionary<string, Action<LinkFlowContainer>>
                {
                    [@"user"] = f => DiscussionLinks.AddUser(f, state, beatmapSetEvent.UserId, bold),
                };

                if (discussionId is int id)
                {
                    parts[@"discussion"] = f =>
                    {
                        if (discussion != null)
                            DiscussionLinks.AddDiscussion(f, state, id, bold);
                        else
                            f.AddLink($@"#{id}", () => screen.OpenOnWebsite($@"{screen.WebsiteUrl}/beatmapsets/beatmap-discussions/{id}"), creationParameters: bold);
                    };

                    parts[@"discussion_user"] = f => DiscussionLinks.AddUser(f, state, discussion?.UserId, bold);

                    // the text of discussion events is a preview of the discussion.
                    parts[@"text"] = f => f.AddText(discussionPreview(discussion), italic);
                }

                switch (type)
                {
                    case @"disqualify":
                        if (discussionId == null)
                            type = @"disqualify_legacy";

                        break;

                    case @"nominate":
                        var modes = comment?[@"modes"] as JArray;

                        if (modes?.Count > 0)
                        {
                            type = @"nominate_modes";

                            string names = string.Join(@", ", modes.Select(m => DiscussionDisplay.RulesetName(m.Value<string>() ?? string.Empty)));
                            parts[@"modes"] = f => f.AddText(names);
                        }

                        break;

                    case @"nsfw_toggle":
                        type = $@"nsfw_toggle.to_{(comment?[@"new"]?.Value<bool>() == true ? 1 : 0)}";
                        break;

                    case @"beatmap_owner_change":
                        parts[@"beatmap"] = f => f.AddText(comment?[@"beatmap_version"]?.Value<string>() ?? string.Empty, bold);
                        parts[@"new_user"] = f => addUserFromComment(f, comment, @"new_user");
                        break;

                    case @"nomination_reset_received":
                        parts[@"source_user"] = f => addUserFromComment(f, comment, @"source_user");
                        break;

                    case @"genre_edit":
                    case @"language_edit":
                    case @"offset_edit":
                    case @"tags_edit":
                        parts[@"old"] = f => f.AddText(comment?[@"old"]?.ToString() ?? string.Empty, bold);
                        parts[@"new"] = f => f.AddText(comment?[@"new"]?.ToString() ?? string.Empty, bold);
                        break;
                }

                // these events have a reason written by the user instead of a discussion.
                if (type == @"discussion_lock" || type == @"remove_from_loved" || type == @"disqualify_legacy" || discussionId == null)
                {
                    string reason = comment?[@"reason"]?.Value<string>() ?? string.Empty;
                    parts[@"text"] = f => f.AddText(reason, italic);
                }

                DiscussionDisplay.AddPattern(flow, SlopDiscussionStrings.Event(type), parts);
            }

            private string discussionPreview(BeatmapDiscussion? discussion)
            {
                if (discussion == null)
                    return SlopDiscussionStrings.EventDeleted.ToString();

                var post = discussion.StartingPost;

                if (post == null || string.IsNullOrEmpty(post.MessageText))
                    return SlopDiscussionStrings.EventNoPreview.ToString();

                return DiscussionDisplay.PlainTextPreview(post.MessageText, 100);
            }

            private void addUserFromComment(LinkFlowContainer flow, JObject? comment, string prefix)
            {
                int? userId = comment?[$@"{prefix}_id"]?.Type == JTokenType.Integer ? comment[$@"{prefix}_id"]!.Value<int>() : null;

                if (state.GetUser(userId) != null)
                {
                    DiscussionLinks.AddUser(flow, state, userId, bold);
                    return;
                }

                string? username = comment?[$@"{prefix}_username"]?.Value<string>();

                if (userId is int id && username != null)
                    flow.AddUserLink(new APIUser { Id = id, Username = username }, bold);
                else
                    flow.AddText(username ?? SlopDiscussionStrings.UnknownUser.ToString(), bold);
            }

            private static void bold(SpriteText s) => s.Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold);

            private static void italic(SpriteText s) => s.Font = OsuFont.Default.With(size: 14, italics: true);

            private (IconUsage, Color4) iconOf(string type)
            {
                switch (type)
                {
                    case @"approve":
                    case @"qualify":
                    case @"rank":
                        return (FontAwesome.Solid.Check, DiscussionDisplay.LIME_1);

                    case @"love":
                        return (FontAwesome.Solid.Heart, DiscussionDisplay.PINK_1);

                    case @"remove_from_loved":
                        return (FontAwesome.Solid.HeartBroken, DiscussionDisplay.PINK_1);

                    case @"nominate":
                        return (FontAwesome.Solid.ThumbsUp, DiscussionDisplay.BLUE_1);

                    case @"disqualify":
                    case @"nomination_reset":
                    case @"nomination_reset_received":
                        return (FontAwesome.Solid.ThumbsDown, DiscussionDisplay.RED_1);

                    case @"issue_resolve":
                        return (FontAwesome.Solid.Check, DiscussionDisplay.GREEN);

                    case @"issue_reopen":
                        return (FontAwesome.Solid.Undo, DiscussionDisplay.ORANGE_1);

                    case @"kudosu_allow":
                    case @"kudosu_deny":
                    case @"kudosu_gain":
                    case @"kudosu_lost":
                    case @"kudosu_recalculate":
                        return (FontAwesome.Solid.Star, DiscussionDisplay.PURPLE_1);

                    case @"discussion_lock":
                        return (FontAwesome.Solid.Lock, DiscussionDisplay.ORANGE_1);

                    case @"discussion_unlock":
                        return (FontAwesome.Solid.LockOpen, DiscussionDisplay.ORANGE_1);

                    case @"discussion_delete":
                    case @"discussion_post_delete":
                        return (FontAwesome.Solid.Trash, colourProvider.Content2);

                    case @"discussion_restore":
                    case @"discussion_post_restore":
                        return (FontAwesome.Solid.Undo, colourProvider.Content2);

                    case @"beatmap_owner_change":
                        return (FontAwesome.Solid.User, colourProvider.Content2);

                    case @"nsfw_toggle":
                        return (FontAwesome.Solid.ExclamationTriangle, DiscussionDisplay.ORANGE_1);

                    default:
                        return (FontAwesome.Solid.Pen, colourProvider.Content2);
                }
            }
        }
    }
}
