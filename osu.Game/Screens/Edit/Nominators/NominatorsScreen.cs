// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Online.BnTracker;
using osu.Game.Online.Chat;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// Tracks which Beatmap Nominators were asked to nominate the beatmap set, using the BN Tracker.
    /// Shows a card for every relevant nominator, which can be filtered in the sidebar, and all details of a nominator when clicking their card.
    /// </summary>
    [Cached]
    public partial class NominatorsScreen : EditorScreen, IKeyBindingHandler<GlobalAction>
    {
        private const float spacing = 10;

        /// <summary>
        /// Whether cards are selected by clicking them, to change the status of multiple nominators at once.
        /// </summary>
        public readonly BindableBool SelectionMode = new BindableBool();

        /// <summary>
        /// The IDs of the selected nominators.
        /// </summary>
        public readonly BindableList<int> Selection = new BindableList<int>();

        [Cached]
        private readonly NominatorsSession session = new NominatorsSession();

        [Cached]
        private readonly NominatorFilter filter = new NominatorFilter();

        internal NominatorsSession Session => session;

        internal NominatorFilter Filter => filter;

        internal bool DetailsShown => details != null;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private UserProfileOverlay? profileOverlay { get; set; }

        private readonly IBindable<NominatorsSessionState> state = new Bindable<NominatorsSessionState>();

        private GridContainer mainContent = null!;
        private NominatorsStateDisplay stateDisplay = null!;
        private NominatorFilterSidebar sidebar = null!;
        private Container detailsContainer = null!;

        private NominatorDetails? details;

        public NominatorsScreen()
            : base(EditorScreenMode.Nominators)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(spacing),
                Children = new Drawable[]
                {
                    session,
                    mainContent = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, spacing),
                            new Dimension(GridSizeMode.Absolute, NominatorFilterSidebar.WIDTH),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    RowDimensions = new[]
                                    {
                                        new Dimension(GridSizeMode.Absolute, BeatmapSetHeader.HEIGHT),
                                        new Dimension(GridSizeMode.Absolute, spacing),
                                        new Dimension(),
                                    },
                                    Content = new[]
                                    {
                                        new Drawable[] { new BeatmapSetHeader() },
                                        new[] { Empty() },
                                        new Drawable[] { new NominatorGrid() },
                                    }
                                },
                                Empty(),
                                sidebar = new NominatorFilterSidebar(),
                            }
                        }
                    },
                    stateDisplay = new NominatorsStateDisplay(),
                    detailsContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            state.BindTo(session.State);
            state.BindValueChanged(s =>
            {
                bool ready = s.NewValue == NominatorsSessionState.Ready;

                mainContent.FadeTo(ready ? 1 : 0, 200, Easing.OutQuint);
                stateDisplay.FadeTo(ready ? 0 : 1, 200, Easing.OutQuint);

                if (!ready)
                {
                    CloseDetails();
                    SelectionMode.Value = false;
                }
            }, true);

            SelectionMode.BindValueChanged(_ => Selection.Clear());
        }

        protected override void PopIn()
        {
            base.PopIn();

            // changes may have been made on the website in the meantime.
            session.SetActive(true);
        }

        protected override void PopOut()
        {
            base.PopOut();
            session.SetActive(false);
        }

        public void ShowDetails(int nominatorId)
        {
            if (details?.NominatorId == nominatorId)
                return;

            details?.Expire();

            detailsContainer.Add(details = new NominatorDetails(nominatorId));
            details.FadeInFromZero(200, Easing.OutQuint);

            mainContent.FadeOut(200, Easing.OutQuint);
        }

        public void CloseDetails()
        {
            if (details == null)
                return;

            details.FadeOut(200, Easing.OutQuint).Expire();
            details = null;

            if (state.Value == NominatorsSessionState.Ready)
                mainContent.FadeIn(200, Easing.OutQuint);
        }

        public void ToggleSelected(int nominatorId)
        {
            if (!Selection.Remove(nominatorId))
                Selection.Add(nominatorId);
        }

        public void SelectAll(IEnumerable<int> nominatorIds) => Selection.AddRange(nominatorIds.Where(id => !Selection.Contains(id)).ToArray());

        /// <summary>
        /// Shows the osu! profile of a nominator, in the game while logged in and otherwise in the browser.
        /// </summary>
        public void ShowProfile(BnNominator nominator)
        {
            if (api.State.Value == APIState.Online && profileOverlay != null)
                profileOverlay.ShowUser(NominatorsDisplay.CreateUser(nominator));
            else
                OpenUrl(string.IsNullOrEmpty(nominator.ProfileUrl) ? $@"https://osu.ppy.sh/users/{nominator.OsuId}" : nominator.ProfileUrl, true);
        }

        /// <param name="url">The URL to open.</param>
        /// <param name="trusted">Whether the URL is known to be safe (e.g. the osu! website), such that the external link warning isn't shown.</param>
        public void OpenUrl(string url, bool trusted) => game?.OpenUrlExternally(url, trusted ? LinkWarnMode.NeverWarn : LinkWarnMode.Default);

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.Back:
                    if (details != null)
                    {
                        CloseDetails();
                        return true;
                    }

                    if (SelectionMode.Value)
                    {
                        SelectionMode.Value = false;
                        return true;
                    }

                    return false;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // ctrl+f focuses the search, like in other searchable lists.
            if (e.ControlPressed && !e.AltPressed && !e.ShiftPressed && e.Key == osuTK.Input.Key.F && state.Value == NominatorsSessionState.Ready && details == null)
            {
                sidebar.FocusSearch();
                return true;
            }

            return base.OnKeyDown(e);
        }
    }
}
