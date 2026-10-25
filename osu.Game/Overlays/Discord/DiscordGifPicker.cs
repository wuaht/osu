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
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
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

        /// <summary>
        /// The container of the search box, which further buttons can be added to.
        /// </summary>
        protected Container SearchBar { get; private set; } = null!;
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
                        Child = SearchBar = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 32,
                            Child = SearchBox = new BasicSearchTextBox
                            {
                                RelativeSizeAxes = Axes.Both,
                                PlaceholderText = SearchPlaceholder,
                            },
                        },
                    },
                    // The results are masked below the search box, which they would otherwise scroll over.
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = 52 },
                        Child = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
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
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SearchBox.OnCommit += (_, _) => Buttons.FirstOrDefault(b => b.IsPresent)?.TriggerClick();
            Schedule(() => GetContainingFocusManager()?.ChangeFocus(SearchBox));
        }

        /// <summary>
        /// The results, the first of which is picked when the search is committed.
        /// </summary>
        protected virtual IEnumerable<PickerButton> Buttons => Grid.OfType<PickerButton>();

        protected void ShowStatus(LocalisableString text)
        {
            Status.Text = text;
            Status.Show();
        }

        protected partial class PickerButton : OsuClickableContainer
        {
            private Box hoverBackground = null!;

            private readonly Func<Drawable> createContent;

            /// <param name="createContent">Creates the media which is shown, which is only done while it is scrolled to.</param>
            public PickerButton(Func<Drawable> createContent, LocalisableString tooltip, Action pick)
            {
                this.createContent = createContent;

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
                    // Only the media which is scrolled to is loaded, and it is unloaded again once it was scrolled away from.
                    new DelayedLoadUnloadWrapper(createContent, 0, 2000)
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
    /// Like in Discord, the GIFs are laid out in two columns, each with the height of its GIF.
    /// </summary>
    public partial class DiscordGifPicker : DiscordMediaPicker
    {
        private const float column_width = (WIDTH - 20 - 6) / 2;

        private readonly DiscordClient client;
        private readonly Action<DiscordGif> onPicked;

        private readonly HashSet<string> favourites = new HashSet<string>();

        private ScheduledDelegate? pendingSearch;
        private int searchVersion;

        private FillFlowContainer leftColumn = null!;
        private FillFlowContainer rightColumn = null!;
        private FavouritesButton favouritesButton = null!;

        private bool showingFavourites;

        /// <param name="client">The client which the GIFs are searched with.</param>
        /// <param name="onPicked">Called with the picked GIF, after which the picker closes.</param>
        public DiscordGifPicker(DiscordClient client, Action<DiscordGif> onPicked)
        {
            this.client = client;
            this.onPicked = onPicked;
        }

        protected override LocalisableString SearchPlaceholder => SlopDiscordStrings.GifSearchPlaceholder;

        protected override IEnumerable<PickerButton> Buttons => leftColumn.Concat(rightColumn).OfType<GifTile>().Select(t => t.Button);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Grid.Direction = FillDirection.Horizontal;
            Grid.AddRange(new Drawable[]
            {
                leftColumn = createColumn(),
                rightColumn = createColumn(),
            });

            SearchBar.Padding = new MarginPadding { Right = 38 };
            SearchBar.Add(favouritesButton = new FavouritesButton
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = 38,
                Action = () =>
                {
                    showingFavourites = !showingFavourites;
                    pendingSearch?.Cancel();

                    if (showingFavourites)
                        SearchBox.Current.Value = string.Empty;

                    runSearch();
                },
            });

            SearchBox.Current.BindValueChanged(_ =>
            {
                // Searching leaves the favourites, as they can't be searched.
                if (showingFavourites && !string.IsNullOrEmpty(SearchBox.Current.Value))
                    showingFavourites = false;

                pendingSearch?.Cancel();
                pendingSearch = Scheduler.AddDelayed(runSearch, 300);
            });

            client.GetFavouriteGifs().ContinueWith(t => Schedule(() =>
            {
                if (!t.IsCompletedSuccessfully)
                    return;

                foreach (var gif in t.GetResultSafely())
                    favourites.Add(gif.Url);

                updateFavouriteStates();
            }), TaskScheduler.Default);

            runSearch();
        }

        private static FillFlowContainer createColumn() => new FillFlowContainer
        {
            Width = column_width,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(6),
        };

        private void runSearch()
        {
            int version = ++searchVersion;

            favouritesButton.Active = showingFavourites;

            leftColumn.Clear();
            rightColumn.Clear();
            Status.Hide();
            Spinner.Show();

            var searching = showingFavourites ? client.GetFavouriteGifs() : client.SearchGifs(SearchBox.Current.Value);

            searching.ContinueWith(t => Schedule(() =>
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

                var gifs = t.GetResultSafely().Where(g => !string.IsNullOrEmpty(g.Src) || !string.IsNullOrEmpty(g.Preview)).ToList();

                if (showingFavourites)
                {
                    favourites.Clear();
                    foreach (var gif in gifs)
                        favourites.Add(gif.Url);
                }

                if (gifs.Count == 0)
                {
                    ShowStatus(showingFavourites ? SlopDiscordStrings.NoFavouriteGifs : SlopDiscordStrings.GifNoResults);
                    return;
                }

                float leftHeight = 0;
                float rightHeight = 0;

                foreach (var gif in gifs)
                {
                    var tile = new GifTile(gif, favourites.Contains(gif.Url), () => pick(gif), toggleFavourite);

                    // Each GIF is added to the shorter column, so that both stay about as high.
                    if (leftHeight <= rightHeight)
                    {
                        leftColumn.Add(tile);
                        leftHeight += tile.Height + 6;
                    }
                    else
                    {
                        rightColumn.Add(tile);
                        rightHeight += tile.Height + 6;
                    }
                }
            }), TaskScheduler.Default);
        }

        private void toggleFavourite(GifTile tile)
        {
            var gif = tile.Gif;
            bool favourite = !favourites.Contains(gif.Url);

            setFavourite(gif.Url, favourite);

            client.SetGifFavourite(gif, favourite).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    return;

                Logger.Log($@"Failed to change the favourite GIFs: {t.Exception?.GetBaseException().Message}", LoggingTarget.Network, LogLevel.Important);
                Schedule(() => setFavourite(gif.Url, !favourite));
            }, TaskScheduler.Default);
        }

        private void setFavourite(string url, bool favourite)
        {
            if (favourite)
                favourites.Add(url);
            else
                favourites.Remove(url);

            updateFavouriteStates();
        }

        private void updateFavouriteStates()
        {
            foreach (var tile in leftColumn.Concat(rightColumn).OfType<GifTile>())
                tile.Favourite = favourites.Contains(tile.Gif.Url);
        }

        private void pick(DiscordGif gif)
        {
            onPicked(gif);
            this.HidePopover();
        }

        private partial class GifTile : Container
        {
            public readonly DiscordGif Gif;
            public readonly PickerButton Button;

            private readonly FavouriteToggle toggle;

            public bool Favourite
            {
                set => toggle.Favourite = value;
            }

            public GifTile(DiscordGif gif, bool favourite, Action pick, Action<GifTile> toggleFavourite)
            {
                Gif = gif;

                Width = column_width;
                Height = gif.Width > 0 && gif.Height > 0
                    ? Math.Clamp(column_width * gif.Height / gif.Width, 60, 320)
                    : column_width * 0.75f;

                Children = new Drawable[]
                {
                    Button = new PickerButton(createMedia, gif.Title ?? string.Empty, pick)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    toggle = new FavouriteToggle
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Margin = new MarginPadding(6),
                        Favourite = favourite,
                        Action = () => toggleFavourite(this),
                    },
                };
            }

            private Drawable createMedia()
            {
                string url = !string.IsNullOrEmpty(Gif.Src) ? Gif.Src : Gif.Preview!;

                if (Gif.IsVideo || url.EndsWith(@".mp4", StringComparison.OrdinalIgnoreCase) || url.EndsWith(@".webm", StringComparison.OrdinalIgnoreCase))
                {
                    return new DiscordMediaVideo(url, null, DrawSize, true)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Size = Vector2.One,
                    };
                }

                return new DiscordRemoteImage(url, url.EndsWith(@".gif", StringComparison.OrdinalIgnoreCase)) { RelativeSizeAxes = Axes.Both };
            }

            protected override bool OnHover(HoverEvent e)
            {
                toggle.FadeIn(100);
                return false;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                toggle.FadeTo(toggle.IsFavourite ? 1 : 0, 100);
            }
        }

        /// <summary>
        /// The star in the corner of a GIF, which is always shown for favourites and otherwise only when the GIF is hovered.
        /// </summary>
        private partial class FavouriteToggle : OsuClickableContainer
        {
            private readonly SpriteIcon icon;

            public bool IsFavourite { get; private set; }

            public bool Favourite
            {
                set
                {
                    IsFavourite = value;
                    icon.Icon = value ? FontAwesome.Solid.Star : FontAwesome.Regular.Star;
                    icon.Colour = value ? DiscordStyle.MentionedMessageBar : Color4.White;
                    TooltipText = value ? SlopDiscordStrings.RemoveGifFromFavourites : SlopDiscordStrings.AddGifToFavourites;

                    if (!IsHovered && Parent?.IsHovered != true)
                        Alpha = value ? 1 : 0;
                }
            }

            public FavouriteToggle()
            {
                Size = new Vector2(28);
                Alpha = 0;

                Children = new Drawable[]
                {
                    new Circle
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.6f,
                    },
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(14),
                        Icon = FontAwesome.Regular.Star,
                    },
                };
            }
        }

        /// <summary>
        /// The button next to the search box which switches to the favourite GIFs.
        /// </summary>
        private partial class FavouritesButton : OsuClickableContainer
        {
            private SpriteIcon icon = null!;
            private Box background = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private bool active;

            public bool Active
            {
                set
                {
                    active = value;

                    if (IsLoaded)
                        updateState();
                }
            }

            public FavouritesButton()
            {
                Size = new Vector2(32);
                Masking = true;
                CornerRadius = 6;
                TooltipText = SlopDiscordStrings.FavouriteGifs;
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
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(16),
                        Icon = FontAwesome.Solid.Star,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateState();
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
                icon.Colour = active ? DiscordStyle.MentionedMessageBar : IsHovered ? colourProvider.Content1 : colourProvider.Content2;
                background.Colour = active || IsHovered ? colourProvider.Background3 : colourProvider.Background4;
            }
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
                    var button = new PickerButton(() => new DiscordRemoteImage(sticker.GetUrl(), sticker.FormatType == 4)
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
