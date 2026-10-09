// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Models;
using osu.Game.Online.API;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Overlays;
using osu.Game.Overlays.Login;
using osu.Game.Overlays.OfflineProfiles;
using osu.Game.Overlays.Profile;
using osu.Game.Overlays.Profile.Header;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Overlays.Profile.Sections.Offline;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Overlays.Rankings.Tables;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Slop;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneOfflineProfiles : OsuTestScene
    {
        [Resolved]
        private OfflineProfileManager manager { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        private DummyAPIAccess dummyAPI => (DummyAPIAccess)API;

        private OfflineProfile profile = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("log out", () => dummyAPI.Logout());

            AddStep("create profile", () =>
            {
                // unique among all tests, as profiles are stored in the storage of the test game.
                profile = manager.Create(Guid.NewGuid().ToString("N")[..10], out string? error)!;
                Assert.That(error, Is.Null);
            });
        }

        [Test]
        public void TestProfileWithoutScores()
        {
            UserProfileOverlay overlay = null!;

            AddStep("show profile", () =>
            {
                overlay = createOverlay();
                overlay.ShowUser(profile.CreateUser());
            });

            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<OfflineRanksSection>().Any() && overlay.ChildrenOfType<OfflineHistoricalSection>().Any());
            AddAssert("no scores", () => overlay.ChildrenOfType<DrawableProfileScore>().Count(), () => Is.Zero);
            AddAssert("username displayed", () => overlay.ChildrenOfType<ProfileHeader>().Single().User.Value?.User.Username, () => Is.EqualTo(profile.Username));
        }

        [Test]
        public void TestProfileWithScore()
        {
            UserProfileOverlay overlay = null!;

            AddStep("add score", () =>
            {
                beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();

                Realm.Write(r =>
                {
                    var beatmap = r.All<BeatmapSetInfo>().Where(s => !s.DeletePending).OrderByDescending(s => s.DateAdded).First().Beatmaps.First(b => b.Ruleset.ShortName == @"osu");
                    beatmap.Status = BeatmapOnlineStatus.Ranked;

                    r.Add(new ScoreInfo(beatmap, r.All<RulesetInfo>().First(ri => ri.ShortName == @"osu"), new RealmUser { OnlineID = profile.UserID, Username = profile.Username })
                    {
                        Rank = ScoreRank.A,
                        TotalScore = 500_000,
                        Accuracy = 0.95,
                        MaxCombo = 50,
                        Statistics = { [HitResult.Great] = 50 },
                        Date = DateTimeOffset.Now,
                    });
                });
            });

            AddStep("show profile", () =>
            {
                overlay = createOverlay();
                overlay.ShowUser(profile.CreateUser());
            });

            AddUntilStep("top play listed", () => overlay.ChildrenOfType<OfflineRanksSection>().SingleOrDefault()?.ChildrenOfType<DrawableProfileWeightedScore>().Count(), () => Is.EqualTo(1));
            AddUntilStep("recent play listed", () => overlay.ChildrenOfType<OfflineHistoricalSection>().SingleOrDefault()?.ChildrenOfType<DrawableProfileScore>().Count(), () => Is.EqualTo(1));

            AddAssert("performance calculated", () => (double?)overlay.ChildrenOfType<ProfileHeader>().Single().User.Value?.User.Statistics.PP, () => Is.GreaterThan(0));
            AddAssert("ranked score", () => overlay.ChildrenOfType<ProfileHeader>().Single().User.Value?.User.Statistics.RankedScore, () => Is.EqualTo(500_000));

            // other profiles created by previous tests may be ranked as well.
            AddAssert("ranked", () => overlay.ChildrenOfType<ProfileHeader>().Single().User.Value?.User.Statistics.GlobalRank, () => Is.Not.Null);
            AddUntilStep("listed in ranking", () => overlay.ChildrenOfType<OfflineRankingSection>().SingleOrDefault()?.ChildrenOfType<PerformanceTable>().SingleOrDefault()?.ChildrenOfType<OsuSpriteText>()
                                                           .Any(t => t.Text.ToString() == profile.Username) == true);
        }

        [Test]
        public void TestOnlineFeaturesHidden()
        {
            UserProfileOverlay overlay = null!;

            AddStep("show profile", () =>
            {
                overlay = createOverlay();
                overlay.ShowUser(profile.CreateUser());
            });

            AddUntilStep("sections loaded", () => overlay.ChildrenOfType<OfflineRankingSection>().Any());
            AddAssert("open in browser hidden", () => overlay.ChildrenOfType<TopHeaderContainer>().Single().ChildrenOfType<ExternalLinkButton>().All(b => !b.IsPresent));
            AddAssert("followers hidden", () => overlay.ChildrenOfType<FollowersButton>().All(b => !b.Parent!.IsPresent));
            AddAssert("all rulesets shown", () => overlay.ChildrenOfType<ProfileRulesetTabItem>().All(t => t.IsPresent));
        }

        [Test]
        public void TestSettingsAndLoginPanel()
        {
            // the colour provider and the popover container are provided by the settings overlay in game.
            AddStep("show settings and login panel", () => Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Purple)) },
                Child = new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Children = new Drawable[]
                        {
                            new Container
                            {
                                Width = 400,
                                RelativeSizeAxes = Axes.Y,
                                Child = new OfflineProfileSettings(),
                            },
                            new Container
                            {
                                Width = 400,
                                RelativeSizeAxes = Axes.Y,
                                Child = new LoginPanel(),
                            },
                        }
                    }
                }
            });

            AddUntilStep("login form shown", () => this.ChildrenOfType<LoginForm>().Any() && this.ChildrenOfType<OfflineProfilePanel>().Any());

            AddStep("activate profile", () => manager.SetActiveProfile(profile));
            AddUntilStep("profile settings shown", () => this.ChildrenOfType<OfflineProfileSettings>().Single().ChildrenOfType<Graphics.UserInterfaceV2.FormFileSelector>().Count(), () => Is.EqualTo(2));
            AddUntilStep("signed in offline", () => this.ChildrenOfType<OfflineProfileRankPanel>().SingleOrDefault()?.User.Id, () => Is.EqualTo(profile.UserID));
            AddAssert("login form hidden", () => !this.ChildrenOfType<LoginForm>().Any());
            AddUntilStep("statistics calculated", () => this.ChildrenOfType<OfflineProfileRankPanel>().Single().ChildrenOfType<LoadingLayer>().All(l => l.State.Value == Visibility.Hidden));

            AddStep("sign out", () => this.ChildrenOfType<LoginPanel>().Single().ChildrenOfType<DangerousSettingsButton>().Single().TriggerClick());
            AddAssert("profile deactivated", () => manager.ActiveProfile.Value, () => Is.Null);
            AddUntilStep("login form shown", () => this.ChildrenOfType<LoginForm>().Any() && !this.ChildrenOfType<OfflineProfileRankPanel>().Any());
            AddUntilStep("profile settings hidden", () => this.ChildrenOfType<OfflineProfileSettings>().Single().ChildrenOfType<Graphics.UserInterfaceV2.FormFileSelector>().Count(), () => Is.Zero);
        }

        private UserProfileOverlay createOverlay()
        {
            var overlay = new UserProfileOverlay();

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[] { (typeof(UserProfileOverlay), overlay) },
                Child = overlay,
            };

            return overlay;
        }
    }
}
