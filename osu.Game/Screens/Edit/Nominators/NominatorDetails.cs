// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.BnTracker;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// Everything about one nominator: how to request them, their preferences and history, and the comments and activity of the mapper with them.
    /// </summary>
    public partial class NominatorDetails : CompositeDrawable
    {
        private const float header_height = 190;
        private const float section_spacing = 20;

        public readonly int NominatorId;

        [Resolved]
        private NominatorsSession session { get; set; } = null!;

        [Resolved]
        private NominatorsScreen screen { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        [Resolved]
        private ChannelManager? channelManager { get; set; }

        [Resolved]
        private ChatOverlay? chatOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        private readonly IBindable<BnBeatmapSetWithNominators?> data = new Bindable<BnBeatmapSetWithNominators?>();

        private BnSetNominator? nominator;

        private OsuSpriteText username = null!;
        private FillFlowContainer groups = null!;
        private FillFlowContainer tags = null!;
        private StatusButton statusButton = null!;
        private FillFlowContainer actions = null!;
        private FillFlowContainer infoFlow = null!;
        private FillFlowContainer commentsFlow = null!;
        private FillFlowContainer activityFlow = null!;
        private OsuTextBox commentTextBox = null!;
        private RoundedButton addCommentButton = null!;
        private OsuSpriteText commentsHeader = null!;

        public NominatorDetails(int nominatorId)
        {
            NominatorId = nominatorId;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            nominator = session.Data.Value?.Nominators.FirstOrDefault(n => n.NominatorOsuId == NominatorId);

            var user = nominator != null ? NominatorsDisplay.CreateUser(nominator.Nominator) : null;

            InternalChild = new NominatorsPanel
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = header_height,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new CoverBackground
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Model = user,
                                Alpha = 0.5f,
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.2f), Color4.Black.Opacity(0.75f)),
                            },
                            new IconButton
                            {
                                Margin = new MarginPadding(10),
                                Icon = FontAwesome.Solid.ArrowLeft,
                                TooltipText = SlopNominatorsStrings.Back,
                                Action = () => screen.CloseDetails(),
                            },
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding { Horizontal = 25, Top = 50, Bottom = 18 },
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.Absolute, 110),
                                    new Dimension(GridSizeMode.Absolute, 18),
                                    new Dimension(),
                                    new Dimension(GridSizeMode.AutoSize),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        new UpdateableAvatar(user, false)
                                        {
                                            Size = new Vector2(110),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Masking = true,
                                            CornerRadius = 10,
                                        },
                                        Empty(),
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 6),
                                            Children = new Drawable[]
                                            {
                                                new OsuClickableContainer
                                                {
                                                    AutoSizeAxes = Axes.Both,
                                                    TooltipText = SlopNominatorsStrings.ViewProfile,
                                                    Action = () =>
                                                    {
                                                        if (nominator != null)
                                                            screen.ShowProfile(nominator.Nominator);
                                                    },
                                                    Child = username = new OsuSpriteText
                                                    {
                                                        Font = OsuFont.Default.With(size: 30, weight: FontWeight.Bold),
                                                    },
                                                },
                                                groups = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Full,
                                                    Spacing = new Vector2(5),
                                                },
                                                tags = new FillFlowContainer
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    AutoSizeAxes = Axes.Y,
                                                    Direction = FillDirection.Full,
                                                    Spacing = new Vector2(5),
                                                },
                                            }
                                        },
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.BottomRight,
                                            Origin = Anchor.BottomRight,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 10),
                                            Children = new Drawable[]
                                            {
                                                statusButton = new StatusButton(34)
                                                {
                                                    Anchor = Anchor.TopRight,
                                                    Origin = Anchor.TopRight,
                                                    StatusPicked = s => session.SetStatus(NominatorId, s),
                                                },
                                                actions = new FillFlowContainer
                                                {
                                                    Anchor = Anchor.TopRight,
                                                    Origin = Anchor.TopRight,
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(6, 0),
                                                },
                                            }
                                        },
                                    }
                                }
                            },
                        }
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = header_height },
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 1),
                            new Dimension(GridSizeMode.Relative, 0.42f),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = infoFlow = createColumn(),
                                },
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.White,
                                    Alpha = 0.1f,
                                },
                                new OsuScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Child = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 10),
                                        Padding = new MarginPadding(20),
                                        Children = new Drawable[]
                                        {
                                            commentsHeader = createSectionHeader(SlopNominatorsStrings.Comments),
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                ColumnDimensions = new[]
                                                {
                                                    new Dimension(),
                                                    new Dimension(GridSizeMode.Absolute, 8),
                                                    new Dimension(GridSizeMode.AutoSize),
                                                },
                                                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                                Content = new[]
                                                {
                                                    new Drawable[]
                                                    {
                                                        commentTextBox = new OsuTextBox
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            Height = 36,
                                                            PlaceholderText = SlopNominatorsStrings.AddCommentPlaceholder,
                                                            LengthLimit = BnTrackerLimits.MAX_COMMENT_LENGTH,
                                                            CommitOnFocusLost = false,
                                                        },
                                                        Empty(),
                                                        addCommentButton = new RoundedButton
                                                        {
                                                            Width = 70,
                                                            Height = 36,
                                                            Text = SlopNominatorsStrings.AddComment,
                                                            Action = addComment,
                                                        },
                                                    }
                                                }
                                            },
                                            commentsFlow = new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 8),
                                            },
                                            createSectionHeader(SlopNominatorsStrings.Activity).With(h => h.Margin = new MarginPadding { Top = section_spacing }),
                                            activityFlow = new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.X,
                                                AutoSizeAxes = Axes.Y,
                                                Direction = FillDirection.Vertical,
                                                Spacing = new Vector2(0, 8),
                                            },
                                        }
                                    }
                                },
                            }
                        }
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            commentTextBox.OnCommit += (_, _) => addComment();
            commentTextBox.Current.BindValueChanged(_ => updateAddCommentButton(), true);

            data.BindTo(session.Data);
            data.BindValueChanged(_ => updateDisplay(), true);
        }

        // the cards behind the details can't be interacted with.
        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e) => true;

        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnScroll(ScrollEvent e) => true;

        private void updateAddCommentButton() => addCommentButton.Enabled.Value = nominator != null && !string.IsNullOrWhiteSpace(commentTextBox.Text);

        private void addComment()
        {
            string text = commentTextBox.Text;

            if (nominator == null || string.IsNullOrWhiteSpace(text))
                return;

            session.AddComment(NominatorId, text, () =>
            {
                // the text box may have been edited while the comment was sent.
                if (commentTextBox.Text == text)
                    commentTextBox.Text = string.Empty;
            });
        }

        private void updateDisplay()
        {
            nominator = data.Value?.Nominators.FirstOrDefault(n => n.NominatorOsuId == NominatorId);

            // e.g. stopped tracking the beatmap set, or the nominator isn't relevant to it anymore.
            if (nominator == null)
            {
                if (data.Value != null)
                    screen.CloseDetails();

                return;
            }

            var n = nominator.Nominator;

            username.Text = n.Username;

            groups.Clear();
            foreach (var mode in nominator.RelevantModes)
                groups.Add(NominatorCard.CreateGroupTag(rulesets, mode));

            // the groups in modes which the beatmap set doesn't have.
            foreach (var mode in n.Modes.Where(m => nominator.RelevantModes.All(r => r.Mode != m.Mode || r.Level != m.Level)))
                groups.Add(NominatorCard.CreateGroupTag(rulesets, mode).With(t => t.Alpha = 0.5f));

            tags.Clear();
            foreach (var tag in NominatorCard.CreateTags(nominator))
                tags.Add(tag);

            statusButton.Status = nominator.Status;
            statusButton.Enabled.Value = !n.IsRemoved;

            actions.Clear();

            if (!string.IsNullOrEmpty(n.RequestLink))
                actions.Add(createActionButton(SlopNominatorsStrings.OpenRequestQueue, () => screen.OpenUrl(n.RequestLink, false)));

            if (n.HasRequestChannel(BnNominator.REQUEST_CHANNEL_GAME_CHAT) && api.State.Value == APIState.Online && channelManager != null)
            {
                actions.Add(createActionButton(SlopNominatorsStrings.SendMessage, () =>
                {
                    channelManager.OpenPrivateChannel(NominatorsDisplay.CreateUser(n));
                    chatOverlay?.Show();
                }));
            }

            actions.Add(createActionButton(SlopNominatorsStrings.CopyUsername, () =>
            {
                clipboard.SetText(n.Username);
                notifications?.Post(new SimpleNotification { Text = SlopNominatorsStrings.UsernameCopied(n.Username) });
            }));

            updateInfo(nominator);
            updateComments(nominator);
            updateActivity(nominator);
            updateAddCommentButton();
        }

        private void updateInfo(BnSetNominator setNominator)
        {
            var n = setNominator.Nominator;

            infoFlow.Clear();

            if (n.IsRemoved)
            {
                infoFlow.Add(createNotice(n.RemovedAt is DateTimeOffset removedAt
                    ? SlopNominatorsStrings.RemovedNoticeSince(removedAt.ToLocalisableString(@"d MMM yyyy"))
                    : SlopNominatorsStrings.RemovedNotice, NominatorsDisplay.REMOVED_COLOUR));
            }

            // how to request
            infoFlow.Add(createSectionHeader(SlopNominatorsStrings.HowToRequest));

            if (!n.IsOpenForRequests && !n.IsRemoved)
                infoFlow.Add(createNotice(SlopNominatorsStrings.NotOpenForRequests, Color4Extensions.FromHex(@"eab308")));

            var channels = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(5),
            };

            if (n.HasRequestChannel(BnNominator.REQUEST_CHANNEL_PERSONAL_QUEUE))
                channels.Add(new NominatorTag(SlopNominatorsStrings.PersonalQueue, Color4.White, textSize: 13));

            if (n.HasRequestChannel(BnNominator.REQUEST_CHANNEL_GAME_CHAT))
                channels.Add(new NominatorTag(SlopNominatorsStrings.GameChat, Color4.White, textSize: 13));

            if (channels.Count == 0)
                channels.Add(new NominatorTag(SlopNominatorsStrings.NotSpecified, Color4.White, textSize: 13));

            infoFlow.Add(channels);

            if (!string.IsNullOrEmpty(n.RequestLink))
            {
                var link = new NominatorTextFlow(13) { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
                link.SetText(n.RequestLink);
                infoFlow.Add(createField(SlopNominatorsStrings.RequestQueue, link));
            }

            infoFlow.Add(createField(SlopNominatorsStrings.LastOpenedForRequests, n.LastOpenedForRequests is DateTimeOffset opened
                ? createDate(opened)
                : createText(SlopNominatorsStrings.NeverOpened)));

            if (!string.IsNullOrWhiteSpace(n.RequestInfo))
            {
                var requestInfo = new NominatorTextFlow { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
                requestInfo.SetText(n.RequestInfo);

                infoFlow.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 6,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                            Alpha = 0.25f,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding(12),
                            Child = requestInfo,
                        },
                    }
                });
            }

            // preferences
            var p = n.Preferences;

            var preferences = new (LocalisableString label, List<string> positive, List<string> negative)[]
            {
                (SlopNominatorsStrings.PreferenceGenres, p.Genres, p.GenresExcluded),
                (SlopNominatorsStrings.PreferenceSongLanguages, p.SongLanguages, p.SongLanguagesExcluded),
                (SlopNominatorsStrings.PreferenceDetails, p.Details, p.DetailsExcluded),
                (SlopNominatorsStrings.PreferenceMappers, p.Mappers, p.MappersExcluded),
                (SlopNominatorsStrings.PreferenceOsuStyles, p.OsuStyles, p.OsuStylesExcluded),
                (SlopNominatorsStrings.PreferenceTaikoStyles, p.TaikoStyles, p.TaikoStylesExcluded),
                (SlopNominatorsStrings.PreferenceCatchStyles, p.CatchStyles, p.CatchStylesExcluded),
                (SlopNominatorsStrings.PreferenceManiaStyles, p.ManiaStyles, p.ManiaStylesExcluded),
                (SlopNominatorsStrings.PreferenceKeymodes, p.ManiaKeymodes, p.ManiaKeymodesExcluded),
                (SlopNominatorsStrings.PreferenceSpokenLanguages, n.SpokenLanguages, new List<string>()),
            }.Where(r => r.positive.Count > 0 || r.negative.Count > 0).ToArray();

            if (preferences.Length > 0)
            {
                infoFlow.Add(createSectionHeader(SlopNominatorsStrings.Preferences).With(h => h.Margin = new MarginPadding { Top = section_spacing }));
                infoFlow.Add(new OsuSpriteText
                {
                    Text = SlopNominatorsStrings.PreferencesHint,
                    Font = OsuFont.Default.With(size: 12),
                    Alpha = 0.6f,
                });

                foreach (var (label, positive, negative) in preferences)
                    infoFlow.Add(createPreferenceRow(label, positive, negative));
            }

            // history
            infoFlow.Add(createSectionHeader(SlopNominatorsStrings.History).With(h => h.Margin = new MarginPadding { Top = section_spacing }));

            if (n.FirstJoinedAt is DateTimeOffset joined)
                infoFlow.Add(createField(SlopNominatorsStrings.FirstJoined, createDate(joined)));

            foreach (var entry in n.History.OrderByDescending(h => h.Date))
            {
                var colour = entry.Kind == BnHistoryEventKind.Joined ? NominatorsDisplay.GetColour(BnNominationStatus.Accepted) : NominatorsDisplay.GetColour(BnNominationStatus.Declined);
                LocalisableString group = entry.Group == BnNominatorGroup.Nat ? SlopNominatorsStrings.LevelNat : SlopNominatorsStrings.LevelFull;

                infoFlow.Add(createTimelineEntry(colour,
                    entry.Kind == BnHistoryEventKind.Joined
                        ? SlopNominatorsStrings.HistoryJoined(group, NominatorsDisplay.GetName(entry.Mode))
                        : SlopNominatorsStrings.HistoryLeft(group, NominatorsDisplay.GetName(entry.Mode)),
                    createDate(entry.Date)));
            }

            infoFlow.Add(createField(SlopNominatorsStrings.FirstSeen, createDate(n.FirstSeenAt)));
            infoFlow.Add(createField(SlopNominatorsStrings.LastSynced, new DrawableDate(n.LastSyncedAt, 13, false)));
        }

        private void updateComments(BnSetNominator setNominator)
        {
            commentsFlow.Clear();

            commentsHeader.Text = setNominator.Comments.Count > 0
                ? LocalisableString.Interpolate($@"{SlopNominatorsStrings.Comments} ({setNominator.Comments.Count})")
                : SlopNominatorsStrings.Comments;

            if (setNominator.Comments.Count == 0)
            {
                commentsFlow.Add(createText(SlopNominatorsStrings.NoCommentsYet).With(t => t.Alpha = 0.6f));
                return;
            }

            foreach (var comment in setNominator.Comments.OrderByDescending(c => c.CreatedAt))
                commentsFlow.Add(new CommentEntry(comment, () => dialogOverlay?.Push(new DeleteCommentDialog(() => session.DeleteComment(NominatorId, comment.Id)))));
        }

        private void updateActivity(BnSetNominator setNominator)
        {
            activityFlow.Clear();

            if (setNominator.Activity.Count == 0)
            {
                activityFlow.Add(createText(SlopNominatorsStrings.NoActivityYet).With(t => t.Alpha = 0.6f));
                return;
            }

            foreach (var entry in setNominator.Activity.OrderByDescending(a => a.Timestamp))
            {
                var timelineEntry = createTimelineEntry(NominatorsDisplay.GetColour(entry.ToStatus),
                    LocalisableString.Interpolate($@"{NominatorsDisplay.GetName(entry.FromStatus)} → {NominatorsDisplay.GetName(entry.ToStatus)}"),
                    new DrawableDate(entry.Timestamp, 12, false));

                if (!string.IsNullOrEmpty(entry.Note))
                {
                    var note = new NominatorTextFlow(13) { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Alpha = 0.8f };
                    note.SetText(entry.Note);
                    timelineEntry.Add(note);
                }

                activityFlow.Add(timelineEntry);
            }
        }

        private static FillFlowContainer createColumn() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 10),
            Padding = new MarginPadding(20),
        };

        private static OsuSpriteText createSectionHeader(LocalisableString text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
        };

        private static OsuSpriteText createText(LocalisableString text) => new OsuSpriteText
        {
            Text = text,
            Font = OsuFont.Default.With(size: 13),
        };

        private static OsuSpriteText createDate(DateTimeOffset date) => new OsuSpriteText
        {
            Text = date.ToLocalTime().ToLocalisableString(@"d MMM yyyy"),
            Font = OsuFont.Default.With(size: 13),
        };

        private RoundedButton createActionButton(LocalisableString text, Action action) => new RoundedButton
        {
            Width = 160,
            Height = 30,
            Text = text,
            Action = action,
        };

        /// <summary>
        /// A label with a value next to it.
        /// </summary>
        private static Drawable createField(LocalisableString label, Drawable value) => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, 170),
                new Dimension(),
            },
            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
            Content = new[]
            {
                new[]
                {
                    new OsuSpriteText
                    {
                        Text = label,
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                        Alpha = 0.7f,
                    },
                    value,
                }
            }
        };

        private static Drawable createNotice(LocalisableString text, Color4 colour) => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Masking = true,
            CornerRadius = 6,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                    Alpha = 0.15f,
                },
                new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(10),
                    Colour = colour,
                    Text = text,
                },
            }
        };

        private static Drawable createPreferenceRow(LocalisableString label, IEnumerable<string> positive, IEnumerable<string> negative)
        {
            var values = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(5),
            };

            foreach (string value in positive)
                values.Add(new NominatorTag(NominatorsDisplay.Capitalise(value), Color4.White, textSize: 12));

            foreach (string value in negative)
            {
                values.Add(new NominatorTag(NominatorsDisplay.Capitalise(value), NominatorsDisplay.GetColour(BnNominationStatus.Declined),
                    new SpriteIcon { Icon = FontAwesome.Solid.Ban, Size = new Vector2(9) }, 12)
                {
                    TooltipText = SlopNominatorsStrings.Excluded,
                });
            }

            return createField(label, values);
        }

        /// <summary>
        /// An entry with a coloured dot, a title and a date, for activity and history.
        /// </summary>
        private static FillFlowContainer createTimelineEntry(Color4 colour, LocalisableString title, Drawable date)
        {
            date.Alpha = 0.7f;

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Padding = new MarginPadding { Left = 16 },
                Children = new[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new Circle
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(8),
                                X = -14,
                                Colour = colour,
                            },
                            new OsuSpriteText
                            {
                                Text = title,
                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                            },
                        }
                    },
                    date,
                }
            };
        }

        private partial class CommentEntry : CompositeDrawable
        {
            public CommentEntry(BnComment comment, Action delete)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 6;

                var text = new NominatorTextFlow { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
                text.SetText(comment.Text);

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.25f,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Padding = new MarginPadding { Horizontal = 12, Top = 8, Bottom = 10 },
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 20,
                                Children = new Drawable[]
                                {
                                    new DrawableDate(comment.CreatedAt, 12, false)
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Alpha = 0.7f,
                                    },
                                    new IconButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Size = new Vector2(20),
                                        IconScale = new Vector2(0.6f),
                                        Icon = FontAwesome.Solid.TrashAlt,
                                        TooltipText = SlopNominatorsStrings.DeleteComment,
                                        Action = delete,
                                    },
                                }
                            },
                            text,
                        }
                    },
                };
            }
        }

        private partial class DeleteCommentDialog : DangerousActionDialog
        {
            public DeleteCommentDialog(Action confirm)
            {
                HeaderText = SlopNominatorsStrings.DeleteCommentConfirmation;
                BodyText = SlopNominatorsStrings.DeleteCommentConfirmationBody;
                DangerousAction = confirm;
            }
        }
    }
}
