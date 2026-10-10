// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.BnTracker
{
    /// <summary>
    /// Builds what the BN Tracker would show for a beatmap set which isn't tracked yet, such that it only has to be tracked once something changes.
    /// The rules match the ones of the BN Tracker server (relevant nominators, required nominations and preference matching).
    /// </summary>
    public static class BnTrackerPreview
    {
        private static readonly string[] named_languages = { @"english", @"japanese", @"chinese", @"korean", @"instrumental" };

        /// <summary>
        /// Creates the preview of an online beatmap set.
        /// </summary>
        /// <param name="beatmapSet">The online beatmap set.</param>
        /// <param name="nominators">All current BN and NAT members.</param>
        public static BnBeatmapSetWithNominators Create(APIBeatmapSet beatmapSet, IEnumerable<BnNominator> nominators)
        {
            var modeCounts = beatmapSet.Beatmaps
                                       .Select(b => GetMode(b.RulesetID))
                                       .OfType<BnGameMode>()
                                       .GroupBy(m => m)
                                       .ToDictionary(g => g.Key, g => g.Count());

            var modes = modeCounts.Keys.OrderBy(m => m).ToList();
            var mainMode = GetMainMode(modes, modeCounts);

            string? genre = getName(beatmapSet.Genre.Name);
            string? language = getName(beatmapSet.Language.Name);

            var setNominators = nominators
                                .Where(n => !n.IsRemoved)
                                .Select(n => new BnSetNominator
                                {
                                    NominatorOsuId = n.OsuId,
                                    Nominator = n,
                                    RelevantModes = GetRelevantModes(n, modes),
                                    PreferenceMatch = new BnPreferenceMatchResult
                                    {
                                        Genre = MatchGenre(n.Preferences, genre),
                                        Language = MatchLanguage(n.Preferences, language),
                                    },
                                    Status = BnNominationStatus.NotAsked,
                                })
                                .Where(n => n.RelevantModes.Count > 0)
                                .OrderBy(n => n.Nominator.Username, StringComparer.OrdinalIgnoreCase)
                                .ToList();

            var perMode = modes.Select(m => new BnModeProgress
            {
                Mode = m,
                Required = m == mainMode ? 2 : 1,
            }).ToList();

            return new BnBeatmapSetWithNominators
            {
                BeatmapSet = new BnBeatmapSet
                {
                    OsuBeatmapSetId = beatmapSet.OnlineID,
                    Title = beatmapSet.Title,
                    Artist = beatmapSet.Artist,
                    CreatorUsername = beatmapSet.AuthorString,
                    CoverUrl = beatmapSet.Covers.Cover,
                    OsuUrl = $@"https://osu.ppy.sh/beatmapsets/{beatmapSet.OnlineID}",
                    Modes = modes,
                    MainMode = mainMode,
                    DifficultyCount = beatmapSet.Beatmaps.Length,
                    RankStatus = GetRankStatus(beatmapSet.Status),
                    Genre = genre,
                    Language = language,
                    Progress = new BnBeatmapSetProgress
                    {
                        PerMode = perMode,
                        TotalRequired = perMode.Sum(m => m.Required),
                        StatusCounts = Enum.GetValues<BnNominationStatus>().ToDictionary(s => s, s => s == BnNominationStatus.NotAsked ? setNominators.Count : 0),
                        RelevantNominatorCount = setNominators.Count,
                    },
                },
                Nominators = setNominators,
            };
        }

        public static BnGameMode? GetMode(int rulesetId)
        {
            switch (rulesetId)
            {
                case 0:
                    return BnGameMode.Osu;

                case 1:
                    return BnGameMode.Taiko;

                case 2:
                    return BnGameMode.Catch;

                case 3:
                    return BnGameMode.Mania;

                default:
                    return null;
            }
        }

        /// <summary>
        /// The mode with the most difficulties, which needs two nominations. Ties go to the mode which comes first.
        /// </summary>
        public static BnGameMode? GetMainMode(IReadOnlyList<BnGameMode> modes, IReadOnlyDictionary<BnGameMode, int> difficultyCounts)
        {
            if (modes.Count == 0)
                return null;

            return modes.OrderByDescending(m => difficultyCounts.GetValueOrDefault(m)).ThenBy(m => m).First();
        }

        /// <summary>
        /// The modes of a nominator which matter for a beatmap set with the given modes. Modes not tied to a game mode always matter.
        /// </summary>
        public static List<BnModeLevel> GetRelevantModes(BnNominator nominator, IReadOnlyCollection<BnGameMode> modes)
            => nominator.Modes.Where(m => m.Mode == BnGameMode.General || modes.Contains(m.Mode)).ToList();

        public static BnBeatmapRankStatus GetRankStatus(BeatmapOnlineStatus status)
        {
            switch (status)
            {
                case BeatmapOnlineStatus.Graveyard:
                    return BnBeatmapRankStatus.Graveyard;

                case BeatmapOnlineStatus.WIP:
                    return BnBeatmapRankStatus.Wip;

                case BeatmapOnlineStatus.Pending:
                    return BnBeatmapRankStatus.Pending;

                case BeatmapOnlineStatus.Qualified:
                    return BnBeatmapRankStatus.Qualified;

                case BeatmapOnlineStatus.Ranked:
                    return BnBeatmapRankStatus.Ranked;

                case BeatmapOnlineStatus.Approved:
                    return BnBeatmapRankStatus.Approved;

                case BeatmapOnlineStatus.Loved:
                    return BnBeatmapRankStatus.Loved;

                default:
                    return BnBeatmapRankStatus.Unknown;
            }
        }

        public static BnPreferenceMatch MatchGenre(BnNominatorPreferences preferences, string? beatmapGenre)
        {
            if (string.IsNullOrWhiteSpace(beatmapGenre))
                return BnPreferenceMatch.Unknown;

            string genre = beatmapGenre.Trim().ToLowerInvariant();

            switch (genre)
            {
                case @"unspecified":
                    return BnPreferenceMatch.Unknown;

                // these genres of osu! are details on Mappers Guild.
                case @"anime":
                    return match(preferences.Details, preferences.DetailsExcluded, @"anime");

                case @"video game":
                    return match(preferences.Details, preferences.DetailsExcluded, @"game");

                case @"hip hop":
                    return match(preferences.Genres, preferences.GenresExcluded, genre, @"hiphop");

                default:
                    return match(preferences.Genres, preferences.GenresExcluded, genre);
            }
        }

        public static BnPreferenceMatch MatchLanguage(BnNominatorPreferences preferences, string? beatmapLanguage)
        {
            if (string.IsNullOrWhiteSpace(beatmapLanguage))
                return BnPreferenceMatch.Unknown;

            string language = beatmapLanguage.Trim().ToLowerInvariant();

            if (language == @"unspecified")
                return BnPreferenceMatch.Unknown;

            // Mappers Guild only names a few languages, every other one is "other".
            string bucket = named_languages.Contains(language) ? language : @"other";

            return match(preferences.SongLanguages, preferences.SongLanguagesExcluded, bucket);
        }

        private static BnPreferenceMatch match(List<string> positive, List<string> negative, string value, string? alternativeValue = null)
        {
            bool contains(List<string> list) => list.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)
                                                              || (alternativeValue != null && string.Equals(x, alternativeValue, StringComparison.OrdinalIgnoreCase)));

            if (contains(negative))
                return BnPreferenceMatch.Excluded;

            if (contains(positive))
                return BnPreferenceMatch.Matches;

            return BnPreferenceMatch.Unknown;
        }

        private static string? getName(string? name) => string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
