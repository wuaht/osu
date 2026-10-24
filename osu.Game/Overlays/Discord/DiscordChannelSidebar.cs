// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
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
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// The channels of the selected guild or the direct messages, with the local user at the bottom.
    /// </summary>
    public partial class DiscordChannelSidebar : CompositeDrawable
    {
        private const float user_panel_height = 52;

        [Resolved]
        private DiscordClient client { get; set; } = null!;

        [Resolved]
        private DiscordNavigation navigation { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Bindable<DiscordGuild?> guild = new Bindable<DiscordGuild?>();
        private readonly Bindable<DiscordChannel?> channel = new Bindable<DiscordChannel?>();
        private readonly BindableList<DiscordChannel> privateChannels = new BindableList<DiscordChannel>();

        private OsuSpriteText headerText = null!;
        private FillFlowContainer channelFlow = null!;

        private readonly HashSet<ulong> collapsedCategories = new HashSet<ulong>();

        public DiscordChannelSidebar()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Returns the channels of a guild in the order they are shown, without categories.
        /// </summary>
        public static IEnumerable<DiscordChannel> GetOrderedChannels(DiscordGuild guild) => getGroups(guild).SelectMany(g => g.channels);

        private static IEnumerable<(DiscordChannel? category, List<DiscordChannel> channels)> getGroups(DiscordGuild guild)
        {
            var shown = guild.Channels.Where(isShown).ToList();

            List<DiscordChannel> sortChannels(IEnumerable<DiscordChannel> channels) =>
                channels.OrderBy(c => isVoice(c) ? 1 : 0).ThenBy(c => c.Position).ThenBy(c => c.Id).ToList();

            var categoryIds = shown.Where(c => c.Type == DiscordChannelType.GuildCategory).Select(c => c.Id).ToHashSet();

            yield return (null, sortChannels(shown.Where(c => c.Type != DiscordChannelType.GuildCategory && (c.ParentId == null || !categoryIds.Contains(c.ParentId.Value)))));

            foreach (var category in shown.Where(c => c.Type == DiscordChannelType.GuildCategory).OrderBy(c => c.Position).ThenBy(c => c.Id))
                yield return (category, sortChannels(shown.Where(c => c.ParentId == category.Id && c.Type != DiscordChannelType.GuildCategory)));
        }

        // Threads are not shown in the channel list.
        private static bool isShown(DiscordChannel channel) =>
            channel.Type != DiscordChannelType.AnnouncementThread && channel.Type != DiscordChannelType.PublicThread && channel.Type != DiscordChannelType.PrivateThread;

        private static bool isVoice(DiscordChannel channel) => channel.Type == DiscordChannelType.GuildVoice || channel.Type == DiscordChannelType.GuildStageVoice;

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
                    new Dimension(GridSizeMode.Absolute, user_panel_height),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                headerText = new TruncatingSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Padding = new MarginPadding { Horizontal = 16 },
                                    Font = DiscordStyle.UiFont(16, FontWeight.Bold),
                                    Colour = colourProvider.Content1,
                                },
                                new Box
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 1,
                                    Colour = Color4.Black,
                                    Alpha = 0.4f,
                                },
                            },
                        },
                    },
                    new Drawable[]
                    {
                        new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ScrollbarOverlapsContent = false,
                            Child = channelFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 2),
                                Padding = new MarginPadding { Horizontal = 8, Vertical = 8 },
                            },
                        },
                    },
                    new Drawable[]
                    {
                        new UserPanel(),
                    },
                },
            };

            guild.BindTo(navigation.Guild);
            channel.BindTo(navigation.Channel);
            privateChannels.BindTo(client.PrivateChannels);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            guild.BindValueChanged(_ => Scheduler.AddOnce(updateChannels), true);
            channel.BindValueChanged(_ =>
            {
                if (guild.Value == null)
                    Scheduler.AddOnce(updateChannels);
            });
            privateChannels.BindCollectionChanged((_, _) =>
            {
                if (guild.Value == null)
                    Scheduler.AddOnce(updateChannels);
            });

            client.GuildChannelsChanged += onGuildChannelsChanged;
            client.MessageCreated += onMessageCreated;
        }

        private void onMessageCreated(DiscordMessage message)
        {
            // A conversation which received its first message is listed now, even if it didn't move in the list.
            if (message.GuildId == null && guild.Value == null)
                Scheduler.AddOnce(updateChannels);
        }

        private void onGuildChannelsChanged(DiscordGuild changed)
        {
            if (changed.Id == guild.Value?.Id)
                Scheduler.AddOnce(updateChannels);
        }

        private void updateChannels()
        {
            channelFlow.Clear();

            if (guild.Value == null)
            {
                headerText.Text = SlopDiscordStrings.DirectMessages;

                // The friends are shown in the friends view, so only the conversations with messages and the open one are listed here.
                channelFlow.Add(new FriendsRow());

                var conversations = privateChannels.Where(c => c.LastMessageId != null || c.Id == channel.Value?.Id).ToList();

                if (channel.Value?.IsPrivate == true && conversations.All(c => c.Id != channel.Value.Id))
                    conversations.Insert(0, channel.Value);

                if (conversations.Count > 0)
                {
                    channelFlow.Add(new CategoryHeader(SlopDiscordStrings.DirectMessages.ToUpper(), false, null));

                    foreach (var c in conversations)
                        channelFlow.Add(new PrivateChannelRow(c));
                }

                return;
            }

            headerText.Text = guild.Value.Name;

            foreach (var (category, channels) in getGroups(guild.Value))
            {
                bool collapsed = category != null && collapsedCategories.Contains(category.Id);

                if (category != null)
                {
                    // Like in Discord, empty categories are not shown.
                    if (channels.Count == 0)
                        continue;

                    channelFlow.Add(new CategoryHeader((category.Name ?? string.Empty).ToUpperInvariant(), collapsed, () =>
                    {
                        if (!collapsedCategories.Remove(category.Id))
                            collapsedCategories.Add(category.Id);

                        Scheduler.AddOnce(updateChannels);
                    }));
                }

                foreach (var c in channels)
                {
                    // A collapsed category still shows the selected channel.
                    if (collapsed && channel.Value?.Id != c.Id)
                        continue;

                    channelFlow.Add(new GuildChannelRow(c));
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
            {
                client.GuildChannelsChanged -= onGuildChannelsChanged;
                client.MessageCreated -= onMessageCreated;
            }
        }

        private partial class CategoryHeader : OsuClickableContainer
        {
            private readonly LocalisableString text;
            private readonly bool collapsed;
            private readonly bool collapsible;

            private OsuSpriteText spriteText = null!;
            private SpriteIcon? chevron;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public CategoryHeader(LocalisableString text, bool collapsed, System.Action? toggle)
            {
                this.text = text;
                this.collapsed = collapsed;

                collapsible = toggle != null;
                Action = toggle;

                RelativeSizeAxes = Axes.X;
                Height = 28;
                Margin = new MarginPadding { Top = 8 };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var flow = new FillFlowContainer
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Margin = new MarginPadding { Bottom = 4 },
                };

                if (collapsible)
                {
                    flow.Add(chevron = new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Icon = collapsed ? FontAwesome.Solid.ChevronRight : FontAwesome.Solid.ChevronDown,
                        Size = new Vector2(8),
                        Colour = colourProvider.Content2,
                    });
                }

                flow.Add(spriteText = new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = text,
                    Font = DiscordStyle.UiFont(12, FontWeight.Bold),
                    Colour = colourProvider.Content2,
                });

                Child = flow;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (collapsible)
                {
                    spriteText.FadeColour(colourProvider.Content1, 100);
                    chevron?.FadeColour(colourProvider.Content1, 100);
                }

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                spriteText.FadeColour(colourProvider.Content2, 100);
                chevron?.FadeColour(colourProvider.Content2, 100);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// A row of the channel list which can be selected.
        /// </summary>
        private abstract partial class SidebarRow : OsuClickableContainer
        {
            private Box background = null!;
            private Container content = null!;

            [Resolved]
            protected DiscordNavigation Navigation { get; private set; } = null!;

            [Resolved]
            protected OverlayColourProvider ColourProvider { get; private set; } = null!;

            private readonly Bindable<DiscordChannel?> selectedChannel = new Bindable<DiscordChannel?>();

            private bool selected => IsSelected(selectedChannel.Value);

            protected virtual bool Selectable => true;

            /// <summary>
            /// Whether the row is shown as bright as a selected one, for example because of unread messages.
            /// </summary>
            protected virtual bool Highlighted => false;

            protected SidebarRow(float height)
            {
                RelativeSizeAxes = Axes.X;
                Height = height;
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
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    content = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 8 },
                        Child = CreateContent(),
                    },
                };

                selectedChannel.BindTo(Navigation.Channel);

                if (Selectable)
                    Action = Select;
            }

            protected abstract bool IsSelected(DiscordChannel? selectedChannel);

            protected abstract void Select();

            protected abstract Drawable CreateContent();

            protected override void LoadComplete()
            {
                base.LoadComplete();

                selectedChannel.BindValueChanged(_ => UpdateState(), true);
                FinishTransforms(true);
            }

            protected override bool OnHover(HoverEvent e)
            {
                UpdateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                UpdateState();
                base.OnHoverLost(e);
            }

            protected void UpdateState()
            {
                if (!Selectable)
                {
                    content.Alpha = 0.5f;
                    return;
                }

                background.FadeTo(selected ? 0.12f : IsHovered ? 0.06f : 0, 100);
                content.FadeColour(selected || IsHovered || Highlighted ? Color4.White : ColourProvider.Content2, 100);
            }
        }

        private abstract partial class ChannelRow : SidebarRow
        {
            protected readonly DiscordChannel Channel;

            protected override bool Selectable => Channel.IsTextBased;

            protected ChannelRow(DiscordChannel channel, float height)
                : base(height)
            {
                Channel = channel;
            }

            protected override bool IsSelected(DiscordChannel? selectedChannel) => selectedChannel?.Id == Channel.Id;

            protected override void Select() => Navigation.Channel.Value = Channel;
        }

        /// <summary>
        /// Opens the friends view.
        /// </summary>
        private partial class FriendsRow : SidebarRow
        {
            public FriendsRow()
                : base(42)
            {
            }

            protected override bool IsSelected(DiscordChannel? selectedChannel) => selectedChannel == null;

            protected override void Select() => Navigation.Channel.Value = null;

            protected override Drawable CreateContent() => new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 42),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = FontAwesome.Solid.UserFriends,
                            Size = new Vector2(20),
                            Margin = new MarginPadding { Left = 6 },
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = SlopDiscordStrings.Friends,
                            Font = DiscordStyle.UiFont(15, FontWeight.Medium),
                        },
                    },
                },
            };
        }

        private partial class GuildChannelRow : ChannelRow
        {
            public GuildChannelRow(DiscordChannel channel)
                : base(channel, 32)
            {
            }

            protected override Drawable CreateContent() => new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Icon = GetIcon(Channel),
                        Size = new Vector2(14),
                        Colour = ColourProvider.Content2,
                    },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Width = 0.85f,
                        Text = Channel.DisplayName,
                        Font = DiscordStyle.UiFont(15, FontWeight.Medium),
                    },
                },
            };
        }

        private partial class PrivateChannelRow : ChannelRow
        {
            [Resolved]
            private DiscordUnreadMessages? unread { get; set; }

            private TruncatingSpriteText name = null!;
            private Container badge = null!;
            private OsuSpriteText badgeText = null!;

            private int unreadCount => unread?.Get(Channel.Id) ?? 0;

            protected override bool Highlighted => unreadCount > 0;

            public PrivateChannelRow(DiscordChannel channel)
                : base(channel, 42)
            {
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (unread != null)
                    unread.Changed += onUnreadChanged;

                updateUnread();
            }

            private void onUnreadChanged() => Scheduler.AddOnce(updateUnread);

            private void updateUnread()
            {
                int count = unreadCount;

                badge.Alpha = count > 0 ? 1 : 0;
                badgeText.Text = count > 99 ? @"99+" : count.ToString();
                name.Font = DiscordStyle.UiFont(15, count > 0 ? FontWeight.Bold : FontWeight.Medium);

                UpdateState();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (unread != null)
                    unread.Changed -= onUnreadChanged;
            }

            protected override Drawable CreateContent()
            {
                Drawable avatar;

                if (Channel.Type == DiscordChannelType.GroupDirectMessage && string.IsNullOrEmpty(Channel.Icon) && Channel.Recipients.Count != 1)
                {
                    avatar = new CircularContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(32),
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = DiscordStyle.Blurple,
                            },
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = FontAwesome.Solid.Users,
                                Size = new Vector2(14),
                            },
                        },
                    };
                }
                else
                {
                    string? url = !string.IsNullOrEmpty(Channel.Icon)
                        ? $@"https://cdn.discordapp.com/channel-icons/{Channel.Id}/{Channel.Icon}.png?size=64"
                        : Channel.Recipients.FirstOrDefault()?.GetAvatarUrl();

                    avatar = new DiscordAvatar(url, 32)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    };
                }

                return new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 42),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new[]
                        {
                            avatar,
                            name = new TruncatingSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                                Text = Channel.DisplayName,
                                Font = DiscordStyle.UiFont(15, FontWeight.Medium),
                            },
                            // The number of unread messages, like the mention badge of Discord.
                            badge = new CircularContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.X,
                                Height = 16,
                                Masking = true,
                                Alpha = 0,
                                Margin = new MarginPadding { Left = 6 },
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = DiscordStyle.Error,
                                    },
                                    badgeText = new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Font = DiscordStyle.UiFont(11, FontWeight.Bold),
                                        Colour = Color4.White,
                                        Margin = new MarginPadding { Horizontal = 5 },
                                    },
                                },
                            },
                        },
                    },
                };
            }
        }

        public static IconUsage GetIcon(DiscordChannel channel)
        {
            switch (channel.Type)
            {
                case DiscordChannelType.GuildAnnouncement:
                    return FontAwesome.Solid.Bullhorn;

                case DiscordChannelType.GuildVoice:
                case DiscordChannelType.GuildStageVoice:
                    return FontAwesome.Solid.VolumeUp;

                case DiscordChannelType.GuildForum:
                case DiscordChannelType.GuildMedia:
                    return FontAwesome.Solid.Comments;

                case DiscordChannelType.DirectMessage:
                    return FontAwesome.Solid.At;

                case DiscordChannelType.GroupDirectMessage:
                    return FontAwesome.Solid.Users;

                default:
                    return FontAwesome.Solid.Hashtag;
            }
        }

        /// <summary>
        /// Shows the local user and a button to log out.
        /// </summary>
        private partial class UserPanel : CompositeDrawable
        {
            [Resolved]
            private DiscordClient client { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private readonly Bindable<DiscordUser?> user = new Bindable<DiscordUser?>();

            private Container avatarContainer = null!;
            private OsuSpriteText displayName = null!;
            private OsuSpriteText username = null!;

            public UserPanel()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                        Alpha = 0.5f,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 8 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                avatarContainer = new Container
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Size = new Vector2(32),
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        displayName = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = DiscordStyle.UiFont(14, FontWeight.SemiBold),
                                            Colour = colourProvider.Content1,
                                        },
                                        username = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = DiscordStyle.UiFont(12),
                                            Colour = colourProvider.Content2,
                                        },
                                    },
                                },
                                new LogoutButton
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Action = client.Logout,
                                },
                            },
                        },
                    },
                };

                user.BindTo(client.LocalUser);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                user.BindValueChanged(u =>
                {
                    avatarContainer.Clear();

                    if (u.NewValue == null)
                    {
                        displayName.Text = string.Empty;
                        username.Text = string.Empty;
                        return;
                    }

                    avatarContainer.Add(new DiscordAvatar(u.NewValue.GetAvatarUrl(), 32));
                    displayName.Text = u.NewValue.DisplayName;
                    username.Text = u.NewValue.Username;
                }, true);
            }
        }

        private partial class LogoutButton : OsuClickableContainer
        {
            private SpriteIcon icon = null!;
            private Box background = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public LogoutButton()
            {
                Size = new Vector2(32);
                Masking = true;
                CornerRadius = 4;
                TooltipText = SlopDiscordStrings.Logout;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Icon = FontAwesome.Solid.SignOutAlt,
                        Size = new Vector2(16),
                        Colour = colourProvider.Content2,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeTo(0.08f, 100);
                icon.FadeColour(DiscordStyle.Error, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeTo(0, 100);
                icon.FadeColour(colourProvider.Content2, 100);
                base.OnHoverLost(e);
            }
        }
    }
}
