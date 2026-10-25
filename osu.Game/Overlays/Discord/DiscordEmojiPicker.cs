// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
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
    /// The custom emojis of a guild which can be used in the picker.
    /// </summary>
    public record DiscordCustomEmojiGroup(string GuildName, IReadOnlyList<DiscordCustomEmoji> Emojis);

    /// <summary>
    /// A popover to pick a standard or custom emoji, for writing it into a message or reacting with it.
    /// </summary>
    [Cached]
    public partial class DiscordEmojiPicker : OsuPopover
    {
        private const float button_size = 40;

        private readonly IReadOnlyList<DiscordCustomEmojiGroup> customGroups;
        private readonly Action<DiscordReactionEmoji> onPicked;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private DiscordEmojiUsage? usage { get; set; }

        private BasicSearchTextBox searchBox = null!;
        private FillFlowContainer<Section> sections = null!;
        private LoadingSpinner spinner = null!;
        private OsuSpriteText status = null!;

        private bool loadFailed;

        /// <summary>
        /// The custom emojis which can be used, which are only the ones of the current guild without Nitro, and only the static ones.
        /// </summary>
        public static IReadOnlyList<DiscordCustomEmojiGroup> GetCustomGroups(DiscordClient client, ulong? currentGuildId)
        {
            bool nitro = client.LocalUser.Value?.HasNitro == true;

            return client.Guilds
                         .Where(g => nitro || g.Id == currentGuildId)
                         .OrderByDescending(g => g.Id == currentGuildId)
                         .Select(g => new DiscordCustomEmojiGroup(g.Name, g.Emojis.Where(e => e.Available && (nitro || !e.Animated)).ToList()))
                         .Where(g => g.Emojis.Count > 0)
                         .ToList();
        }

        /// <param name="customGroups">The custom emojis which can be used, from <see cref="GetCustomGroups"/>.</param>
        /// <param name="onPicked">Called with the picked emoji, after which the picker closes.</param>
        public DiscordEmojiPicker(IReadOnlyList<DiscordCustomEmojiGroup> customGroups, Action<DiscordReactionEmoji> onPicked)
            : base(false)
        {
            this.customGroups = customGroups;
            this.onPicked = onPicked;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new Container
            {
                Size = new Vector2(button_size * 9 + 28, 420),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(10),
                        Child = searchBox = new BasicSearchTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 32,
                            PlaceholderText = SlopDiscordStrings.EmojiSearchPlaceholder,
                        },
                    },
                    new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = 52 },
                        Child = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Horizontal = 10, Bottom = 10 },
                            Children = new Drawable[]
                            {
                                sections = new FillFlowContainer<Section>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 8),
                                },
                                spinner = new LoadingSpinner
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Size = new Vector2(24),
                                    Margin = new MarginPadding { Vertical = 12 },
                                },
                                status = new OsuSpriteText
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Font = DiscordStyle.UiFont(14),
                                    Colour = colourProvider.Content2,
                                    Margin = new MarginPadding { Vertical = 12 },
                                    Alpha = 0,
                                },
                            },
                        },
                    },
                },
            };

            if (usage != null)
                sections.Add(new Section(SlopDiscordStrings.EmojiFrequentlyUsed, usage.GetMostUsed(18).Select(e => new EmojiButton(e, e.Id == null ? e.Name ?? string.Empty : $@":{e.Name}:", null)), true));

            foreach (var group in customGroups)
            {
                sections.Add(new Section(group.GuildName, group.Emojis.Select(e =>
                {
                    var emoji = new DiscordReactionEmoji { Id = e.Id, Name = e.Name, Animated = e.Animated };
                    return new EmojiButton(emoji, $@":{e.Name}:", s => e.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
                })));
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            spinner.Show();

            DiscordEmojiData.Load().ContinueWith(t => Schedule(() =>
            {
                spinner.Hide();

                if (!t.IsCompletedSuccessfully)
                {
                    loadFailed = true;
                    status.Text = SlopDiscordStrings.EmojiLoadFailed(t.Exception?.GetBaseException().Message ?? string.Empty);
                    status.Show();
                    return;
                }

                foreach (var category in t.GetResultSafely())
                {
                    sections.Add(new Section(category.Name, category.Emojis.Select(e =>
                    {
                        string tooltip = e.Shortcodes.Count > 0 ? $@":{e.Shortcodes[0]}:" : e.Label;
                        return new EmojiButton(new DiscordReactionEmoji { Name = e.Unicode }, tooltip, e.Matches);
                    })));
                }

                updateFilter();
            }), TaskScheduler.Default);

            searchBox.Current.BindValueChanged(_ => updateFilter());
            searchBox.OnCommit += (_, _) => sections.Where(s => s.IsPresent).SelectMany(s => s.Buttons).FirstOrDefault(b => b.IsPresent)?.TriggerClick();

            Schedule(() => GetContainingFocusManager()?.ChangeFocus(searchBox));
        }

        private void updateFilter()
        {
            string search = searchBox.Current.Value.Trim().Trim(':');
            bool anyMatch = false;

            foreach (var section in sections)
                anyMatch |= section.Filter(search);

            if (loadFailed)
                return;

            status.Text = SlopDiscordStrings.EmojiNoResults;
            status.Alpha = anyMatch || spinner.State.Value == Visibility.Visible ? 0 : 1;
        }

        private void pick(DiscordReactionEmoji emoji)
        {
            onPicked(emoji);
            this.HidePopover();
        }

        private partial class Section : FillFlowContainer
        {
            public readonly FillFlowContainer<EmojiButton> Buttons;

            private readonly bool hideWhileSearching;
            private readonly OsuSpriteText header;

            public Section(LocalisableString name, IEnumerable<EmojiButton> buttons, bool hideWhileSearching = false)
            {
                this.hideWhileSearching = hideWhileSearching;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Vertical;
                Spacing = new Vector2(0, 4);

                Children = new Drawable[]
                {
                    header = new OsuSpriteText
                    {
                        Text = name.ToUpper(),
                        Font = DiscordStyle.UiFont(12, FontWeight.Bold),
                        Margin = new MarginPadding { Left = 4 },
                    },
                    Buttons = new FillFlowContainer<EmojiButton>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ChildrenEnumerable = buttons,
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                header.Colour = colourProvider.Content2;
            }

            /// <summary>
            /// Shows only the emojis which match the search, returning whether any does.
            /// </summary>
            public bool Filter(string search)
            {
                bool searching = !string.IsNullOrEmpty(search);
                bool any = false;

                foreach (var button in Buttons)
                {
                    bool matches = !searching || (!hideWhileSearching && button.Matches(search));
                    button.Alpha = matches ? 1 : 0;
                    any |= matches;
                }

                Alpha = any ? 1 : 0;
                return any;
            }
        }

        private partial class EmojiButton : OsuClickableContainer
        {
            private readonly DiscordReactionEmoji emoji;
            private readonly Func<string, bool>? matches;

            private Box hoverBackground = null!;

            [Resolved]
            private DiscordEmojiPicker picker { get; set; } = null!;

            public EmojiButton(DiscordReactionEmoji emoji, string tooltip, Func<string, bool>? matches)
            {
                this.emoji = emoji;
                this.matches = matches;

                TooltipText = tooltip;
                Size = new Vector2(button_size);
                Masking = true;
                CornerRadius = 6;
            }

            public bool Matches(string search) => matches?.Invoke(search) == true;

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    // Only the emojis which are scrolled to are loaded, as there are about two thousand of them.
                    new DelayedLoadWrapper(() => new DrawableDiscordEmoji(DiscordEmoji.GetUrl(emoji) ?? string.Empty, emoji.Name ?? string.Empty, string.Empty, 30, emoji.Animated)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    }, 0)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                };

                Action = () => picker.pick(emoji);
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverBackground.FadeTo(0.08f, 50);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBackground.FadeOut(100);
                base.OnHoverLost(e);
            }
        }
    }
}
