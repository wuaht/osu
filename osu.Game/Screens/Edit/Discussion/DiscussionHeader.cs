// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The header of the discussion page: the rulesets, the beatmap set and the beatmap picker with its stats.
    /// </summary>
    public partial class DiscussionHeader : DiscussionSection
    {
        private const float cover_height = 220;
        private const float horizontal_padding = 20;

        private FillFlowContainer rulesetTabs = null!;
        private Container coverContainer = null!;
        private LinkFlowContainer title = null!;
        private OsuSpriteText artist = null!;
        private LinkFlowContainer mappedBy = null!;
        private FillFlowContainer badges = null!;
        private LinkFlowContainer dates = null!;
        private FillFlowContainer deletedBanner = null!;
        private BeatmapDropdown dropdown = null!;
        private OsuSpriteText unresolvedCount = null!;
        private LinkFlowContainer guestBy = null!;
        private FillFlowContainer stats = null!;
        private HeaderButton watchButton = null!;
        private HeaderButton changeOwnersButton = null!;

        private string? displayedCover;
        private bool updatingDropdown;

        public DiscussionHeader()
        {
            // the beatmap picker expands over the sections below.
            Depth = -1;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new NominatorsPanel(Axes.Y)
            {
                RelativeSizeAxes = Axes.X,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = ColourProvider.Background4,
                                    },
                                    rulesetTabs = new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(20, 0),
                                        Padding = new MarginPadding { Horizontal = horizontal_padding },
                                    },
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = cover_height,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = ColourProvider.Background6,
                                    },
                                    coverContainer = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Alpha = 0.6f,
                                    },
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.1f), Color4.Black.Opacity(0.75f)),
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(8, 0),
                                        Margin = new MarginPadding { Horizontal = horizontal_padding, Vertical = 15 },
                                        Children = new Drawable[]
                                        {
                                            changeOwnersButton = new HeaderButton(FontAwesome.Solid.UserEdit, SlopDiscussionStrings.ChangeOwnersButton, () =>
                                            {
                                                if (State?.CurrentBeatmap is DiscussionBeatmap beatmap)
                                                    Screen.ChangeOwners(beatmap);
                                            }),
                                            watchButton = new HeaderButton(FontAwesome.Solid.Eye, SlopDiscussionStrings.Watch, () => Screen.ToggleWatch()),
                                            new HeaderButton(FontAwesome.Solid.Info, SlopDiscussionStrings.BeatmapPage, () =>
                                            {
                                                if (State != null)
                                                    Screen.HandleLink($@"{Screen.WebsiteUrl}/beatmapsets/{State.BeatmapSet.Id}");
                                            }),
                                        }
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.BottomLeft,
                                        Origin = Anchor.BottomLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                        Padding = new MarginPadding { Horizontal = horizontal_padding, Vertical = 15 },
                                        Children = new Drawable[]
                                        {
                                            badges = new FillFlowContainer
                                            {
                                                AutoSizeAxes = Axes.Both,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(5, 0),
                                                Margin = new MarginPadding { Bottom = 5 },
                                            },
                                            title = new LinkFlowContainer(s => s.Font = OsuFont.Default.With(size: 28, weight: FontWeight.Bold))
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                            },
                                            artist = new TruncatingSpriteText
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                Font = OsuFont.Default.With(size: 18, weight: FontWeight.SemiBold),
                                            },
                                            mappedBy = new LinkFlowContainer(s => s.Font = OsuFont.Default.With(size: 14))
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Margin = new MarginPadding { Top = 6 },
                                            },
                                            dates = new LinkFlowContainer(s =>
                                            {
                                                s.Font = OsuFont.Default.With(size: 12);
                                                s.Colour = ColourProvider.Content2;
                                            })
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                            },
                                        }
                                    },
                                }
                            },
                            deletedBanner = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Padding = new MarginPadding { Horizontal = horizontal_padding, Vertical = 8 },
                                Children = new Drawable[]
                                {
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Icon = FontAwesome.Solid.TrashAlt,
                                        Size = new Vector2(13),
                                        Colour = DiscussionDisplay.RED_1,
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = SlopDiscussionStrings.Deleted,
                                        Colour = DiscussionDisplay.RED_1,
                                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = SlopDiscussionStrings.DeletedModeratorsOnly,
                                        Colour = ColourProvider.Content2,
                                        Font = OsuFont.Default.With(size: 12),
                                    },
                                }
                            },
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Horizontal = horizontal_padding, Vertical = 12 },
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 6),
                                        Children = new Drawable[]
                                        {
                                            new FillFlowContainer
                                            {
                                                AutoSizeAxes = Axes.Both,
                                                Direction = FillDirection.Horizontal,
                                                Spacing = new Vector2(12, 0),
                                                Children = new Drawable[]
                                                {
                                                    dropdown = new BeatmapDropdown
                                                    {
                                                        Anchor = Anchor.CentreLeft,
                                                        Origin = Anchor.CentreLeft,
                                                        Width = 380,
                                                    },
                                                    unresolvedCount = new OsuSpriteText
                                                    {
                                                        Anchor = Anchor.CentreLeft,
                                                        Origin = Anchor.CentreLeft,
                                                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                                                        Colour = DiscussionDisplay.RED_1,
                                                    },
                                                }
                                            },
                                            guestBy = new LinkFlowContainer(s =>
                                            {
                                                s.Font = OsuFont.Default.With(size: 13);
                                                s.Colour = ColourProvider.Content2;
                                            })
                                            {
                                                AutoSizeAxes = Axes.Both,
                                            },
                                        }
                                    },
                                    stats = new FillFlowContainer
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(18, 0),
                                        Margin = new MarginPadding { Top = 8 },
                                    },
                                }
                            },
                        }
                    },
                }
            };

            dropdown.Current.BindValueChanged(b =>
            {
                if (updatingDropdown || State == null || b.NewValue == null || b.NewValue.Id == State.CurrentBeatmapId)
                    return;

                State.ChangeBeatmap(b.NewValue.Id);
            });
        }

        protected override void OnStateReplaced(DiscussionsState newState)
        {
            var beatmapSet = newState.BeatmapSet;

            string? cover = beatmapSet.GetCover(@"cover@2x") ?? beatmapSet.GetCover(@"cover");

            if (cover != displayedCover)
            {
                displayedCover = cover;
                coverContainer.Clear();

                if (cover != null)
                    LoadComponentAsync(new UrlBackground(cover), coverContainer.Add);
            }

            title.Clear();
            title.AddLink(beatmapSet.TitleUnicode ?? beatmapSet.Title, () => Screen.HandleLink($@"{Screen.WebsiteUrl}/beatmapsets/{beatmapSet.Id}"));
            artist.Text = beatmapSet.ArtistUnicode ?? beatmapSet.Artist;

            mappedBy.Clear();
            DiscussionDisplay.AddPattern(mappedBy, SlopDiscussionStrings.MappedBy, new Dictionary<string, System.Action<LinkFlowContainer>>
            {
                [@"0"] = f =>
                {
                    if (newState.GetUser(beatmapSet.UserId) != null)
                        DiscussionLinks.AddUser(f, newState, beatmapSet.UserId, s => s.Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold));
                    else
                        f.AddText(beatmapSet.Creator, s => s.Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold));
                },
            });

            badges.Clear();
            badges.Add(new DiscussionPill(statusName(beatmapSet.Status).ToString().ToUpperInvariant(), Color4.Black.Opacity(0.5f), statusColour(beatmapSet.Status)));

            if (beatmapSet.Nsfw)
                badges.Add(new DiscussionPill(SlopDiscussionStrings.BadgeNsfw, DiscussionDisplay.ORANGE_2, Color4.Black));

            if (beatmapSet.Spotlight)
                badges.Add(new DiscussionPill(SlopDiscussionStrings.BadgeSpotlight, DiscussionDisplay.PINK_1, Color4.Black));

            if (beatmapSet.TrackId != null)
                badges.Add(new DiscussionPill(SlopDiscussionStrings.BadgeFeaturedArtist, DiscussionDisplay.BLUE_1, Color4.Black));

            dates.Clear();

            if (beatmapSet.SubmittedDate is System.DateTimeOffset submitted)
                addDate(SlopDiscussionStrings.DateSubmitted, submitted);

            if (beatmapSet.RankedDate is System.DateTimeOffset ranked)
            {
                switch (beatmapSet.Status)
                {
                    case @"approved":
                        addDate(SlopDiscussionStrings.DateApproved, ranked);
                        break;

                    case @"loved":
                        addDate(SlopDiscussionStrings.DateLoved, ranked);
                        break;

                    case @"qualified":
                        addDate(SlopDiscussionStrings.DateQualified, ranked);
                        break;

                    case @"ranked":
                        addDate(SlopDiscussionStrings.DateRanked, ranked);
                        break;
                }
            }
            else if (beatmapSet.LastUpdated is System.DateTimeOffset updated)
                addDate(SlopDiscussionStrings.DateUpdated, updated);

            deletedBanner.Alpha = beatmapSet.DeletedAt != null ? 1 : 0;
        }

        private void addDate(TranslatableString pattern, System.DateTimeOffset date)
        {
            if (dates.Children.Any())
                dates.AddText(@"  ·  ");

            DiscussionDisplay.AddPattern(dates, pattern, new Dictionary<string, System.Action<LinkFlowContainer>>
            {
                [@"0"] = f => f.AddArbitraryDrawable(new DrawableDate(date, 12, false) { Colour = ColourProvider.Content1 }),
            });
        }

        protected override void UpdateDisplay(DiscussionsState state)
        {
            var current = state.CurrentBeatmap;
            var attributes = state.BeatmapSet.CurrentUserAttributes;

            bool watching = attributes?.IsWatching == true;
            watchButton.Icon = watching ? FontAwesome.Solid.EyeSlash : FontAwesome.Solid.Eye;
            watchButton.Text = watching ? SlopDiscussionStrings.Unwatch : SlopDiscussionStrings.Watch;
            watchButton.Alpha = Screen.IsSignedIn ? 1 : 0;

            changeOwnersButton.Alpha = attributes?.CanBeatmapUpdateOwner == true && current != null && current.DeletedAt == null ? 1 : 0;

            rulesetTabs.Clear();

            foreach (var group in state.GroupedBeatmaps)
            {
                string ruleset = group.Ruleset;

                rulesetTabs.Add(new RulesetTab(ruleset, state.UnresolvedCountOfRuleset(ruleset) ?? 0, current?.Mode == ruleset, group.Beatmaps.All(b => b.DeletedAt != null))
                {
                    Action = () => state.ChangeGameMode(ruleset),
                });
            }

            updatingDropdown = true;

            var beatmaps = current != null ? state.BeatmapsOfRuleset(current.Mode) : new List<DiscussionBeatmap>();
            dropdown.State = state;
            dropdown.Items = beatmaps;

            if (current != null)
                dropdown.Current.Value = current;

            updatingDropdown = false;

            int unresolved = current != null ? state.UnresolvedCountOfBeatmap(current.Id) : 0;
            unresolvedCount.Text = unresolved == 0
                ? default(LocalisableString)
                : unresolved == 1
                    ? SlopDiscussionStrings.UnresolvedCountOne(DiscussionDisplay.FormatNumber(unresolved))
                    : SlopDiscussionStrings.UnresolvedCountMany(DiscussionDisplay.FormatNumber(unresolved));

            guestBy.Clear();

            if (current != null && state.HasGuestOwners(current) && current.Owners != null)
            {
                DiscussionDisplay.AddPattern(guestBy, SlopDiscussionStrings.GuestDifficultyBy, new Dictionary<string, System.Action<LinkFlowContainer>>
                {
                    [@"0"] = f =>
                    {
                        for (int i = 0; i < current.Owners.Count; i++)
                        {
                            if (i > 0)
                                f.AddText(@", ");

                            var owner = current.Owners[i];

                            if (state.GetUser(owner.Id) != null)
                                DiscussionLinks.AddUser(f, state, owner.Id, s => s.Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold));
                            else
                                f.AddText(owner.Username, s => s.Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold));
                        }
                    },
                });
            }

            stats.Clear();

            if (current != null)
            {
                stats.AddRange(new Drawable[]
                {
                    new StatItem(BeatmapStatisticsIconType.Length, DiscussionDisplay.FormatDuration(current.TotalLength),
                        SlopDiscussionStrings.StatLength(DiscussionDisplay.FormatDuration(current.HitLength))),
                    new StatItem(BeatmapStatisticsIconType.Bpm, DiscussionDisplay.FormatNumber(current.Bpm),
                        state.BeatmapSet.Offset == 0 ? SlopDiscussionStrings.StatBpm : SlopDiscussionStrings.StatBpmOffset(DiscussionDisplay.FormatNumber(state.BeatmapSet.Offset))),
                    new StatItem(BeatmapStatisticsIconType.Circles, DiscussionDisplay.FormatNumber(current.CountCircles), SlopDiscussionStrings.StatCircles),
                    new StatItem(BeatmapStatisticsIconType.Sliders, DiscussionDisplay.FormatNumber(current.CountSliders), SlopDiscussionStrings.StatSliders),
                });
            }
        }

        private static LocalisableString statusName(string status)
        {
            switch (status)
            {
                case @"wip":
                    return BeatmapsetsStrings.ShowStatusWip;

                case @"pending":
                    return BeatmapsetsStrings.ShowStatusPending;

                case @"graveyard":
                    return BeatmapsetsStrings.ShowStatusGraveyard;

                case @"approved":
                    return BeatmapsetsStrings.ShowStatusApproved;

                case @"qualified":
                    return BeatmapsetsStrings.ShowStatusQualified;

                case @"loved":
                    return BeatmapsetsStrings.ShowStatusLoved;

                case @"ranked":
                    return BeatmapsetsStrings.ShowStatusRanked;

                default:
                    return status;
            }
        }

        private Color4 statusColour(string status)
        {
            switch (status)
            {
                case @"ranked":
                case @"approved":
                    return DiscussionDisplay.LIME_1;

                case @"qualified":
                    return DiscussionDisplay.BLUE_1;

                case @"loved":
                    return DiscussionDisplay.PINK_1;

                case @"pending":
                case @"wip":
                    return DiscussionDisplay.ORANGE_1;

                default:
                    return ColourProvider.Content2;
            }
        }

        private partial class BeatmapDropdown : OsuDropdown<DiscussionBeatmap>
        {
            public DiscussionsState? State;

            protected override LocalisableString GenerateItemText(DiscussionBeatmap item)
            {
                int unresolved = State?.UnresolvedCountOfBeatmap(item.Id) ?? 0;
                string text = $@"{item.Version} ({item.DifficultyRating:0.00}★)";

                if (unresolved > 0)
                    text += $@" · {unresolved}";

                if (item.DeletedAt != null)
                    text += @" ✕";

                return text;
            }
        }

        private partial class RulesetTab : OsuClickableContainer
        {
            private readonly string ruleset;
            private readonly int unresolved;
            private readonly bool selected;
            private readonly bool allDeleted;

            private FillFlowContainer content = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public RulesetTab(string ruleset, int unresolved, bool selected, bool allDeleted)
            {
                this.ruleset = ruleset;
                this.unresolved = unresolved;
                this.selected = selected;
                this.allDeleted = allDeleted;

                AutoSizeAxes = Axes.X;
                Height = 40;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    content = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = DiscussionDisplay.RulesetIcon(ruleset),
                                Size = new Vector2(14),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = DiscussionDisplay.RulesetName(ruleset),
                                Font = OsuFont.Default.With(size: 14, weight: selected ? FontWeight.Bold : FontWeight.SemiBold),
                            },
                            new CircularContainer
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                AutoSizeAxes = Axes.X,
                                Height = 16,
                                Masking = true,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = unresolved > 0 ? DiscussionDisplay.RED_3 : colourProvider.Background2,
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.Centre,
                                        Origin = Anchor.Centre,
                                        Margin = new MarginPadding { Horizontal = 6 },
                                        Text = DiscussionDisplay.FormatNumber(unresolved),
                                        Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                                    },
                                }
                            },
                        }
                    },
                    new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 3,
                        Colour = colourProvider.Highlight1,
                        Alpha = selected ? 1 : 0,
                    },
                };

                Enabled.Value = !selected;
                content.Alpha = allDeleted ? 0.5f : 1;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateColour();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateColour();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateColour();
                base.OnHoverLost(e);
            }

            private void updateColour() => content.FadeColour(selected || IsHovered ? Color4.White : colourProvider.Content2, 100, Easing.OutQuint);
        }

        private partial class StatItem : FillFlowContainer, IHasTooltip
        {
            public LocalisableString TooltipText { get; }

            public StatItem(BeatmapStatisticsIconType icon, string value, LocalisableString tooltip)
            {
                TooltipText = tooltip;

                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(5, 0);

                Children = new Drawable[]
                {
                    new BeatmapStatisticIcon(icon)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(18),
                        Colour = DiscussionDisplay.ORANGE_1,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = value,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                    },
                };
            }
        }

        private partial class HeaderButton : OsuClickableContainer
        {
            private IconUsage icon;
            private LocalisableString text;

            private Box background = null!;
            private SpriteIcon iconSprite = null!;
            private OsuSpriteText textSprite = null!;

            public IconUsage Icon
            {
                set
                {
                    icon = value;

                    if (IsLoaded)
                        iconSprite.Icon = value;
                }
            }

            public LocalisableString Text
            {
                set
                {
                    text = value;

                    if (IsLoaded)
                        textSprite.Text = value;
                }
            }

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public HeaderButton(IconUsage icon, LocalisableString text, System.Action action)
            {
                this.icon = icon;
                this.text = text;

                Action = action;
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black.Opacity(0.5f),
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(6, 0),
                        Padding = new MarginPadding { Horizontal = 10, Vertical = 6 },
                        Children = new Drawable[]
                        {
                            iconSprite = new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = icon,
                                Size = new Vector2(12),
                            },
                            textSprite = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = text,
                                Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                            },
                        }
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeColour(colourProvider.Background3, 100, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(Color4.Black.Opacity(0.5f), 100, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }
    }
}
