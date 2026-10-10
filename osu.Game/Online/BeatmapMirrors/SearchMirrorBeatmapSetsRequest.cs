// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Online.BeatmapMirrors
{
    /// <summary>
    /// Searches the beatmap sets of a <see cref="BeatmapMirror"/>, one page of <see cref="PAGE_SIZE"/> results at a time.
    /// </summary>
    public class SearchMirrorBeatmapSetsRequest : BeatmapMirrorJsonRequest<List<APIBeatmapSet>>
    {
        public const int PAGE_SIZE = 50;

        private readonly string? query;
        private readonly int rulesetId;
        private readonly IReadOnlyCollection<BeatmapOnlineStatus>? statuses;
        private readonly SortCriteria sortCriteria;
        private readonly SortDirection sortDirection;
        private readonly int page;

        /// <param name="mirror">The mirror to search. Must provide a metadata API.</param>
        /// <param name="query">The text to search for.</param>
        /// <param name="rulesetId">The online ID of the ruleset the beatmap sets must contain beatmaps of, or a negative value for any ruleset.</param>
        /// <param name="statuses">The statuses the beatmap sets must have one of, or <c>null</c> for any status.</param>
        /// <param name="sortCriteria">How to sort the results. Ignored by mirrors which can't sort.</param>
        /// <param name="sortDirection">The direction to sort the results in.</param>
        /// <param name="page">The zero-based index of the page of results to return.</param>
        public SearchMirrorBeatmapSetsRequest(BeatmapMirror mirror, string? query, int rulesetId, IReadOnlyCollection<BeatmapOnlineStatus>? statuses,
                                              SortCriteria sortCriteria, SortDirection sortDirection, int page)
            : base(mirror)
        {
            this.query = query;
            this.rulesetId = rulesetId;
            this.statuses = statuses;
            this.sortCriteria = sortCriteria;
            this.sortDirection = sortDirection;
            this.page = page;
        }

        protected override string Target => @"search";

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();

            if (!string.IsNullOrEmpty(query))
                req.AddParameter(@"q", query);

            req.AddParameter(Mirror == BeatmapMirror.OsuDirect ? @"amount" : @"limit", PAGE_SIZE.ToString(CultureInfo.InvariantCulture));
            req.AddParameter(@"offset", (page * PAGE_SIZE).ToString(CultureInfo.InvariantCulture));

            if (rulesetId >= 0)
                req.AddParameter(@"mode", rulesetId.ToString(CultureInfo.InvariantCulture));

            if (statuses != null)
            {
                var values = statuses.Select(s => ((int)s).ToString(CultureInfo.InvariantCulture)).ToArray();

                // osu.direct expects a comma separated list and only reads the last of repeated parameters.
                // mino returns nothing for an encoded comma, but accepts repeated parameters.
                if (Mirror == BeatmapMirror.OsuDirect)
                    req.AddParameter(@"status", string.Join(',', values));
                else
                {
                    foreach (string value in values)
                        req.AddParameter(@"status", value);
                }
            }

            // mino has no sorting options.
            if (Mirror == BeatmapMirror.OsuDirect)
            {
                string? sortField = getOsuDirectSortField(sortCriteria);

                if (sortField != null)
                    req.AddParameter(@"sort", $@"{sortField}:{(sortDirection == SortDirection.Ascending ? @"asc" : @"desc")}");
            }

            return req;
        }

        /// <summary>
        /// Whether beatmap sets of the given <see cref="SearchCategory"/> can be searched for on mirrors.
        /// </summary>
        /// <remarks>
        /// <see cref="SearchCategory.Favourites"/> and <see cref="SearchCategory.Mine"/> depend on the logged in user, which mirrors know nothing about.
        /// </remarks>
        public static bool SupportsCategory(SearchCategory category) => category != SearchCategory.Favourites && category != SearchCategory.Mine;

        /// <summary>
        /// Returns the statuses which beatmap sets of the given <see cref="SearchCategory"/> have, matching osu-web's definitions of the categories.
        /// </summary>
        /// <returns>The statuses, or <c>null</c> if the category includes beatmap sets of any status.</returns>
        public static BeatmapOnlineStatus[]? GetStatuses(SearchCategory category)
        {
            switch (category)
            {
                case SearchCategory.Leaderboard:
                    return new[] { BeatmapOnlineStatus.Ranked, BeatmapOnlineStatus.Approved, BeatmapOnlineStatus.Qualified, BeatmapOnlineStatus.Loved };

                case SearchCategory.Ranked:
                    return new[] { BeatmapOnlineStatus.Ranked, BeatmapOnlineStatus.Approved };

                case SearchCategory.Qualified:
                    return new[] { BeatmapOnlineStatus.Qualified };

                case SearchCategory.Loved:
                    return new[] { BeatmapOnlineStatus.Loved };

                case SearchCategory.Pending:
                    return new[] { BeatmapOnlineStatus.Pending };

                case SearchCategory.Wip:
                    return new[] { BeatmapOnlineStatus.WIP };

                case SearchCategory.Graveyard:
                    return new[] { BeatmapOnlineStatus.Graveyard };

                default:
                    return null;
            }
        }

        private static string? getOsuDirectSortField(SortCriteria criteria)
        {
            switch (criteria)
            {
                case SortCriteria.Title:
                    return @"title";

                case SortCriteria.Artist:
                    return @"artist";

                case SortCriteria.Difficulty:
                    return @"beatmaps.difficulty_rating";

                case SortCriteria.Updated:
                    return @"last_updated";

                case SortCriteria.Ranked:
                    return @"ranked_date";

                case SortCriteria.Plays:
                    return @"play_count";

                case SortCriteria.Favourites:
                    return @"favourite_count";

                // rating, relevance and nominations can't be sorted by, so the results are left in osu.direct's order (relevance).
                default:
                    return null;
            }
        }
    }
}
