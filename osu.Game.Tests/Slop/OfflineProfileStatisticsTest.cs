// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Users;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class OfflineProfileStatisticsTest
    {
        private readonly RulesetInfo ruleset = new OsuRuleset().RulesetInfo;

        [Test]
        public void TestTotalPerformance()
        {
            // weighted by 0.95^i, plus bonus performance for the number of scores.
            double expected = 300 + 200 * 0.95 + 100 * 0.95 * 0.95 + 416.6667 * (1 - Math.Pow(0.9994, 3));

            Assert.That(OfflineProfileStatistics.CalculateTotalPerformance(new double[] { 300, 200, 100 }), Is.EqualTo(expected).Within(1e-6));
            Assert.That(OfflineProfileStatistics.CalculateTotalPerformance(Array.Empty<double>()), Is.EqualTo(0));
        }

        [Test]
        public void TestAccuracy()
        {
            double expected = (1 * 1 + 0.9 * 0.95) / (1 + 0.95);

            Assert.That(OfflineProfileStatistics.CalculateAccuracy(new[] { 1, 0.9 }), Is.EqualTo(expected).Within(1e-9));
            Assert.That(OfflineProfileStatistics.CalculateAccuracy(Array.Empty<double>()), Is.EqualTo(0));
        }

        [TestCase(0, 1, 0)]
        [TestCase(29_999, 1, 99)]
        [TestCase(30_000, 2, 0)]
        [TestCase(1_000_000, 5, 54)]
        public void TestLevel(long totalScore, int expectedLevel, int expectedProgress)
        {
            var level = OfflineProfileStatistics.CalculateLevel(totalScore);

            Assert.That(level.Current, Is.EqualTo(expectedLevel));
            Assert.That(level.Progress, Is.EqualTo(expectedProgress));
        }

        [Test]
        public void TestAwardsPerformance()
        {
            var ranked = createBeatmap(BeatmapOnlineStatus.Ranked);

            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(ranked, ScoreRank.A)), Is.True);
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(createBeatmap(BeatmapOnlineStatus.Approved), ScoreRank.A)), Is.True);

            // like the results screen: no performance for unranked beatmaps, failed scores and unranked mods.
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(createBeatmap(BeatmapOnlineStatus.Loved), ScoreRank.A)), Is.False);
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(createBeatmap(BeatmapOnlineStatus.Graveyard), ScoreRank.A)), Is.False);
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(ranked, ScoreRank.F)), Is.False);
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(ranked, ScoreRank.A, mods: new Mod[] { new OsuModAutopilot() })), Is.False);
        }

        [Test]
        public void TestAwardsPerformanceIncludingUnrankedBeatmaps()
        {
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(createBeatmap(BeatmapOnlineStatus.Graveyard), ScoreRank.A), true), Is.True);
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(createBeatmap(BeatmapOnlineStatus.LocallyModified), ScoreRank.A), true), Is.True);
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(createBeatmap(BeatmapOnlineStatus.Loved), ScoreRank.A), true), Is.True);

            // failed scores and unranked mods still don't award performance.
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(createBeatmap(BeatmapOnlineStatus.Graveyard), ScoreRank.F), true), Is.False);
            Assert.That(OfflineProfileStatistics.AwardsPerformance(createScore(createBeatmap(BeatmapOnlineStatus.Graveyard), ScoreRank.A, mods: new Mod[] { new OsuModAutopilot() }), true), Is.False);
        }

        [Test]
        public void TestStatisticsIncludingUnrankedBeatmaps()
        {
            var ranked = createBeatmap(BeatmapOnlineStatus.Ranked);
            var graveyard = createBeatmap(BeatmapOnlineStatus.Graveyard);

            var r = createScore(ranked, ScoreRank.A, totalScore: 800_000);
            var g = createScore(graveyard, ScoreRank.S, totalScore: 500_000);

            var performance = new ConcurrentDictionary<Guid, double> { [r.ID] = 100, [g.ID] = 200 };

            var excluded = OfflineProfileStatistics.CalculateAsync(new[] { r, g }, ruleset, 0, new BeatmapDifficultyCache(), performance).GetAwaiter().GetResult();

            Assert.That(excluded.TopPlays, Is.EqualTo(new[] { r }));
            Assert.That(excluded.Statistics.RankedScore, Is.EqualTo(800_000));
            Assert.That(excluded.Statistics.GradesCount.S, Is.Zero);
            Assert.That(excluded.IncludesUnrankedBeatmaps, Is.False);

            var included = OfflineProfileStatistics.CalculateAsync(new[] { r, g }, ruleset, 0, new BeatmapDifficultyCache(), performance, true).GetAwaiter().GetResult();

            Assert.That(included.TopPlays, Is.EqualTo(new[] { g, r }));
            Assert.That((double)included.Statistics.PP!.Value, Is.EqualTo(OfflineProfileStatistics.CalculateTotalPerformance(new double[] { 200, 100 })).Within(1e-6));
            Assert.That(included.Statistics.RankedScore, Is.EqualTo(1_300_000));
            Assert.That(included.Statistics.GradesCount.S, Is.EqualTo(1));
            Assert.That(included.IncludesUnrankedBeatmaps, Is.True);
        }

        [Test]
        public void TestRanking()
        {
            var beatmap = createBeatmap(BeatmapOnlineStatus.Ranked);
            var performance = new ConcurrentDictionary<Guid, double>();

            OfflineProfileRanking.Entry createEntry(int userId, CountryCode country, double pp, long totalScore = 100_000)
            {
                var scores = new List<ScoreInfo>();

                if (pp > 0)
                {
                    var score = createScore(beatmap, ScoreRank.A, totalScore: totalScore);
                    performance[score.ID] = pp;
                    scores.Add(score);
                }

                var profile = new OfflineProfile { UserID = userId, Username = $"user {-userId}", CountryCode = country };
                return new OfflineProfileRanking.Entry(profile, calculate(scores, 0, performance));
            }

            var oldest = createEntry(OfflineProfile.FIRST_USER_ID, CountryCode.AT, 100);
            var best = createEntry(OfflineProfile.FIRST_USER_ID - 1, CountryCode.DE, 300);
            var tieByScore = createEntry(OfflineProfile.FIRST_USER_ID - 2, CountryCode.AT, 100, totalScore: 200_000);
            var tieByAge = createEntry(OfflineProfile.FIRST_USER_ID - 3, CountryCode.Unknown, 100);
            var unranked = createEntry(OfflineProfile.FIRST_USER_ID - 4, CountryCode.AT, 0);

            var ranking = new OfflineProfileRanking(new[] { unranked, tieByAge, oldest, tieByScore, best });

            // by performance, then by ranked score, then older profiles first.
            Assert.That(ranking.Entries, Is.EqualTo(new[] { best, tieByScore, oldest, tieByAge, unranked }));

            Assert.That(ranking.Entries.Select(e => e.Statistics.Statistics.GlobalRank), Is.EqualTo(new int?[] { 1, 2, 3, 4, null }));
            Assert.That(ranking.Entries.Select(e => e.Statistics.Statistics.CountryRank), Is.EqualTo(new int?[] { 1, 1, 2, null, null }));

            Assert.That(ranking.GetEntry(new OfflineProfile { ID = best.Profile.ID }), Is.SameAs(best));
        }

        [Test]
        public void TestStatistics()
        {
            var rankedA = createBeatmap(BeatmapOnlineStatus.Ranked);
            var rankedB = createBeatmap(BeatmapOnlineStatus.Ranked);
            var loved = createBeatmap(BeatmapOnlineStatus.Loved);
            var graveyard = createBeatmap(BeatmapOnlineStatus.Graveyard);

            var performance = new ConcurrentDictionary<Guid, double>();

            // two scores on the same beatmap: the better one by performance is a top play, the better one by score counts for the ranked score.
            var a1 = createScore(rankedA, ScoreRank.S, totalScore: 900_000, accuracy: 0.98, maxCombo: 500);
            var a2 = createScore(rankedA, ScoreRank.A, totalScore: 800_000, accuracy: 0.95, maxCombo: 300);
            var b = createScore(rankedB, ScoreRank.XH, totalScore: 1_000_000, accuracy: 1, maxCombo: 200, mods: new Mod[] { new OsuModHidden() });
            var l = createScore(loved, ScoreRank.X, totalScore: 950_000, accuracy: 1, maxCombo: 100);
            var g = createScore(graveyard, ScoreRank.A, totalScore: 500_000, accuracy: 0.9, maxCombo: 50);

            performance[a1.ID] = 100;
            performance[a2.ID] = 150;
            performance[b.ID] = 120;

            var statistics = calculate(new[] { a1, a2, b, l, g }, 3, performance);

            Assert.That(statistics.TopPlays, Is.EqualTo(new[] { a2, b }));
            Assert.That(statistics.TopPlays.Select(s => s.PP), Is.EqualTo(new double?[] { 150, 120 }));

            var stats = statistics.Statistics;

            Assert.That((double)stats.PP!.Value, Is.EqualTo(OfflineProfileStatistics.CalculateTotalPerformance(new double[] { 150, 120 })).Within(1e-6));
            Assert.That(stats.Accuracy, Is.EqualTo(OfflineProfileStatistics.CalculateAccuracy(new[] { 0.95, 1 }) * 100).Within(1e-9));

            // the best score by total score on each ranked or loved beatmap.
            Assert.That(stats.RankedScore, Is.EqualTo(900_000 + 1_000_000 + 950_000));
            Assert.That(stats.TotalScore, Is.EqualTo(900_000 + 800_000 + 1_000_000 + 950_000 + 500_000));

            Assert.That(stats.GradesCount.SSPlus, Is.EqualTo(1));
            Assert.That(stats.GradesCount.SS, Is.EqualTo(1));
            Assert.That(stats.GradesCount.S, Is.EqualTo(1));
            Assert.That(stats.GradesCount.A, Is.EqualTo(0));

            Assert.That(stats.MaxCombo, Is.EqualTo(500));

            // at least the number of scores, even if fewer plays were counted.
            Assert.That(stats.PlayCount, Is.EqualTo(5));

            Assert.That(statistics.RecentPlays.First(), Is.EqualTo(g));
        }

        [Test]
        public void TestPlayCountIncludesUnfinishedPlays()
        {
            var score = createScore(createBeatmap(BeatmapOnlineStatus.Graveyard), ScoreRank.A);

            Assert.That(calculate(new[] { score }, 7, new ConcurrentDictionary<Guid, double>()).Statistics.PlayCount, Is.EqualTo(7));
        }

        private OfflineProfileStatistics calculate(IReadOnlyList<ScoreInfo> scores, int playCount, ConcurrentDictionary<Guid, double> performance)
            // the difficulty cache isn't used, as the performance of all scores which award performance is already known.
            => OfflineProfileStatistics.CalculateAsync(scores, ruleset, playCount, new BeatmapDifficultyCache(), performance).GetAwaiter().GetResult();

        private BeatmapInfo createBeatmap(BeatmapOnlineStatus status) => new BeatmapInfo(ruleset)
        {
            Status = status,
            Length = 60_000,
        };

        private int scoreCount;

        private ScoreInfo createScore(BeatmapInfo beatmap, ScoreRank rank, long totalScore = 100_000, double accuracy = 0.9, int maxCombo = 10, Mod[]? mods = null) => new ScoreInfo(beatmap, ruleset)
        {
            Rank = rank,
            TotalScore = totalScore,
            Accuracy = accuracy,
            MaxCombo = maxCombo,
            Mods = mods ?? Array.Empty<Mod>(),
            // later scores are more recent.
            Date = DateTimeOffset.Now.AddMinutes(scoreCount++),
        };
    }
}
