// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select.Filter;
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

        private static FontUsage font(float stableSize, FontWeight weight = FontWeight.Regular) => OsuFont.GetFont(size: stableSize * scale, weight: weight);

        private readonly FilterControl filterControl;
        private readonly BeatmapDetailsArea detailsArea;

        private LegacyDropdown<BeatmapLeaderboardScope> scopeDropdown = null!;

        private readonly Bindable<string> query = new Bindable<string>(string.Empty);

        private OsuSpriteText queryText = null!;
        private OsuSpriteText statusText = null!;
        private Container noRecords = null!;
        private bool showingNoRecords;

        // stable's "selection-norecords" box, centred at (118, 200).
        private static readonly Vector2 no_records_position = new Vector2(13, 187.6f) * scale;

        private LocalisableString status;

        /// <summary>
        /// Whether the display is in use (a legacy skin is active).
        /// </summary>
        public bool InUse { get; set; }

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

        public LegacyFilterDisplay(FilterControl filterControl, BeatmapDetailsArea detailsArea)
        {
            this.filterControl = filterControl;
            this.detailsArea = detailsArea;
            RelativeSizeAxes = Axes.Both;
            // keeps updating so it can follow the filter control's visibility.
            AlwaysPresent = true;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var groupDropdown = new LegacyDropdown<GroupMode>(new Color4(146, 195, 230, 255))
            {
                Position = new Vector2(-352, 18) * scale,
                Items = Enum.GetValues<GroupMode>(),
            };

            var sortDropdown = new LegacyDropdown<SortMode>(new Color4(174, 210, 139, 255))
            {
                Position = new Vector2(-130, 18) * scale,
                Items = Enum.GetValues<SortMode>(),
            };

            scopeDropdown = new LegacyDropdown<BeatmapLeaderboardScope>(new Color4(31, 184, 255, 255), new Color4(31, 184, 255, 180), 14, 190)
            {
                Anchor = Anchor.TopLeft,
                Position = new Vector2(5, 73.5f) * scale,
                Items = Enum.GetValues<BeatmapLeaderboardScope>(),
            };

            config.BindWith(OsuSetting.SongSelectGroupMode, groupDropdown.Current);
            config.BindWith(OsuSetting.SongSelectSortingMode, sortDropdown.Current);

            InternalChildren = new Drawable[]
            {
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
                noRecords = new Container
                {
                    Position = no_records_position,
                    Size = new Vector2(210, 25) * scale,
                    Alpha = 0,
                    Masking = true,
                    BorderColour = Color4.White,
                    BorderThickness = 1.5f * scale,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(20, 20, 20, 255),
                        },
                        new SpriteIcon
                        {
                            Origin = Anchor.Centre,
                            Position = new Vector2(15.5f, 12.5f) * scale,
                            Size = new Vector2(14 * scale),
                            Icon = FontAwesome.Solid.Trophy,
                        },
                        new OsuSpriteText
                        {
                            Origin = Anchor.Centre,
                            Position = new Vector2(116.5f, 12) * scale,
                            Text = "No records set!",
                            Font = font(18, FontWeight.Light),
                            Colour = new Color4(47, 198, 223, 255),
                        },
                    }
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

            query.BindTo(filterControl.SearchQuery);
            query.BindValueChanged(q =>
            {
                queryText.Text = string.IsNullOrEmpty(q.NewValue) ? "Type to search!" : q.NewValue;
                updateStatus();
            }, true);
        }

        private void updateStatus() => statusText.Text = string.IsNullOrEmpty(query.Value) ? default : status;

        protected override void Update()
        {
            base.Update();

            // follow the filter control when the carousel hides the wedges.
            Alpha = InUse ? filterControl.Alpha : 0;

            if (detailsArea.ShowsNoScores == showingNoRecords)
                return;

            showingNoRecords = detailsArea.ShowsNoScores;

            // stable slides the box down into place as it fades in.
            if (showingNoRecords)
                noRecords.MoveToY(no_records_position.Y - 30 * scale).MoveToY(no_records_position.Y, 400, Easing.Out).FadeIn(400);
            else
                noRecords.FadeOut(150, Easing.OutQuint);
        }

        private partial class LegacyDropdown<T> : OsuDropdown<T>
        {
            private readonly Color4 highlight;

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

                Menu.MaxHeight = 300 * scale;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (Menu is OsuDropdownMenu menu)
                {
                    menu.BackgroundColour = Color4.Black.Opacity(240 / 255f);
                    menu.HoverColour = highlight;
                    menu.SelectionColour = highlight.Opacity(0.5f);
                }
            }

            protected override LocalisableString GenerateItemText(T item) => item is Enum e ? e.GetLocalisableDescription() : base.GenerateItemText(item);

            protected override DropdownHeader CreateHeader() => new LegacyDropdownHeader();

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
                    CornerRadius = 2 * scale;
                    BorderThickness = scale;
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
