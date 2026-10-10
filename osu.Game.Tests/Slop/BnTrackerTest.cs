// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.BnTracker;
using osu.Game.Screens.Edit.Nominators;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class BnTrackerTest
    {
        /// <summary>
        /// A response in the shape documented in docs/API.md of the BN Tracker, with a property the client doesn't know.
        /// </summary>
        private const string set_with_nominators_json = @"{
  ""beatmapSet"": {
    ""id"": ""3f2b5c1e-8d2a-4c1b-9f0e-1a2b3c4d5e6f"",
    ""osuBeatmapsetId"": 123456,
    ""title"": ""Title"",
    ""artist"": ""Artist"",
    ""creatorUsername"": ""mapper"",
    ""coverUrl"": null,
    ""cardUrl"": null,
    ""osuUrl"": ""https://osu.ppy.sh/beatmapsets/123456"",
    ""modes"": [""Osu"", ""Taiko""],
    ""mainMode"": ""Osu"",
    ""modeDifficultyCounts"": { ""Osu"": 4, ""Taiko"": 1 },
    ""difficultyCount"": 5,
    ""osuStatus"": ""pending"",
    ""rankStatus"": ""Pending"",
    ""genre"": ""Anime"",
    ""language"": ""Japanese"",
    ""priority"": ""High"",
    ""addedAt"": ""2026-10-01T12:00:00+00:00"",
    ""someNewField"": 42,
    ""progress"": {
      ""perMode"": [ { ""mode"": ""Osu"", ""required"": 2, ""filled"": 1, ""isSatisfied"": false }, { ""mode"": ""Taiko"", ""required"": 1, ""filled"": 1, ""isSatisfied"": true } ],
      ""totalRequired"": 3, ""totalFilled"": 2, ""isFullBn"": false,
      ""statusCounts"": { ""NotAsked"": 1, ""Declined"": 0, ""Pending"": 0, ""Maybe"": 0, ""Unlikely"": 0, ""Accepted"": 2 },
      ""relevantNominatorCount"": 3
    }
  },
  ""nominators"": [
    {
      ""nominatorOsuId"": 2,
      ""nominator"": {
        ""osuId"": 2, ""username"": ""peppy"", ""avatarUrl"": ""https://a.ppy.sh/2"", ""profileUrl"": ""https://osu.ppy.sh/users/2"", ""coverUrl"": null,
        ""modes"": [ { ""mode"": ""Osu"", ""level"": ""Full"" }, { ""mode"": ""General"", ""level"": ""Evaluator"" } ],
        ""highestLevel"": ""Evaluator"",
        ""spokenLanguages"": [""english""],
        ""requestChannels"": [""PersonalQueue"", ""GameChat""],
        ""isOpenForRequests"": true,
        ""requestLink"": ""https://example.com/queue"",
        ""requestInfo"": ""[b]hi[/b]"",
        ""lastOpenedForRequests"": ""2026-09-30T00:00:00+00:00"",
        ""preferences"": { ""genres"": [""rock""], ""genresExcluded"": [], ""customGenres"": [], ""songLanguages"": [], ""songLanguagesExcluded"": [], ""customSongLanguages"": [],
                          ""osuStyles"": [], ""osuStylesExcluded"": [], ""taikoStyles"": [], ""taikoStylesExcluded"": [], ""catchStyles"": [], ""catchStylesExcluded"": [],
                          ""maniaStyles"": [], ""maniaStylesExcluded"": [], ""maniaKeymodes"": [], ""maniaKeymodesExcluded"": [], ""details"": [""anime""], ""detailsExcluded"": [],
                          ""customDetails"": [], ""mappers"": [], ""mappersExcluded"": [], ""customMappers"": [], ""customMaps"": [] },
        ""history"": [ { ""date"": ""2020-01-01T00:00:00+00:00"", ""mode"": ""Osu"", ""group"": ""Bn"", ""kind"": ""Joined"", ""relatedEvaluationId"": null },
                       { ""date"": ""2022-01-01T00:00:00+00:00"", ""mode"": ""General"", ""group"": ""Nat"", ""kind"": ""Joined"", ""relatedEvaluationId"": ""x"" } ],
        ""isRemoved"": false, ""removedAt"": null,
        ""firstSeenAt"": ""2026-01-01T00:00:00+00:00"", ""lastSyncedAt"": ""2026-10-10T00:00:00+00:00""
      },
      ""relevantModes"": [ { ""mode"": ""Osu"", ""level"": ""Full"" }, { ""mode"": ""General"", ""level"": ""Evaluator"" } ],
      ""preferenceMatch"": { ""genre"": ""Matches"", ""language"": ""Unknown"" },
      ""status"": ""Accepted"",
      ""statusUpdatedAt"": ""2026-10-02T00:00:00+00:00"",
      ""comments"": [ { ""id"": ""11111111-1111-1111-1111-111111111111"", ""text"": ""first"", ""createdAt"": ""2026-10-01T00:00:00+00:00"" },
                      { ""id"": ""22222222-2222-2222-2222-222222222222"", ""text"": ""second"", ""createdAt"": ""2026-10-03T00:00:00+00:00"" } ],
      ""activity"": [ { ""id"": ""33333333-3333-3333-3333-333333333333"", ""fromStatus"": ""NotAsked"", ""toStatus"": ""Accepted"", ""timestamp"": ""2026-10-02T00:00:00+00:00"", ""note"": null } ]
    }
  ]
}";

        [Test]
        public void TestDeserialiseBeatmapSetWithNominators()
        {
            var result = JsonConvert.DeserializeObject<BnBeatmapSetWithNominators>(set_with_nominators_json)!;

            Assert.That(result.BeatmapSet.Id, Is.EqualTo(Guid.Parse("3f2b5c1e-8d2a-4c1b-9f0e-1a2b3c4d5e6f")));
            Assert.That(result.BeatmapSet.OsuBeatmapSetId, Is.EqualTo(123456));
            Assert.That(result.BeatmapSet.Modes, Is.EqualTo(new[] { BnGameMode.Osu, BnGameMode.Taiko }));
            Assert.That(result.BeatmapSet.MainMode, Is.EqualTo(BnGameMode.Osu));
            Assert.That(result.BeatmapSet.RankStatus, Is.EqualTo(BnBeatmapRankStatus.Pending));
            Assert.That(result.BeatmapSet.Priority, Is.EqualTo(BnBeatmapPriority.High));
            Assert.That(result.BeatmapSet.Progress.PerMode, Has.Count.EqualTo(2));
            Assert.That(result.BeatmapSet.Progress.StatusCounts[BnNominationStatus.Accepted], Is.EqualTo(2));

            var nominator = result.Nominators.Single();

            Assert.That(nominator.Status, Is.EqualTo(BnNominationStatus.Accepted));
            Assert.That(nominator.Nominator.Username, Is.EqualTo("peppy"));
            Assert.That(nominator.Nominator.HasRequestChannel(BnNominator.REQUEST_CHANNEL_GAME_CHAT), Is.True);
            Assert.That(nominator.Nominator.FirstJoinedAt, Is.EqualTo(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.That(nominator.RelevantModes.Select(m => m.Level), Is.EqualTo(new[] { BnNominatorLevel.Full, BnNominatorLevel.Evaluator }));
            Assert.That(nominator.PreferenceMatch.Genre, Is.EqualTo(BnPreferenceMatch.Matches));
            Assert.That(nominator.LatestComment?.Text, Is.EqualTo("second"));
            Assert.That(nominator.Activity.Single().ToStatus, Is.EqualTo(BnNominationStatus.Accepted));
        }

        [Test]
        public void TestSerialiseStatusAsName()
        {
            string json = JsonConvert.SerializeObject(new { status = BnNominationStatus.NotAsked, priority = BnBeatmapPriority.Medium });

            Assert.That(json, Is.EqualTo(@"{""status"":""NotAsked"",""priority"":""Medium""}"));
        }

        [TestCase("wort.ee", "https://wort.ee")]
        [TestCase("  https://wort.ee/  ", "https://wort.ee")]
        [TestCase("http://localhost:5127", "http://localhost:5127")]
        [TestCase("", null)]
        [TestCase("   ", null)]
        public void TestNormaliseServerUrl(string address, string? expected)
        {
            Assert.That(BnTrackerClient.NormaliseServerUrl(address), Is.EqualTo(expected));
        }

        [Test]
        public void TestPreviewRelevantNominators()
        {
            var beatmapSet = createBeatmapSet(0, 0, 0, 1);

            var removed = createNominator(5, "removed osu bn", (BnGameMode.Osu, BnNominatorLevel.Full));
            removed.IsRemoved = true;

            var preview = BnTrackerPreview.Create(beatmapSet, new[]
            {
                createNominator(1, "osu bn", (BnGameMode.Osu, BnNominatorLevel.Full)),
                createNominator(2, "mania bn", (BnGameMode.Mania, BnNominatorLevel.Full)),
                createNominator(3, "general nat", (BnGameMode.General, BnNominatorLevel.Evaluator)),
                createNominator(4, "taiko and mania", (BnGameMode.Taiko, BnNominatorLevel.Probation), (BnGameMode.Mania, BnNominatorLevel.Full)),
                removed,
            });

            Assert.That(preview.BeatmapSet.Id, Is.EqualTo(Guid.Empty));
            Assert.That(preview.BeatmapSet.Modes, Is.EqualTo(new[] { BnGameMode.Osu, BnGameMode.Taiko }));
            Assert.That(preview.BeatmapSet.MainMode, Is.EqualTo(BnGameMode.Osu));

            Assert.That(preview.Nominators.Select(n => n.Nominator.Username), Is.EqualTo(new[] { "general nat", "osu bn", "taiko and mania" }));
            Assert.That(preview.Nominators.Single(n => n.NominatorOsuId == 4).RelevantModes.Single().Mode, Is.EqualTo(BnGameMode.Taiko));
            Assert.That(preview.Nominators.All(n => n.Status == BnNominationStatus.NotAsked), Is.True);

            var progress = preview.BeatmapSet.Progress;

            Assert.That(progress.PerMode.Select(m => (m.Mode, m.Required)), Is.EqualTo(new[] { (BnGameMode.Osu, 2), (BnGameMode.Taiko, 1) }));
            Assert.That(progress.TotalRequired, Is.EqualTo(3));
            Assert.That(progress.TotalFilled, Is.Zero);
            Assert.That(progress.IsFullBn, Is.False);
            Assert.That(progress.RelevantNominatorCount, Is.EqualTo(3));
            Assert.That(progress.StatusCounts[BnNominationStatus.NotAsked], Is.EqualTo(3));
        }

        [Test]
        public void TestPreviewMainModeTieGoesToFirstMode()
        {
            var preview = BnTrackerPreview.Create(createBeatmapSet(3, 1), Array.Empty<BnNominator>());

            Assert.That(preview.BeatmapSet.MainMode, Is.EqualTo(BnGameMode.Taiko));
            Assert.That(preview.BeatmapSet.Progress.PerMode.Single(m => m.Mode == BnGameMode.Mania).Required, Is.EqualTo(1));
        }

        [TestCase("Rock", BnPreferenceMatch.Matches)]
        [TestCase("Pop", BnPreferenceMatch.Excluded)]
        [TestCase("Hip Hop", BnPreferenceMatch.Matches)]
        [TestCase("Anime", BnPreferenceMatch.Matches)]
        [TestCase("Video Game", BnPreferenceMatch.Excluded)]
        [TestCase("Electronic", BnPreferenceMatch.Unknown)]
        [TestCase("Unspecified", BnPreferenceMatch.Unknown)]
        [TestCase(null, BnPreferenceMatch.Unknown)]
        public void TestMatchGenre(string? genre, BnPreferenceMatch expected)
        {
            var preferences = new BnNominatorPreferences
            {
                Genres = { "rock", "hiphop" },
                GenresExcluded = { "pop" },
                Details = { "anime" },
                DetailsExcluded = { "game" },
            };

            Assert.That(BnTrackerPreview.MatchGenre(preferences, genre), Is.EqualTo(expected));
        }

        [TestCase("Japanese", BnPreferenceMatch.Matches)]
        [TestCase("German", BnPreferenceMatch.Excluded)]
        [TestCase("English", BnPreferenceMatch.Unknown)]
        public void TestMatchLanguage(string language, BnPreferenceMatch expected)
        {
            var preferences = new BnNominatorPreferences
            {
                SongLanguages = { "japanese" },
                // every language which Mappers Guild doesn't name is "other".
                SongLanguagesExcluded = { "other" },
            };

            Assert.That(BnTrackerPreview.MatchLanguage(preferences, language), Is.EqualTo(expected));
        }

        [Test]
        public void TestFilter()
        {
            var now = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

            var nominators = new List<BnSetNominator>
            {
                createSetNominator(createNominator(1, "Bravo", (BnGameMode.Osu, BnNominatorLevel.Full)), BnNominationStatus.Pending, now.AddDays(-3)),
                createSetNominator(createNominator(2, "alpha", (BnGameMode.Taiko, BnNominatorLevel.Probation)), BnNominationStatus.NotAsked, now.AddDays(-40)),
                createSetNominator(createNominator(3, "Charlie", (BnGameMode.General, BnNominatorLevel.Evaluator)), BnNominationStatus.Accepted, null),
            };

            var filter = new NominatorFilter();

            Assert.That(names(), Is.EqualTo(new[] { "alpha", "Bravo", "Charlie" }));

            filter.Sort.Value = NominatorSortOrder.NameDescending;
            Assert.That(names(), Is.EqualTo(new[] { "Charlie", "Bravo", "alpha" }));

            filter.Sort.Value = NominatorSortOrder.Status;
            Assert.That(names(), Is.EqualTo(new[] { "alpha", "Bravo", "Charlie" }));

            filter.Sort.Value = NominatorSortOrder.LastOpenedNewest;
            Assert.That(names(), Is.EqualTo(new[] { "Bravo", "alpha", "Charlie" }));

            filter.Sort.Value = NominatorSortOrder.LastOpenedOldest;
            Assert.That(names(), Is.EqualTo(new[] { "alpha", "Bravo", "Charlie" }));

            filter.Clear();

            filter.Search.Value = "AV";
            Assert.That(names(), Is.EqualTo(new[] { "Bravo" }));
            filter.Search.SetDefault();

            // nominators not tied to a mode are relevant to every mode.
            filter.Modes.Add(BnGameMode.Osu);
            Assert.That(names(), Is.EqualTo(new[] { "Bravo", "Charlie" }));
            Assert.That(filter.ActiveFilterCount, Is.EqualTo(1));
            filter.Modes.Clear();

            filter.Group.Value = NominatorGroupFilter.ProbationaryBn;
            Assert.That(names(), Is.EqualTo(new[] { "alpha" }));
            filter.Group.SetDefault();

            filter.Status.Value = NominatorStatusFilter.Accepted;
            Assert.That(names(), Is.EqualTo(new[] { "Charlie" }));
            filter.Status.SetDefault();

            filter.LastOpened.Value = NominatorLastOpenedFilter.ThirtyDays;
            Assert.That(names(), Is.EqualTo(new[] { "Bravo" }));

            filter.Clear();
            Assert.That(filter.ActiveFilterCount, Is.Zero);
            Assert.That(names(), Has.Length.EqualTo(3));

            string[] names() => filter.Apply(nominators, now).Select(n => n.Nominator.Username).ToArray();
        }

        [Test]
        public void TestStatusFilterMatchesStatuses()
        {
            // the status filter relies on the statuses following "All" in the same order.
            foreach (var status in Enum.GetValues<BnNominationStatus>())
                Assert.That(((NominatorStatusFilter)((int)status + 1)).ToString(), Is.EqualTo(status.ToString()));
        }

        private static APIBeatmapSet createBeatmapSet(params int[] rulesetIds) => new APIBeatmapSet
        {
            OnlineID = 1,
            Title = "title",
            Artist = "artist",
            Status = BeatmapOnlineStatus.Pending,
            Genre = new BeatmapSetOnlineGenre { Id = 2, Name = "Video Game" },
            Language = new BeatmapSetOnlineLanguage { Id = 3, Name = "Japanese" },
            Beatmaps = rulesetIds.Select(r => new APIBeatmap { RulesetID = r }).ToArray(),
        };

        private static BnNominator createNominator(int id, string username, params (BnGameMode mode, BnNominatorLevel level)[] modes) => new BnNominator
        {
            OsuId = id,
            Username = username,
            Modes = modes.Select(m => new BnModeLevel { Mode = m.mode, Level = m.level }).ToList(),
            RequestChannels = { BnNominator.REQUEST_CHANNEL_PERSONAL_QUEUE },
            IsOpenForRequests = true,
        };

        private static BnSetNominator createSetNominator(BnNominator nominator, BnNominationStatus status, DateTimeOffset? lastOpened)
        {
            nominator.LastOpenedForRequests = lastOpened;

            return new BnSetNominator
            {
                NominatorOsuId = nominator.OsuId,
                Nominator = nominator,
                RelevantModes = nominator.Modes,
                Status = status,
            };
        }
    }
}
