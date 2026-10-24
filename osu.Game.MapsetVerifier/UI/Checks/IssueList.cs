// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.OSD;
using osuTK;
using MvDifficulty = MapsetVerifier.Parser.Objects.Beatmap.Difficulty;

namespace osu.Game.MapsetVerifier.UI.Checks
{
    /// <summary>
    /// The issues of the selected category, grouped by the check which found them.
    /// </summary>
    public partial class IssueList : CompositeDrawable
    {
        /// <summary>
        /// How many issues are copied without asking first.
        /// </summary>
        private const int copy_confirmation_threshold = 25;

        [Resolved]
        private VerifierScreen screen { get; set; } = null!;

        [Resolved]
        private IssueFilter filter { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        private OsuSpriteText titleText = null!;
        private OsuSpriteText countText = null!;
        private LevelDropdown levelDropdown = null!;
        private Container levelDropdownContainer = null!;
        private LoadingSpinner reinterpretingSpinner = null!;
        private RoundedButton copyButton = null!;
        private OsuScrollContainer scroll = null!;
        private GridContainer header = null!;
        private Container issuesContainer = null!;
        private FillFlowContainer<CheckCard> cardFlow = null!;
        private VerifierMessage message = null!;
        private LoadingSpinner loadingSpinner = null!;

        private readonly Bindable<MvDifficulty> level = new Bindable<MvDifficulty>();
        private bool updatingLevel;

        /// <summary>
        /// The checks of which all issues are shown, per category.
        /// </summary>
        private readonly HashSet<(string Category, int CheckId)> expandedChecks = new HashSet<(string, int)>();

        private VerifierSession? session;
        private VerifierCategory? displayedCategory;
        private List<VerifierIssue>? displayedIssues;
        private List<VerifierIssue> shownIssues = new List<VerifierIssue>();

        public IssueList()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new VerifierPanel
            {
                RelativeSizeAxes = Axes.Both,
                // the header is drawn above the issues, so the open level dropdown overlaps them instead of making the header taller.
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        header = new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Depth = -1,
                            Padding = new MarginPadding { Horizontal = 16, Top = 12, Bottom = 8 },
                            ColumnDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize, minSize: 30) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(10, 0),
                                        Children = new Drawable[]
                                        {
                                            titleText = new TruncatingSpriteText
                                            {
                                                Anchor = Anchor.BottomLeft,
                                                Origin = Anchor.BottomLeft,
                                                Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
                                            },
                                            countText = new OsuSpriteText
                                            {
                                                Anchor = Anchor.BottomLeft,
                                                Origin = Anchor.BottomLeft,
                                                Margin = new MarginPadding { Bottom = 2 },
                                                Font = OsuFont.Default.With(size: 14),
                                                Alpha = 0.7f,
                                            },
                                        }
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
                                            reinterpretingSpinner = new LoadingSpinner
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Size = new Vector2(20),
                                            },
                                            // form controls are relatively sized, so they need a fixed width parent inside an auto-sized flow.
                                            levelDropdownContainer = new Container
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Width = 220,
                                                AutoSizeAxes = Axes.Y,
                                                Child = levelDropdown = new LevelDropdown
                                                {
                                                    Caption = VerifierStrings.InterpretedAs,
                                                    Current = level,
                                                },
                                            },
                                            copyButton = new RoundedButton
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Height = 30,
                                                Width = 120,
                                                Action = copyAll,
                                            },
                                        }
                                    },
                                }
                            }
                        },
                        issuesContainer = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                scroll = new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = cardFlow = new FillFlowContainer<CheckCard>
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 8),
                                        Padding = new MarginPadding { Horizontal = 16, Bottom = 16, Top = 4 },
                                    }
                                },
                                message = new VerifierMessage { Alpha = 0 },
                                loadingSpinner = new LoadingSpinner(),
                            }
                        }
                    }
                }
            };

            copyButton.BackgroundColour = colourProvider.Background3;
        }

        protected override void Update()
        {
            base.Update();

            issuesContainer.Padding = new MarginPadding { Top = header.DrawHeight };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            filter.Changed += onFilterChanged;

            level.BindValueChanged(l =>
            {
                if (updatingLevel || session == null || displayedCategory == null)
                    return;

                session.SetLevelOverride(displayedCategory.Name, l.NewValue);
            });
        }

        private void onFilterChanged() => Scheduler.AddOnce(() =>
        {
            if (session != null)
                rebuild(session, true);
        });

        public void UpdateFrom(VerifierSession session)
        {
            this.session = session;

            var category = session.Results?.Get(session.SelectedCategory);

            bool categoryChanged = category?.Name != displayedCategory?.Name;

            if (category != displayedCategory || category?.Issues != displayedIssues)
                rebuild(session, false);

            if (categoryChanged)
                scroll.ScrollToStart(false);

            updateHeader(session);
        }

        private static LocalisableString formatCount(int shown, int total)
        {
            if (shown != total)
                return VerifierStrings.FilteredIssueCount(shown, total);

            return total == 1 ? VerifierStrings.SingleIssue : VerifierStrings.IssueCount(total);
        }

        private void updateHeader(VerifierSession session)
        {
            var category = displayedCategory;

            titleText.Text = category == null || category.IsGeneral ? VerifierStrings.General : category.Name;
            countText.Text = category == null ? string.Empty : formatCount(shownIssues.Count, category.Issues.Count);

            bool showLevel = category is { IsGeneral: false, DetectedLevel: not null };

            levelDropdownContainer.Alpha = showLevel ? 1 : 0;
            levelDropdown.SetDifficulty(category?.DetectedLevel, category?.Mode);

            if (showLevel)
            {
                updatingLevel = true;
                level.Value = category!.Level ?? category.DetectedLevel!.Value;
                updatingLevel = false;
            }

            if (category?.IsReinterpreting == true)
                reinterpretingSpinner.Show();
            else
                reinterpretingSpinner.Hide();

            copyButton.Text = VerifierStrings.CopyAll(shownIssues.Count);
            copyButton.Enabled.Value = shownIssues.Count > 0;
            copyButton.Alpha = category == null ? 0 : 1;

            if (session.Results == null && session.State == VerifierRunState.Running)
                loadingSpinner.Show();
            else
                loadingSpinner.Hide();
        }

        private void rebuild(VerifierSession session, bool keepScroll)
        {
            var category = session.Results?.Get(session.SelectedCategory);

            displayedCategory = category;
            displayedIssues = category?.Issues;

            double scrollPosition = scroll.Current;

            cardFlow.Clear();

            if (category == null)
            {
                shownIssues = new List<VerifierIssue>();

                if (session.State == VerifierRunState.Failed)
                    showMessage(FontAwesome.Solid.ExclamationTriangle, VerifierStrings.CheckFailed, VerifierStrings.CheckFailedDescription(session.Error ?? string.Empty));
                else if (session.State == VerifierRunState.Running)
                    hideMessage();
                else
                    showMessage(FontAwesome.Solid.Search, VerifierStrings.NotRunYet, string.Empty);

                updateHeader(session);
                return;
            }

            // a snapshot of the issues, as they may be replaced while reinterpreting the difficulty.
            var issues = category.Issues.ToList();

            shownIssues = issues.Where(filter.IsShown).ToList();

            if (issues.Count == 0)
                showMessage(FontAwesome.Solid.CheckCircle, VerifierStrings.NoIssues, VerifierStrings.NoIssuesDescription);
            else if (shownIssues.Count == 0)
                showMessage(FontAwesome.Solid.EyeSlash, VerifierStrings.NoVisibleIssues, VerifierStrings.NoVisibleIssuesDescription);
            else
                hideMessage();

            // checks with negligible issues have a card even if those issues are hidden, such that they can be shown again.
            var groups = issues
                         .GroupBy(i => i.CheckId)
                         .Select(g => (CheckId: g.Key, All: g.ToList(), Shown: g.Where(filter.IsShown).ToList()))
                         .Where(g => g.Shown.Count > 0)
                         .OrderByDescending(g => getSeverity(g.Shown))
                         .ThenBy(g => g.All[0].CheckName);

            foreach (var group in groups)
            {
                var key = (category.Name, group.CheckId);

                cardFlow.Add(new CheckCard(category.Name, group.CheckId, group.All[0].CheckName, group.Shown, group.All.Any(i => i.Level == global::MapsetVerifier.Framework.Objects.Issue.Level.Minor))
                {
                    Expanded = expandedChecks.Contains(key),
                    ExpandedChanged = expanded =>
                    {
                        if (expanded)
                            expandedChecks.Add(key);
                        else
                            expandedChecks.Remove(key);
                    },
                });
            }

            if (keepScroll)
                ScheduleAfterChildren(() => scroll.ScrollTo(scrollPosition, false));

            updateHeader(session);
        }

        /// <summary>
        /// The severity of the most severe of the given issues, which the cards are ordered by.
        /// </summary>
        private static int getSeverity(IEnumerable<VerifierIssue> issues) => issues.Max(i => VerifierDisplay.GetSeverity(i.Level));

        private void showMessage(IconUsage icon, LocalisableString title, LocalisableString description)
        {
            message.Set(icon, title, description);
            message.FadeIn(200, Easing.OutQuint);
        }

        private void hideMessage() => message.FadeOut(200, Easing.OutQuint);

        private void copyAll()
        {
            if (displayedCategory == null || shownIssues.Count == 0)
                return;

            var issues = shownIssues;
            string category = displayedCategory.IsGeneral ? VerifierStrings.General.ToString() : displayedCategory.Name;

            if (issues.Count > copy_confirmation_threshold && dialogOverlay != null)
                dialogOverlay.Push(new CopyAllDialog(issues.Count, () => copy(category, issues)));
            else
                copy(category, issues);
        }

        private void copy(string category, List<VerifierIssue> issues)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"[{category}]");

            // in the same order as the cards.
            var groups = issues.GroupBy(i => i.CheckName)
                               .OrderByDescending(getSeverity)
                               .ThenBy(g => g.Key);

            foreach (var group in groups)
            {
                builder.AppendLine();
                builder.AppendLine(group.Key);

                foreach (var issue in group)
                    builder.AppendLine($"  {VerifierDisplay.FormatNumbers(issue.Message)}");
            }

            clipboard.SetText(builder.ToString().TrimEnd());
            onScreenDisplay?.Display(new CopiedToClipboardToast());
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (filter != null)
                filter.Changed -= onFilterChanged;
        }

        private partial class LevelDropdown : FormDropdown<MvDifficulty>
        {
            private MvDifficulty? detectedLevel;
            private global::MapsetVerifier.Parser.Objects.Beatmap.Mode? mode;

            /// <summary>
            /// Sets the level which the difficulty was detected as and its mode, which the names of the levels depend on.
            /// </summary>
            public void SetDifficulty(MvDifficulty? detectedLevel, global::MapsetVerifier.Parser.Objects.Beatmap.Mode? mode)
            {
                if (detectedLevel == this.detectedLevel && mode == this.mode)
                    return;

                this.detectedLevel = detectedLevel;
                this.mode = mode;

                // regenerates the text of the items.
                Items = VerifierDisplay.INTERPRETABLE_LEVELS;
            }

            private readonly Bindable<bool> useModeNames = new Bindable<bool>();

            public LevelDropdown()
            {
                Items = VerifierDisplay.INTERPRETABLE_LEVELS;

                // the open menu overlaps what is below instead of resizing the header.
                Menu.BypassAutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                config.BindWith(OsuSetting.SlopMapsetVerifierShowGamemodeDifficultyNames, useModeNames);
            }

            protected override LocalisableString GenerateItemText(MvDifficulty item)
            {
                string name = VerifierDisplay.GetName(item, mode, useModeNames.Value);
                return item == detectedLevel ? VerifierStrings.DetectedLevel(name) : name;
            }
        }

        private partial class CopyAllDialog : DangerousActionDialog
        {
            public CopyAllDialog(int count, Action copy)
            {
                Icon = FontAwesome.Regular.Copy;
                HeaderText = VerifierStrings.CopyAllConfirmation;
                BodyText = VerifierStrings.CopyAllConfirmationBody(count);
                DangerousAction = copy;
            }
        }
    }
}
