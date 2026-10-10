// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.BeatmapMirrors;
using osu.Game.Resources.Localisation.Web;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapListing
{
    public partial class BeatmapListingFilterControl : CompositeDrawable
    {
        /// <summary>
        /// Fired when a search finishes.
        /// </summary>
        public Action<SearchResult> SearchFinished;

        /// <summary>
        /// Fired when search criteria change.
        /// </summary>
        public Action SearchStarted;

        /// <summary>
        /// Any time the search text box receives key events (even while masked).
        /// </summary>
        public Action TypingStarted;

        /// <summary>
        /// True when pagination has reached the end of available results.
        /// </summary>
        private bool noMoreResults;

        /// <summary>
        /// The current page fetched of results (zero index).
        /// </summary>
        public int CurrentPage { get; private set; }

        /// <summary>
        /// The currently selected <see cref="BeatmapCardSize"/>.
        /// </summary>
        public IBindable<BeatmapCardSize> CardSize => cardSize;

        private readonly Bindable<BeatmapCardSize> cardSize = new Bindable<BeatmapCardSize>();

        private readonly BeatmapListingSearchControl searchControl;
        private readonly BeatmapListingSortTabControl sortControl;
        private readonly Box sortControlBackground;

        private ScheduledDelegate queryChangedDebounce;

        private SearchBeatmapSetsRequest getSetsRequest;
        private SearchBeatmapSetsResponse lastResponse;

        private BeatmapMirrorLookup<List<APIBeatmapSet>> mirrorSearch;
        private bool mirrorPageFetched;

        /// <summary>
        /// The index of the next page to request from the beatmap mirror.
        /// This may run ahead of <see cref="CurrentPage"/>, as pages without any results matching the filters are skipped.
        /// </summary>
        private int nextMirrorPage;

        /// <summary>
        /// The maximum number of consecutive mirror pages without results matching the filters to skip before giving up.
        /// </summary>
        private const int max_skipped_mirror_pages = 10;

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved(CanBeNull = true)]
        private BeatmapMirrorProvider mirrors { get; set; }

        private IBindable<APIUser> apiUser;

        public BeatmapListingFilterControl()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Masking = true,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Colour = Color4.Black.Opacity(0.25f),
                            Type = EdgeEffectType.Shadow,
                            Radius = 3,
                            Offset = new Vector2(0f, 1f),
                        },
                        Child = searchControl = new BeatmapListingSearchControl
                        {
                            TypingStarted = () => TypingStarted?.Invoke()
                        }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                        Children = new Drawable[]
                        {
                            sortControlBackground = new Box
                            {
                                RelativeSizeAxes = Axes.Both
                            },
                            sortControl = new BeatmapListingSortTabControl
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Margin = new MarginPadding { Left = 20 }
                            },
                            new BeatmapListingCardSizeTabControl
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Margin = new MarginPadding { Right = 20 },
                                Current = { BindTarget = CardSize }
                            }
                        }
                    }
                }
            };
        }

        [Resolved]
        private OsuConfigManager config { get; set; }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            sortControlBackground.Colour = colourProvider.Background4;
        }

        public void Search(string query)
            => Schedule(() => searchControl.Query.Value = query);

        public void FilterGenre(SearchGenre genre)
            => Schedule(() => searchControl.Genre.Value = genre);

        public void FilterLanguage(SearchLanguage language)
            => Schedule(() => searchControl.Language.Value = language);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            config.BindWith(OsuSetting.BeatmapListingCardSize, cardSize);

            searchControl.Query.BindValueChanged(_ =>
            {
                resetSortControl();
                queueUpdateSearch(true);
            });

            searchControl.Category.BindValueChanged(_ =>
            {
                resetSortControl();
                queueUpdateSearch();
            });

            searchControl.General.CollectionChanged += (_, _) => queueUpdateSearch();
            searchControl.Ruleset.BindValueChanged(_ => queueUpdateSearch());
            searchControl.Genre.BindValueChanged(_ => queueUpdateSearch());
            searchControl.Language.BindValueChanged(_ => queueUpdateSearch());
            searchControl.Extra.CollectionChanged += (_, _) => queueUpdateSearch();
            searchControl.Ranks.CollectionChanged += (_, _) => queueUpdateSearch();
            searchControl.Played.BindValueChanged(_ => queueUpdateSearch());
            searchControl.ExplicitContent.BindValueChanged(_ => queueUpdateSearch());

            sortControl.Current.BindValueChanged(_ => queueUpdateSearch());
            sortControl.SortDirection.BindValueChanged(_ => queueUpdateSearch());

            apiUser = api.LocalUser.GetBoundCopy();
            apiUser.BindValueChanged(_ => queueUpdateSearch());
        }

        public void TakeFocus() => searchControl.TakeFocus();

        /// <summary>
        /// Fetch the next page of results. May result in a no-op if a fetch is already in progress, or if there are no results left.
        /// </summary>
        public void FetchNextPage()
        {
            // there may be no results left.
            if (noMoreResults)
                return;

            // there may already be an active request.
            if (getSetsRequest != null || mirrorSearch != null)
                return;

            if (lastResponse != null || mirrorPageFetched)
                CurrentPage++;

            performRequest();
        }

        private void resetSortControl() => sortControl.Reset(searchControl.Category.Value, !string.IsNullOrEmpty(searchControl.Query.Value));

        private void queueUpdateSearch(bool queryTextChanged = false)
        {
            SearchStarted?.Invoke();

            resetSearch();

            // beatmap mirrors can be searched without logging in.
            if (!api.IsLoggedIn && mirrors == null)
                return;

            queryChangedDebounce = Scheduler.AddDelayed(() =>
            {
                resetSearch();
                FetchNextPage();
            }, queryTextChanged ? 500 : 100);
        }

        private void performRequest()
        {
            if (mirrors?.IsActive == true)
            {
                performMirrorRequest();
                return;
            }

            getSetsRequest = new SearchBeatmapSetsRequest(
                searchControl.Query.Value,
                searchControl.Ruleset.Value,
                lastResponse?.Cursor,
                searchControl.General,
                searchControl.Category.Value,
                sortControl.Current.Value,
                sortControl.SortDirection.Value,
                searchControl.Genre.Value,
                searchControl.Language.Value,
                searchControl.Extra,
                searchControl.Ranks,
                searchControl.Played.Value,
                searchControl.ExplicitContent.Value);

            getSetsRequest.Success += response =>
            {
                var sets = response.BeatmapSets.ToList();

                // If the previous request returned a null cursor, the API is indicating we can't paginate further (maybe there are no more beatmaps left).
                if (sets.Count == 0 || response.Cursor == null)
                    noMoreResults = true;

                lastResponse = response;
                getSetsRequest = null;

                finishSearch(sets);
            };

            api.Queue(getSetsRequest);
        }

        /// <summary>
        /// Searches the beatmap mirror instead of the official servers.
        /// </summary>
        /// <remarks>
        /// Mirrors only support the query, ruleset, category and (in case of osu.direct) sorting.
        /// Explicit content, extra, genre and language filters are applied to the returned results instead.
        /// </remarks>
        private void performMirrorRequest()
        {
            var category = searchControl.Category.Value;

            if (!SearchMirrorBeatmapSetsRequest.SupportsCategory(category))
            {
                mirrorPageFetched = true;
                noMoreResults = true;
                finishSearch(new List<APIBeatmapSet>());
                return;
            }

            performMirrorPageRequest(0);
        }

        private void performMirrorPageRequest(int skippedPages)
        {
            string query = searchControl.Query.Value;
            int rulesetId = searchControl.Ruleset.Value.OnlineID;
            var statuses = SearchMirrorBeatmapSetsRequest.GetStatuses(searchControl.Category.Value);
            var sortCriteria = sortControl.Current.Value;
            var sortDirection = sortControl.SortDirection.Value;
            int page = nextMirrorPage++;

            mirrorSearch = mirrors.PerformLookup(
                mirror => new SearchMirrorBeatmapSetsRequest(mirror, query, rulesetId, statuses, sortCriteria, sortDirection, page),
                results =>
                {
                    // a partial page means that there are no further results.
                    // this has to be checked before filtering, as the filters may remove results from full pages too.
                    if (results.Count < SearchMirrorBeatmapSetsRequest.PAGE_SIZE)
                        noMoreResults = true;

                    var sets = results.Where(matchesMirrorFilters).ToList();

                    // the filters may have removed every result of this page while later pages still contain matching ones.
                    // as the overlay can only fetch further pages by scrolling, those pages are fetched right away instead of showing no results.
                    if (sets.Count == 0 && !noMoreResults && skippedPages < max_skipped_mirror_pages)
                    {
                        performMirrorPageRequest(skippedPages + 1);
                        return;
                    }

                    mirrorSearch = null;
                    mirrorPageFetched = true;

                    finishSearch(sets);
                },
                e =>
                {
                    mirrorSearch = null;
                    mirrorPageFetched = true;
                    noMoreResults = true;

                    Logger.Log($@"Beatmap mirror search failed: {e.Message}", LoggingTarget.Network);
                    finishSearch(new List<APIBeatmapSet>());
                });
        }

        private bool matchesMirrorFilters(APIBeatmapSet set)
        {
            if (searchControl.ExplicitContent.Value == SearchExplicit.Hide && set.HasExplicitContent)
                return false;

            if (searchControl.Extra.Contains(SearchExtra.Video) && !set.HasVideo)
                return false;

            if (searchControl.Extra.Contains(SearchExtra.Storyboard) && !set.HasStoryboard)
                return false;

            // not every mirror returns the genre and language (their IDs are 0 then), so sets without them aren't filtered out.
            if (searchControl.Genre.Value != SearchGenre.Any && set.Genre.Id != 0 && set.Genre.Id != (int)searchControl.Genre.Value)
                return false;

            if (searchControl.Language.Value != SearchLanguage.Any && set.Language.Id != 0 && set.Language.Id != (int)searchControl.Language.Value)
                return false;

            return true;
        }

        private void finishSearch(List<APIBeatmapSet> sets)
        {
            if (CurrentPage == 0)
                searchControl.BeatmapSet = sets.FirstOrDefault();

            // check if a non-supporter used supporter-only filters
            if (!api.LocalUser.Value.IsSupporter)
            {
                List<LocalisableString> filters = new List<LocalisableString>();

                if (searchControl.Played.Value != SearchPlayed.Any)
                    filters.Add(BeatmapsStrings.ListingSearchFiltersPlayed);

                if (searchControl.Ranks.Any())
                    filters.Add(BeatmapsStrings.ListingSearchFiltersRank);

                if (filters.Any())
                {
                    var supporterOnlyFilters = SearchResult.SupporterOnlyFilters(filters);
                    SearchFinished?.Invoke(supporterOnlyFilters);
                    return;
                }
            }

            var resultsReturned = SearchResult.ResultsReturned(sets);
            SearchFinished?.Invoke(resultsReturned);
        }

        private void resetSearch()
        {
            noMoreResults = false;
            CurrentPage = 0;

            lastResponse = null;

            getSetsRequest?.Cancel();
            getSetsRequest = null;

            mirrorSearch?.Cancel();
            mirrorSearch = null;
            mirrorPageFetched = false;
            nextMirrorPage = 0;

            queryChangedDebounce?.Cancel();
        }

        protected override void Dispose(bool isDisposing)
        {
            resetSearch();

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Indicates the type of result of a user-requested beatmap search.
        /// </summary>
        public enum SearchResultType
        {
            /// <summary>
            /// Actual results have been returned from API.
            /// </summary>
            ResultsReturned,

            /// <summary>
            /// The user is not a supporter, but used supporter-only search filters.
            /// </summary>
            SupporterOnlyFilters
        }

        /// <summary>
        /// Describes the result of a user-requested beatmap search.
        /// </summary>
        public struct SearchResult
        {
            public SearchResultType Type { get; private set; }

            /// <summary>
            /// Contains the beatmap sets returned from API.
            /// Valid for read if and only if <see cref="Type"/> is <see cref="SearchResultType.ResultsReturned"/>.
            /// </summary>
            public List<APIBeatmapSet> Results { get; private set; }

            /// <summary>
            /// Contains the names of supporter-only filters requested by the user.
            /// Valid for read if and only if <see cref="Type"/> is <see cref="SearchResultType.SupporterOnlyFilters"/>.
            /// </summary>
            public List<LocalisableString> SupporterOnlyFiltersUsed { get; private set; }

            public static SearchResult ResultsReturned(List<APIBeatmapSet> results) => new SearchResult
            {
                Type = SearchResultType.ResultsReturned,
                Results = results,
            };

            public static SearchResult SupporterOnlyFilters(List<LocalisableString> filters) => new SearchResult
            {
                Type = SearchResultType.SupporterOnlyFilters,
                SupporterOnlyFiltersUsed = filters
            };
        }
    }
}
