// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Online.BnTracker;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Nominators;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneNominatorsScreen : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private NominatorsScreen screen => Editor.ChildrenOfType<NominatorsScreen>().Single();

        private NominatorCard[] visibleCards => screen.ChildrenOfType<NominatorCard>().Where(c => c.IsPresent).ToArray();

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            // the screen doesn't try to reach the BN Tracker for beatmap sets which weren't submitted.
            AddStep("mark beatmap set as not submitted", () => EditorBeatmap.BeatmapInfo.BeatmapSet!.OnlineID = -1);

            AddStep("switch to request screen", () => Editor.Mode.Value = EditorScreenMode.Nominators);
            AddUntilStep("wait for screen", () => Editor.ChildrenOfType<NominatorsScreen>().SingleOrDefault()?.IsLoaded == true);
        }

        [Test]
        public void TestNotSubmitted()
        {
            AddUntilStep("not submitted shown", () => screen.Session.State.Value == NominatorsSessionState.NotSubmitted);
            AddAssert("no cards", () => visibleCards.Length == 0);
        }

        [Test]
        public void TestNominators()
        {
            showData();

            AddUntilStep("all cards shown", () => visibleCards.Length == 4);

            AddStep("search for alpha", () => screen.Filter.Search.Value = "alpha");
            AddUntilStep("one card shown", () => visibleCards.Single().NominatorId == 1);

            AddStep("clear filters", () => screen.Filter.Clear());
            AddUntilStep("all cards shown", () => visibleCards.Length == 4);

            AddStep("hide removed", () => screen.Filter.HideRemoved.Value = true);
            AddUntilStep("removed nominator hidden", () => visibleCards.Length == 3);
            AddStep("clear filters", () => screen.Filter.Clear());

            AddStep("click first card", () =>
            {
                InputManager.MoveMouseTo(visibleCards.First());
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("details shown", () => screen.DetailsShown && screen.ChildrenOfType<NominatorDetails>().Single().IsLoaded);

            AddStep("press escape", () => InputManager.Key(Key.Escape));
            AddAssert("details closed", () => !screen.DetailsShown);
            AddAssert("still in editor", () => Editor.IsCurrentScreen());

            AddStep("enable frosted panels", () => config.SetValue(OsuSetting.SlopBnTrackerFrostedPanels, true));
            AddStep("show details of removed nominator", () => screen.ShowDetails(4));
            AddUntilStep("details shown", () => screen.ChildrenOfType<NominatorDetails>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("status locked", () => !screen.ChildrenOfType<NominatorDetails>().Single().ChildrenOfType<StatusButton>().Single().Enabled.Value);
            AddStep("close details", () => screen.CloseDetails());
            AddStep("disable frosted panels", () => config.SetValue(OsuSetting.SlopBnTrackerFrostedPanels, false));

            AddStep("enter selection mode", () => screen.SelectionMode.Value = true);
            AddStep("click two cards", () =>
            {
                InputManager.MoveMouseTo(visibleCards[0]);
                InputManager.Click(MouseButton.Left);
                InputManager.MoveMouseTo(visibleCards[1]);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("two selected", () => screen.Selection.Count == 2);
            AddAssert("details not shown", () => !screen.DetailsShown);

            AddStep("press escape", () => InputManager.Key(Key.Escape));
            AddAssert("selection mode left", () => !screen.SelectionMode.Value && screen.Selection.Count == 0);

            AddStep("update nominator", () =>
            {
                var data = screen.Session.Data.Value!;
                var nominators = data.Nominators.ToList();

                nominators[0] = createSetNominator(1, "alpha", BnNominationStatus.Declined);

                screen.Session.Data.Value = new BnBeatmapSetWithNominators { BeatmapSet = data.BeatmapSet, Nominators = nominators };
            });
            AddAssert("card updated", () => screen.ChildrenOfType<NominatorCard>().Single(c => c.NominatorId == 1).Nominator.Status == BnNominationStatus.Declined);
        }

        private void showData()
        {
            AddStep("show nominators", () =>
            {
                screen.Session.Data.Value = new BnBeatmapSetWithNominators
                {
                    BeatmapSet = new BnBeatmapSet
                    {
                        Id = Guid.NewGuid(),
                        OsuBeatmapSetId = 1,
                        Title = "Title",
                        Artist = "Artist",
                        CreatorUsername = "mapper",
                        Modes = { BnGameMode.Osu, BnGameMode.Taiko },
                        MainMode = BnGameMode.Osu,
                        DifficultyCount = 5,
                        Genre = "Anime",
                        Language = "Japanese",
                        Progress = new BnBeatmapSetProgress
                        {
                            PerMode =
                            {
                                new BnModeProgress { Mode = BnGameMode.Osu, Required = 2, Filled = 1 },
                                new BnModeProgress { Mode = BnGameMode.Taiko, Required = 1, Filled = 1, IsSatisfied = true },
                            },
                            TotalRequired = 3,
                            TotalFilled = 2,
                        },
                    },
                    Nominators =
                    {
                        createSetNominator(1, "alpha", BnNominationStatus.Accepted),
                        createSetNominator(2, "bravo", BnNominationStatus.Pending),
                        createSetNominator(3, "charlie", BnNominationStatus.NotAsked),
                        createSetNominator(4, "delta", BnNominationStatus.Declined, removed: true),
                    },
                };

                screen.Session.State.Value = NominatorsSessionState.Ready;
            });
        }

        private static BnSetNominator createSetNominator(int id, string username, BnNominationStatus status, bool removed = false)
        {
            var modes = new[] { new BnModeLevel { Mode = BnGameMode.Osu, Level = BnNominatorLevel.Full }, new BnModeLevel { Mode = BnGameMode.General, Level = BnNominatorLevel.Evaluator } };

            return new BnSetNominator
            {
                NominatorOsuId = id,
                Nominator = new BnNominator
                {
                    OsuId = id,
                    Username = username,
                    Modes = modes.ToList(),
                    SpokenLanguages = { "english" },
                    RequestChannels = { BnNominator.REQUEST_CHANNEL_PERSONAL_QUEUE, BnNominator.REQUEST_CHANNEL_GAME_CHAT },
                    IsOpenForRequests = !removed,
                    IsRemoved = removed,
                    RemovedAt = removed ? DateTimeOffset.Now.AddDays(-5) : null,
                    RequestLink = "https://example.com/queue",
                    RequestInfo = "[b]Open[/b] for [url=https://example.com]requests[/url], see ![](https://example.com/image.png) or https://example.com/rules.",
                    LastOpenedForRequests = DateTimeOffset.Now.AddDays(-id),
                    Preferences = new BnNominatorPreferences { Genres = { "rock" }, GenresExcluded = { "pop" } },
                    History =
                    {
                        new BnGroupHistoryEvent { Date = DateTimeOffset.Now.AddYears(-2), Mode = BnGameMode.Osu, Group = BnNominatorGroup.Bn, Kind = BnHistoryEventKind.Joined },
                    },
                    FirstSeenAt = DateTimeOffset.Now.AddMonths(-1),
                    LastSyncedAt = DateTimeOffset.Now,
                },
                RelevantModes = modes.ToList(),
                PreferenceMatch = new BnPreferenceMatchResult { Genre = BnPreferenceMatch.Matches, Language = BnPreferenceMatch.Excluded },
                Status = status,
                StatusUpdatedAt = status == BnNominationStatus.NotAsked ? null : DateTimeOffset.Now.AddHours(-id),
                Comments =
                {
                    new BnComment { Id = Guid.NewGuid(), Text = "asked in DMs\nwaiting", CreatedAt = DateTimeOffset.Now.AddDays(-1) },
                    new BnComment { Id = Guid.NewGuid(), Text = "link: https://example.com/discussion", CreatedAt = DateTimeOffset.Now },
                },
                Activity =
                {
                    new BnActivity { Id = Guid.NewGuid(), FromStatus = BnNominationStatus.NotAsked, ToStatus = status, Timestamp = DateTimeOffset.Now.AddHours(-id), Note = "note" },
                },
            };
        }
    }
}
