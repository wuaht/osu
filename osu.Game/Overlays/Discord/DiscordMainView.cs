// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Game.Online.Discord;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// The selected guild and channel of the <see cref="DiscordMainView"/>.
    /// </summary>
    public class DiscordNavigation
    {
        /// <summary>
        /// The selected guild, or null if the direct messages are shown.
        /// </summary>
        public readonly Bindable<DiscordGuild?> Guild = new Bindable<DiscordGuild?>();

        public readonly Bindable<DiscordChannel?> Channel = new Bindable<DiscordChannel?>();

        private readonly Dictionary<ulong, ulong> lastChannels = new Dictionary<ulong, ulong>();

        public void OpenChannel(DiscordChannel channel, DiscordClient client)
        {
            if (channel.GuildId is ulong guildId)
            {
                var guild = client.GetGuild(guildId);

                if (guild != null && Guild.Value != guild)
                    Guild.Value = guild;
            }
            else if (Guild.Value != null)
                Guild.Value = null;

            Channel.Value = channel;
        }

        public void RememberChannel(DiscordChannel channel) => lastChannels[channel.GuildId ?? 0] = channel.Id;

        public ulong? GetLastChannel(DiscordGuild? guild) => lastChannels.TryGetValue(guild?.Id ?? 0, out ulong id) ? id : null;
    }

    /// <summary>
    /// Which side panels of the <see cref="DiscordMainView"/> are shown. Kept by the overlay so that it survives reconnecting.
    /// </summary>
    public class DiscordLayout
    {
        public readonly BindableBool ShowGuilds = new BindableBool(true);

        public readonly BindableBool ShowChannels = new BindableBool(true);

        /// <summary>
        /// Whether the shown client has guilds, so that they can be hidden.
        /// </summary>
        public readonly BindableBool HasGuilds = new BindableBool();
    }

    /// <summary>
    /// Shows the guilds, the channels and the chat of a logged in <see cref="DiscordClient"/>.
    /// </summary>
    public partial class DiscordMainView : CompositeDrawable
    {
        public readonly DiscordClient Client;

        [Cached]
        private readonly DiscordNavigation navigation = new DiscordNavigation();

        public DiscordNavigation Navigation => navigation;

        private DiscordChatView chatView = null!;
        private DiscordFriendsView friendsView = null!;

        private DiscordTextureStore? ownTextures;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private DiscordLayout? layout { get; set; }

        private CollapsiblePanel? guildPanel;
        private CollapsiblePanel channelPanel = null!;

        public DiscordMainView(DiscordClient client)
        {
            Client = client;
            RelativeSizeAxes = Axes.Both;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs(Client);

            // The overlay provides the texture store, but the view may also be shown on its own (e.g. in tests).
            if (parent.Get<DiscordTextureStore>() == null)
                dependencies.Cache(ownTextures = new DiscordTextureStore(parent.Get<GameHost>()));

            return dependencies;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            ownTextures?.Dispose();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var columns = new List<Dimension>();
            var content = new List<Drawable>();

            if (Client.SupportsGuilds)
            {
                columns.Add(new Dimension(GridSizeMode.AutoSize));
                content.Add(guildPanel = new CollapsiblePanel(DiscordStyle.GUILD_RAIL_WIDTH)
                {
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background6,
                            Alpha = 0.6f,
                        },
                        new DiscordGuildRail(),
                    },
                });
            }

            columns.Add(new Dimension(GridSizeMode.AutoSize));
            content.Add(channelPanel = new CollapsiblePanel(DiscordStyle.SIDEBAR_WIDTH)
            {
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                        Alpha = 0.5f,
                    },
                    new DiscordChannelSidebar(),
                },
            });

            columns.Add(new Dimension());
            content.Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    chatView = new DiscordChatView(),
                    friendsView = new DiscordFriendsView { Alpha = 0 },
                },
            });

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = columns.ToArray(),
                Content = new[] { content.ToArray() },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (layout != null)
            {
                layout.HasGuilds.Value = Client.SupportsGuilds;
                guildPanel?.Expanded.BindTo(layout.ShowGuilds);
                channelPanel.Expanded.BindTo(layout.ShowChannels);
            }

            navigation.Guild.BindValueChanged(g =>
            {
                if (g.NewValue != null)
                    Client.SelectGuild(g.NewValue);

                // Open the channel which was open the last time the guild was selected, or the first channel of the guild.
                // The direct messages start with the friends view.
                DiscordChannel? channel = null;

                if (g.NewValue != null)
                {
                    ulong? last = navigation.GetLastChannel(g.NewValue);
                    channel = g.NewValue.Channels.FirstOrDefault(c => c.Id == last) ?? DiscordChannelSidebar.GetOrderedChannels(g.NewValue).FirstOrDefault(c => c.IsTextBased);
                }

                if (navigation.Channel.Value?.GuildId != g.NewValue?.Id || navigation.Channel.Value == null || navigation.Channel.Value.IsPrivate != (g.NewValue == null))
                    navigation.Channel.Value = channel;

                updateFriendsView();
            });

            navigation.Channel.BindValueChanged(c =>
            {
                updateFriendsView();

                if (c.NewValue == null)
                    return;

                navigation.RememberChannel(c.NewValue);
                Client.SelectChannel(c.NewValue);
            });

            // Start with the first guild if guilds are available, as they are what can't be seen in the official client.
            if (Client.SupportsGuilds && Client.Guilds.Count > 0)
                navigation.Guild.Value = Client.Guilds[0];
            else
                navigation.Guild.TriggerChange();
        }

        private void updateFriendsView()
        {
            bool showFriends = navigation.Guild.Value == null && navigation.Channel.Value == null;

            friendsView.Alpha = showFriends ? 1 : 0;
            chatView.Alpha = showFriends ? 0 : 1;
        }

        public void TakeTextBoxFocus()
        {
            if (friendsView.IsPresent)
                friendsView.TakeSearchFocus();
            else
                chatView.TakeTextBoxFocus();
        }

        public void KillTextBoxFocus() => chatView.KillTextBoxFocus();

        /// <summary>
        /// A side panel which slides out to the left when collapsed.
        /// </summary>
        private partial class CollapsiblePanel : Container
        {
            public readonly BindableBool Expanded = new BindableBool(true);

            private readonly float expandedWidth;

            private readonly Container content;

            protected override Container<Drawable> Content => content;

            public CollapsiblePanel(float width)
            {
                expandedWidth = width;

                RelativeSizeAxes = Axes.Y;
                Width = width;
                Masking = true;

                InternalChild = content = new Container
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = width,
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Expanded.BindValueChanged(e =>
                {
                    this.ResizeWidthTo(e.NewValue ? expandedWidth : 0, 300, Easing.OutQuint);
                    content.FadeTo(e.NewValue ? 1 : 0, 300, Easing.OutQuint);
                }, true);
                FinishTransforms(true);
            }
        }
    }
}
