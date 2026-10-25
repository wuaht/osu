// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// What can be done with the messages of a chat, which is provided by the chat to its messages.
    /// </summary>
    public interface IDiscordMessageActions
    {
        void Reply(DiscordMessage message);

        void StartEdit(DiscordMessage message);

        void Delete(DiscordMessage message);

        /// <summary>
        /// Adds the reaction of the local user, or removes it if it was added already.
        /// </summary>
        void ToggleReaction(DiscordMessage message, DiscordReactionEmoji emoji);

        /// <summary>
        /// The custom emojis which can be picked, see <see cref="DiscordEmojiPicker.GetCustomGroups"/>.
        /// </summary>
        IReadOnlyList<DiscordCustomEmojiGroup> GetCustomEmojis();
    }

    /// <summary>
    /// The reaction of a message, which toggles the reaction of the local user when clicked.
    /// Super reactions are coloured with their burst colour, but not animated.
    /// </summary>
    public partial class DiscordReactionChip : OsuClickableContainer
    {
        private readonly DiscordReaction reaction;
        private readonly bool burst;
        private readonly Func<Task<IReadOnlyList<DiscordUser>>>? fetchUsers;
        private readonly string emojiName;

        private Color4 accent;
        private Color4 idleBorder;
        private Color4 hoverBorder;

        private bool fetchedUsers;

        /// <param name="fetchUsers">Fetches the first users who reacted, which are shown in the tooltip once the chip is hovered.</param>
        public DiscordReactionChip(DiscordReaction reaction, bool burst, Action? toggle, Func<Task<IReadOnlyList<DiscordUser>>>? fetchUsers = null)
        {
            this.reaction = reaction;
            this.burst = burst;
            this.fetchUsers = fetchUsers;

            Action = toggle;
            Enabled.Value = toggle != null;

            Height = 24;
            AutoSizeAxes = Axes.X;
            Masking = true;
            CornerRadius = 8;
            BorderThickness = 2;

            emojiName = reaction.Emoji.Id == null ? reaction.Emoji.Name ?? string.Empty : $@":{reaction.Emoji.Name}:";
            TooltipText = withBurst(emojiName);
        }

        private int count => burst ? reaction.BurstCount : reaction.Count;

        private bool me => burst ? reaction.MeBurst : reaction.Me;

        private LocalisableString withBurst(LocalisableString text) => burst ? LocalisableString.Interpolate($@"{text} ({SlopDiscordStrings.SuperReaction})") : text;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            accent = burst ? parseColour(reaction.BurstColours.FirstOrDefault()) ?? DiscordStyle.Blurple : DiscordStyle.Blurple;

            // Like in Discord, chips of the local user are tinted with an outline, while the others only get an outline when hovered.
            idleBorder = me ? accent : burst ? accent.Opacity(0.4f) : colourProvider.Background4;
            hoverBorder = me ? accent : burst ? accent.Opacity(0.8f) : colourProvider.Light4.Opacity(0.5f);
            BorderColour = idleBorder;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = me ? accent.Opacity(0.18f) : burst ? accent.Opacity(0.1f) : colourProvider.Background4,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Padding = new MarginPadding { Horizontal = 8 },
                    Children = new Drawable[]
                    {
                        new DrawableDiscordEmoji(DiscordEmoji.GetUrl(reaction.Emoji) ?? string.Empty, reaction.Emoji.Name ?? string.Empty, string.Empty, 16, reaction.Emoji.Animated)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Margin = new MarginPadding(),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = count.ToString(),
                            Font = DiscordStyle.UiFont(14, FontWeight.SemiBold),
                            Colour = me || burst ? Interpolation.ValueAt(0.7f, accent, Color4.White, 0, 1) : colourProvider.Content2,
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            this.TransformTo(nameof(BorderColour), (ColourInfo)hoverBorder, 100);

            if (!fetchedUsers && fetchUsers != null)
            {
                fetchedUsers = true;
                fetchUsers().ContinueWith(t => Schedule(() =>
                {
                    if (t.IsCompletedSuccessfully)
                        showUsers(t.GetResultSafely());
                    else
                        fetchedUsers = false;
                }), TaskScheduler.Default);
            }

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.TransformTo(nameof(BorderColour), (ColourInfo)idleBorder, 100);
            base.OnHoverLost(e);
        }

        private void showUsers(IReadOnlyList<DiscordUser> users)
        {
            if (users.Count == 0)
                return;

            const int shown_users = 3;

            string names = string.Join(@", ", users.Take(shown_users).Select(u => u.GlobalName ?? u.Username));
            int others = count - Math.Min(users.Count, shown_users);

            LocalisableString who = others > 0 ? SlopDiscordStrings.ReactionUsersAndOthers(names, others) : names;
            TooltipText = withBurst(SlopDiscordStrings.ReactedWith(who, emojiName));
        }

        private static Color4? parseColour(string? hex)
        {
            if (string.IsNullOrEmpty(hex))
                return null;

            try
            {
                return Color4Extensions.FromHex(hex.TrimStart('#'));
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// A small icon button of the message toolbar.
    /// </summary>
    public partial class DiscordIconButton : OsuClickableContainer
    {
        private readonly IconUsage icon;
        private readonly Drawable? content;

        private Box hoverBackground = null!;
        private SpriteIcon? sprite;
        private Color4 idleColour;

        public Color4? HoverColour { get; init; }

        public DiscordIconButton(IconUsage icon, LocalisableString tooltip, Action? action = null)
        {
            this.icon = icon;
            Init(tooltip, action);
        }

        /// <summary>
        /// A button which shows a drawable instead of an icon.
        /// </summary>
        public DiscordIconButton(Drawable content, LocalisableString tooltip, Action? action = null)
        {
            this.content = content;
            Init(tooltip, action);
        }

        protected void Init(LocalisableString tooltip, Action? action)
        {
            TooltipText = tooltip;
            Action = action;
            Size = new Vector2(32);
            Masking = true;
            CornerRadius = 6;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            idleColour = colourProvider.Content2;

            Children = new[]
            {
                hoverBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0,
                },
                content ?? (sprite = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Icon = icon,
                    Size = new Vector2(16),
                    Colour = colourProvider.Content2,
                }),
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverBackground.FadeTo(0.08f, 50);
            sprite?.FadeColour(HoverColour ?? Color4.White, 50);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverBackground.FadeOut(100);
            sprite?.FadeColour(idleColour, 100);
            base.OnHoverLost(e);
        }
    }

    /// <summary>
    /// A <see cref="DiscordIconButton"/> which opens a popover.
    /// </summary>
    public partial class DiscordPopoverButton : DiscordIconButton, IHasPopover
    {
        private readonly Func<Popover> createPopover;

        /// <summary>
        /// The popover which is open, if any.
        /// </summary>
        public Popover? OpenPopover { get; private set; }

        public DiscordPopoverButton(IconUsage icon, LocalisableString tooltip, Func<Popover> createPopover)
            : base(icon, tooltip)
        {
            this.createPopover = createPopover;
            Action = this.ShowPopover;
        }

        public bool PopoverOpen => OpenPopover?.State.Value == Visibility.Visible;

        public Popover GetPopover() => OpenPopover = createPopover();
    }

    /// <summary>
    /// The buttons which are shown over the top right corner of a hovered message.
    /// </summary>
    public partial class DiscordMessageToolbar : CompositeDrawable
    {
        private const int quick_reaction_count = 3;

        private readonly DiscordMessage message;
        private readonly IDiscordMessageActions actions;
        private readonly bool own;

        [Resolved]
        private DiscordEmojiUsage? emojiUsage { get; set; }

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        private FillFlowContainer quickReactions = null!;
        private DiscordPopoverButton reactionButton = null!;
        private DiscordPopoverButton moreButton = null!;

        private IReadOnlyList<DiscordReactionEmoji> shownQuickReactions = Array.Empty<DiscordReactionEmoji>();

        public DiscordMessageToolbar(DiscordMessage message, IDiscordMessageActions actions, bool own)
        {
            this.message = message;
            this.actions = actions;
            this.own = own;

            AutoSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Whether the toolbar is in use, so that it stays visible after the message is no longer hovered.
        /// </summary>
        public bool InUse => IsHovered || reactionButton.PopoverOpen || moreButton.PopoverOpen;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChild = new Container
            {
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 8,
                BorderThickness = 1,
                BorderColour = colourProvider.Background6,
                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Shadow,
                    Radius = 4,
                    Colour = Color4.Black.Opacity(0.2f),
                },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Padding = new MarginPadding(2),
                        Children = new Drawable[]
                        {
                            quickReactions = new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                            },
                            new Box
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(1, 20),
                                Margin = new MarginPadding { Horizontal = 4 },
                                Colour = colourProvider.Background6,
                            },
                            reactionButton = new DiscordPopoverButton(FontAwesome.Regular.Smile, SlopDiscordStrings.AddReaction,
                                () => new DiscordEmojiPicker(actions.GetCustomEmojis(), e => actions.ToggleReaction(message, e))),
                            own
                                ? new DiscordIconButton(FontAwesome.Solid.Pen, SlopDiscordStrings.EditMessage, () => actions.StartEdit(message))
                                : Empty(),
                            new DiscordIconButton(FontAwesome.Solid.Reply, SlopDiscordStrings.ReplyToMessage, () => actions.Reply(message)),
                            moreButton = new DiscordPopoverButton(FontAwesome.Solid.EllipsisH, SlopDiscordStrings.MoreActions, createMoreMenu),
                        },
                    },
                },
            };

            UpdateQuickReactions();
        }

        /// <summary>
        /// Shows the currently most used emojis, which may have changed since the toolbar was created.
        /// </summary>
        public void UpdateQuickReactions()
        {
            var emojis = emojiUsage?.GetMostUsed(quick_reaction_count) ?? Array.Empty<DiscordReactionEmoji>();

            if (emojis.SequenceEqual(shownQuickReactions))
                return;

            shownQuickReactions = emojis;
            quickReactions.ChildrenEnumerable = emojis.Select(e => new DiscordIconButton(DiscordEmoji.CreateDrawable(e, 20), e.Id == null ? e.Name ?? string.Empty : $@":{e.Name}:",
                () => actions.ToggleReaction(message, e)));
        }

        private Popover createMoreMenu() => new DiscordMessageMenuPopover(DrawableDiscordMessage.CreateMenuItems(message, actions, own, clipboard));

        // The toolbar covers the message above, which shouldn't be hovered through it.
        protected override bool OnHover(HoverEvent e) => true;
    }

    /// <summary>
    /// A popover which lists the actions of a message, like its context menu.
    /// </summary>
    public partial class DiscordMessageMenuPopover : OsuPopover
    {
        public DiscordMessageMenuPopover(IEnumerable<MenuItem> items)
            : base(false)
        {
            Child = new FillFlowContainer
            {
                Width = 200,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding(6),
                ChildrenEnumerable = items.Select(i => new Row(i, this)),
            };
        }

        private partial class Row : OsuClickableContainer
        {
            private readonly MenuItem item;

            private Box hoverBackground = null!;
            private OsuSpriteText text = null!;

            private Color4 colour;

            public Row(MenuItem item, Popover popover)
            {
                this.item = item;

                RelativeSizeAxes = Axes.X;
                Height = 32;
                Masking = true;
                CornerRadius = 4;

                Action = () =>
                {
                    popover.HidePopover();
                    item.Action.Value?.Invoke();
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                bool destructive = item is OsuMenuItem { Type: MenuItemType.Destructive };
                colour = destructive ? DiscordStyle.Error : colourProvider.Content1;

                Children = new Drawable[]
                {
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = destructive ? DiscordStyle.Error : DiscordStyle.Blurple,
                        Alpha = 0,
                    },
                    text = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 8,
                        Text = item.Text.Value,
                        Font = DiscordStyle.UiFont(14, FontWeight.SemiBold),
                        Colour = colour,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverBackground.FadeIn(50);
                text.FadeColour(Color4.White, 50);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBackground.FadeOut(100);
                text.FadeColour(colour, 100);
                base.OnHoverLost(e);
            }
        }
    }
}
