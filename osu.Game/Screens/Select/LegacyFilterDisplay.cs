// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// stable's top-right song select controls: the group and sort dropdowns and the search line.
    /// Typing still goes to the (hidden) <see cref="FilterControl"/> search box.
    /// </summary>
    public partial class LegacyFilterDisplay : CompositeDrawable
    {
        private const float scale = LegacySongSelectComponent.STABLE_SCALE;

        private static FontUsage font(float stableSize, FontWeight weight = FontWeight.Regular) => OsuFont.GetFont(size: stableSize * LegacyUserPanel.StableText.EM * scale, weight: weight);

        private readonly FilterControl filterControl;
        private readonly BeatmapDetailsArea detailsArea;

        private LegacyDropdown<BeatmapLeaderboardScope> scopeDropdown = null!;
        private LegacyDropdown<GroupMode> groupDropdown = null!;
        private OsuSpriteText groupText = null!;
        private OsuSpriteText sortText = null!;

        private readonly Bindable<string> query = new Bindable<string>(string.Empty);

        private OsuSpriteText queryText = null!;
        private OsuSpriteText statusText = null!;
        private LegacyInfoCard infoCard = null!;
        private LegacyWebButton webButton = null!;

        private readonly LegacyModeButtonLayer modeButtonLayer;
        private string? infoCardTexture;

        [Resolved]
        private OsuGame? game { get; set; }

        // stable's info cards (selection-norecords and friends) slide down from (118, 170) to (118, 200).
        private static readonly Vector2 info_card_position = new Vector2(118, 200) * scale;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private SkinManager? skinManager { get; set; }

        [Resolved]
        private BeatmapModelDownloader? beatmapDownloader { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private LocalisableString status;

        private readonly Bindable<GroupMode> groupMode = new Bindable<GroupMode>();
        private Container<LegacyGroupTab> tabs = null!;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        /// <summary>
        /// Whether the display is in use (a legacy skin is active).
        /// </summary>
        public bool InUse { get; set; }

        /// <summary>
        /// Invoked when stable's play button is clicked.
        /// </summary>
        public Action? StartRequested { get; set; }

        public override bool PropagatePositionalInputSubTree => InUse && Alpha > 0 && base.PropagatePositionalInputSubTree;

        public LocalisableString StatusText
        {
            get => status;
            set
            {
                status = value;
                updateStatus();
            }
        }

        /// <summary>
        /// stable's search background, which darkens the search line. Stable draws it below the <c>songselect-top</c> artwork,
        /// so it is shown through a proxy below the skinnable layer.
        /// </summary>
        public readonly Drawable SearchBackground;

        /// <summary>
        /// stable's play button. Its beating logo is shown through a proxy below the skinnable layer, so the skinned interface covers it.
        /// </summary>
        public readonly LegacyPlayButton PlayButton;

        private readonly Sprite searchBackground;

        private const float search_background_y = 46;
        private const float search_background_search_y = 54;

        public LegacyFilterDisplay(FilterControl filterControl, BeatmapDetailsArea detailsArea, LegacyModeButtonLayer modeButtonLayer)
        {
            this.filterControl = filterControl;
            this.detailsArea = detailsArea;
            this.modeButtonLayer = modeButtonLayer;
            RelativeSizeAxes = Axes.Both;

            PlayButton = new LegacyPlayButton { Action = () => StartRequested?.Invoke() };

            // stable's colour alpha (102) also scales the sprite's alpha, which starts at the same 0.4.
            SearchBackground = searchBackground = new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-14, search_background_y) * scale,
                Colour = Color4.Black.Opacity(102 / 255f),
                Alpha = 102 / 255f,
            };
            // keeps updating so it can follow the filter control's visibility.
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            groupDropdown = new LegacyDropdown<GroupMode>(new Color4(146, 195, 230, 255))
            {
                Position = new Vector2(-352, 18) * scale,
                Items = Enum.GetValues<GroupMode>(),
            };

            var sortDropdown = new LegacyDropdown<SortMode>(new Color4(174, 210, 139, 255))
            {
                Position = new Vector2(-130, 18) * scale,
                Items = Enum.GetValues<SortMode>(),
            };

            // stable keeps its default pink (NEW_SKIN_COLOUR_MAIN_DARKER) here and only tints the border.
            scopeDropdown = new LegacyDropdown<BeatmapLeaderboardScope>(new Color4(187, 17, 119, 255), new Color4(31, 184, 255, 180), 14, 190)
            {
                Anchor = Anchor.TopLeft,
                Position = new Vector2(5, 73.5f) * scale,
                Items = Enum.GetValues<BeatmapLeaderboardScope>(),
            };

            config.BindWith(OsuSetting.SongSelectGroupMode, groupDropdown.Current);
            config.BindWith(OsuSetting.SongSelectGroupMode, groupMode);

            // stable's play mode tabs, laid out right to left from (W - 55, 41) every 74 units.
            (GroupMode mode, string label)[] tabModes =
            {
                (GroupMode.None, "No Grouping"),
                (GroupMode.Difficulty, "By Difficulty"),
                (GroupMode.Artist, "By Artist"),
                (GroupMode.LastPlayed, "Recently Played"),
                (GroupMode.Collections, "Collections"),
            };

            tabs = new Container<LegacyGroupTab> { RelativeSizeAxes = Axes.Both };

            for (int i = 0; i < tabModes.Length; i++)
            {
                var (mode, label) = tabModes[i];

                tabs.Add(new LegacyGroupTab(label)
                {
                    Anchor = Anchor.TopRight,
                    Position = new Vector2(-55 - 74 * i, 41) * scale,
                    // stable draws earlier tabs above later ones.
                    Depth = i,
                    Action = () => groupMode.Value = mode,
                    Mode = mode,
                    Index = i,
                });
            }
            config.BindWith(OsuSetting.SongSelectSortingMode, sortDropdown.Current);

            searchBackground.Texture = skinManager?.DefaultClassicSkin.GetTexture("menu-button-background");

            InternalChildren = new Drawable[]
            {
                searchBackground,
                // stable's labels sit left of their dropdowns.
                sortText = new OsuSpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    X = -130 * scale,
                    Text = SongSelectStrings.Sort,
                    Font = font(22),
                    Colour = new Color4(174, 210, 139, 255),
                    Shadow = true,
                },
                groupText = new OsuSpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Text = SongSelectStrings.Group,
                    Font = font(22),
                    Colour = new Color4(146, 195, 230, 255),
                    Shadow = true,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(-220, 54) * scale,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4 * scale, 0),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Search:",
                            Font = font(13, FontWeight.Bold),
                            Colour = new Color4(173, 255, 47, 255),
                            Shadow = true,
                        },
                        queryText = new TruncatingSpriteText
                        {
                            MaxWidth = 174 * scale,
                            Font = font(13, FontWeight.Bold),
                            Shadow = true,
                        },
                    }
                },
                statusText = new OsuSpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(-220, 66) * scale,
                    Font = font(11, FontWeight.Bold),
                    Shadow = true,
                },
                infoCard = new LegacyInfoCard
                {
                    Position = info_card_position,
                    Action = updateBeatmap,
                },
                tabs,
                PlayButton,
                // stable's mode button covers the tabs and the search line in skins like WhiteCat's, and is covered by the web button.
                modeButtonLayer,
                // stable's web access button right of the leaderboard dropdown, centred at (214, 81).
                webButton = new LegacyWebButton
                {
                    Position = new Vector2(214, 81) * scale,
                    Action = () =>
                    {
                        if (beatmap.Value.BeatmapInfo.OnlineID > 0)
                            game?.ShowBeatmap(beatmap.Value.BeatmapInfo.OnlineID);
                    },
                },
                // the group dropdown opens over the sort dropdown's area, so it goes on top.
                scopeDropdown,
                sortDropdown,
                groupDropdown,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (detailsArea.LeaderboardScope is Bindable<BeatmapLeaderboardScope> scope)
                scopeDropdown.Current.BindTo(scope);
            else
                scopeDropdown.Hide();

            groupMode.BindValueChanged(m =>
            {
                for (int i = 0; i < tabs.Count; i++)
                {
                    var tab = tabs[i];
                    tab.Selected = tab.Mode == m.NewValue;
                    // stable raises the selected tab above its neighbours.
                    tabs.ChangeChildDepth(tab, tab.Selected ? -1 : tab.Index);
                }
            }, true);

            skin.SourceChanged += updateSkinTextures;
            updateSkinTextures();

            beatmap.BindValueChanged(b =>
            {
                if (b.NewValue.BeatmapInfo.OnlineID > 0)
                    webButton.FadeIn(300);
                else
                    webButton.FadeOut(300);
            }, true);

            query.BindTo(filterControl.SearchQuery);
            query.BindValueChanged(q =>
            {
                queryText.Text = string.IsNullOrEmpty(q.NewValue) ? "Type to search!" : q.NewValue;
                updateStatus();

                if (string.IsNullOrEmpty(q.OldValue) != string.IsNullOrEmpty(q.NewValue))
                    updateSearchBackground(!string.IsNullOrEmpty(q.NewValue));
            }, true);
            updateSearchBackground(!string.IsNullOrEmpty(query.Value), true);
        }

        private void updateSkinTextures() => Schedule(() =>
        {
            foreach (var tab in tabs)
                tab.Texture = skin.GetTexture("selection-tab");

            webButton.Texture = skin.GetTexture("rank-forum");
        });

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= updateSkinTextures;
        }

        /// <summary>
        /// stable's <c>ShowSearch</c> and <c>ResetSearch</c>: the background brightens and moves down to cover the matches line while searching.
        /// </summary>
        private void updateSearchBackground(bool searching, bool instant = false)
        {
            if (searching)
            {
                searchBackground.FadeTo(0.95f, instant ? 0 : 300);
                searchBackground.MoveToY(search_background_search_y * scale, instant ? 0 : 200, Easing.Out);
            }
            else
            {
                searchBackground.FadeTo(102 / 255f, instant ? 0 : 200);
                searchBackground.MoveToY(search_background_y * scale, instant ? 0 : 200, Easing.Out);
            }
        }

        private void updateStatus() => statusText.Text = string.IsNullOrEmpty(query.Value) ? default : status;

        protected override void Update()
        {
            base.Update();

            // follow the filter control when the carousel hides the wedges.
            Alpha = InUse ? filterControl.Alpha : 0;

            // stable places the group label and dropdown 258 units from the right, further left by the sort label's width.
            float groupX = -258 * scale - sortText.DrawWidth;
            groupText.X = groupX;

            // in stable the labels' baseline lines up with the bottom of their dropdowns.
            sortText.Y = groupText.Y = groupDropdown.Y + groupDropdown.HeaderHeight - sortText.LineBaseHeight;
            groupDropdown.X = groupX;

            string? texture = getInfoCardTexture();

            if (texture == infoCardTexture)
                return;

            infoCardTexture = texture;

            if (texture == null)
                infoCard.FadeOut(150, Easing.OutQuint);
            else
                infoCard.Display(skinManager?.DefaultClassicSkin.GetTexture(texture), texture == update_card);
        }

        private const string update_card = "selection-update";

        /// <summary>
        /// stable's <c>ShowStatusIcon</c>: the card shown in place of an empty leaderboard, always from its default skin.
        /// </summary>
        private string? getInfoCardTexture()
        {
            if (detailsArea.LeaderboardState is not (LeaderboardState.NoScores or LeaderboardState.BeatmapUnavailable))
                return null;

            var info = beatmap.Value.BeatmapInfo;

            if (beatmap.IsDefault || scopeDropdown.Current.Value == BeatmapLeaderboardScope.Local)
                return "selection-norecords";

            if (!info.MatchesOnlineVersion)
                return update_card;

            switch (info.Status)
            {
                case BeatmapOnlineStatus.None:
                    return "selection-notsubmitted";

                case BeatmapOnlineStatus.LocallyModified:
                    return "selection-unknown";

                case BeatmapOnlineStatus.Graveyard:
                case BeatmapOnlineStatus.WIP:
                case BeatmapOnlineStatus.Pending:
                    return "selection-notranked";

                default:
                    return "selection-norecords";
            }
        }

        private void updateBeatmap()
        {
            if (beatmap.IsDefault || infoCardTexture != update_card)
                return;

            beatmapDownloader?.DownloadAsUpdate(beatmap.Value.BeatmapSetInfo, config.Get<bool>(OsuSetting.PreferNoVideo));
        }

        /// <summary>
        /// stable's <c>rank-forum</c>: centred on the full texture, but only its top left 25x25 pixels are drawn (and clickable).
        /// </summary>
        private partial class LegacyWebButton : OsuClickableContainer
        {
            private const float draw_size = 25;

            private readonly Sprite sprite;

            public LegacyWebButton()
            {
                Alpha = 0;
                Masking = true;
                TooltipText = "Web access";
                Child = sprite = new Sprite { Origin = Anchor.Centre };
            }

            public Texture? Texture
            {
                set
                {
                    sprite.Texture = value;

                    Vector2 textureSize = value?.DisplaySize ?? Vector2.Zero;
                    sprite.Size = textureSize;
                    sprite.Position = textureSize / 2;
                    Size = Vector2.ComponentMin(textureSize, new Vector2(draw_size));
                    OriginPosition = textureSize / 2;
                }
            }

            public override bool PropagatePositionalInputSubTree => Alpha > 0 && base.PropagatePositionalInputSubTree;

            protected override bool OnClick(ClickEvent e)
            {
                // stable's ClickEffect scales the whole sprite around its centre.
                sprite.ScaleTo(1.1f).ScaleTo(1, 50);
                return base.OnClick(e);
            }
        }

        /// <summary>
        /// One of stable's song select info cards. The update card is clickable, turns lime green on hover and pulses.
        /// </summary>
        private partial class LegacyInfoCard : CompositeDrawable
        {
            public Action? Action { get; init; }

            private readonly Sprite card;
            private readonly Sprite pulse;
            private bool clickable;

            public LegacyInfoCard()
            {
                AutoSizeAxes = Axes.Both;
                Origin = Anchor.Centre;
                Alpha = 0;

                InternalChildren = new Drawable[]
                {
                    card = new Sprite(),
                    pulse = new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Alpha = 0,
                    },
                };
            }

            public void Display(Texture? texture, bool isClickable)
            {
                clickable = isClickable;
                card.Texture = pulse.Texture = texture;
                card.Colour = Color4.White;

                ClearTransforms();
                this.MoveToY(info_card_position.Y - 30 * scale).MoveToY(info_card_position.Y, 400, Easing.Out).FadeInFromZero(400);

                pulse.ClearTransforms();
                pulse.Alpha = 0;
                pulse.Scale = Vector2.One;

                if (clickable)
                {
                    using (pulse.BeginDelayedSequence(400))
                    {
                        pulse.FadeTo(1).FadeOut(1000, Easing.Out).Loop();
                        pulse.ScaleTo(1).ScaleTo(2, 1000, Easing.Out).Loop();
                    }
                }
            }

            public override bool PropagatePositionalInputSubTree => clickable && base.PropagatePositionalInputSubTree;

            protected override bool OnHover(HoverEvent e)
            {
                card.FadeColour(Color4.LimeGreen, 200);
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e) => card.FadeColour(Color4.White, 200);

            protected override bool OnClick(ClickEvent e)
            {
                Action?.Invoke();
                return true;
            }
        }

        /// <summary>
        /// stable's <c>selection-tab</c>: crimson, white when selected, with its label centred on the texture's (70, 12).
        /// </summary>
        private partial class LegacyGroupTab : CompositeDrawable
        {
            private static readonly Color4 base_colour = new Color4(220, 20, 60, 255);
            private static readonly Color4 hover_colour = new Color4(240, 70, 110, 255);
            private static readonly Vector2 origin = new Vector2(70, 12);

            public GroupMode Mode { get; init; }
            public int Index { get; init; }
            public Action? Action { get; init; }

            private readonly Sprite background;
            private readonly OsuSpriteText text;
            private bool selected;

            public LegacyGroupTab(string label)
            {
                // stable shortens labels longer than 13 / text size characters.
                const int max_length = (int)(13 / 0.8f);

                if (label.Length > max_length)
                    label = label.Remove(max_length) + "..";

                Size = origin * 2;
                OriginPosition = origin;

                InternalChildren = new Drawable[]
                {
                    new HoverClickSounds(),
                    background = new Sprite { Colour = base_colour },
                    text = new OsuSpriteText
                    {
                        Origin = Anchor.Centre,
                        Position = origin,
                        Text = label,
                        Font = OsuFont.GetFont(size: 12 * 0.8f * LegacyUserPanel.StableText.EM * scale, weight: FontWeight.Bold),
                        Shadow = true,
                    },
                };
            }

            public Texture? Texture
            {
                set
                {
                    background.Texture = value;

                    if (value != null)
                        Size = new Vector2(value.DisplayWidth, value.DisplayHeight);

                    OriginPosition = origin;
                }
            }

            public bool Selected
            {
                get => selected;
                set
                {
                    if (selected == value)
                        return;

                    selected = value;
                    background.FadeColour(selected ? Color4.White : IsHovered ? hover_colour : base_colour, selected ? 100 : 20);
                    text.FadeColour(selected ? Color4.Black : Color4.White, 20);
                    text.Shadow = !selected;
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (!selected)
                    background.FadeColour(hover_colour, 80);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                if (!selected)
                    background.FadeColour(base_colour, 80);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                Action?.Invoke();
                return true;
            }
        }

        private partial class LegacyDropdown<T> : OsuDropdown<T>
        {
            // stable scales pText corners and borders by window height / 768 rather than its 640x480 space, which is lazer's 768 unit height.
            private const float corner_radius = 5;

            private readonly Color4 highlight;

            public float HeaderHeight => Header.DrawHeight;

            public LegacyDropdown(Color4 highlight, Color4? border = null, float textSize = 13, float width = 118)
            {
                this.highlight = highlight;

                Anchor = Anchor.TopRight;
                Origin = Anchor.TopLeft;
                Width = (width + 2) * scale;

                if (Header is LegacyDropdownHeader legacyHeader)
                {
                    legacyHeader.BorderColour = border ?? highlight.Darken(0.2f);
                    legacyHeader.TextSize = textSize;
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (Menu is OsuDropdownMenu menu)
                {
                    menu.BackgroundColour = Color4.Black.Opacity(240 / 255f);
                    menu.HoverColour = highlight;
                }
            }

            protected override LocalisableString GenerateItemText(T item) => item is Enum e ? e.GetLocalisableDescription() : base.GenerateItemText(item);

            protected override DropdownHeader CreateHeader() => new LegacyDropdownHeader();

            protected override DropdownMenu CreateMenu() => new LegacyDropdownMenu();

            /// <summary>
            /// stable's dropdown options: 17 units tall, text one size below the header, padded 12 units with the chevron at 6.
            /// </summary>
            private partial class LegacyDropdownMenu : OsuDropdownMenu
            {
                public LegacyDropdownMenu()
                {
                    CornerRadius = corner_radius;
                    MaskingContainer.CornerRadius = corner_radius;
                    ItemsContainer.Padding = new MarginPadding();
                }

                protected override DrawableDropdownMenuItem CreateDrawableDropdownMenuItem(MenuItem item) => new LegacyDropdownMenuItem(item)
                {
                    BackgroundColourHover = HoverColour,
                };

                // stable lays out every option at once, so the menu takes its full size immediately instead of growing (and scrolling to the selection meanwhile).
                protected override void UpdateSize(Vector2 newSize)
                {
                    Width = newSize.X;

                    // keep the height while closing, so the menu fades out in place.
                    if (State == MenuState.Open)
                        Height = newSize.Y;
                }

                protected override ScrollContainer<Drawable> CreateScrollContainer(Direction direction) => new FixedScrollContainer(direction);

                /// <summary>
                /// stable's dropdowns show all of their options and never scroll.
                /// </summary>
                private partial class FixedScrollContainer : BasicScrollContainer
                {
                    public FixedScrollContainer(Direction direction)
                        : base(direction)
                    {
                        ScrollbarVisible = false;
                        ClampExtension = 0;
                    }

                    protected override void Update()
                    {
                        base.Update();

                        // the dropdown scrolls its selection into view, which has to stay a no-op.
                        if (Current != 0)
                            ScrollToStart(false);
                    }

                    // swallow the wheel so the carousel below doesn't scroll either.
                    protected override bool OnScroll(ScrollEvent e) => true;

                    protected override bool OnDragStart(DragStartEvent e) => false;
                }

                private partial class LegacyDropdownMenuItem : DrawableOsuDropdownMenuItem
                {
                    private Content? content;

                    public LegacyDropdownMenuItem(MenuItem item)
                        : base(item)
                    {
                        Foreground.Padding = new MarginPadding();

                        // a mask here would replace the menu's rounded mask, leaving the first and last highlights square.
                        Masking = false;

                        // stable's 10pt chevron_right, centred at (6, 9) in every option and drawn in black, so it only shows on the highlight.
                        AddInternal(new SpriteIcon
                        {
                            Icon = FontAwesome.Solid.ChevronRight,
                            Origin = Anchor.Centre,
                            Position = new Vector2(6, 9) * scale,
                            // sized like the header's 14pt chevron.
                            Size = new Vector2(11 * scale * 10 / 14f),
                            Colour = Color4.Black,
                        });
                    }

                    protected override Drawable CreateContent()
                    {
                        content = (Content)base.CreateContent();

                        // stable draws each option into a fixed 17 unit row, offsetting its text by a (12, 3) padding that is scaled by window height / 768 like the corners.
                        // the content auto-sizes to its label, so the label carries the row height.
                        content.Label.Height = 17 * scale;
                        content.Label.Font = font(12);
                        content.Label.Shadow = true;
                        content.Label.Padding = new MarginPadding { Left = 12, Top = 3 };
                        // the base chevron slides in on hover; stable's static one is added by the item instead.
                        // its colour is set on load and its alpha on hover, so it is hidden by its scale.
                        content.Chevron.Scale = Vector2.Zero;

                        return content;
                    }

                    // stable only highlights the hovered option, instantly, and marks the selected one in bold instead.
                    protected override void UpdateBackgroundColour()
                    {
                        Background.ClearTransforms();
                        Background.Colour = BackgroundColourHover;
                        Background.Alpha = IsPreSelected ? 1 : 0;
                    }

                    protected override void UpdateForegroundColour()
                    {
                        base.UpdateForegroundColour();

                        if (content != null)
                            content.Label.Font = font(12, IsSelected ? FontWeight.Bold : FontWeight.Regular);
                    }
                }
            }

            private partial class LegacyDropdownHeader : OsuDropdownHeader
            {
                public float TextSize
                {
                    set => Text.Font = font(value);
                }

                public LegacyDropdownHeader()
                {
                    AutoSizeAxes = Axes.None;
                    Height = 15 * scale;
                    Margin = new MarginPadding();
                    CornerRadius = corner_radius;
                    BorderThickness = 1;
                    Foreground.Padding = new MarginPadding { Horizontal = 2 * scale };

                    // stable uses a 14pt chevron, about 11 units wide.
                    Chevron.Size = new Vector2(11 * scale);

                    // the base header recolours its background on hover; stable keeps it dark.
                    Background.Alpha = 0;
                    AddInternal(new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.5f),
                        Depth = float.MaxValue,
                    });
                }
            }
        }
    }
}
