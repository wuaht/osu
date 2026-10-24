// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Shows the posts of a forum as cards, like in Discord. Clicking a post opens it as a thread.
    /// </summary>
    public partial class DiscordForumView : CompositeDrawable
    {
        public readonly DiscordChannel Forum;

        [Resolved]
        private DiscordClient client { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private FillFlowContainer<PostCard> postFlow = null!;
        private Container noteContainer = null!;
        private LoadingSpinner spinner = null!;

        /// <summary>
        /// The posts found by searching the forum, which include archived ones.
        /// </summary>
        private List<DiscordChannel> searchedPosts = new List<DiscordChannel>();

        private Dictionary<ulong, DiscordMessage> firstMessages = new Dictionary<ulong, DiscordMessage>();

        private bool loading = true;
        private string? loadError;

        public DiscordForumView(DiscordChannel forum)
        {
            Forum = forum;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, DiscordStyle.HEADER_HEIGHT),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[] { new DiscordChannelHeader(Forum) },
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ScrollbarOverlapsContent = false,
                                    Child = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 8),
                                        Padding = new MarginPadding(16),
                                        Children = new Drawable[]
                                        {
                                            noteContainer = new Container
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                            },
                                            postFlow = new FillFlowContainer<PostCard>
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 8),
                                            },
                                        },
                                    },
                                },
                                spinner = new LoadingSpinner
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(32),
                                },
                            },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            client.GuildChannelsChanged += onGuildChannelsChanged;

            // The known posts are shown right away, the search adds the archived ones.
            updatePosts();
            spinner.Show();

            client.SearchThreads(Forum).ContinueWith(t => Schedule(() =>
            {
                loading = false;
                spinner.Hide();

                if (t.IsFaulted)
                    loadError = t.Exception?.GetBaseException().Message ?? string.Empty;
                else
                {
                    var result = t.GetResultSafely();
                    searchedPosts = result.Threads;
                    firstMessages = result.FirstMessages;
                }

                updatePosts();
            }), TaskScheduler.Default);
        }

        private void onGuildChannelsChanged(DiscordGuild guild) => Schedule(() =>
        {
            if (guild.Id == Forum.GuildId)
                updatePosts();
        });

        private void updatePosts()
        {
            var known = Forum.GuildId is ulong guildId && client.GetGuild(guildId) is DiscordGuild guild
                ? guild.Threads.Where(t => t.ParentId == Forum.Id)
                : Enumerable.Empty<DiscordChannel>();

            // The posts known to the guild are more recent than the search result.
            var posts = known.Concat(searchedPosts)
                             .DistinctBy(t => t.Id)
                             .OrderByDescending(t => t.LastMessageId ?? t.Id)
                             .ToList();

            postFlow.Clear();

            foreach (var post in posts)
                postFlow.Add(new PostCard(post, Forum, firstMessages.GetValueOrDefault(post.Id)));

            if (loadError != null)
                noteContainer.Child = createNote(SlopDiscordStrings.ForumLoadFailed(loadError), FontAwesome.Solid.ExclamationTriangle, DiscordStyle.Error);
            else if (!loading && posts.Count == 0)
                noteContainer.Child = createNote(SlopDiscordStrings.ForumEmpty, FontAwesome.Solid.InfoCircle, colourProvider.Content2);
            else
                noteContainer.Clear();
        }

        private static Drawable createNote(LocalisableString text, IconUsage icon, Color4 colour) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(8, 0),
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Icon = icon,
                    Size = new Vector2(14),
                    Colour = colour,
                    Margin = new MarginPadding { Top = 2 },
                },
                new OsuTextFlowContainer(s =>
                {
                    s.Font = DiscordStyle.UiFont(14);
                    s.Colour = colour;
                })
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Width = 0.9f,
                    Text = text,
                },
            },
        };

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
                client.GuildChannelsChanged -= onGuildChannelsChanged;
        }

        private partial class PostCard : OsuClickableContainer
        {
            private readonly DiscordChannel post;
            private readonly DiscordChannel forum;
            private readonly DiscordMessage? firstMessage;

            [Resolved]
            private DiscordClient client { get; set; } = null!;

            [Resolved]
            private DiscordNavigation navigation { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private Box hoverBackground = null!;

            public PostCard(DiscordChannel post, DiscordChannel forum, DiscordMessage? firstMessage)
            {
                this.post = post;
                this.forum = forum;
                this.firstMessage = firstMessage;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Padding = new MarginPadding(12),
                };

                var tags = post.AppliedTags.Select(id => forum.AvailableTags.FirstOrDefault(t => t.Id == id)).OfType<DiscordForumTag>().ToList();

                if (tags.Count > 0)
                {
                    content.Add(new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(4),
                        ChildrenEnumerable = tags.Select(createTag),
                    });
                }

                content.Add(new TruncatingSpriteText
                {
                    RelativeSizeAxes = Axes.X,
                    Text = post.DisplayName,
                    Font = DiscordStyle.UiFont(16, FontWeight.Bold),
                    Colour = colourProvider.Content1,
                });

                var author = firstMessage?.Author ?? (post.OwnerId is ulong ownerId ? client.GetUser(ownerId) : null);
                string? preview = firstMessage?.Content.ReplaceLineEndings(@" ").Trim();

                if (author != null || !string.IsNullOrEmpty(preview))
                {
                    content.Add(new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = author == null ? string.Empty : string.IsNullOrEmpty(preview) ? author.DisplayName : $@"{author.DisplayName}: ",
                                    Font = DiscordStyle.UiFont(14, FontWeight.SemiBold),
                                    Colour = colourProvider.Content1,
                                },
                                new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Text = preview ?? string.Empty,
                                    Font = DiscordStyle.UiFont(14),
                                    Colour = colourProvider.Content2,
                                },
                            },
                        },
                    });
                }

                content.Add(createFooter());

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                        Alpha = 0.8f,
                    },
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    content,
                };

                Action = () => navigation.Channel.Value = post;
            }

            private Drawable createFooter()
            {
                var footer = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Colour = colourProvider.Content2,
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Regular.Comment,
                            Size = new Vector2(12),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = (post.MessageCount ?? 0).ToString(),
                            Font = DiscordStyle.UiFont(13, FontWeight.SemiBold),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = @"·",
                            Font = DiscordStyle.UiFont(13),
                        },
                    },
                };

                if (post.IsArchived)
                {
                    footer.Add(new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = SlopDiscordStrings.ArchivedThread,
                        Font = DiscordStyle.UiFont(13),
                    });
                }
                else
                {
                    footer.Add(new DrawableDate(getSnowflakeTime(post.LastMessageId ?? post.Id))
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = DiscordStyle.UiFont(13),
                    });
                }

                return footer;
            }

            private Drawable createTag(DiscordForumTag tag) => new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 4,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                    },
                    new OsuSpriteText
                    {
                        Text = tag.Name,
                        Font = DiscordStyle.UiFont(12, FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                        Padding = new MarginPadding { Horizontal = 6, Vertical = 2 },
                    },
                },
            };

            /// <summary>
            /// Discord IDs contain the time they were created at.
            /// </summary>
            private static DateTimeOffset getSnowflakeTime(ulong id) => DateTimeOffset.FromUnixTimeMilliseconds((long)(id >> 22) + 1420070400000);

            protected override bool OnHover(HoverEvent e)
            {
                hoverBackground.FadeTo(0.04f, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBackground.FadeTo(0, 100);
                base.OnHoverLost(e);
            }
        }
    }
}
