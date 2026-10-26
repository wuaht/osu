// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osuTK;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The discussion of the beatmap set, like the discussion page on the website.
    /// Timestamps open the editor, and selecting a difficulty switches the editor to it.
    /// </summary>
    [Cached]
    public partial class DiscussionScreen : EditorScreen, ILinkHandler
    {
        public const float SPACING = 10;

        /// <summary>
        /// The width of the content, like the maximum width of the website.
        /// </summary>
        public const float CONTENT_WIDTH = 1000;

        [Cached]
        private readonly DiscussionSession session = new DiscussionSession();

        /// <summary>
        /// The shown discussion, which is replaced whenever the discussion is loaded again.
        /// </summary>
        public readonly Bindable<DiscussionsState?> CurrentState = new Bindable<DiscussionsState?>();

        internal DiscussionSession Session => session;

        [Resolved]
        private Editor? editor { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private readonly IBindable<DiscussionSessionState> sessionState = new Bindable<DiscussionSessionState>();
        private readonly IBindable<WebSessionState> webSessionState = new Bindable<WebSessionState>();

        private OsuScrollContainer scroll = null!;
        private Container mainContent = null!;
        private DiscussionStateDisplay stateDisplay = null!;

        /// <summary>
        /// The beatmap which was selected last, to switch the editor to a beatmap once another one is selected.
        /// </summary>
        private int? lastBeatmapId;

        public DiscussionScreen()
            : base(EditorScreenMode.Discussion)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                session,
                mainContent = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        scroll = new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = new FillFlowContainer
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding { Horizontal = SPACING * 2, Vertical = SPACING },
                                Spacing = new Vector2(0, SPACING),
                                Children = new Drawable[]
                                {
                                    new DiscussionHeader(),
                                    new DiscussionNominationStatus(),
                                    new DiscussionToolbar(),
                                    new DiscussionModeSwitcher(),
                                    new DiscussionNewDiscussion(),
                                    new DiscussionList(),
                                    new DiscussionEventsList(),
                                }
                            }
                        },
                        new DiscussionRefreshButton
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Margin = new MarginPadding(SPACING * 2),
                        },
                    }
                },
                stateDisplay = new DiscussionStateDisplay(),
                promptOverlay = new DiscussionPromptOverlay(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            sessionState.BindTo(session.State);
            sessionState.BindValueChanged(s =>
            {
                bool ready = s.NewValue == DiscussionSessionState.Ready;

                mainContent.FadeTo(ready ? 1 : 0, 200, Easing.OutQuint);
                stateDisplay.FadeTo(ready ? 0 : 1, 200, Easing.OutQuint);
            }, true);

            session.BeatmapSet.BindValueChanged(b => updateState(b.NewValue), true);

            if (webSession != null)
            {
                webSessionState.BindTo(webSession.State);

                // once signed into the website, the discussion is loaded again with what the user is allowed to do in it.
                webSessionState.BindValueChanged(s =>
                {
                    if (s.NewValue == WebSessionState.LoggedIn && session.BeatmapSet.Value != null)
                        session.Refresh();
                });
            }
        }

        private void updateState(DiscussionBeatmapSet? beatmapSet)
        {
            if (CurrentState.Value != null)
                CurrentState.Value.Changed -= onStateChanged;

            if (beatmapSet == null || editor == null)
            {
                CurrentState.Value = null;
                return;
            }

            bool firstLoad = CurrentState.Value == null;

            var state = new DiscussionsState(beatmapSet, editor.DiscussionState, api.LocalUser.Value?.OnlineID is int id && id > 1 ? id : null);

            // show the difficulty which is open in the editor when opening the screen.
            if (firstLoad && EditorBeatmap.BeatmapInfo.OnlineID is int beatmapId && state.Beatmaps.ContainsKey(beatmapId))
                state.View.CurrentBeatmapId = beatmapId;

            lastBeatmapId = state.CurrentBeatmapId;

            state.Changed += onStateChanged;
            CurrentState.Value = state;
        }

        private void onStateChanged()
        {
            var state = CurrentState.Value;

            if (state == null || state.CurrentBeatmapId == lastBeatmapId)
                return;

            lastBeatmapId = state.CurrentBeatmapId;

            // the general (all difficulties) page isn't about a difficulty, so the editor stays on the current one.
            if (state.View.Page == DiscussionPage.GeneralAll || state.View.Page == DiscussionPage.Reviews || state.View.Page == DiscussionPage.Events)
                return;

            var difficulty = findDifficulty(state.CurrentBeatmapId);

            if (difficulty != null && !difficulty.Equals(EditorBeatmap.BeatmapInfo))
                editor?.SwitchToDifficulty(difficulty);
        }

        /// <summary>
        /// Finds the local difficulty of a beatmap of the beatmap set.
        /// </summary>
        private BeatmapInfo? findDifficulty(int? beatmapId)
        {
            if (beatmapId is not int id || id <= 0)
                return null;

            return EditorBeatmap.BeatmapInfo.BeatmapSet?.Beatmaps.FirstOrDefault(b => b.OnlineID == id);
        }

        protected override void PopIn()
        {
            base.PopIn();
            session.SetActive(true);
        }

        protected override void PopOut()
        {
            base.PopOut();
            session.SetActive(false);
        }

        /// <summary>
        /// Shows an editor timestamp of a discussion in the compose screen, on the difficulty of the discussion.
        /// </summary>
        /// <param name="timestamp">The editor timestamp, e.g. "00:12:345 (1,2) -".</param>
        /// <param name="beatmapId">The ID of the beatmap of the discussion, or null for discussions about all difficulties.</param>
        public void ShowTimestamp(string timestamp, int? beatmapId)
        {
            editor?.ShowTimestampInCompose(timestamp, findDifficulty(beatmapId));
        }

        /// <summary>
        /// Scrolls to a drawable in the discussion.
        /// </summary>
        public void ScrollIntoView(Drawable drawable) => scroll.ScrollIntoView(drawable, true);

        /// <summary>
        /// Invoked when the box for posting a new discussion should be focused, with the type of the discussion to post.
        /// </summary>
        public event Action<string?>? NewDiscussionRequested;

        /// <summary>
        /// Focuses the box for posting a new discussion, like the hype and feedback buttons of the website.
        /// </summary>
        /// <param name="messageType">The type of the discussion to post, or null to keep the selected one.</param>
        public void FocusNewDiscussion(string? messageType)
        {
            var state = CurrentState.Value;

            if (state == null)
                return;

            // hypes are only about all difficulties, and neither events nor reviews have the box.
            if (messageType == @"hype" || state.View.Page is DiscussionPage.Events or DiscussionPage.Reviews)
                state.ChangeDiscussionPage(DiscussionPage.GeneralAll);

            NewDiscussionRequested?.Invoke(messageType);
        }

        /// <summary>
        /// Opens a page of the website, for everything which can only be done there.
        /// </summary>
        public void OpenOnWebsite(string url) => game?.OpenUrlExternally(url, LinkWarnMode.NeverWarn);

        public string WebsiteUrl => api.Endpoints.WebsiteUrl;

        public void HandleLink(string url)
        {
            var state = CurrentState.Value;

            // links to discussions of this beatmap set are shown here instead of opening the website.
            if (state != null && DiscussionDisplay.TryParseDiscussionLink(url, out int beatmapSetId, out int discussionId) && beatmapSetId == state.BeatmapSet.Id)
            {
                state.View.ScrollToHighlighted = true;

                if (state.ChangeToDiscussion(discussionId))
                    return;

                state.View.ScrollToHighlighted = false;
            }

            if (game != null)
                game.HandleLink(url);
        }

        public void HandleLink(LinkDetails link)
        {
            if (link.Action == LinkAction.External && link.Argument is string url)
            {
                HandleLink(url);
                return;
            }

            game?.HandleLink(link);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (CurrentState.Value != null)
                CurrentState.Value.Changed -= onStateChanged;

            base.Dispose(isDisposing);
        }
    }
}
