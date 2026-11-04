// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Models;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using Realms;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// stable's user panel next to the song select buttons (avatar, name, performance, accuracy, level and rank).
    /// </summary>
    public partial class LegacyUserPanel : LegacySongSelectComponent
    {
        private const float scale = STABLE_SCALE;

        // stable's level bar fills 198 texture pixels, which is 198 units here (a 1x texture pixel is 1/1.6 stable units).
        private const float level_bar_width = 198;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private LoginOverlay? loginOverlay { get; set; }

        [Resolved]
        private OfflineProfileManager? offlineProfiles { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();

        private readonly UpdateableAvatar avatar;
        private readonly StableText name;
        private readonly StableText[] infoLines = new StableText[3];
        private readonly StableText rank;
        private readonly Container levelBar;
        private readonly Sprite levelBarBackground;
        private readonly Container levelProgress;
        private readonly Sprite levelBarForeground;

        private UserStatistics? displayedStatistics;
        private bool displayValid;
        private (int userId, string ruleset)? requestedStatistics;

        /// <summary>
        /// The calculated statistics of the active offline profile by ruleset short name.
        /// </summary>
        private readonly Dictionary<string, UserStatistics> offlineStatistics = new Dictionary<string, UserStatistics>();

        private string? calculatingRuleset;
        private CancellationTokenSource? offlineCalculation;
        private IDisposable? offlineScoresSubscription;

        public LegacyUserPanel()
        {
            // stable draws the panel at (389.6, 429), right of the last footer button.
            // Its sprites render about (5, 4.5) further in, measured against stable at 1080p.
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.TopLeft;
            Position = new Vector2(389.6f + 5, 429 + 4.5f - 480) * scale;
            Size = new Vector2(204, 47) * scale;

            InternalChildren = new Drawable[]
            {
                new HoverClickSounds(),
                avatar = new UpdateableAvatar(isInteractive: false)
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(23) * scale,
                    // stable shows the avatar as a square of about 42 units.
                    Size = new Vector2(42 * scale),
                    Masking = true,
                },
                name = new StableText(new Vector2(48, -2), 14),
                infoLines[0] = new StableText(new Vector2(48, 12), 10),
                infoLines[1] = new StableText(new Vector2(48, 12), 10, 1),
                infoLines[2] = new StableText(new Vector2(48, 12), 10, 2),
                rank = new StableText(new Vector2(204, 7), 36, alignRight: true, weight: FontWeight.Light) { Shadow = false },
                // stable draws the level bar textures additively, 120x62 texture pixels from the panel origin.
                levelBar = new Container
                {
                    Position = new Vector2(120, 62),
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        levelBarBackground = new Sprite
                        {
                            Blending = BlendingParameters.Additive,
                            Alpha = 0.4f,
                        },
                        levelProgress = new Container
                        {
                            AutoSizeAxes = Axes.Y,
                            Masking = true,
                            Child = levelBarForeground = new Sprite
                            {
                                Blending = BlendingParameters.Additive,
                                Alpha = 0.7f,
                                Colour = new Color4(252, 184, 6, 255),
                            },
                        },
                    }
                },
            };
        }

        protected override void UpdateArtwork()
        {
            levelBarBackground.Texture = Skin.GetTexture("levelbar-bg");
            levelBarForeground.Texture = Skin.GetTexture("levelbar");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localUser.BindTo(api.LocalUser);
            localUser.BindValueChanged(_ =>
            {
                displayValid = false;
                resetOfflineStatistics();
            }, true);
            ruleset.BindValueChanged(_ => displayValid = false);
        }

        private bool isOfflineProfile => OfflineProfileUser.IsOfflineProfileID(localUser.Value.Id);

        private bool isGuest => localUser.Value.Id <= 1 && !isOfflineProfile;

        protected override bool OnClick(ClickEvent e)
        {
            if (isGuest)
                loginOverlay?.Show();
            else
                game?.ShowUser(localUser.Value);

            return true;
        }

        protected override void Update()
        {
            base.Update();

            // polled rather than driven by events, as the statistics arrive (and get cleared) asynchronously around user and ruleset changes.
            var statistics = isOfflineProfile ? getOfflineStatistics() : statisticsProvider?.GetStatisticsFor(ruleset.Value);

            if (statistics == null && !isOfflineProfile)
                requestMissingStatistics();

            if (displayValid && ReferenceEquals(statistics, displayedStatistics))
                return;

            displayValid = true;
            displayedStatistics = statistics;
            updateDisplay(statistics);
        }

        private void requestMissingStatistics()
        {
            if (statisticsProvider == null || isGuest || api.State.Value != APIState.Online || !ruleset.Value.IsLegacyRuleset())
                return;

            var key = (localUser.Value.Id, ruleset.Value.ShortName);

            if (requestedStatistics == key)
                return;

            requestedStatistics = key;
            statisticsProvider!.RefetchStatistics(ruleset.Value);
        }

        private void resetOfflineStatistics()
        {
            offlineCalculation?.Cancel();
            offlineScoresSubscription?.Dispose();
            offlineScoresSubscription = null;
            offlineStatistics.Clear();
            calculatingRuleset = null;

            if (!isOfflineProfile || offlineProfiles == null)
                return;

            // offline profiles are ranked from their local scores, like their toolbar card. Also invoked initially.
            offlineScoresSubscription = realm.RegisterForNotifications(r => r.All<ScoreInfo>().Filter($"{nameof(ScoreInfo.User)}.{nameof(RealmUser.OnlineID)} == $0", localUser.Value.Id),
                (_, _) =>
                {
                    // the current statistics stay displayed until recalculated.
                    foreach (string shortName in offlineStatistics.Keys.Where(k => k != ruleset.Value.ShortName).ToArray())
                        offlineStatistics.Remove(shortName);

                    Scheduler.AddOnce(calculateOfflineStatistics);
                });
        }

        private UserStatistics? getOfflineStatistics()
        {
            if (offlineStatistics.TryGetValue(ruleset.Value.ShortName, out var statistics))
                return statistics;

            if (offlineProfiles != null && calculatingRuleset != ruleset.Value.ShortName)
                calculateOfflineStatistics();

            return null;
        }

        private void calculateOfflineStatistics()
        {
            var profile = offlineProfiles?.GetProfileByUserID(localUser.Value.Id);

            if (profile == null)
                return;

            offlineCalculation?.Cancel();

            var cancellation = offlineCalculation = new CancellationTokenSource();
            var calculatedRuleset = ruleset.Value;
            calculatingRuleset = calculatedRuleset.ShortName;

            offlineProfiles!.CalculateRankingAsync(calculatedRuleset, cancellation.Token).ContinueWith(task => Schedule(() =>
            {
                if (cancellation.IsCancellationRequested)
                    return;

                if (!task.IsCompletedSuccessfully)
                    Logger.Error(task.Exception, $"Failed to calculate the statistics of offline profile {profile.Username}");

                // empty statistics if they can't be calculated, so that the card doesn't keep calculating.
                offlineStatistics[calculatedRuleset.ShortName] = (task.IsCompletedSuccessfully ? task.GetResultSafely().GetEntry(profile)?.Statistics.Statistics : null) ?? new UserStatistics();
                calculatingRuleset = null;
            }), CancellationToken.None);
        }

        protected override void Dispose(bool isDisposing)
        {
            offlineCalculation?.Cancel();
            offlineScoresSubscription?.Dispose();

            base.Dispose(isDisposing);
        }

        private void updateDisplay(UserStatistics? statistics)
        {
            var user = localUser.Value;

            avatar.User = user;
            name.Text = user.Username;

            foreach (var line in infoLines)
                line.Text = string.Empty;

            rank.Text = string.Empty;
            levelBar.Hide();

            // stable shows the login prompt for guests, and nothing while the statistics are still loading.
            if (isGuest)
            {
                infoLines[0].Text = "Click to sign in!";
                return;
            }

            if (statistics == null)
                return;

            levelBar.Show();
            levelProgress.Width = level_bar_width * statistics.Level.Progress / 100f;

            if (statistics.PP > 0)
                infoLines[0].Text = string.Format(CultureInfo.InvariantCulture, "Performance:{0:#,0}pp", statistics.PP);
            else if (statistics.RankedScore > 0)
                infoLines[0].Text = $"Score:{formatScore(statistics.RankedScore)}";
            else
            {
                infoLines[0].Text = "Never played";
                return;
            }

            infoLines[1].Text = string.Format(CultureInfo.InvariantCulture, "Accuracy:{0:0.00}%", statistics.Accuracy);
            infoLines[2].Text = $"Lv{statistics.Level.Current}";

            int? globalRank = statistics.GlobalRank;
            rank.Text = globalRank > 0 ? $"#{globalRank}" : string.Empty;
            rank.Colour = rankColour(globalRank ?? 0);
        }

        private static string formatScore(long score)
        {
            (double value, string suffix) = score > 1_000_000_000 ? (score / 1e9, "b") : score > 1_000_000 ? (score / 1e6, "m") : (score / 1e3, "k");
            return value.ToString(value % 1 != 0 ? "#,0.0" : "#,0", CultureInfo.InvariantCulture) + suffix;
        }

        // stable fades the rank in as it improves, with gold and blue for the very top.
        private static Color4 rankColour(int rank)
        {
            if (rank > 100000) return Color4.White.Opacity(40 / 255f);
            if (rank > 50000) return Color4.White.Opacity(60 / 255f);
            if (rank > 1000) return Color4.White.Opacity(80 / 255f);
            if (rank > 10) return Color4.White.Opacity(100 / 255f);
            if (rank > 1) return new Color4(244, 218, 73, 120);

            return new Color4(88, 171, 248, 120);
        }

        /// <summary>
        /// A line of stable text, placed by its baseline the way stable renders text at a given origin and size.
        /// </summary>
        internal partial class StableText : OsuSpriteText
        {
            // stable's Aller renders at 1.333x the requested size, shown at 1/1.6 scale.
            public const float EM = 1.3333f / scale;

            // Aller's ascent (0.936 em) puts the first baseline 0.78 size below the origin; lines are 1.185 em apart.
            private const float baseline = 0.936f * EM;
            private const float line_height = 1.185f * EM;

            // stable pads text by size/6 pixels.
            private const float padding = 1 / 6f / scale;

            private readonly Vector2 stablePosition;
            private readonly float stableSize;
            private readonly int line;

            public StableText(Vector2 stablePosition, float stableSize, int line = 0, bool alignRight = false, FontWeight weight = FontWeight.Regular)
            {
                this.stablePosition = stablePosition;
                this.stableSize = stableSize;
                this.line = line;

                Origin = alignRight ? Anchor.TopRight : Anchor.TopLeft;
                Font = OsuFont.GetFont(size: stableSize * EM * scale, weight: weight);
                Shadow = true;
            }

            protected override void Update()
            {
                base.Update();

                float x = stablePosition.X + (Origin == Anchor.TopRight ? -1 : 1) * padding * stableSize;
                float y = stablePosition.Y + (baseline + line * line_height) * stableSize;

                Position = new Vector2(x * scale, y * scale - LineBaseHeight);
            }
        }
    }
}
