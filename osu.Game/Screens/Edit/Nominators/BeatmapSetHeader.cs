// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.BnTracker;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Rulesets;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// The beatmap set, its progress towards being fully nominated, and the actions for it.
    /// </summary>
    public partial class BeatmapSetHeader : CompositeDrawable
    {
        public const float HEIGHT = 160;

        [Resolved]
        private NominatorsSession session { get; set; } = null!;

        [Resolved]
        private NominatorsScreen screen { get; set; } = null!;

        [Resolved]
        private BnTrackerClient client { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        private readonly IBindable<BnBeatmapSetWithNominators?> data = new Bindable<BnBeatmapSetWithNominators?>();
        private readonly IBindable<bool> isBusy = new Bindable<bool>();

        private Container coverContainer = null!;
        private OsuSpriteText title = null!;
        private OsuSpriteText artist = null!;
        private FillFlowContainer tags = null!;
        private FillFlowContainer progress = null!;
        private OsuSpriteText progressText = null!;
        private StatusProgressBar progressBar = null!;
        private FillFlowContainer statusCounts = null!;
        private PriorityButton priorityButton = null!;
        private IconButton websiteButton = null!;
        private IconButton stopTrackingButton = null!;
        private IconButton refreshButton = null!;
        private LoadingSpinner busySpinner = null!;

        private string? displayedCoverUrl;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;

            InternalChild = new NominatorsPanel
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    coverContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.3f,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientHorizontal(Color4.Black.Opacity(0.6f), Color4.Black.Opacity(0.1f)),
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 18, Vertical = 12 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 24),
                            new Dimension(GridSizeMode.AutoSize),
                        },
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
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 3),
                                    Children = new Drawable[]
                                    {
                                        title = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = OsuFont.Default.With(size: 22, weight: FontWeight.Bold),
                                        },
                                        artist = new TruncatingSpriteText
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Font = OsuFont.Default.With(size: 14),
                                        },
                                        tags = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Full,
                                            Spacing = new Vector2(5),
                                            Margin = new MarginPadding { Top = 5 },
                                        },
                                        progressBar = new StatusProgressBar
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 6,
                                            Margin = new MarginPadding { Top = 9 },
                                        },
                                        statusCounts = new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Full,
                                            Spacing = new Vector2(14, 4),
                                            Margin = new MarginPadding { Top = 3 },
                                        },
                                    }
                                },
                                Empty(),
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 8),
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(4, 0),
                                            Children = new Drawable[]
                                            {
                                                busySpinner = new LoadingSpinner
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Size = new Vector2(18),
                                                    Margin = new MarginPadding { Right = 6 },
                                                },
                                                priorityButton = new PriorityButton
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    PriorityPicked = p => session.SetPriority(p),
                                                },
                                                createIconButton(FontAwesome.Solid.ExternalLinkAlt, SlopNominatorsStrings.ViewOnOsu, () =>
                                                {
                                                    if (data.Value != null)
                                                        screen.OpenUrl(data.Value.BeatmapSet.OsuUrl, true);
                                                }),
                                                websiteButton = createIconButton(FontAwesome.Solid.Globe, SlopNominatorsStrings.ViewOnBnTracker, () =>
                                                {
                                                    if (session.IsTracked && client.ServerUrl != null)
                                                        screen.OpenUrl($@"{client.ServerUrl}/beatmapsets/{data.Value!.BeatmapSet.Id}", true);
                                                }),
                                                refreshButton = createIconButton(FontAwesome.Solid.SyncAlt, SlopNominatorsStrings.Refresh, () => session.Refresh()),
                                                stopTrackingButton = createIconButton(FontAwesome.Solid.TrashAlt, SlopNominatorsStrings.StopTracking, confirmStopTracking),
                                            }
                                        },
                                        progressText = new OsuSpriteText
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                                        },
                                        progress = new FillFlowContainer
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(5, 0),
                                        },
                                    }
                                },
                            }
                        }
                    },
                }
            };

            stopTrackingButton.Colour = Color4Extensions.FromHex(@"f87171");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            data.BindTo(session.Data);
            data.BindValueChanged(_ => updateDisplay(), true);

            isBusy.BindTo(session.IsBusy);
            isBusy.BindValueChanged(b =>
            {
                if (b.NewValue)
                    busySpinner.Show();
                else
                    busySpinner.Hide();
            }, true);
        }

        private void updateDisplay()
        {
            var set = data.Value?.BeatmapSet;

            if (set == null)
                return;

            if (set.CoverUrl != displayedCoverUrl)
            {
                displayedCoverUrl = set.CoverUrl;
                coverContainer.Clear();

                if (!string.IsNullOrEmpty(set.CoverUrl))
                    coverContainer.Add(new DelayedLoadWrapper(() => new UrlBackground(set.CoverUrl), 0) { RelativeSizeAxes = Axes.Both });
            }

            title.Text = set.Title;
            artist.Text = string.IsNullOrEmpty(set.CreatorUsername)
                ? SlopNominatorsStrings.ByArtist(set.Artist)
                : SlopNominatorsStrings.ByArtistMappedBy(set.Artist, set.CreatorUsername);

            tags.Clear();

            foreach (var mode in set.Modes)
            {
                tags.Add(new NominatorTag(NominatorsDisplay.GetName(mode), Color4.White, NominatorsDisplay.CreateIcon(rulesets, mode, 11), 12)
                {
                    TooltipText = mode == set.MainMode && set.Modes.Count > 1 ? SlopNominatorsStrings.MainModeTooltip : default,
                });
            }

            tags.Add(new NominatorTag(SlopNominatorsStrings.DifficultyCount(set.DifficultyCount), Color4.White, textSize: 12));
            tags.Add(new NominatorTag(NominatorsDisplay.GetName(set.RankStatus), Color4Extensions.FromHex(@"facc15"), textSize: 12));

            if (!string.IsNullOrEmpty(set.Genre))
                tags.Add(new NominatorTag(set.Genre, Color4.White, textSize: 12) { TooltipText = SlopNominatorsStrings.Genre });

            if (!string.IsNullOrEmpty(set.Language))
                tags.Add(new NominatorTag(set.Language, Color4.White, textSize: 12) { TooltipText = SlopNominatorsStrings.SongLanguage });

            if (!session.IsTracked)
                tags.Add(new NominatorTag(SlopNominatorsStrings.NotTrackedYet, Color4Extensions.FromHex(@"38bdf8"), textSize: 12) { TooltipText = SlopNominatorsStrings.NotTrackedYetTooltip });

            var p = set.Progress;

            progressText.Text = p.IsFullBn ? SlopNominatorsStrings.FullyNominated : SlopNominatorsStrings.NominationProgress(p.TotalFilled, p.TotalRequired);
            progressText.Colour = p.IsFullBn ? NominatorsDisplay.GetColour(BnNominationStatus.Accepted) : Color4.White;

            progress.Clear();

            foreach (var mode in p.PerMode)
            {
                var colour = mode.IsSatisfied ? NominatorsDisplay.GetColour(BnNominationStatus.Accepted) : Color4.White;

                progress.Add(new NominatorTag($@"{mode.Filled}/{mode.Required}", colour, NominatorsDisplay.CreateIcon(rulesets, mode.Mode, 11), 12)
                {
                    TooltipText = SlopNominatorsStrings.ModeProgressTooltip(NominatorsDisplay.GetName(mode.Mode), mode.Filled, mode.Required),
                });
            }

            progressBar.SetCounts(p.StatusCounts, p.RelevantNominatorCount);

            // the legend of the bar, in the same order, with the nominators who weren't asked yet (the empty part of the bar) last.
            statusCounts.Clear();

            foreach (var status in StatusProgressBar.ORDER.Append(BnNominationStatus.NotAsked))
                statusCounts.Add(new StatusCount(status, p.StatusCounts.GetValueOrDefault(status)));

            priorityButton.Priority = set.Priority;

            websiteButton.Alpha = session.IsTracked ? 1 : 0;
            stopTrackingButton.Alpha = session.IsTracked ? 1 : 0;
        }

        private void confirmStopTracking()
        {
            if (!session.IsTracked)
                return;

            dialogOverlay?.Push(new StopTrackingDialog(() => session.StopTracking()));
        }

        private static IconButton createIconButton(IconUsage icon, LocalisableString tooltip, Action action) => new IconButton
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Icon = icon,
            TooltipText = tooltip,
            Action = action,
            Size = new Vector2(30),
            IconScale = new Vector2(0.8f),
        };

        private partial class StatusCount : FillFlowContainer
        {
            public StatusCount(BnNominationStatus status, int count)
            {
                AutoSizeAxes = Axes.Both;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(4, 0);
                Alpha = count > 0 ? 1 : 0.4f;

                var colour = NominatorsDisplay.GetColour(status);

                Children = new Drawable[]
                {
                    new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(8),
                        Colour = colour,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = count.ToString(),
                        Colour = colour,
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = NominatorsDisplay.GetName(status),
                        Font = OsuFont.Default.With(size: 12),
                        Alpha = 0.8f,
                    },
                };
            }
        }

        /// <summary>
        /// Shows the priority of the beatmap set, and opens a popover to change it.
        /// </summary>
        private partial class PriorityButton : OsuClickableContainer, IHasPopover
        {
            public Action<BnBeatmapPriority>? PriorityPicked;

            private BnBeatmapPriority priority;

            public BnBeatmapPriority Priority
            {
                get => priority;
                set
                {
                    priority = value;
                    tag.Child = new NominatorTag(NominatorsDisplay.GetName(value), NominatorsDisplay.GetColour(value), new SpriteIcon { Icon = FontAwesome.Solid.Flag, Size = new Vector2(10) }, 12);
                }
            }

            private readonly Container tag;

            public PriorityButton()
            {
                AutoSizeAxes = Axes.Both;
                Child = tag = new Container { AutoSizeAxes = Axes.Both };
                TooltipText = SlopNominatorsStrings.ChangePriority;
                Action = this.ShowPopover;
            }

            public Popover GetPopover()
            {
                var flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                };

                foreach (var p in Enum.GetValues<BnBeatmapPriority>())
                {
                    flow.Add(new OsuClickableContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Child = new NominatorTag(NominatorsDisplay.GetName(p), NominatorsDisplay.GetColour(p), textSize: 14),
                        Action = () =>
                        {
                            this.HidePopover();
                            PriorityPicked?.Invoke(p);
                        },
                    });
                }

                return new OsuPopover { Child = flow };
            }
        }

        private partial class StopTrackingDialog : DangerousActionDialog
        {
            public StopTrackingDialog(Action confirm)
            {
                HeaderText = SlopNominatorsStrings.StopTrackingConfirmation;
                BodyText = SlopNominatorsStrings.StopTrackingConfirmationBody;
                DangerousAction = confirm;
            }
        }
    }
}
