// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.BnTracker;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// The cards of the nominators which match the filter, and the tools for changing the status of multiple nominators at once.
    /// </summary>
    public partial class NominatorGrid : CompositeDrawable
    {
        private const float top_bar_height = 50;
        private const float bulk_bar_height = 50;
        private const float card_spacing = 10;
        private const float horizontal_padding = 15;

        [Resolved]
        private NominatorsSession session { get; set; } = null!;

        [Resolved]
        private NominatorsScreen screen { get; set; } = null!;

        [Resolved]
        private NominatorFilter filter { get; set; } = null!;

        private readonly IBindable<BnBeatmapSetWithNominators?> data = new Bindable<BnBeatmapSetWithNominators?>();
        private readonly IBindableList<int> selection = new BindableList<int>();
        private readonly IBindable<bool> selectionMode = new Bindable<bool>();

        private readonly Dictionary<int, NominatorCard> cards = new Dictionary<int, NominatorCard>();

        private FillFlowContainer<NominatorCard> flow = null!;
        private OsuSpriteText countText = null!;
        private OsuSpriteText emptyText = null!;
        private RoundedButton selectButton = null!;
        private RoundedButton selectAllButton = null!;
        private Container bulkBar = null!;
        private OsuSpriteText selectedText = null!;
        private Container scrollContainer = null!;
        private RoundedButton clearSelectionButton = null!;

        private List<int> visibleIds = new List<int>();

        private float cardWidth = NominatorCard.MIN_WIDTH;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new NominatorsPanel
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = top_bar_height,
                        Padding = new MarginPadding { Horizontal = 15 },
                        Children = new Drawable[]
                        {
                            countText = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Font = OsuFont.Default.With(size: 15),
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = new Drawable[]
                                {
                                    selectAllButton = new RoundedButton
                                    {
                                        Width = 150,
                                        Height = 30,
                                        Text = SlopNominatorsStrings.SelectAllShown,
                                        Action = () => screen.SelectAll(visibleIds),
                                    },
                                    selectButton = new RoundedButton
                                    {
                                        Width = 150,
                                        Height = 30,
                                        Action = () => screen.SelectionMode.Toggle(),
                                    },
                                }
                            },
                        }
                    },
                    scrollContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = top_bar_height },
                        Children = new Drawable[]
                        {
                            new OsuScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = flow = new FillFlowContainer<NominatorCard>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Full,
                                    Spacing = new Vector2(card_spacing),
                                    Padding = new MarginPadding { Horizontal = horizontal_padding, Bottom = horizontal_padding },
                                },
                            },
                            emptyText = new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = SlopNominatorsStrings.NoNominatorsMatch,
                                Font = OsuFont.Default.With(size: 16),
                                Alpha = 0,
                            },
                        }
                    },
                    bulkBar = new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = bulk_bar_height,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = EditorPanelStyle.PanelBackground,
                            },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Padding = new MarginPadding { Horizontal = 15 },
                                Children = new Drawable[]
                                {
                                    selectedText = new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Font = OsuFont.Default.With(size: 15, weight: FontWeight.Bold),
                                        Margin = new MarginPadding { Right = 8 },
                                    },
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = SlopNominatorsStrings.SetStatusTo,
                                        Font = OsuFont.Default.With(size: 14),
                                    },
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(5, 0),
                                        ChildrenEnumerable = Enum.GetValues<BnNominationStatus>().Select(s => new BulkStatusButton(s)
                                        {
                                            Action = () => applyBulkStatus(s),
                                        }),
                                    },
                                    clearSelectionButton = new RoundedButton
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Width = 100,
                                        Height = 30,
                                        Margin = new MarginPadding { Left = 8 },
                                        Text = SlopNominatorsStrings.ClearSelection,
                                        Action = () => screen.Selection.Clear(),
                                    },
                                }
                            },
                        }
                    },
                }
            };

            // the secondary actions don't draw attention away from the cards.
            selectButton.BackgroundColour = colourProvider.Background3;
            selectAllButton.BackgroundColour = colourProvider.Background3;
            clearSelectionButton.BackgroundColour = colourProvider.Background3;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            data.BindTo(session.Data);
            selection.BindTo(screen.Selection);
            selectionMode.BindTo(screen.SelectionMode);

            data.BindValueChanged(_ => updateCards(), true);
            filter.Changed += onFilterChanged;

            selection.BindCollectionChanged((_, _) => updateSelection());
            selectionMode.BindValueChanged(_ => updateSelection(), true);
        }

        private void onFilterChanged() => Scheduler.AddOnce(applyFilter);

        protected override void Update()
        {
            base.Update();

            // as many cards as fit next to each other, sized to fill the width.
            float available = flow.DrawWidth - horizontal_padding * 2;
            int columns = Math.Max(1, (int)((available + card_spacing) / (NominatorCard.MIN_WIDTH + card_spacing)));
            float width = MathF.Floor(Math.Max(NominatorCard.MIN_WIDTH, (available - card_spacing * (columns - 1)) / columns));

            if (width == cardWidth)
                return;

            cardWidth = width;

            foreach (var card in cards.Values)
                card.Width = cardWidth;
        }

        private void updateCards()
        {
            var nominators = data.Value?.Nominators ?? new List<BnSetNominator>();
            var ids = nominators.Select(n => n.NominatorOsuId).ToHashSet();

            foreach (int id in cards.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                flow.Remove(cards[id], true);
                cards.Remove(id);
            }

            foreach (var nominator in nominators)
            {
                if (cards.TryGetValue(nominator.NominatorOsuId, out var card))
                    card.Nominator = nominator;
                else
                    flow.Add(cards[nominator.NominatorOsuId] = new NominatorCard(nominator) { Width = cardWidth });
            }

            applyFilter();
        }

        private void applyFilter()
        {
            var nominators = data.Value?.Nominators ?? new List<BnSetNominator>();

            visibleIds = filter.Apply(nominators, DateTimeOffset.Now).Select(n => n.NominatorOsuId).ToList();

            var visible = visibleIds.ToHashSet();

            foreach (var (id, card) in cards)
                card.Alpha = visible.Contains(id) ? 1 : 0;

            for (int i = 0; i < visibleIds.Count; i++)
                flow.SetLayoutPosition(cards[visibleIds[i]], i);

            countText.Text = SlopNominatorsStrings.ShowingNominators(visibleIds.Count, nominators.Count);
            emptyText.Alpha = visibleIds.Count == 0 ? 1 : 0;
        }

        private void updateSelection()
        {
            selectButton.Text = selectionMode.Value ? SlopNominatorsStrings.CancelSelection : SlopNominatorsStrings.SelectMultiple;
            selectAllButton.Alpha = selectionMode.Value ? 1 : 0;

            bool showBulkBar = selectionMode.Value && selection.Count > 0;

            bulkBar.FadeTo(showBulkBar ? 1 : 0, 100);
            scrollContainer.Padding = new MarginPadding { Top = top_bar_height, Bottom = showBulkBar ? bulk_bar_height : 0 };

            selectedText.Text = SlopNominatorsStrings.SelectedCount(selection.Count);
        }

        private void applyBulkStatus(BnNominationStatus status)
        {
            session.SetStatuses(selection.ToArray(), status);
            screen.SelectionMode.Value = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (filter.IsNotNull())
                filter.Changed -= onFilterChanged;

            base.Dispose(isDisposing);
        }

        private partial class BulkStatusButton : OsuClickableContainer
        {
            private readonly Box background;

            public BulkStatusButton(BnNominationStatus status)
            {
                AutoSizeAxes = Axes.X;
                Height = 26;
                Masking = true;
                CornerRadius = 13;

                var colour = NominatorsDisplay.GetColour(status);

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colour,
                        Alpha = 0.25f,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = NominatorsDisplay.GetName(status),
                        Colour = colour,
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                        Margin = new MarginPadding { Horizontal = 10 },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeTo(0.45f, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeTo(0.25f, 100);
                base.OnHoverLost(e);
            }
        }
    }
}
