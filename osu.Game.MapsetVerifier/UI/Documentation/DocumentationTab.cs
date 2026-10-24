// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Server.Model;
using MapsetVerifier.Server.Service;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;
using MvMode = MapsetVerifier.Parser.Objects.Beatmap.Mode;

namespace osu.Game.MapsetVerifier.UI.Documentation
{
    public enum DocumentationModeFilter
    {
        All,
        Standard,
        Taiko,
        Catch,
        Mania,
    }

    /// <summary>
    /// The documentation of all checks: what they are for, why, and what they can find.
    /// </summary>
    public partial class DocumentationTab : VerifierTab
    {
        public const float LIST_WIDTH = 460;

        private static readonly MvMode[] all_modes = { MvMode.Standard, MvMode.Taiko, MvMode.Catch, MvMode.Mania };

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Bindable<DocumentationModeFilter> modeFilter = new Bindable<DocumentationModeFilter>();
        private readonly Bindable<int?> selectedCheck = new Bindable<int?>();

        private SearchTextBox searchBox = null!;
        private FillFlowContainer listFlow = null!;
        private LoadingSpinner listSpinner = null!;
        private Container detailsContainer = null!;

        private List<ApiDocumentationCheck>? generalChecks;
        private Dictionary<MvMode, List<ApiDocumentationCheck>>? beatmapChecks;

        private bool loadStarted;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, LIST_WIDTH),
                    new Dimension(GridSizeMode.Absolute, VerifierScreen.SPACING),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new VerifierPanel
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                RowDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 8),
                                            Padding = new MarginPadding(12),
                                            Children = new Drawable[]
                                            {
                                                searchBox = new SearchTextBox
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    PlaceholderText = VerifierStrings.SearchChecks,
                                                    HoldFocus = false,
                                                },
                                                new ModeDropdown
                                                {
                                                    Caption = VerifierStrings.GameMode,
                                                    Current = modeFilter,
                                                },
                                            }
                                        },
                                    },
                                    new Drawable[]
                                    {
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Children = new Drawable[]
                                            {
                                                new OsuScrollContainer
                                                {
                                                    RelativeSizeAxes = Axes.Both,
                                                    Child = listFlow = new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Direction = FillDirection.Vertical,
                                                        Spacing = new Vector2(0, 2),
                                                        Padding = new MarginPadding { Left = 12, Right = 20, Bottom = 12 },
                                                    }
                                                },
                                                listSpinner = new LoadingSpinner
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                },
                                            }
                                        },
                                    },
                                }
                            }
                        },
                        Empty(),
                        new VerifierPanel
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = detailsContainer = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                            }
                        },
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            searchBox.Current.BindValueChanged(_ => Scheduler.AddOnce(updateList));
            modeFilter.BindValueChanged(_ => Scheduler.AddOnce(updateList));
            selectedCheck.BindValueChanged(_ => updateDetails(), true);
        }

        public override void OnShown()
        {
            base.OnShown();

            if (loadStarted)
                return;

            loadStarted = true;

            // the mode of the selected difficulty is shown first, as its checks are the most relevant.
            var mode = Session.Results?.Get(Session.SelectedCategory)?.Mode;
            if (mode != null)
                modeFilter.Value = (DocumentationModeFilter)((int)mode.Value + 1);

            listSpinner.Show();

            VerifierEngine.RunAsync(() =>
            {
                var general = DocumentationService.GetGeneralDocumentation().ToList();
                var beatmap = all_modes.ToDictionary(m => m, m => DocumentationService.GetBeatmapDocumentation(m).ToList());
                return (general, beatmap);
            }, cancellation.Token).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Logger.Error(t.Exception, @"Mapset Verifier failed to load the documentation.");

                Schedule(() =>
                {
                    listSpinner.Hide();

                    if (!t.IsCompletedSuccessfully)
                        return;

                    (generalChecks, beatmapChecks) = t.GetResultSafely();
                    updateList();
                });
            }, TaskContinuationOptions.NotOnCanceled);
        }

        protected override void OnSessionChanged()
        {
        }

        public override bool OnBack()
        {
            if (searchBox.HasFocus)
            {
                GetContainingFocusManager()?.ChangeFocus(null);
                return true;
            }

            if (selectedCheck.Value != null)
            {
                selectedCheck.Value = null;
                return true;
            }

            // like the search of song select, a search is cleared before leaving.
            if (!string.IsNullOrEmpty(searchBox.Current.Value))
            {
                searchBox.Current.Value = string.Empty;
                return true;
            }

            return false;
        }

        private void updateList()
        {
            if (generalChecks == null || beatmapChecks == null)
                return;

            listFlow.Clear();

            string[] terms = searchBox.Current.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            bool matches(ApiDocumentationCheck check) => terms.All(term =>
                check.Description.Contains(term, StringComparison.OrdinalIgnoreCase)
                || check.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                || check.Author.Contains(term, StringComparison.OrdinalIgnoreCase));

            IEnumerable<ApiDocumentationCheck> difficultyChecks = modeFilter.Value == DocumentationModeFilter.All
                ? beatmapChecks.Values.SelectMany(c => c).DistinctBy(c => c.Id).OrderBy(c => c.Category).ThenBy(c => c.Description)
                : beatmapChecks[(MvMode)((int)modeFilter.Value - 1)];

            var shownGeneral = generalChecks.Where(matches).ToList();
            var shownDifficulty = difficultyChecks.Where(matches).ToList();

            if (shownGeneral.Count == 0 && shownDifficulty.Count == 0)
            {
                listFlow.Add(new VerifierMessage(FontAwesome.Solid.Search, VerifierStrings.NoChecksFound));
                return;
            }

            addSection(VerifierStrings.GeneralChecks, shownGeneral);
            addSection(VerifierStrings.DifficultyChecks, shownDifficulty);
        }

        private void addSection(LocalisableString title, List<ApiDocumentationCheck> checks)
        {
            if (checks.Count == 0)
                return;

            listFlow.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Margin = new MarginPadding { Top = listFlow.Count > 0 ? 12 : 0 },
                Children = new Drawable[]
                {
                    new VerifierTitle(title)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Text = checks.Count == 1 ? VerifierStrings.SingleCheck : VerifierStrings.ChecksCount(checks.Count),
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                        Colour = colourProvider.Content2,
                    },
                }
            });

            foreach (var group in checks.GroupBy(c => c.Category))
            {
                listFlow.Add(new VerifierSubtitle(group.Key) { Margin = new MarginPadding { Top = 8, Bottom = 2, Left = 4 } });

                foreach (var check in group)
                    listFlow.Add(new CheckRow(check, selectedCheck));
            }
        }

        private void updateDetails()
        {
            if (selectedCheck.Value is not int id)
            {
                detailsContainer.Child = new VerifierMessage(FontAwesome.Solid.Book, VerifierStrings.NoDocumentationSelected, VerifierStrings.NoDocumentationSelectedDescription);
                return;
            }

            var check = generalChecks?.Concat(beatmapChecks?.Values.SelectMany(c => c) ?? Enumerable.Empty<ApiDocumentationCheck>()).FirstOrDefault(c => c.Id == id);

            if (check == null)
                return;

            detailsContainer.Child = new CheckDetails(check.Value);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
        }

        private partial class ModeDropdown : FormEnumDropdown<DocumentationModeFilter>
        {
            protected override LocalisableString GenerateItemText(DocumentationModeFilter item)
            {
                if (item == DocumentationModeFilter.All)
                    return VerifierStrings.AllModes;

                return VerifierDisplay.GetName((MvMode)((int)item - 1));
            }
        }

        private partial class CheckRow : OsuClickableContainer
        {
            private readonly ApiDocumentationCheck check;
            private readonly Bindable<int?> selectedCheck = new Bindable<int?>();

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [Resolved]
            private RulesetStore rulesets { get; set; } = null!;

            private Box background = null!;
            private Box selectionBar = null!;

            public CheckRow(ApiDocumentationCheck check, Bindable<int?> selectedCheck)
            {
                this.check = check;
                this.selectedCheck.BindTo(selectedCheck);

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var levels = VerifierDisplay.OUTCOME_ORDER.Where(l => check.Outcomes.Contains(l)).Select(VerifierDisplay.GetDisplayLevel).Distinct().ToArray();

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                        Alpha = 0,
                    },
                    selectionBar = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = colourProvider.Highlight1,
                        Alpha = 0,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = 10, Vertical = 6 },
                        ColumnDimensions = new[]
                        {
                            // fits all five issue levels a check can report.
                            new Dimension(GridSizeMode.Absolute, 72),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3, 0),
                                    Margin = new MarginPadding { Top = 2 },
                                    ChildrenEnumerable = levels.Select(l => VerifierDisplay.CreateIcon(l, 11)),
                                },
                                new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 14))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = check.Description,
                                },
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3, 0),
                                    Margin = new MarginPadding { Left = 8, Top = 2 },
                                    Alpha = check.Modes.Length < all_modes.Length ? 0.6f : 0,
                                    ChildrenEnumerable = check.Modes.Select(m => VerifierDisplay.CreateIcon(rulesets, m, 12)),
                                },
                            }
                        }
                    },
                };

                Action = () => selectedCheck.Value = selectedCheck.Value == check.Id ? null : check.Id;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                selectedCheck.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                bool selected = selectedCheck.Value == check.Id;

                background.FadeTo(selected ? 1 : IsHovered ? 0.5f : 0, 150, Easing.OutQuint);
                selectionBar.FadeTo(selected ? 1 : 0, 150, Easing.OutQuint);
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
        }

        private partial class CheckDetails : CompositeDrawable
        {
            private readonly ApiDocumentationCheck check;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [Resolved]
            private RulesetStore rulesets { get; set; } = null!;

            /// <summary>
            /// The widest the documentation gets, such that its lines stay readable.
            /// </summary>
            private const float max_content_width = 820;

            private FillFlowContainer content = null!;

            public CheckDetails(ApiDocumentationCheck check)
            {
                this.check = check;
                RelativeSizeAxes = Axes.Both;
            }

            protected override void Update()
            {
                base.Update();

                float right = Math.Max(24, DrawWidth - 24 - max_content_width);

                if (content.Padding.Right != right)
                    content.Padding = new MarginPadding { Left = 24, Right = right, Vertical = 20 };
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = content = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                        Padding = new MarginPadding { Horizontal = 24, Vertical = 20 },
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(6),
                                Children = new Drawable[]
                                {
                                    new NominatorTag(check.Category, colourProvider.Highlight1, textSize: 11),
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(4, 0),
                                        ChildrenEnumerable = check.Modes.Select(m => VerifierDisplay.CreateIcon(rulesets, m, 14)),
                                    },
                                }
                            },
                            new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 24, weight: FontWeight.Bold))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = check.Description,
                            },
                            new OsuSpriteText
                            {
                                Text = VerifierStrings.Author(check.Author),
                                Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                                Colour = colourProvider.Content2,
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 1,
                                Colour = Color4.White,
                                Alpha = 0.1f,
                                Margin = new MarginPadding { Vertical = 4 },
                            },
                            new CheckDocumentation(check.Id),
                        }
                    }
                };
            }
        }
    }
}
