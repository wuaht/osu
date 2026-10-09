// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Users;
using osu.Game.Utils;

namespace osu.Game.Online.OfflineProfiles
{
    /// <summary>
    /// The statistics of an <see cref="OfflineProfile"/> in a ruleset, calculated from its local scores like the statistics of osu! accounts.
    /// </summary>
    public class OfflineProfileStatistics
    {
        /// <summary>
        /// The number of scores which are listed in <see cref="RecentPlays"/>.
        /// </summary>
        public const int RECENT_PLAYS_COUNT = 50;

        /// <summary>
        /// The weight of each further score in the total performance and accuracy (like osu!).
        /// </summary>
        public const double WEIGHT_FACTOR = 0.95;

        public readonly UserStatistics Statistics;

        /// <summary>
        /// The best score (by performance) on each beatmap which awards performance, sorted by performance. <see cref="ScoreInfo.PP"/> is set on these scores.
        /// </summary>
        public readonly IReadOnlyList<ScoreInfo> TopPlays;

        /// <summary>
        /// The most recent scores, newest first.
        /// </summary>
        public readonly IReadOnlyList<ScoreInfo> RecentPlays;

        /// <summary>
        /// Whether scores on unranked beatmaps were counted like scores on ranked beatmaps.
        /// </summary>
        public readonly bool IncludesUnrankedBeatmaps;

        private OfflineProfileStatistics(UserStatistics statistics, IReadOnlyList<ScoreInfo> topPlays, IReadOnlyList<ScoreInfo> recentPlays, bool includesUnrankedBeatmaps)
        {
            Statistics = statistics;
            TopPlays = topPlays;
            RecentPlays = recentPlays;
            IncludesUnrankedBeatmaps = includesUnrankedBeatmaps;
        }

        /// <summary>
        /// Calculates the statistics from the given scores.
        /// </summary>
        /// <param name="scores">All (detached) scores of the profile in the ruleset.</param>
        /// <param name="ruleset">The ruleset.</param>
        /// <param name="playCount">The number of plays started on the profile in the ruleset.</param>
        /// <param name="difficultyCache">The difficulty cache, for calculating performance.</param>
        /// <param name="performanceCache">Calculated performance by score ID, which is used and updated.</param>
        /// <param name="includeUnrankedBeatmaps">Whether scores on beatmaps without a leaderboard (or which don't award performance) count like scores on ranked beatmaps.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public static async Task<OfflineProfileStatistics> CalculateAsync(IReadOnlyList<ScoreInfo> scores, RulesetInfo ruleset, int playCount, BeatmapDifficultyCache difficultyCache,
                                                                          ConcurrentDictionary<Guid, double> performanceCache, bool includeUnrankedBeatmaps = false,
                                                                          CancellationToken cancellationToken = default)
        {
            var performanceCalculator = ruleset.CreateInstance().CreatePerformanceCalculator();

            // the performance of each score which awards performance.
            var performance = new Dictionary<ScoreInfo, double>();

            foreach (var score in scores)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (performanceCalculator == null || !AwardsPerformance(score, includeUnrankedBeatmaps))
                    continue;

                if (!performanceCache.TryGetValue(score.ID, out double pp))
                {
                    var attributes = await difficultyCache.GetDifficultyAsync(score.BeatmapInfo!, score.Ruleset, score.Mods, cancellationToken).ConfigureAwait(false);

                    // the beatmap may not be available locally anymore.
                    if (attributes?.DifficultyAttributes == null)
                        continue;

                    var result = await performanceCalculator.CalculateAsync(score, attributes.Value.DifficultyAttributes, cancellationToken).ConfigureAwait(false);

                    pp = result.Total;

                    if (!double.IsFinite(pp))
                        continue;

                    performanceCache[score.ID] = pp;
                }

                performance[score] = pp;
            }

            // the best score by performance on each beatmap.
            var topPlays = performance.GroupBy(kvp => kvp.Key.BeatmapInfo!.ID)
                                      .Select(group => group.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key.Date).First())
                                      .OrderByDescending(kvp => kvp.Value)
                                      .Select(kvp =>
                                      {
                                          kvp.Key.PP = kvp.Value;
                                          return kvp.Key;
                                      })
                                      .ToList();

            // the best score by total score on each beatmap with a leaderboard (like the ranked score and grades of osu! accounts).
            var bestRankedScores = scores.Where(s => includeUnrankedBeatmaps || countsForRankedScore(s.BeatmapInfo!.Status))
                                         .GroupBy(s => s.BeatmapInfo!.ID)
                                         .Select(group => group.OrderByDescending(s => s.TotalScore).ThenBy(s => s.Date).First())
                                         .ToList();

            long totalScore = scores.Sum(s => s.TotalScore);

            var statistics = new UserStatistics
            {
                PP = (decimal)CalculateTotalPerformance(topPlays.Select(s => s.PP!.Value).ToList()),
                Accuracy = CalculateAccuracy(topPlays.Select(s => s.Accuracy).ToList()) * 100,
                RankedScore = bestRankedScores.Sum(s => s.TotalScore),
                TotalScore = totalScore,
                PlayCount = Math.Max(playCount, scores.Count),
                PlayTime = (int)(scores.Sum(getPlayTime) / 1000),
                MaxCombo = scores.Select(s => s.MaxCombo).DefaultIfEmpty().Max(),
                TotalHits = scores.Sum(s => s.Statistics.Where(kvp => kvp.Key.IsHit() && kvp.Key.IsBasic()).Sum(kvp => kvp.Value)),
                Level = CalculateLevel(totalScore),
                IsRanked = topPlays.Count > 0,
                GradesCount = new UserStatistics.Grades
                {
                    SSPlus = bestRankedScores.Count(s => s.Rank == ScoreRank.XH),
                    SS = bestRankedScores.Count(s => s.Rank == ScoreRank.X),
                    SPlus = bestRankedScores.Count(s => s.Rank == ScoreRank.SH),
                    S = bestRankedScores.Count(s => s.Rank == ScoreRank.S),
                    A = bestRankedScores.Count(s => s.Rank == ScoreRank.A),
                },
            };

            var recentPlays = scores.OrderByDescending(s => s.Date).Take(RECENT_PLAYS_COUNT).ToList();

            return new OfflineProfileStatistics(statistics, topPlays, recentPlays, includeUnrankedBeatmaps);
        }

        /// <summary>
        /// Whether a score awards performance, with the same rules as the results screen.
        /// </summary>
        /// <param name="score">The score.</param>
        /// <param name="includeUnrankedBeatmaps">Whether scores on beatmaps which don't award performance on osu! award performance as well.</param>
        public static bool AwardsPerformance(ScoreInfo score, bool includeUnrankedBeatmaps = false)
        {
            if (score.BeatmapInfo == null || score.Rank == ScoreRank.F)
                return false;

            if (!includeUnrankedBeatmaps && !score.BeatmapInfo.Status.GrantsPerformancePoints())
                return false;

            IEnumerable<Mod> mods = score.Mods;

            if (score.IsLegacyScore)
                mods = mods.Where(m => m is not ModClassic);

            return mods.All(m => m.Ranked);
        }

        /// <summary>
        /// Calculates the total performance from the performance of the best score on each beatmap, like osu!:
        /// the scores are weighted by <see cref="WEIGHT_FACTOR"/>^i, and bonus performance is awarded for the number of scores.
        /// </summary>
        /// <param name="performance">The performance of the best score on each beatmap, sorted by performance.</param>
        public static double CalculateTotalPerformance(IReadOnlyList<double> performance)
        {
            double total = 0;

            for (int i = 0; i < performance.Count; i++)
                total += performance[i] * Math.Pow(WEIGHT_FACTOR, i);

            return total + 416.6667 * (1 - Math.Pow(0.9994, performance.Count));
        }

        /// <summary>
        /// Calculates the accuracy from the accuracy of the top plays, weighted like their performance (like osu!).
        /// </summary>
        /// <param name="accuracies">The accuracy of the top plays, sorted by their performance.</param>
        /// <returns>The accuracy from 0 to 1.</returns>
        public static double CalculateAccuracy(IReadOnlyList<double> accuracies)
        {
            if (accuracies.Count == 0)
                return 0;

            double weighted = 0;
            double weights = 0;

            for (int i = 0; i < accuracies.Count; i++)
            {
                double weight = Math.Pow(WEIGHT_FACTOR, i);
                weighted += accuracies[i] * weight;
                weights += weight;
            }

            return weighted / weights;
        }

        /// <summary>
        /// Calculates the level from the total score, with the formula of osu!.
        /// </summary>
        public static UserStatistics.LevelInfo CalculateLevel(long totalScore)
        {
            int level = 1;

            while (requiredScore(level + 1) <= totalScore)
                level++;

            double current = requiredScore(level);
            double next = requiredScore(level + 1);

            return new UserStatistics.LevelInfo
            {
                Current = level,
                Progress = (int)Math.Clamp((totalScore - current) / (next - current) * 100, 0, 99),
            };
        }

        /// <summary>
        /// The total score required for reaching a level.
        /// </summary>
        private static double requiredScore(int level)
        {
            if (level <= 100)
                return 5000.0 / 3 * (4 * Math.Pow(level, 3) - 3 * Math.Pow(level, 2) - level) + 1.25 * Math.Pow(1.8, level - 60);

            return 26931190827 + 99999999999.0 * (level - 100);
        }

        private static bool countsForRankedScore(BeatmapOnlineStatus status) => status is BeatmapOnlineStatus.Ranked or BeatmapOnlineStatus.Approved or BeatmapOnlineStatus.Loved;

        /// <summary>
        /// The duration of a play in milliseconds, considering mods which change the speed.
        /// </summary>
        private static double getPlayTime(ScoreInfo score)
        {
            double rate = ModUtils.CalculateRateWithMods(score.Mods);
            return score.BeatmapInfo!.Length / (rate > 0 ? rate : 1);
        }
    }
}
