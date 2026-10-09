// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Users;

namespace osu.Game.Online.OfflineProfiles
{
    /// <summary>
    /// The ranking of all <see cref="OfflineProfile"/>s in a ruleset, like the global ranking of osu! accounts.
    /// </summary>
    public class OfflineProfileRanking
    {
        /// <summary>
        /// All profiles with their statistics: ranked profiles by rank, followed by unranked profiles by username.
        /// <see cref="UserStatistics.GlobalRank"/> and <see cref="UserStatistics.CountryRank"/> are set on the statistics of ranked profiles.
        /// </summary>
        public readonly IReadOnlyList<Entry> Entries;

        public OfflineProfileRanking(IEnumerable<Entry> entries)
        {
            var all = entries.ToList();

            // like osu!, by performance. ties are resolved by the ranked score and then by the age of the profile (older profiles have greater IDs).
            var ranked = all.Where(e => e.Statistics.Statistics.IsRanked)
                            .OrderByDescending(e => e.Statistics.Statistics.PP ?? 0)
                            .ThenByDescending(e => e.Statistics.Statistics.RankedScore)
                            .ThenByDescending(e => e.Profile.UserID)
                            .ToList();

            var unranked = all.Except(ranked).OrderBy(e => e.Profile.Username).ToList();

            var countryRanks = new Dictionary<CountryCode, int>();

            for (int i = 0; i < ranked.Count; i++)
            {
                var statistics = ranked[i].Statistics.Statistics;
                var countryCode = ranked[i].Profile.CountryCode;

                statistics.GlobalRank = i + 1;

                if (countryCode != CountryCode.Unknown)
                    statistics.CountryRank = countryRanks[countryCode] = countryRanks.GetValueOrDefault(countryCode) + 1;
            }

            foreach (var entry in unranked)
            {
                entry.Statistics.Statistics.GlobalRank = null;
                entry.Statistics.Statistics.CountryRank = null;
            }

            Entries = ranked.Concat(unranked).ToList();
        }

        /// <summary>
        /// Returns the entry of the given profile, if it is part of the ranking.
        /// </summary>
        public Entry? GetEntry(OfflineProfile profile) => Entries.FirstOrDefault(e => e.Profile.ID == profile.ID);

        /// <param name="Profile">The profile.</param>
        /// <param name="Statistics">The statistics of the profile in the ruleset of the ranking.</param>
        public record Entry(OfflineProfile Profile, OfflineProfileStatistics Statistics);
    }
}
