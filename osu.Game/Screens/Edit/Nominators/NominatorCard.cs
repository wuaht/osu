// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.BnTracker;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// A card of a nominator, showing their groups, whether they are open for requests, the latest comment and the status with them.
    /// Clicking it shows all details of the nominator.
    /// </summary>
    public partial class NominatorCard : OsuClickableContainer, IHasContextMenu
    {
        /// <summary>
        /// The minimum width of a card. Cards are wider to fill the available width.
        /// </summary>
        public const float MIN_WIDTH = 230;

        public const float HEIGHT = 140;

        private const float padding = 8;

        private const float avatar_size = 36;

        /// <summary>
        /// The opacity of the content of nominators who are no longer BN or NAT, such that the current ones stand out.
        /// </summary>
        private const float removed_alpha = 0.55f;

        public readonly int NominatorId;

        private BnSetNominator nominator;

        public BnSetNominator Nominator
        {
            get => nominator;
            set
            {
                nominator = value;

                if (IsLoaded)
                    updateDetails();
            }
        }

        [Resolved]
        private NominatorsScreen screen { get; set; } = null!;

        [Resolved]
        private NominatorsSession session { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly IBindableList<int> selection = new BindableList<int>();
        private readonly IBindable<bool> selectionMode = new Bindable<bool>();

        private Box statusStrip = null!;
        private Container content = null!;
        private Box hoverBox = null!;
        private Container selectionBorder = null!;
        private SpriteIcon selectionIcon = null!;
        private OsuSpriteText username = null!;
        private FillFlowContainer groups = null!;
        private FillFlowContainer tags = null!;
        private FillFlowContainer lastOpened = null!;
        private Container latestComment = null!;
        private Container statusChanged = null!;
        private StatusButton statusButton = null!;

        public NominatorCard(BnSetNominator nominator)
        {
            this.nominator = nominator;
            NominatorId = nominator.NominatorOsuId;

            Size = new Vector2(MIN_WIDTH, HEIGHT);
            Masking = true;
            CornerRadius = 8;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var user = NominatorsDisplay.CreateUser(nominator.Nominator);

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                new CoverBackground
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Model = user,
                },
                // covers can be bright and busy, so they are darkened enough to keep the text readable, especially further down.
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientVertical(colourProvider.Background4.Opacity(0.7f), colourProvider.Background4.Opacity(0.95f)),
                },
                hoverBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0,
                },
                statusStrip = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 4,
                },
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Vertical = padding, Left = padding + 4, Right = padding },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 5),
                            Children = new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = avatar_size,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.Absolute, avatar_size),
                                        new Dimension(GridSizeMode.Absolute, 8),
                                        new Dimension(),
                                        new Dimension(GridSizeMode.AutoSize),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[]
                                        {
                                            new UpdateableAvatar(user, false)
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Masking = true,
                                                CornerRadius = 5,
                                            },
                                            Empty(),
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 3),
                                                Children = new Drawable[]
                                                {
                                                    username = new TruncatingSpriteText
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                                                    },
                                                    groups = new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Full,
                                                        Spacing = new Vector2(4),
                                                    },
                                                }
                                            },
                                            selectionIcon = new SpriteIcon
                                            {
                                                Anchor = Anchor.TopRight,
                                                Origin = Anchor.TopRight,
                                                Size = new Vector2(14),
                                                Margin = new MarginPadding { Left = 6 },
                                                Alpha = 0,
                                            },
                                        }
                                    }
                                },
                                tags = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Full,
                                    Spacing = new Vector2(4),
                                },
                                lastOpened = createMetaRow(),
                                latestComment = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 14,
                                },
                            }
                        },
                        new GridContainer
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ColumnDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(),
                            },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    statusButton = new StatusButton(22)
                                    {
                                        StatusPicked = s => session.SetStatus(NominatorId, s),
                                    },
                                    statusChanged = new Container
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        AutoSizeAxes = Axes.Both,
                                    },
                                }
                            }
                        },
                    }
                },
                selectionBorder = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 3,
                    BorderColour = colourProvider.Highlight1,
                    Alpha = 0,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        AlwaysPresent = true,
                    },
                },
            };

            Action = onClick;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selection.BindTo(screen.Selection);
            selectionMode.BindTo(screen.SelectionMode);

            selection.BindCollectionChanged((_, _) => updateSelection());
            selectionMode.BindValueChanged(_ => updateSelection(), true);

            updateDetails();
        }

        private void onClick()
        {
            if (selectionMode.Value)
                screen.ToggleSelected(NominatorId);
            else
                screen.ShowDetails(NominatorId);
        }

        private void updateSelection()
        {
            bool selected = selection.Contains(NominatorId);

            selectionIcon.Alpha = selectionMode.Value ? 1 : 0;
            selectionIcon.Icon = selected ? FontAwesome.Solid.CheckSquare : FontAwesome.Regular.Square;
            selectionIcon.Colour = selected ? colourProvider.Highlight1 : Color4.White;
            selectionBorder.Alpha = selectionMode.Value && selected ? 1 : 0;
        }

        private void updateDetails()
        {
            var n = nominator.Nominator;

            statusStrip.Colour = n.IsRemoved ? NominatorsDisplay.REMOVED_COLOUR : NominatorsDisplay.GetColour(nominator.Status);
            content.Alpha = n.IsRemoved ? removed_alpha : 1;
            username.Text = n.Username;

            groups.Clear();
            foreach (var mode in nominator.RelevantModes)
                groups.Add(CreateGroupTag(rulesets, mode));

            tags.Clear();
            foreach (var tag in CreateTags(nominator))
                tags.Add(tag);

            lastOpened.Clear();
            lastOpened.Add(createMetaIcon(FontAwesome.Regular.Clock));

            if (n.LastOpenedForRequests is DateTimeOffset opened)
            {
                lastOpened.AddRange(new Drawable[]
                {
                    createMetaText(SlopNominatorsStrings.LastOpened),
                    new DrawableDate(opened, 11, false) { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                });
            }
            else
                lastOpened.Add(createMetaText(SlopNominatorsStrings.NeverOpened));

            var comment = nominator.LatestComment;

            latestComment.Child = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.Absolute, 5),
                    new Dimension(),
                    new Dimension(GridSizeMode.AutoSize),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        createMetaIcon(FontAwesome.Regular.Comment),
                        Empty(),
                        new TruncatingSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            Text = comment != null ? collapse(comment.Text) : SlopNominatorsStrings.NoComments,
                            Font = OsuFont.Default.With(size: 11, italics: comment == null),
                            Alpha = comment != null ? 1 : 0.6f,
                        },
                        nominator.Comments.Count > 1
                            ? createMetaText(SlopNominatorsStrings.MoreComments(nominator.Comments.Count - 1)).With(t => t.Margin = new MarginPadding { Left = 5 })
                            : Empty(),
                    }
                }
            };

            statusButton.Status = nominator.Status;
            statusButton.Enabled.Value = !n.IsRemoved;

            statusChanged.Child = nominator.StatusUpdatedAt is DateTimeOffset changed
                ? new DrawableDate(changed, 10, false) { Alpha = 0.6f }
                : new OsuSpriteText { Text = SlopNominatorsStrings.NotAskedYet, Font = OsuFont.Default.With(size: 10), Alpha = 0.6f };
        }

        /// <summary>
        /// Creates the tag of a group of a nominator in a mode, e.g. "BN" with the osu!taiko icon.
        /// </summary>
        public static NominatorTag CreateGroupTag(RulesetStore rulesets, BnModeLevel mode) =>
            new NominatorTag(NominatorsDisplay.GetName(mode.Level), NominatorsDisplay.GetColour(mode.Level), NominatorsDisplay.CreateIcon(rulesets, mode.Mode, 9))
            {
                TooltipText = LocalisableString.Interpolate($@"{NominatorsDisplay.GetName(mode.Level)} · {NominatorsDisplay.GetName(mode.Mode)}"),
            };

        /// <summary>
        /// Creates the tags of whether a nominator is open for requests, and whether they like the genre and language of the beatmap set.
        /// </summary>
        public static IEnumerable<NominatorTag> CreateTags(BnSetNominator nominator)
        {
            var n = nominator.Nominator;

            if (n.IsRemoved)
                yield return new NominatorTag(SlopNominatorsStrings.Removed, NominatorsDisplay.REMOVED_COLOUR) { TooltipText = SlopNominatorsStrings.RemovedTooltip };
            else if (n.IsOpenForRequests)
                yield return new NominatorTag(SlopNominatorsStrings.Open, NominatorsDisplay.OPEN_COLOUR) { TooltipText = SlopNominatorsStrings.OpenTooltip };
            else
                yield return new NominatorTag(SlopNominatorsStrings.Closed, NominatorsDisplay.CLOSED_COLOUR) { TooltipText = SlopNominatorsStrings.ClosedTooltip };

            if (nominator.PreferenceMatch.Genre != BnPreferenceMatch.Unknown)
            {
                bool matches = nominator.PreferenceMatch.Genre == BnPreferenceMatch.Matches;

                yield return new NominatorTag(SlopNominatorsStrings.Genre, matches ? Color4Extensions.FromHex(@"2dd4bf") : Color4Extensions.FromHex(@"f472b6"), createMatchIcon(matches))
                {
                    TooltipText = matches ? SlopNominatorsStrings.GenreMatchesTooltip : SlopNominatorsStrings.GenreExcludedTooltip,
                };
            }

            if (nominator.PreferenceMatch.Language != BnPreferenceMatch.Unknown)
            {
                bool matches = nominator.PreferenceMatch.Language == BnPreferenceMatch.Matches;

                yield return new NominatorTag(SlopNominatorsStrings.LanguageTag, matches ? Color4Extensions.FromHex(@"818cf8") : Color4Extensions.FromHex(@"fb7185"), createMatchIcon(matches))
                {
                    TooltipText = matches ? SlopNominatorsStrings.LanguageMatchesTooltip : SlopNominatorsStrings.LanguageExcludedTooltip,
                };
            }
        }

        private static SpriteIcon createMatchIcon(bool matches) => new SpriteIcon
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Size = new Vector2(8),
            Icon = matches ? FontAwesome.Solid.Check : FontAwesome.Solid.Times,
        };

        public MenuItem[] ContextMenuItems
        {
            get
            {
                var n = nominator.Nominator;

                var items = new List<MenuItem>
                {
                    new OsuMenuItem(SlopNominatorsStrings.ShowDetails, MenuItemType.Highlighted, () => screen.ShowDetails(NominatorId)),
                };

                if (!n.IsRemoved)
                {
                    items.Add(new OsuMenuItem(SlopNominatorsStrings.SetStatus)
                    {
                        Items = Enum.GetValues<BnNominationStatus>()
                                    .Select(s => (MenuItem)new OsuMenuItem(NominatorsDisplay.GetName(s), s == nominator.Status ? MenuItemType.Highlighted : MenuItemType.Standard,
                                        () => session.SetStatus(NominatorId, s)))
                                    .ToArray(),
                    });
                }

                if (!string.IsNullOrEmpty(n.RequestLink))
                    items.Add(new OsuMenuItem(SlopNominatorsStrings.OpenRequestQueue, MenuItemType.Standard, () => screen.OpenUrl(n.RequestLink, false)));

                items.Add(new OsuMenuItem(SlopNominatorsStrings.ViewProfile, MenuItemType.Standard, () => screen.ShowProfile(n)));

                return items.ToArray();
            }
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverBox.FadeTo(0.05f, 100);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverBox.FadeTo(0, 100);
            base.OnHoverLost(e);
        }

        private static FillFlowContainer createMetaRow() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            Height = 14,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(5, 0),
        };

        private static SpriteIcon createMetaIcon(IconUsage icon) => new SpriteIcon
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Size = new Vector2(10),
            Icon = icon,
            Alpha = 0.7f,
        };

        private static OsuSpriteText createMetaText(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = text,
            Font = OsuFont.Default.With(size: 11),
            Alpha = 0.7f,
        };

        /// <summary>
        /// Puts a comment on one line.
        /// </summary>
        private static string collapse(string text)
        {
            string collapsed = text.ReplaceLineEndings(@" ").Trim();
            return collapsed.Length <= 300 ? collapsed : collapsed[..300];
        }
    }
}
