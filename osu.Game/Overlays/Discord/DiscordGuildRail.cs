// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
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
    /// The list of guilds on the left side, with a button for the direct messages at the top.
    /// </summary>
    public partial class DiscordGuildRail : CompositeDrawable
    {
        private const float icon_size = 48;

        [Resolved]
        private DiscordClient client { get; set; } = null!;

        [Resolved]
        private DiscordNavigation navigation { get; set; } = null!;

        [Resolved]
        private DiscordUnreadMessages? unread { get; set; }

        private readonly BindableList<DiscordGuild> guilds = new BindableList<DiscordGuild>();
        private readonly BindableList<DiscordChannel> privateChannels = new BindableList<DiscordChannel>();

        private FillFlowContainer guildFlow = null!;
        private FillFlowContainer unreadFlow = null!;
        private OsuScrollContainer scroll = null!;
        private NewMentionsPill topPill = null!;
        private NewMentionsPill bottomPill = null!;

        public DiscordGuildRail()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                scroll = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ScrollbarVisible = false,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Padding = new MarginPadding { Vertical = 12 },
                        Children = new Drawable[]
                        {
                            new GuildButton(null, SlopDiscordStrings.DirectMessages),
                            // Like in Discord, the conversations with unread messages are shown below the direct messages, the newest first.
                            unreadFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 8),
                            },
                            new Box
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Size = new Vector2(32, 2),
                                Colour = colourProvider.Background3,
                            },
                            guildFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 8),
                            },
                        },
                    },
                },
                // Like in Discord, guilds with mentions which are scrolled out of view are pointed at by a pill, which jumps to them.
                topPill = new NewMentionsPill
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Margin = new MarginPadding { Top = 8 },
                    Action = () => jumpTo(topPill.Target),
                },
                bottomPill = new NewMentionsPill
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Margin = new MarginPadding { Bottom = 8 },
                    Action = () => jumpTo(bottomPill.Target),
                },
            };

            guilds.BindTo(client.Guilds);
            privateChannels.BindTo(client.PrivateChannels);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            guilds.BindCollectionChanged((_, _) => Scheduler.AddOnce(updateGuilds), true);
            privateChannels.BindCollectionChanged((_, _) => Scheduler.AddOnce(updateUnread), true);

            if (unread != null)
                unread.Changed += onUnreadChanged;
        }

        private void onUnreadChanged() => Scheduler.AddOnce(updateUnread);

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            GuildButton? above = null;
            GuildButton? below = null;

            foreach (var button in guildFlow.OfType<GuildButton>())
            {
                if (!button.HasMentions)
                    continue;

                double top = scroll.GetChildPosInContent(button);

                if (top < scroll.Current)
                    above = button;
                else if (top + button.DrawHeight > scroll.Current + scroll.DrawHeight && below == null)
                    below = button;
            }

            topPill.Target = above;
            bottomPill.Target = below;
        }

        private void jumpTo(GuildButton? button)
        {
            if (button == null)
                return;

            scroll.ScrollIntoView(button);
            navigation.Guild.Value = button.Guild;
        }

        /// <summary>
        /// The red "NEW" pill of Discord, which jumps to a guild with mentions that isn't in view.
        /// </summary>
        private partial class NewMentionsPill : OsuClickableContainer
        {
            private GuildButton? target;

            public GuildButton? Target
            {
                get => target;
                set
                {
                    if (target == value)
                        return;

                    target = value;
                    this.FadeTo(value != null ? 1 : 0, 150, Easing.OutQuint);
                }
            }

            public NewMentionsPill()
            {
                AutoSizeAxes = Axes.Both;
                Alpha = 0;
                Masking = true;
                CornerRadius = 8;

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = DiscordStyle.Error,
                    },
                    new OsuSpriteText
                    {
                        Text = SlopDiscordStrings.NewMentions,
                        Font = DiscordStyle.UiFont(11, FontWeight.Bold),
                        Colour = Color4.White,
                        Margin = new MarginPadding { Horizontal = 8, Vertical = 2 },
                    },
                };
            }

            public override bool PropagatePositionalInputSubTree => target != null && base.PropagatePositionalInputSubTree;
        }

        /// <summary>
        /// The red count of unread messages or mentions in the corner of an icon, which is cut out of the icon by a ring in the colour of the rail like in Discord.
        /// </summary>
        private partial class MentionBadge : CircularContainer
        {
            private const float size = 16;

            private readonly CircularContainer fill;
            private readonly OsuSpriteText text;

            public int Count
            {
                set
                {
                    Alpha = value > 0 ? 1 : 0;
                    text.Text = value > 99 ? @"99+" : value.ToString();

                    // Single digits stay a circle, longer counts become a pill.
                    fill.Width = value < 10 ? size : value < 100 ? size + 6 : size + 13;
                }
            }

            public MentionBadge(Color4 ringColour)
            {
                Anchor = Anchor.BottomRight;
                Origin = Anchor.BottomRight;
                AutoSizeAxes = Axes.Both;
                Margin = new MarginPadding { Right = -4, Bottom = -4 };
                Masking = true;
                Alpha = 0;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ringColour,
                    },
                    fill = new CircularContainer
                    {
                        Size = new Vector2(size),
                        Margin = new MarginPadding(3),
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = DiscordStyle.Error,
                            },
                            text = new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Font = DiscordStyle.UiFont(11, FontWeight.Bold),
                                Colour = Color4.White,
                            },
                        },
                    },
                };
            }
        }

        private void updateUnread()
        {
            unreadFlow.Clear();

            if (unread == null)
                return;

            // The private channels are ordered by their latest message.
            foreach (var channel in privateChannels.Where(c => unread.Get(c.Id) > 0))
                unreadFlow.Add(new UnreadChannelButton(channel, unread.Get(channel.Id)));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (unread != null)
                unread.Changed -= onUnreadChanged;
        }

        private partial class UnreadChannelButton : OsuClickableContainer
        {
            private readonly DiscordChannel channel;
            private readonly int count;

            [Resolved]
            private DiscordClient client { get; set; } = null!;

            [Resolved]
            private DiscordNavigation navigation { get; set; } = null!;

            private Container iconContainer = null!;
            private Circle pill = null!;

            public UnreadChannelButton(DiscordChannel channel, int count)
            {
                this.channel = channel;
                this.count = count;

                RelativeSizeAxes = Axes.X;
                Height = icon_size;
                TooltipText = channel.DisplayName;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                string? url = !string.IsNullOrEmpty(channel.Icon)
                    ? $@"https://cdn.discordapp.com/channel-icons/{channel.Id}/{channel.Icon}.png?size=96"
                    : channel.Recipients.FirstOrDefault()?.GetAvatarUrl(96);

                Children = new Drawable[]
                {
                    pill = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        Size = new Vector2(8),
                        Colour = colourProvider.Content1,
                    },
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(icon_size),
                        Children = new Drawable[]
                        {
                            iconContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                CornerRadius = icon_size / 2,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = colourProvider.Background3,
                                    },
                                    new DiscordRemoteImage(url)
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        ShowPlaceholder = false,
                                    },
                                },
                            },
                            // The number of unread messages, which is cut out of the icon like in Discord.
                            new MentionBadge(colourProvider.Background6) { Count = count },
                        },
                    },
                };

                Action = () => navigation.OpenChannel(channel, client);
            }

            protected override bool OnHover(HoverEvent e)
            {
                iconContainer.TransformTo(nameof(CornerRadius), 16f, 200, Easing.OutQuint);
                pill.ResizeHeightTo(20, 200, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                iconContainer.TransformTo(nameof(CornerRadius), icon_size / 2, 200, Easing.OutQuint);
                pill.ResizeHeightTo(8, 200, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }

        private void updateGuilds()
        {
            guildFlow.Clear();

            foreach (var guild in guilds)
                guildFlow.Add(new GuildButton(guild, guild.Name));
        }

        private partial class GuildButton : OsuClickableContainer
        {
            private readonly DiscordGuild? guild;

            private Container iconContainer = null!;
            private Box iconBackground = null!;
            private Circle pill = null!;

            [Resolved]
            private DiscordNavigation navigation { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [Resolved]
            private DiscordUnreadMessages? unread { get; set; }

            private MentionBadge badge = null!;

            public DiscordGuild? Guild => guild;

            /// <summary>
            /// Whether the guild has unread mentions of the local user.
            /// </summary>
            public bool HasMentions => guild != null && unread?.GetGuild(guild.Id) > 0;

            private readonly Bindable<DiscordGuild?> selectedGuild = new Bindable<DiscordGuild?>();

            private bool selected => selectedGuild.Value?.Id == guild?.Id;

            public GuildButton(DiscordGuild? guild, LocalisableString name)
            {
                this.guild = guild;

                RelativeSizeAxes = Axes.X;
                Height = icon_size;
                TooltipText = name;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Drawable icon;

                if (guild == null)
                {
                    icon = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(0.6f),
                        FillMode = FillMode.Fit,
                        Texture = textures.Get(@"Slop/discord"),
                    };
                }
                else if (guild.GetIconUrl() is string url)
                {
                    icon = new DiscordRemoteImage(url)
                    {
                        RelativeSizeAxes = Axes.Both,
                        ShowPlaceholder = false,
                    };
                }
                else
                {
                    icon = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = guild.Acronym,
                        Font = DiscordStyle.UiFont(guild.Acronym.Length > 2 ? 14 : 17, FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                    };
                }

                Children = new Drawable[]
                {
                    pill = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        Size = new Vector2(8, 0),
                        Colour = colourProvider.Content1,
                    },
                    iconContainer = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(icon_size),
                        Masking = true,
                        CornerRadius = icon_size / 2,
                        Children = new[]
                        {
                            iconBackground = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background3,
                            },
                            icon,
                        },
                    },
                    // The number of unread mentions in the guild, which is cut out of the icon like in Discord.
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(icon_size),
                        Child = badge = new MentionBadge(colourProvider.Background6),
                    },
                };

                selectedGuild.BindTo(navigation.Guild);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Action = () => navigation.Guild.Value = guild;

                selectedGuild.BindValueChanged(_ => updateState(), true);
                FinishTransforms(true);

                if (unread != null && guild != null)
                    unread.Changed += onUnreadChanged;

                updateMentions();
            }

            private void onUnreadChanged() => Scheduler.AddOnce(updateMentions);

            private void updateMentions()
            {
                int count = guild != null ? unread?.GetGuild(guild.Id) ?? 0 : 0;

                badge.Count = count;

                updateState();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (unread != null)
                    unread.Changed -= onUnreadChanged;
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateState();
                base.OnHoverLost(e);
            }

            private void updateState()
            {
                bool active = selected || IsHovered;

                iconContainer.TransformTo(nameof(CornerRadius), active ? 16f : icon_size / 2, 200, Easing.OutQuint);
                iconBackground.FadeColour(active ? DiscordStyle.Blurple : colourProvider.Background3, 200, Easing.OutQuint);

                bool mentioned = guild != null && unread?.GetGuild(guild.Id) > 0;
                float pillHeight = selected ? 40 : IsHovered ? 20 : mentioned ? 8 : 0;
                pill.ResizeHeightTo(pillHeight, 200, Easing.OutQuint);
            }
        }
    }
}
