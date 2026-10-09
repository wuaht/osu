// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Models;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Scoring;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class OfflineProfileScoreOwnershipTest : RealmTest
    {
        private const int online_user = 123;
        private const int profile_a = OfflineProfile.FIRST_USER_ID;
        private const int profile_b = OfflineProfile.FIRST_USER_ID - 1;

        private static readonly int[] all_users = { 0, 1, online_user, 456, profile_a, profile_b };

        [Test]
        public void TestScoresOfLocalUsers()
        {
            RunTestWithRealm((realm, _) =>
            {
                realm.Write(r =>
                {
                    var ruleset = CreateRuleset();
                    r.Add(ruleset);

                    var beatmapSet = CreateBeatmapSet(ruleset);
                    r.Add(beatmapSet);

                    var beatmap = beatmapSet.Beatmaps.First();

                    foreach (int userId in all_users)
                        r.Add(new ScoreInfo(beatmap, ruleset, new RealmUser { OnlineID = userId, Username = $"user {userId}" }));
                });

                realm.Run(r =>
                {
                    // offline profiles only see their own scores.
                    Assert.That(userIdsOfScores(r, profile_a), Is.EquivalentTo(new[] { profile_a }));
                    Assert.That(userIdsOfScores(r, profile_b), Is.EquivalentTo(new[] { profile_b }));

                    // logged in users and guests keep seeing guest scores and scores of unknown provenance, but not those of offline profiles.
                    Assert.That(userIdsOfScores(r, online_user), Is.EquivalentTo(new[] { 0, 1, online_user }));
                    Assert.That(userIdsOfScores(r, 0), Is.EquivalentTo(new[] { 0, 1 }));
                });
            });
        }

        [Test]
        public void TestIsScoreOfLocalUserMatchesQuery()
        {
            foreach (int localUser in all_users)
            {
                var expected = all_users.Where(scoreUser =>
                    OfflineProfileUser.IsOfflineProfileID(localUser)
                        ? scoreUser == localUser
                        : scoreUser == localUser || (scoreUser is 0 or 1));

                Assert.That(all_users.Where(scoreUser => ScoreInfoExtensions.IsScoreOfLocalUser(scoreUser, localUser)), Is.EquivalentTo(expected), $"local user {localUser}");
            }
        }

        private static int[] userIdsOfScores(Realms.Realm realm, int userId) => realm.GetAllLocalScoresForUser(userId).AsEnumerable().Select(s => s.UserID).ToArray();
    }
}
