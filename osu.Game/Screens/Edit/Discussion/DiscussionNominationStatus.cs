// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The hype train, the nomination status and the messages about the state of the beatmap set, like the nominations of the website.
    /// </summary>
    public partial class DiscussionNominationStatus : DiscussionSection
    {
        private FillFlowContainer progress = null!;
        private FillFlowContainer messages = null!;
        private FillFlowContainer actions = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new NominatorsPanel(Axes.Y)
            {
                RelativeSizeAxes = Axes.X,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 14),
                    Padding = new MarginPadding { Horizontal = 20, Vertical = 16 },
                    Children = new Drawable[]
                    {
                        progress = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 14),
                        },
                        messages = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 6),
                        },
                        actions = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Full,
                            Spacing = new Vector2(8),
                        },
                    }
                }
            };
        }

        protected override void UpdateDisplay(DiscussionsState state)
        {
            var beatmapSet = state.BeatmapSet;

            progress.Clear();
            messages.Clear();

            bool showProgress = beatmapSet.CanBeHyped || beatmapSet.Status == @"qualified";

            if (showProgress)
            {
                var row = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 30),
                        new Dimension(),
                    },
                    RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                    Content = new[]
                    {
                        new[]
                        {
                            beatmapSet.CanBeHyped ? createHype(state) : Empty(),
                            Empty(),
                            createNominationStatus(state),
                        }
                    }
                };

                progress.Add(row);

                if (state.BeatmapSet.Nominations?.LegacyMode == false && state.RulesetsWithoutDeletedBeatmaps.Count() > 1)
                    progress.Add(createRulesetNominations(state));

                var nominators = state.Nominators;

                if (nominators.Count > 0)
                {
                    var flow = createTextFlow();

                    DiscussionDisplay.AddPattern(flow, SlopDiscussionStrings.NominatedBy, new Dictionary<string, Action<LinkFlowContainer>>
                    {
                        [@"0"] = f =>
                        {
                            for (int i = 0; i < nominators.Count; i++)
                            {
                                if (i > 0)
                                    f.AddText(@", ");

                                DiscussionLinks.AddUser(f, state, nominators[i].Id, bold);
                            }
                        },
                    });

                    progress.Add(flow);
                }
            }

            progress.Alpha = showProgress ? 1 : 0;

            addMessages(state);

            messages.Alpha = messages.Count > 0 ? 1 : 0;

            actions.Clear();
            addActions(state);
            actions.Alpha = actions.Count > 0 ? 1 : 0;

            Alpha = showProgress || messages.Count > 0 || actions.Count > 0 ? 1 : 0;
        }

        private Drawable createHype(DiscussionsState state)
        {
            var hype = state.BeatmapSet.Hype;
            int current = hype?.Current ?? state.TotalHypeCount;
            int required = Math.Max(1, hype?.Required ?? 0);
            bool hyped = state.HasCurrentUserHyped;

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    createProgressTitle(SlopDiscussionStrings.HypeTrain, current, required),
                    new SegmentedBar(current, required, DiscussionDisplay.PINK_1),
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8, 0),
                        Children = new Drawable[]
                        {
                            new RoundedButton
                            {
                                Width = 170,
                                Height = 30,
                                Text = hyped ? SlopDiscussionStrings.HypeButtonDone : SlopDiscussionStrings.HypeButton,
                                BackgroundColour = DiscussionDisplay.PINK_DARKER,
                                Enabled = { Value = !hyped && state.BeatmapSet.CurrentUserAttributes?.CanHype != false },
                                TooltipText = state.BeatmapSet.CurrentUserAttributes?.CanHypeReason ?? default(LocalisableString),
                                Action = () => Screen.FocusNewDiscussion(@"hype"),
                            },
                            new RoundedButton
                            {
                                Width = 170,
                                Height = 30,
                                Text = SlopDiscussionStrings.LeaveFeedback,
                                Action = () => Screen.FocusNewDiscussion(null),
                            },
                        }
                    },
                }
            };
        }

        private Drawable createNominationStatus(DiscussionsState state)
        {
            int current = state.NominationsCurrent;
            int required = Math.Max(1, state.NominationsRequired);

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    createProgressTitle(SlopDiscussionStrings.NominationStatus, current, required),
                    new SegmentedBar(current, required, state.BeatmapSet.Status == @"qualified" ? DiscussionDisplay.BLUE_1 : DiscussionDisplay.LIME_1),
                }
            };
        }

        private Drawable createRulesetNominations(DiscussionsState state)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(16, 4),
            };

            var meta = state.BeatmapSet.Nominations?.RequiredMeta;
            string? mainRuleset = state.CalculatedMainRuleset;

            foreach (string ruleset in state.RulesetsWithoutDeletedBeatmaps)
            {
                int required = meta == null ? 0 : mainRuleset == null || ruleset == mainRuleset ? meta.MainRuleset : meta.NonMainRuleset;
                int current = state.NominationsOfRuleset(ruleset);

                flow.Add(new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(5, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = DiscussionDisplay.RulesetIcon(ruleset),
                            Size = new Vector2(12),
                            Colour = ColourProvider.Content2,
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = $@"{current} / {required}",
                            Colour = current >= required && required > 0 ? DiscussionDisplay.LIME_1 : ColourProvider.Content1,
                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                        },
                    }
                });
            }

            return flow;
        }

        private Drawable createProgressTitle(LocalisableString title, int current, int required) => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                new DiscussionTitle(title),
                new OsuSpriteText
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Text = $@"{DiscussionDisplay.FormatNumber(current)} / {DiscussionDisplay.FormatNumber(required)}",
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                    Colour = ColourProvider.Content2,
                },
            }
        };

        private void addMessages(DiscussionsState state)
        {
            var beatmapSet = state.BeatmapSet;
            var nominations = beatmapSet.Nominations;

            switch (beatmapSet.Status)
            {
                case @"approved":
                case @"loved":
                case @"ranked":
                    if (beatmapSet.RankedDate is DateTimeOffset rankedDate)
                    {
                        string date = formatDate(rankedDate);
                        addMessage(beatmapSet.Status == @"approved"
                            ? SlopDiscussionStrings.StatusApproved(date)
                            : beatmapSet.Status == @"loved"
                                ? SlopDiscussionStrings.StatusLoved(date)
                                : SlopDiscussionStrings.StatusRanked(date), FontAwesome.Solid.CheckCircle, DiscussionDisplay.LIME_1);
                    }

                    break;

                case @"graveyard":
                    if (beatmapSet.LastUpdated is DateTimeOffset lastUpdated)
                        addMessage(SlopDiscussionStrings.StatusGraveyard(formatDate(lastUpdated)), FontAwesome.Solid.Skull, ColourProvider.Content2);

                    break;

                case @"wip":
                    addMessage(SlopDiscussionStrings.StatusWip, FontAwesome.Solid.Hammer, DiscussionDisplay.ORANGE_1);
                    break;
            }

            if (nominations != null && beatmapSet.Status is @"pending" or @"qualified" or @"wip")
                addResetMessage(state, nominations);

            if (beatmapSet.Status == @"qualified" && nominations != null)
            {
                var flow = createMessageFlow(FontAwesome.Solid.Clock, DiscussionDisplay.BLUE_1);

                DiscussionDisplay.AddPattern(flow, SlopDiscussionStrings.RankEstimate, new Dictionary<string, Action<LinkFlowContainer>>
                {
                    [@"0"] = f => f.AddText(nominations.RankingEta is DateTimeOffset eta
                        ? SlopDiscussionStrings.RankEstimateOn(formatDate(eta))
                        : SlopDiscussionStrings.RankEstimateSoon, bold),
                    [@"1"] = f => f.AddText(DiscussionDisplay.FormatNumber(nominations.RankingQueuePosition ?? 0), bold),
                    [@"2"] = f => f.AddLink(SlopDiscussionStrings.RankEstimateQueue, $@"{Screen.WebsiteUrl}/beatmapsets?s=qualified"),
                });

                if (state.FirstUnresolvedDiscussion is BeatmapDiscussion unresolved)
                {
                    var problems = createMessageFlow(FontAwesome.Solid.ExclamationTriangle, DiscussionDisplay.RED_1);

                    DiscussionDisplay.AddPattern(problems, SlopDiscussionStrings.UnresolvedProblems, new Dictionary<string, Action<LinkFlowContainer>>
                    {
                        [@"0"] = f => f.AddLink(SlopDiscussionStrings.Problems, () =>
                        {
                            state.View.ScrollToHighlighted = true;

                            if (!state.ChangeToDiscussion(unresolved.Id))
                                state.View.ScrollToHighlighted = false;
                        }),
                    });
                }
            }

            if (beatmapSet.DiscussionLocked)
            {
                string reason = state.BeatmapSet.Events.LastOrDefault(e => e.Type == @"discussion_lock")?.Comment is JObject comment
                                && comment[@"reason"]?.Type == JTokenType.String
                    ? comment[@"reason"]!.Value<string>() ?? string.Empty
                    : string.Empty;

                addMessage(SlopDiscussionStrings.DiscussionLocked(reason), FontAwesome.Solid.Lock, DiscussionDisplay.ORANGE_1);
            }
        }

        /// <summary>
        /// The buttons of the website for nominators, moderators and the mapper.
        /// </summary>
        private void addActions(DiscussionsState state)
        {
            var beatmapSet = state.BeatmapSet;
            var attributes = beatmapSet.CurrentUserAttributes;

            if (beatmapSet.DeletedAt != null)
                return;

            if (beatmapSet.Status == @"pending" && Screen.IsCurrentUserNominator)
            {
                bool nominated = beatmapSet.Nominations?.Nominated == true;
                bool ownMap = state.CurrentUserId == beatmapSet.UserId;

                if (!nominated && !ownMap)
                    addAction(SlopDiscussionStrings.NominateButton, DiscussionDisplay.BLUE_DARKER, () => Screen.Nominate(), state.FirstUnresolvedDiscussion == null);

                if (state.NominationsCurrent > 0)
                    addAction(SlopDiscussionStrings.ResetNominationsButton, DiscussionDisplay.YELLOW_DARKER, () => Screen.FocusNewDiscussion(@"problem"));
            }

            if (beatmapSet.Status == @"qualified" && Screen.IsCurrentUserModerator)
                addAction(SlopDiscussionStrings.DisqualifyButton, DiscussionDisplay.RED_3, () => Screen.FocusNewDiscussion(@"problem"));

            if (attributes?.CanLove == true)
                addAction(SlopDiscussionStrings.LoveButton, DiscussionDisplay.PINK_DARKER, () => Screen.Love());

            if (attributes?.CanRemoveFromLoved == true)
                addAction(SlopDiscussionStrings.RemoveFromLovedButton, DiscussionDisplay.PINK_DARKER, () => Screen.RemoveFromLoved());

            if (Screen.IsCurrentUserModerator)
            {
                if (beatmapSet.DiscussionLocked)
                    addAction(SlopDiscussionStrings.UnlockButton, DiscussionDisplay.ORANGE_3, () => Screen.UnlockDiscussion());
                else
                    addAction(SlopDiscussionStrings.LockButton, DiscussionDisplay.ORANGE_3, () => Screen.LockDiscussion());
            }

            if (attributes?.CanDelete == true)
                addAction(SlopDiscussionStrings.DeleteBeatmapSetButton, DiscussionDisplay.RED_3, () => Screen.DeleteBeatmapSet());
        }

        private void addAction(LocalisableString text, Color4 colour, Action action, bool enabled = true)
        {
            actions.Add(new RoundedButton
            {
                Width = 170,
                Height = 30,
                Text = text,
                BackgroundColour = colour,
                Enabled = { Value = enabled },
                Action = action,
            });
        }

        private void addResetMessage(DiscussionsState state, DiscussionNominations nominations)
        {
            var disqualification = nominations.Disqualification;
            var reset = nominations.NominationReset;

            if (disqualification == null && reset == null)
                return;

            var flow = createMessageFlow(FontAwesome.Solid.ExclamationCircle, DiscussionDisplay.ORANGE_1);

            if (disqualification != null && (reset == null || disqualification.CreatedAt > reset.CreatedAt))
            {
                int? discussionId = disqualification.CommentDiscussionId;

                DiscussionDisplay.AddPattern(flow, SlopDiscussionStrings.DisqualifiedAt, new Dictionary<string, Action<LinkFlowContainer>>
                {
                    [@"0"] = f => f.AddArbitraryDrawable(new DrawableDate(disqualification.CreatedAt, 14, false)),
                    [@"1"] = f =>
                    {
                        if (discussionId is int id)
                        {
                            DiscussionLinks.AddDiscussion(f, state, id, bold);
                            f.AddText(@": ");
                            f.AddText(discussionPreview(state, id));
                        }
                        else
                            f.AddText(SlopDiscussionStrings.DisqualifiedNoReason);
                    },
                });

                return;
            }

            var resetEvent = reset!;
            int? resetDiscussionId = resetEvent.CommentDiscussionId;

            DiscussionDisplay.AddPattern(flow, resetEvent.Type == @"disqualify" ? SlopDiscussionStrings.ResetAtDisqualify : SlopDiscussionStrings.ResetAtNominationReset,
                new Dictionary<string, Action<LinkFlowContainer>>
                {
                    [@"0"] = f => f.AddArbitraryDrawable(new DrawableDate(resetEvent.CreatedAt, 14, false)),
                    [@"1"] = f => DiscussionLinks.AddUser(f, state, resetEvent.UserId, bold),
                    [@"2"] = f =>
                    {
                        if (resetDiscussionId is int id)
                            DiscussionLinks.AddDiscussion(f, state, id, bold);
                    },
                    [@"3"] = f => f.AddText(resetDiscussionId is int id ? discussionPreview(state, id) : SlopDiscussionStrings.ResetMessageDeleted.ToString()),
                });
        }

        private static string discussionPreview(DiscussionsState state, int discussionId)
        {
            var post = state.GetDiscussion(discussionId)?.StartingPost;

            if (post == null || post.DeletedAt != null)
                return SlopDiscussionStrings.ResetMessageDeleted.ToString();

            return DiscussionDisplay.PlainTextPreview(post.MessageText, 100);
        }

        private static string formatDate(DateTimeOffset date) => date.ToLocalTime().ToString(@"d MMMM yyyy", CultureInfo.CurrentCulture);

        private static void bold(SpriteText s) => s.Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold);

        private LinkFlowContainer createTextFlow() => new LinkFlowContainer(s =>
        {
            s.Font = OsuFont.Default.With(size: 14);
            s.Colour = ColourProvider.Content1;
        })
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
        };

        private void addMessage(LocalisableString text, IconUsage icon, Color4 iconColour)
        {
            createMessageFlow(icon, iconColour).AddText(text);
        }

        private LinkFlowContainer createMessageFlow(IconUsage icon, Color4 iconColour)
        {
            LinkFlowContainer flow;

            messages.Add(new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, 24),
                    new Dimension(),
                },
                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Icon = icon,
                            Size = new Vector2(14),
                            Colour = iconColour,
                            Margin = new MarginPadding { Top = 2 },
                        },
                        flow = createTextFlow(),
                    }
                }
            });

            return flow;
        }

        /// <summary>
        /// A bar of segments, of which as many as reached are filled, like the hype and nomination meters of the website.
        /// </summary>
        private partial class SegmentedBar : FillFlowContainer
        {
            private const float max_segments = 20;

            private readonly int current;
            private readonly int required;
            private readonly Color4 colour;

            public SegmentedBar(int current, int required, Color4 colour)
            {
                this.current = current;
                this.required = required;
                this.colour = colour;

                RelativeSizeAxes = Axes.X;
                Height = 8;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(3, 0);
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour osuColour, Overlays.OverlayColourProvider colourProvider)
            {
                // too many segments don't fit, in which case it's shown as one bar.
                if (required > max_segments)
                {
                    Add(new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 4,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background6,
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Width = Math.Clamp((float)current / required, 0, 1),
                                Colour = colour,
                            },
                        }
                    });

                    return;
                }

                float width = 1f / required;

                for (int i = 0; i < required; i++)
                {
                    Add(new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Width = width,
                        Padding = new MarginPadding { Right = i < required - 1 ? 3 : 0 },
                        Child = new CircularContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = i < current ? colour : colourProvider.Background6,
                            },
                        },
                    });
                }

                Spacing = Vector2.Zero;
            }
        }
    }
}
