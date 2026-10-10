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
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.BnTracker;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// The search and filters of the nominators.
    /// </summary>
    public partial class NominatorFilterSidebar : CompositeDrawable
    {
        public const float WIDTH = 300;

        [Resolved]
        private NominatorsSession session { get; set; } = null!;

        [Resolved]
        private NominatorFilter filter { get; set; } = null!;

        private readonly IBindable<BnBeatmapSetWithNominators?> data = new Bindable<BnBeatmapSetWithNominators?>();

        private SearchTextBox searchTextBox = null!;
        private FillFlowContainer modeFlow = null!;
        private SpokenLanguageDropdown spokenLanguageDropdown = null!;
        private OsuSpriteText activeFiltersText = null!;
        private RoundedButton clearButton = null!;

        /// <summary>
        /// The modes of the beatmap set which the mode checkboxes were created for.
        /// </summary>
        private BnGameMode[] displayedModes = Array.Empty<BnGameMode>();

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.Y;
            Width = WIDTH;

            InternalChild = new NominatorsPanel
            {
                RelativeSizeAxes = Axes.Both,
                Child = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Padding = new MarginPadding(12),
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(8, 0),
                                        Children = new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Text = SlopNominatorsStrings.Filters,
                                                Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
                                            },
                                            activeFiltersText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Font = OsuFont.Default.With(size: 14),
                                                Alpha = 0.7f,
                                            },
                                        }
                                    },
                                    clearButton = new RoundedButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Width = 90,
                                        Height = 26,
                                        Text = SlopNominatorsStrings.ClearFilters,
                                        Action = () => filter.Clear(),
                                    },
                                }
                            },
                            searchTextBox = new BasicSearchTextBox
                            {
                                RelativeSizeAxes = Axes.X,
                                PlaceholderText = SlopNominatorsStrings.SearchPlaceholder,
                                Current = filter.Search,
                            },
                            modeFlow = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                            },
                            new FormEnumDropdown<NominatorGroupFilter>
                            {
                                Caption = SlopNominatorsStrings.Group,
                                Current = filter.Group,
                            },
                            new FormEnumDropdown<NominatorStatusFilter>
                            {
                                Caption = SlopNominatorsStrings.Status,
                                Current = filter.Status,
                            },
                            spokenLanguageDropdown = new SpokenLanguageDropdown
                            {
                                Caption = SlopNominatorsStrings.SpokenLanguage,
                                Current = filter.SpokenLanguage,
                            },
                            new FormEnumDropdown<NominatorPreferenceFilter>
                            {
                                Caption = SlopNominatorsStrings.Genre,
                                HintText = SlopNominatorsStrings.GenreHint,
                                Current = filter.GenreMatch,
                            },
                            new FormEnumDropdown<NominatorPreferenceFilter>
                            {
                                Caption = SlopNominatorsStrings.SongLanguage,
                                HintText = SlopNominatorsStrings.SongLanguageHint,
                                Current = filter.LanguageMatch,
                            },
                            new FormEnumDropdown<NominatorLastOpenedFilter>
                            {
                                Caption = SlopNominatorsStrings.LastOpenedFilter,
                                HintText = SlopNominatorsStrings.LastOpenedFilterHint,
                                Current = filter.LastOpened,
                            },
                            new FormEnumDropdown<NominatorSortOrder>
                            {
                                Caption = SlopNominatorsStrings.Sort,
                                Current = filter.Sort,
                            },
                            new FormCheckBox
                            {
                                Caption = SlopNominatorsStrings.OpenOnly,
                                HintText = SlopNominatorsStrings.OpenOnlyHint,
                                Current = filter.OpenOnly,
                            },
                            new FormCheckBox
                            {
                                Caption = SlopNominatorsStrings.HideRemoved,
                                HintText = SlopNominatorsStrings.HideRemovedHint,
                                Current = filter.HideRemoved,
                            },
                        }
                    }
                }
            };

            clearButton.BackgroundColour = colourProvider.Background3;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            data.BindTo(session.Data);
            data.BindValueChanged(_ => updateOptions(), true);

            filter.Changed += onFilterChanged;
            onFilterChanged();
        }

        /// <summary>
        /// Focuses the search, such that typing searches immediately.
        /// </summary>
        public void FocusSearch() => GetContainingFocusManager()?.ChangeFocus(searchTextBox);

        private void onFilterChanged()
        {
            int count = filter.ActiveFilterCount;

            activeFiltersText.Text = count > 0 ? SlopNominatorsStrings.ActiveFilters(count) : default;

            // like on the website, only offered while there is something to clear.
            clearButton.Alpha = count > 0 || !string.IsNullOrEmpty(filter.Search.Value) || !filter.Sort.IsDefault ? 1 : 0;
        }

        private void updateOptions()
        {
            var value = data.Value;

            if (value == null)
                return;

            var modes = value.BeatmapSet.Modes.ToArray();

            if (!modes.SequenceEqual(displayedModes))
            {
                displayedModes = modes;

                // the modes of another beatmap set can't be filtered by.
                filter.Modes.RemoveAll(m => !modes.Contains(m));

                modeFlow.Clear();

                // like on the website, the modes can only be filtered by for hybrid beatmap sets.
                if (modes.Length > 1)
                {
                    modeFlow.Add(new OsuSpriteText
                    {
                        Text = SlopNominatorsStrings.Modes,
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                    });

                    foreach (var mode in modes)
                        modeFlow.Add(new ModeCheckBox(mode, filter.Modes, modes));
                }
            }

            var languages = value.Nominators
                                 .SelectMany(n => n.Nominator.SpokenLanguages)
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                                 .Prepend(string.Empty)
                                 .ToList();

            if (!languages.SequenceEqual(spokenLanguageDropdown.Items))
            {
                if (!languages.Contains(filter.SpokenLanguage.Value, StringComparer.OrdinalIgnoreCase))
                    filter.SpokenLanguage.SetDefault();

                spokenLanguageDropdown.Items = languages;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (filter.IsNotNull())
                filter.Changed -= onFilterChanged;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Whether nominators of a mode are shown.
        /// While all modes are ticked, the modes aren't filtered by. Unticking all modes also shows all nominators, like on the website.
        /// </summary>
        private partial class ModeCheckBox : CompositeDrawable
        {
            private readonly BnGameMode mode;
            private readonly BindableList<BnGameMode> selectedModes = new BindableList<BnGameMode>();
            private readonly IReadOnlyList<BnGameMode> allModes;

            private readonly BindableBool ticked = new BindableBool();

            private bool updating;

            public ModeCheckBox(BnGameMode mode, BindableList<BnGameMode> selectedModes, IReadOnlyList<BnGameMode> allModes)
            {
                this.mode = mode;
                this.allModes = allModes;

                this.selectedModes.BindTo(selectedModes);

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                InternalChild = new FormCheckBox
                {
                    Caption = NominatorsDisplay.GetName(mode),
                    Current = ticked,
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                selectedModes.BindCollectionChanged((_, _) => updateTicked(), true);

                ticked.BindValueChanged(t =>
                {
                    if (updating)
                        return;

                    // no selection means all modes.
                    var selected = selectedModes.Count == 0 ? allModes.ToList() : selectedModes.ToList();

                    if (t.NewValue)
                        selected.Add(mode);
                    else
                        selected.Remove(mode);

                    selected = selected.Distinct().ToList();

                    // all or none ticked are not filtering.
                    if (selected.Count == 0 || selected.Count == allModes.Count)
                        selectedModes.Clear();
                    else
                    {
                        selectedModes.Clear();
                        selectedModes.AddRange(selected);
                    }

                    // unticking the last mode shows all modes again.
                    updateTicked();
                });
            }

            private void updateTicked()
            {
                updating = true;
                ticked.Value = selectedModes.Count == 0 || selectedModes.Contains(mode);
                updating = false;
            }
        }

        private partial class SpokenLanguageDropdown : FormDropdown<string>
        {
            public SpokenLanguageDropdown()
            {
                Items = new[] { string.Empty };
            }

            protected override LocalisableString GenerateItemText(string item)
                => string.IsNullOrEmpty(item) ? SlopNominatorsStrings.AnySpokenLanguage : NominatorsDisplay.Capitalise(item);
        }
    }
}
