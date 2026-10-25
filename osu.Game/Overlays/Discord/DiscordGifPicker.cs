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
using osu.Framework.Threading;
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
    /// A popover with a search box and a grid of results, the base of the GIF and sticker pickers.
    /// </summary>
    public abstract partial class DiscordMediaPicker : OsuPopover
    {
        protected const float WIDTH = 400;

        [Resolved]
        protected OverlayColourProvider ColourProvider { get; private set; } = null!;

        protected BasicSearchTextBox SearchBox { get; private set; } = null!;
        protected FillFlowContainer Grid { get; private set; } = null!;
        protected LoadingSpinner Spinner { get; private set; } = null!;
        protected OsuSpriteText Status { get; private set; } = null!;

        /// <summary>
        /// The height which the picker is limited to, so that it fits into the space it is shown in.
        /// </summary>
        public float MaxHeight { get; init; } = 440;

        protected DiscordMediaPicker()
            : base(false)
        {
        }

        protected abstract LocalisableString SearchPlaceholder { get; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new Container
            {
                Size = new Vector2(WIDTH, Math.Clamp(MaxHeight, 200, 440)),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(10),
                        Child = SearchBox = new BasicSearchTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 32,
                            PlaceholderText = SearchPlaceholder,
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
                                Grid = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Spacing = new Vector2(6),
                                },
                                Spinner = new LoadingSpinner
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Size = new Vector2(24),
                                    Margin = new MarginPadding { Vertical = 12 },
                                },
                                Status = new OsuSpriteText
                                {
                                    Anchor = Anchor.TopCentre,
                                    Origin = Anchor.TopCentre,
                                    Font = DiscordStyle.UiFont(14),
                                    Colour = ColourProvider.Content2,
                                    Margin = new MarginPadding { Vertical = 12 },
                                    Alpha = 0,
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

            SearchBox.OnCommit += (_, _) => Grid.OfType<PickerButton>().FirstOrDefault(b => b.IsPresent)?.TriggerClick();
            Schedule(() => GetContainingFocusManager()?.ChangeFocus(SearchBox));
        }

        protected void ShowStatus(LocalisableString text)
        {
            Status.Text = text;
            Status.Show();
        }

        protected partial class PickerButton : OsuClickableContainer
        {
            private Box hoverBackground = null!;

            private readonly Drawable content;

            public PickerButton(Drawable content, LocalisableString tooltip, Action pick)
            {
                this.content = content;

                TooltipText = tooltip;
                Action = pick;
                Masking = true;
                CornerRadius = 6;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    // Only the media which is scrolled to is loaded.
                    new DelayedLoadWrapper(() => content, 0)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverBackground.FadeTo(0.1f, 50);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBackground.FadeOut(100);
                base.OnHoverLost(e);
            }
        }
    }

    /// <summary>
    /// A popover to search the GIFs of Discord's GIF picker, which shows the trending ones until something is searched.
    /// </summary>
    public partial class DiscordGifPicker : DiscordMediaPicker
    {
        private readonly Func<string, Task<IReadOnlyList<DiscordGif>>> search;
        private readonly Action<DiscordGif> onPicked;

        private ScheduledDelegate? pendingSearch;
        private int searchVersion;

        /// <param name="search">Searches the GIFs, or fetches the trending ones if the query is empty.</param>
        /// <param name="onPicked">Called with the picked GIF, after which the picker closes.</param>
        public DiscordGifPicker(Func<string, Task<IReadOnlyList<DiscordGif>>> search, Action<DiscordGif> onPicked)
        {
            this.search = search;
            this.onPicked = onPicked;
        }

        protected override LocalisableString SearchPlaceholder => SlopDiscordStrings.GifSearchPlaceholder;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SearchBox.Current.BindValueChanged(_ =>
            {
                pendingSearch?.Cancel();
                pendingSearch = Scheduler.AddDelayed(runSearch, 300);
            });

            runSearch();
        }

        private void runSearch()
        {
            int version = ++searchVersion;

            Grid.Clear();
            Status.Hide();
            Spinner.Show();

            search(SearchBox.Current.Value).ContinueWith(t => Schedule(() =>
            {
                // A newer search was started while this one was running.
                if (version != searchVersion)
                    return;

                Spinner.Hide();

                if (!t.IsCompletedSuccessfully)
                {
                    ShowStatus(SlopDiscordStrings.GifLoadFailed(t.Exception?.GetBaseException().Message ?? string.Empty));
                    return;
                }

                var gifs = t.GetResultSafely().Where(g => !string.IsNullOrEmpty(g.Preview)).ToList();

                if (gifs.Count == 0)
                {
                    ShowStatus(SlopDiscordStrings.GifNoResults);
                    return;
                }

                foreach (var gif in gifs)
                {
                    Grid.Add(new PickerButton(new DiscordRemoteImage(gif.Preview) { RelativeSizeAxes = Axes.Both }, gif.Title ?? string.Empty, () => pick(gif))
                    {
                        Width = (WIDTH - 20 - 6) / 2,
                        Height = 110,
                    });
                }
            }), TaskScheduler.Default);
        }

        private void pick(DiscordGif gif)
        {
            onPicked(gif);
            this.HidePopover();
        }
    }

    /// <summary>
    /// A popover to pick a sticker of a guild.
    /// </summary>
    public partial class DiscordStickerPicker : DiscordMediaPicker
    {
        private const float button_size = 88;

        private readonly IReadOnlyList<DiscordStickerGroup> groups;
        private readonly Action<DiscordSticker> onPicked;

        private readonly List<(PickerButton button, DiscordSticker sticker)> buttons = new List<(PickerButton, DiscordSticker)>();

        /// <summary>
        /// The stickers which can be sent, which are only the ones of the current guild without Nitro.
        /// Lottie stickers aren't included, as they can't be shown.
        /// </summary>
        public static IReadOnlyList<DiscordStickerGroup> GetStickerGroups(DiscordClient client, ulong? currentGuildId)
        {
            bool nitro = client.LocalUser.Value?.HasNitro == true;

            return client.Guilds
                         .Where(g => nitro || g.Id == currentGuildId)
                         .OrderByDescending(g => g.Id == currentGuildId)
                         .Select(g => new DiscordStickerGroup(g.Name, g.Stickers.Where(s => s.Available && s.FormatType != 3).ToList()))
                         .Where(g => g.Stickers.Count > 0)
                         .ToList();
        }

        /// <param name="groups">The stickers which can be sent, from <see cref="GetStickerGroups"/>.</param>
        /// <param name="onPicked">Called with the picked sticker, after which the picker closes.</param>
        public DiscordStickerPicker(IReadOnlyList<DiscordStickerGroup> groups, Action<DiscordSticker> onPicked)
        {
            this.groups = groups;
            this.onPicked = onPicked;
        }

        protected override LocalisableString SearchPlaceholder => SlopDiscordStrings.StickerSearchPlaceholder;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Spinner.Hide();

            foreach (var group in groups)
            {
                Grid.Add(new OsuSpriteText
                {
                    Text = group.GuildName.ToUpper(),
                    Font = DiscordStyle.UiFont(12, FontWeight.Bold),
                    Colour = ColourProvider.Content2,
                    Margin = new MarginPadding { Left = 4, Top = 4 },
                    // Takes the whole row, so that the stickers of each guild start on a new line.
                    RelativeSizeAxes = Axes.X,
                });

                foreach (var sticker in group.Stickers)
                {
                    var button = new PickerButton(new DiscordRemoteImage(sticker.GetUrl(), sticker.FormatType == 4)
                    {
                        RelativeSizeAxes = Axes.Both,
                        ImageFillMode = FillMode.Fit,
                        ShowPlaceholder = false,
                    }, sticker.Name, () => pick(sticker))
                    {
                        Size = new Vector2(button_size),
                    };

                    buttons.Add((button, sticker));
                    Grid.Add(button);
                }
            }

            if (buttons.Count == 0)
                ShowStatus(SlopDiscordStrings.NoStickers);

            SearchBox.Current.BindValueChanged(_ => updateFilter());
        }

        private void updateFilter()
        {
            string query = SearchBox.Current.Value.Trim();

            foreach (var (button, sticker) in buttons)
                button.Alpha = string.IsNullOrEmpty(query) || sticker.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        private void pick(DiscordSticker sticker)
        {
            onPicked(sticker);
            this.HidePopover();
        }
    }

    /// <summary>
    /// The stickers of a guild which can be used in the picker.
    /// </summary>
    public record DiscordStickerGroup(string GuildName, IReadOnlyList<DiscordSticker> Stickers);
}
