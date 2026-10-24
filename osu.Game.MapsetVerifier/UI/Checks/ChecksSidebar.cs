// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Framework.Objects;
using MapsetVerifier.Server.Model;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.MapsetVerifier.UI.Checks
{
    /// <summary>
    /// The filters of the issues, and what changed since the last check run.
    /// </summary>
    public partial class ChecksSidebar : CompositeDrawable
    {
        /// <summary>
        /// How many of the slowest checks are listed.
        /// </summary>
        private const int listed_timing_count = 10;

        [Resolved]
        private IssueFilter filter { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        private readonly Bindable<bool> showDelta = new Bindable<bool>();
        private readonly Bindable<bool> showUnchanged = new Bindable<bool>();
        private readonly Bindable<bool> showTimings = new Bindable<bool>();

        private FillFlowContainer hiddenChecksFlow = null!;
        private FillFlowContainer deltaSection = null!;
        private OsuTextFlowContainer deltaSinceText = null!;
        private FillFlowContainer deltaFlow = null!;
        private IconButton clearHistoryButton = null!;
        private FillFlowContainer timingsSection = null!;
        private OsuSpriteText timingsTotalText = null!;
        private FillFlowContainer timingsFlow = null!;

        private VerifierSession? session;
        private VerifierResults? displayedResults;
        private ApiCheckRunDelta? displayedDelta;
        private string? displayedHiddenChecks;

        public ChecksSidebar()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopMapsetVerifierShowCheckRunDelta, showDelta);
            config.BindWith(OsuSetting.SlopMapsetVerifierDeltaShowUnchanged, showUnchanged);
            config.BindWith(OsuSetting.SlopMapsetVerifierShowCheckSpeedStats, showTimings);

            InternalChild = new VerifierPanel
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
                            createHeader(VerifierStrings.Filter),
                            new FormCheckBox
                            {
                                Caption = VerifierDisplay.GetName(Issue.Level.Problem),
                                Current = filter.GetLevel(Issue.Level.Problem),
                            },
                            new FormCheckBox
                            {
                                Caption = VerifierDisplay.GetName(Issue.Level.Warning),
                                Current = filter.GetLevel(Issue.Level.Warning),
                            },
                            new FormCheckBox
                            {
                                Caption = VerifierStrings.ShowNegligible,
                                HintText = VerifierStrings.ShowNegligibleDescription,
                                Current = filter.ShowMinor,
                            },
                            new FormCheckBox
                            {
                                Caption = VerifierDisplay.GetName(Issue.Level.Info),
                                Current = filter.GetLevel(Issue.Level.Info),
                            },
                            new FormCheckBox
                            {
                                Caption = VerifierDisplay.GetName(Issue.Level.Error),
                                Current = filter.GetLevel(Issue.Level.Error),
                            },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                                Margin = new MarginPadding { Top = 4 },
                                Children = new Drawable[]
                                {
                                    new VerifierSubtitle(VerifierStrings.HiddenNegligibleChecks),
                                    hiddenChecksFlow = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                    },
                                }
                            },
                            deltaSection = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 8),
                                Margin = new MarginPadding { Top = 8 },
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Children = new Drawable[]
                                        {
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                // leave room for the clear history button.
                                                Padding = new MarginPadding { Right = 36 },
                                                Children = new Drawable[]
                                                {
                                                    new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold))
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Text = VerifierStrings.Changes,
                                                    },
                                                    deltaSinceText = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 12))
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                        AutoSizeAxes = Axes.Y,
                                                        Alpha = 0.7f,
                                                    },
                                                }
                                            },
                                            clearHistoryButton = new IconButton
                                            {
                                                Anchor = Anchor.TopRight,
                                                Origin = Anchor.TopRight,
                                                Icon = FontAwesome.Solid.TrashAlt,
                                                Size = new Vector2(30),
                                                IconScale = new Vector2(0.7f),
                                                TooltipText = VerifierStrings.ClearHistory,
                                                Action = clearHistory,
                                            },
                                        }
                                    },
                                    deltaFlow = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 6),
                                    },
                                }
                            },
                            timingsSection = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                                Margin = new MarginPadding { Top = 8 },
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = VerifierStrings.CheckTimings,
                                        Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                                    },
                                    timingsTotalText = new OsuSpriteText
                                    {
                                        Font = OsuFont.Default.With(size: 12),
                                        Alpha = 0.7f,
                                    },
                                    timingsFlow = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 2),
                                    },
                                }
                            },
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            filter.Changed += onFilterChanged;

            showDelta.BindValueChanged(_ => updateDelta(true));
            showUnchanged.BindValueChanged(_ => updateDelta(true));
            showTimings.BindValueChanged(_ => updateTimings());
        }

        private Drawable createHeader(LocalisableString text) => new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 30,
            Child = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = text,
                Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
            },
        };

        private void onFilterChanged() => Scheduler.AddOnce(updateHiddenChecks);

        public void UpdateFrom(VerifierSession session)
        {
            this.session = session;

            if (session.Results != displayedResults)
            {
                displayedResults = session.Results;
                displayedHiddenChecks = null;
                updateTimings();
            }

            updateHiddenChecks();
            updateDelta(false);
        }

        private void updateHiddenChecks()
        {
            string key = string.Join(',', filter.HiddenMinorChecks.OrderBy(i => i));

            if (key == displayedHiddenChecks)
                return;

            displayedHiddenChecks = key;
            hiddenChecksFlow.Clear();

            if (filter.HiddenMinorChecks.Count == 0)
            {
                hiddenChecksFlow.Add(new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 12))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Alpha = 0.6f,
                    Text = VerifierStrings.NoHiddenNegligibleChecks,
                });
                return;
            }

            foreach (int id in filter.HiddenMinorChecks.OrderBy(i => i))
            {
                string name = displayedResults?.CheckNames.GetValueOrDefault(id) ?? $"#{id}";
                hiddenChecksFlow.Add(new HiddenCheckRow(name, () => filter.SetMinorHidden(id, false)));
            }
        }

        private void updateDelta(bool force)
        {
            var delta = displayedResults?.Delta;

            deltaSection.Alpha = showDelta.Value && displayedResults != null ? 1 : 0;

            if (!force && EqualityComparer<ApiCheckRunDelta?>.Default.Equals(delta, displayedDelta) && deltaFlow.Count > 0)
                return;

            displayedDelta = delta;
            deltaFlow.Clear();

            if (delta == null)
            {
                deltaSinceText.Text = string.Empty;
                clearHistoryButton.Alpha = 0;
                deltaFlow.Add(createNote(VerifierStrings.NoPreviousRun));
                return;
            }

            var value = delta.Value;

            deltaSinceText.Text = VerifierStrings.ChangesSince(value.PreviousRunAt.ToLocalTime().ToString(@"HH:mm:ss"));
            clearHistoryButton.Alpha = 1;

            var groups = new List<(LocalisableString Name, Color4 Colour, IconUsage Icon, List<ApiCheckDeltaIssue> Issues)>
            {
                (VerifierStrings.DeltaNew, VerifierDisplay.PROBLEM, FontAwesome.Solid.Plus, value.NewIssues.ToList()),
                (VerifierStrings.DeltaWorsened, VerifierDisplay.WARNING, FontAwesome.Solid.ArrowUp, value.WorsenedIssues.ToList()),
                (VerifierStrings.DeltaImproved, VerifierDisplay.TIMESTAMP, FontAwesome.Solid.ArrowDown, value.ImprovedIssues.ToList()),
                (VerifierStrings.DeltaResolved, VerifierDisplay.CHECK, FontAwesome.Solid.Check, value.ResolvedIssues.ToList()),
            };

            if (showUnchanged.Value)
                groups.Add((VerifierStrings.DeltaUnchanged, VerifierDisplay.ERROR, FontAwesome.Solid.Equals, value.UnchangedIssues.ToList()));

            if (groups.Sum(g => g.Issues.Count) == 0)
            {
                deltaFlow.Add(createNote(VerifierStrings.NoChanges));
                return;
            }

            var tags = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(4),
            };

            foreach (var group in groups)
            {
                tags.Add(new NominatorTag(LocalisableString.Interpolate($"{group.Name} {group.Issues.Count}"), group.Colour, new SpriteIcon { Icon = group.Icon, Size = new Vector2(8) }, 11)
                {
                    Alpha = group.Issues.Count > 0 ? 1 : 0.4f,
                });
            }

            deltaFlow.Add(tags);

            foreach (var group in groups.Where(g => g.Issues.Count > 0))
            {
                var list = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    Child = new VerifierSubtitle(group.Name) { Colour = group.Colour, Alpha = 1 },
                };

                foreach (var issue in group.Issues)
                    list.Add(new DeltaIssueRow(issue));

                deltaFlow.Add(list);
            }
        }

        private void updateTimings()
        {
            var timings = displayedResults?.Timings;

            timingsSection.Alpha = showTimings.Value && timings != null ? 1 : 0;
            timingsFlow.Clear();

            if (timings == null)
                return;

            timingsTotalText.Text = VerifierStrings.CheckTimingsTotal(timings.TotalElapsedMs.ToString(@"N0"));

            var slowest = timings.Checks
                                 .GroupBy(t => t.CheckName)
                                 .Select(g => (Name: g.Key, Ms: g.Sum(t => t.ElapsedMs)))
                                 .OrderByDescending(t => t.Ms)
                                 .Take(listed_timing_count)
                                 .ToList();

            long max = Math.Max(1, slowest.FirstOrDefault().Ms);

            foreach (var (name, ms) in slowest)
                timingsFlow.Add(new TimingRow(name, ms, (float)ms / max));
        }

        private static Drawable createNote(LocalisableString text) => new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 12))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Alpha = 0.6f,
            Text = text,
        };

        private void clearHistory()
        {
            if (session == null)
                return;

            var target = session;

            if (dialogOverlay == null)
                target.ClearHistory();
            else
                dialogOverlay.Push(new ClearHistoryDialog(target.ClearHistory));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (filter != null)
                filter.Changed -= onFilterChanged;
        }

        private partial class HiddenCheckRow : CompositeDrawable
        {
            public HiddenCheckRow(string name, Action show)
            {
                RelativeSizeAxes = Axes.X;
                Height = 24;

                InternalChildren = new Drawable[]
                {
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Padding = new MarginPadding { Right = 28 },
                        Text = name,
                        Font = OsuFont.Default.With(size: 13),
                        ShowTooltip = true,
                    },
                    new IconButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Size = new Vector2(24),
                        IconScale = new Vector2(0.6f),
                        Icon = FontAwesome.Solid.Eye,
                        TooltipText = VerifierStrings.ShowNegligibleOfCheck,
                        Action = show,
                    },
                };
            }
        }

        private partial class DeltaIssueRow : CompositeDrawable
        {
            private readonly ApiCheckDeltaIssue issue;

            public DeltaIssueRow(ApiCheckDeltaIssue issue)
            {
                this.issue = issue;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var levelIcons = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(3, 0),
                };

                if (issue.PreviousLevel is Issue.Level previous && previous != issue.Level)
                {
                    levelIcons.Add(VerifierDisplay.CreateIcon(previous, 10));
                    levelIcons.Add(new SpriteIcon { Icon = FontAwesome.Solid.LongArrowAltRight, Size = new Vector2(10), Alpha = 0.6f });
                }

                levelIcons.Add(VerifierDisplay.CreateIcon(issue.Level, 10));

                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 2),
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            Children = new Drawable[]
                            {
                                levelIcons,
                                new OsuSpriteText
                                {
                                    Text = issue.Category == VerifierSession.GENERAL ? VerifierStrings.General : issue.Category,
                                    Font = OsuFont.Default.With(size: 11, weight: FontWeight.SemiBold),
                                    Alpha = 0.7f,
                                },
                            }
                        },
                        new IssueTextFlow(issue.Message, issue.Category, 12),
                    }
                };
            }
        }

        private partial class TimingRow : CompositeDrawable
        {
            public TimingRow(string name, long ms, float fraction)
            {
                RelativeSizeAxes = Axes.X;
                Height = 20;

                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 3,
                        Child = new osu.Framework.Graphics.Shapes.Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = Math.Max(0.02f, fraction),
                            Colour = VerifierDisplay.SNAPSHOT_CHANGES,
                            Alpha = 0.2f,
                        },
                    },
                    new TruncatingSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Padding = new MarginPadding { Left = 6, Right = 60 },
                        Text = name,
                        Font = OsuFont.Default.With(size: 12),
                        ShowTooltip = true,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Margin = new MarginPadding { Right = 6 },
                        Text = $@"{ms:N0} ms",
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                    },
                };
            }
        }

        private partial class ClearHistoryDialog : DangerousActionDialog
        {
            public ClearHistoryDialog(Action clear)
            {
                HeaderText = VerifierStrings.ClearHistoryConfirmation;
                BodyText = VerifierStrings.ClearHistoryConfirmationBody;
                DangerousAction = clear;
            }
        }
    }
}
