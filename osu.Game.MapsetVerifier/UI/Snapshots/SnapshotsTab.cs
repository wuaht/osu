// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Server.Model;
using MapsetVerifier.Snapshots;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.MapsetVerifier.UI.Checks;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.MapsetVerifier.UI.Snapshots
{
    /// <summary>
    /// The changes of the beatmap set between check runs, like a commit history.
    /// </summary>
    public partial class SnapshotsTab : VerifierTab
    {
        public const float HISTORY_WIDTH = 300;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Bindable<bool> snapshotsEnabled = new Bindable<bool>();
        private readonly Bindable<string?> selectedCommit = new Bindable<string?>();

        private DifficultyPicker difficultyPicker = null!;
        private FillFlowContainer<CommitRow> commitFlow = null!;
        private Container changesContainer = null!;
        private VerifierMessage message = null!;
        private VerifierPanel messagePanel = null!;
        private LoadingSpinner spinner = null!;
        private GridContainer content = null!;

        private ApiSnapshotResult? displayedSnapshots;
        private string? displayedCategory;
        private ApiSnapshotHistory? history;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopMapsetVerifierAutoSnapshot, snapshotsEnabled);

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(GridSizeMode.Absolute, VerifierScreen.SPACING),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[] { difficultyPicker = new DifficultyPicker() },
                    new[] { Empty() },
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                content = new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ColumnDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.Absolute, HISTORY_WIDTH),
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
                                                            new Container
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                Height = 30,
                                                                Margin = new MarginPadding { Top = 12 },
                                                                Padding = new MarginPadding { Horizontal = 12 },
                                                                Child = new OsuSpriteText
                                                                {
                                                                    Anchor = Anchor.CentreLeft,
                                                                    Origin = Anchor.CentreLeft,
                                                                    Text = VerifierStrings.SnapshotHistory,
                                                                    Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
                                                                },
                                                            },
                                                        },
                                                        new Drawable[]
                                                        {
                                                            new OsuScrollContainer
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                Child = commitFlow = new FillFlowContainer<CommitRow>
                                                                {
                                                                    RelativeSizeAxes = Axes.X,
                                                                    AutoSizeAxes = Axes.Y,
                                                                    Direction = FillDirection.Vertical,
                                                                    Spacing = new Vector2(0, 2),
                                                                    Padding = new MarginPadding { Horizontal = 8, Top = 4, Bottom = 8 },
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
                                                Child = changesContainer = new Container { RelativeSizeAxes = Axes.Both },
                                            },
                                        }
                                    }
                                },
                                messagePanel = new VerifierPanel
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    Children = new Drawable[]
                                    {
                                        message = new VerifierMessage(),
                                        spinner = new LoadingSpinner
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                        },
                                    }
                                },
                            }
                        },
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            selectedCommit.BindValueChanged(_ => updateChanges());
        }

        public override void OnShown()
        {
            base.OnShown();
            Session.RequestSnapshots();
        }

        protected override void OnSessionChanged()
        {
            difficultyPicker.UpdateFrom(Session);

            var snapshots = Session.Snapshots;

            if (snapshots == null)
            {
                displayedSnapshots = null;
                showMessage(Session.State == VerifierRunState.Failed
                    ? new VerifierMessageContent(FontAwesome.Solid.ExclamationTriangle, VerifierStrings.CheckFailed, Session.Error ?? string.Empty)
                    : null);
                return;
            }

            if (snapshots.Value.ErrorMessage != null)
            {
                displayedSnapshots = snapshots;
                showMessage(snapshots.Value.ErrorMessage == global::MapsetVerifier.Server.Service.SnapshotService.NOT_SUBMITTED_MESSAGE
                    ? new VerifierMessageContent(FontAwesome.Solid.CloudUploadAlt, VerifierStrings.SnapshotsNotSubmitted, VerifierStrings.SnapshotsNotSubmittedDescription)
                    : new VerifierMessageContent(FontAwesome.Solid.ExclamationTriangle, VerifierStrings.TabSnapshots, VerifierStrings.SnapshotsFailed(snapshots.Value.ErrorMessage)));
                return;
            }

            if (EqualityComparer<ApiSnapshotResult?>.Default.Equals(displayedSnapshots, snapshots) && displayedCategory == Session.SelectedCategory)
                return;

            displayedSnapshots = snapshots;
            displayedCategory = Session.SelectedCategory;

            history = Session.SelectedCategory == VerifierSession.GENERAL
                ? snapshots.Value.General
                : snapshots.Value.BeatmapHistories.FirstOrDefault(h => h.DifficultyName == Session.SelectedCategory);

            var commits = history?.Commits.Where(c => c.HasSnapshot).ToArray() ?? Array.Empty<ApiSnapshotCommit>();

            if (commits.Length == 0)
            {
                showMessage(new VerifierMessageContent(FontAwesome.Solid.History, VerifierStrings.NoSnapshots,
                    snapshotsEnabled.Value ? VerifierStrings.NoSnapshotsDescription : VerifierStrings.NoSnapshotsDisabledDescription));
                return;
            }

            showMessage(null, false);

            string? previousSelection = selectedCommit.Value;

            commitFlow.Clear();

            for (int i = 0; i < commits.Length; i++)
                commitFlow.Add(new CommitRow(commits[i], i == commits.Length - 1, selectedCommit));

            // the selected snapshot is kept when switching difficulties, as all difficulties share the same dates.
            string selection = commits.Any(c => c.Id == previousSelection) ? previousSelection! : commits.FirstOrDefault(c => c.TotalChanges > 0, commits[0]).Id;

            if (selection == selectedCommit.Value)
                updateChanges();
            else
                selectedCommit.Value = selection;
        }

        private readonly record struct VerifierMessageContent(IconUsage Icon, osu.Framework.Localisation.LocalisableString Title, osu.Framework.Localisation.LocalisableString Description);

        /// <summary>
        /// Shows a message instead of the snapshots, or a loading spinner if <paramref name="content"/> is null and <paramref name="loading"/> is set.
        /// </summary>
        private void showMessage(VerifierMessageContent? content, bool loading = true)
        {
            if (content == null && !loading)
            {
                messagePanel.FadeOut(200, Easing.OutQuint);
                this.content.FadeIn(200, Easing.OutQuint);
                spinner.Hide();
                return;
            }

            messagePanel.FadeIn(200, Easing.OutQuint);
            this.content.FadeOut(200, Easing.OutQuint);

            if (content == null)
            {
                message.Set(default, VerifierStrings.LoadingSnapshots, default);
                message.Margin = new MarginPadding { Top = 80 };
                spinner.Show();
            }
            else
            {
                message.Set(content.Value.Icon, content.Value.Title, content.Value.Description);
                message.Margin = new MarginPadding();
                spinner.Hide();
            }
        }

        private void updateChanges()
        {
            var commit = history?.Commits.FirstOrDefault(c => c.Id == selectedCommit.Value);

            if (commit == null || commit.Value.Id == null)
            {
                changesContainer.Child = new VerifierMessage(FontAwesome.Solid.History, VerifierStrings.SelectSnapshot);
                return;
            }

            bool isInitial = commitFlow.LastOrDefault()?.Commit.Id == commit.Value.Id;

            changesContainer.Child = new CommitChanges(commit.Value, isInitial, Session.SelectedCategory);
        }

        public override bool OnBack() => false;

        private partial class CommitRow : OsuClickableContainer
        {
            public readonly ApiSnapshotCommit Commit;

            private readonly bool isInitial;
            private readonly Bindable<string?> selectedCommit = new Bindable<string?>();

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private Box background = null!;
            private Box selectionBar = null!;

            public CommitRow(ApiSnapshotCommit commit, bool isInitial, Bindable<string?> selectedCommit)
            {
                Commit = commit;
                this.isInitial = isInitial;
                this.selectedCommit.BindTo(selectedCommit);

                RelativeSizeAxes = Axes.X;
                Height = 48;
                Masking = true;
                CornerRadius = 6;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
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
                        Colour = VerifierDisplay.SNAPSHOT_CHANGES,
                        Alpha = 0,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 3),
                        Padding = new MarginPadding { Horizontal = 12 },
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = Commit.Date.ToLocalTime().ToLocalisableString(@"dd MMM yyyy, HH:mm:ss"),
                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(8, 0),
                                Children = isInitial
                                    ? new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Text = VerifierStrings.InitialSnapshot,
                                            Font = OsuFont.Default.With(size: 11, weight: FontWeight.SemiBold),
                                            Colour = colourProvider.Content2,
                                        }
                                    }
                                    : Commit.TotalChanges == 0
                                        ? new Drawable[]
                                        {
                                            new OsuSpriteText
                                            {
                                                Text = VerifierStrings.NoChanges,
                                                Font = OsuFont.Default.With(size: 11, weight: FontWeight.SemiBold),
                                                Colour = colourProvider.Content2,
                                                Alpha = 0.6f,
                                            }
                                        }
                                        : new Drawable[]
                                        {
                                            new ChangeCount(Snapshotter.DiffType.Added, Commit.Additions),
                                            new ChangeCount(Snapshotter.DiffType.Removed, Commit.Removals),
                                            new ChangeCount(Snapshotter.DiffType.Changed, Commit.Modifications),
                                        },
                            },
                        }
                    },
                };

                Action = () => selectedCommit.Value = Commit.Id;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                selectedCommit.BindValueChanged(_ => updateState(), true);
            }

            private void updateState()
            {
                bool selected = selectedCommit.Value == Commit.Id;

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

        /// <summary>
        /// The number of changes of a type, e.g. "+3".
        /// </summary>
        private partial class ChangeCount : FillFlowContainer
        {
            public ChangeCount(Snapshotter.DiffType type, int count)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(3, 0);
                Colour = VerifierDisplay.GetColour(type);
                Alpha = count > 0 ? 1 : 0.35f;

                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Icon = VerifierDisplay.GetIcon(type),
                        Size = new Vector2(9),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = count.ToString(),
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                    },
                };
            }
        }

        private partial class CommitChanges : CompositeDrawable
        {
            private readonly ApiSnapshotCommit commit;
            private readonly bool isInitial;
            private readonly string category;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public CommitChanges(ApiSnapshotCommit commit, bool isInitial, string category)
            {
                this.commit = commit;
                this.isInitial = isInitial;
                this.category = category;

                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Padding = new MarginPadding(16),
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
                                    Spacing = new Vector2(10, 0),
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = VerifierStrings.SnapshotChanges,
                                            Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
                                        },
                                        new OsuSpriteText
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Text = commit.Date.ToLocalTime().ToLocalisableString(@"dd MMM yyyy, HH:mm:ss"),
                                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                                            Colour = colourProvider.Content2,
                                        },
                                    }
                                },
                                new NominatorTag(isInitial ? VerifierStrings.InitialSnapshot : VerifierStrings.ChangesCount(commit.TotalChanges), VerifierDisplay.SNAPSHOT_CHANGES, textSize: 11)
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                },
                            }
                        },
                    }
                };

                var sections = commit.Sections.Where(s => s.Diffs.Any()).ToArray();

                if (sections.Length == 0)
                    flow.Add(new VerifierMessage(FontAwesome.Solid.Check, VerifierStrings.NoChangesInCommit));

                foreach (var section in sections)
                    flow.Add(new SectionCard(section, category));

                InternalChild = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = flow,
                };
            }
        }

        private partial class SectionCard : CompositeDrawable
        {
            private readonly ApiSnapshotSection section;
            private readonly string category;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public SectionCard(ApiSnapshotSection section, string category)
            {
                this.section = section;
                this.category = category;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var colour = VerifierDisplay.GetColour(section.AggregatedDiffType);

                var diffFlow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 2),
                    Padding = new MarginPadding { Horizontal = 6, Bottom = 8 },
                };

                foreach (var diff in section.Diffs)
                    diffFlow.Add(new DiffRow(diff, category));

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = colour,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 3 },
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 38,
                                Padding = new MarginPadding { Left = 12, Right = 12 },
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(10, 0),
                                        Children = new Drawable[]
                                        {
                                            new SpriteIcon
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Icon = VerifierDisplay.GetIcon(section.AggregatedDiffType),
                                                Size = new Vector2(13),
                                                Colour = colour,
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Text = section.Name,
                                                Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
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
                                            new ChangeCount(Snapshotter.DiffType.Added, section.Additions),
                                            new ChangeCount(Snapshotter.DiffType.Removed, section.Removals),
                                            new ChangeCount(Snapshotter.DiffType.Changed, section.Modifications),
                                        }
                                    },
                                }
                            },
                            diffFlow,
                        }
                    },
                };
            }
        }

        private partial class DiffRow : CompositeDrawable
        {
            private readonly ApiSnapshotDiff diff;
            private readonly string category;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public DiffRow(ApiSnapshotDiff diff, string category)
            {
                this.diff = diff;
                this.category = category;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                var colour = VerifierDisplay.GetColour(diff.DiffType);

                var flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                    Children = new Drawable[]
                    {
                        new IssueTextFlow(diff.Message, category, 13),
                    }
                };

                // values which are part of the message already aren't repeated.
                if (diff.DiffType == Snapshotter.DiffType.Changed && (diff.OldValue != null || diff.NewValue != null))
                {
                    flow.Add(new ValueRow(VerifierStrings.OldValue, diff.OldValue, VerifierDisplay.REMOVED));
                    flow.Add(new ValueRow(VerifierStrings.NewValue, diff.NewValue, VerifierDisplay.ADDED));
                }

                foreach (string detail in diff.Details)
                {
                    flow.Add(new OsuTextFlowContainer(s =>
                    {
                        s.Font = OsuFont.Default.With(size: 12);
                        s.Colour = colourProvider.Content2;
                    })
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = detail,
                    });
                }

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colour,
                        Alpha = 0.05f,
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Horizontal = 8, Vertical = 5 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, 20),
                            new Dimension(),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new SpriteIcon
                                {
                                    Icon = VerifierDisplay.GetIcon(diff.DiffType),
                                    Size = new Vector2(10),
                                    Colour = colour,
                                    Margin = new MarginPadding { Top = 3 },
                                },
                                flow,
                            }
                        }
                    },
                };
            }

            private partial class ValueRow : GridContainer
            {
                public ValueRow(osu.Framework.Localisation.LocalisableString label, string? value, Color4 colour)
                {
                    RelativeSizeAxes = Axes.X;
                    AutoSizeAxes = Axes.Y;
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 52),
                        new Dimension(),
                    };
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) };
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = label,
                                Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                                Colour = colour,
                                Margin = new MarginPadding { Top = 1 },
                            },
                            new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold, fixedWidth: true))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = string.IsNullOrEmpty(value) ? "—" : value,
                            },
                        }
                    };
                }
            }
        }
    }
}
