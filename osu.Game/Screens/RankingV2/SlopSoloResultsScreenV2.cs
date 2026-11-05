// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// SLOP-RESULTS-V2: this whole file is a temporary stand-in until upstream finishes SoloResultsScreenV2.
// When upstream's finished results screen is merged, delete this file (and every other SLOP-RESULTS-V2 change)
// and use upstream's results screen instead.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Screens;
using osu.Game.Configuration;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;

namespace osu.Game.Screens.RankingV2
{
    /// <summary>
    /// Upstream's work-in-progress <see cref="SoloResultsScreenV2"/>, made usable in gameplay: retry, watching the replay, the online ranking and the entry animation.
    /// </summary>
    /// <remarks>
    /// SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2.
    /// When upstream's finished results screen is merged, delete this class and use upstream's results screen instead.
    /// </remarks>
    [Cached]
    public partial class SlopSoloResultsScreenV2 : SoloResultsScreenV2
    {
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        private readonly ScoreInfo score;

        /// <summary>
        /// Whether the user can retry the beatmap, like <see cref="ResultsScreen.AllowRetry"/>.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public bool AllowRetry { get; init; }

        /// <summary>
        /// Whether the user can watch the replay, like <see cref="ResultsScreen.AllowWatchingReplay"/>.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public bool AllowWatchingReplay { get; init; } = true;

        /// <summary>
        /// Whether the score was just set by the local user, like <see cref="ResultsScreen.IsLocalPlay"/>.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public bool IsLocalPlay { get; init; }

        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        [Resolved]
        private Player? player { get; set; }

        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        [Resolved]
        private OsuGame? game { get; set; }

        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        private bool animationStarted;

        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public SlopSoloResultsScreenV2(ScoreInfo score)
            : base(score)
        {
            this.score = score;
        }

        /// <summary>
        /// Whether <see cref="Retry"/> does anything, i.e. this screen was reached from gameplay that allows retrying.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public bool CanRetry => player != null && AllowRetry;

        /// <summary>
        /// Whether <see cref="WatchReplay"/> does anything.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public bool CanWatchReplay => game != null && AllowWatchingReplay;

        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        [BackgroundDependencyLoader]
        private void load()
        {
            // SLOP-RESULTS-V2: same quick retry rules as the old ResultsScreen. Remove once upstream's results screen is merged.
            bool allowHotkeyRetry = (AllowWatchingReplay && player is ReplayPlayer) || CanRetry;

            // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
            if (allowHotkeyRetry)
            {
                AddInternal(new HotkeyRetryOverlay
                {
                    Action = () =>
                    {
                        if (!this.IsCurrentScreen()) return;

                        player?.Restart(true);
                    },
                });
            }
        }

        /// <summary>
        /// Restarts the beatmap, like stable's retry button.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public void Retry()
        {
            if (!this.IsCurrentScreen() || !CanRetry) return;

            player?.Restart();
        }

        /// <summary>
        /// Plays back the score's replay, like the old results screen's replay button.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public void WatchReplay()
        {
            if (!this.IsCurrentScreen() || !CanWatchReplay) return;

            game?.PresentScore(score, ScorePresentType.Gameplay);
        }

        /// <summary>
        /// Shows the old results screen for the same score, which has the online leaderboard and the user's ranking changes.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public void ShowOnlineRanking()
        {
            if (!this.IsCurrentScreen()) return;

            this.Push(new SoloResultsScreen(score)
            {
                AllowRetry = AllowRetry,
                AllowWatchingReplay = AllowWatchingReplay,
                IsLocalPlay = IsLocalPlay,
            });
        }

        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            // SLOP-RESULTS-V2: upstream doesn't start the animation itself yet; the skin's components load asynchronously, so wait for them.
            var mainScreen = InternalChildren.OfType<SkinnableContainer>().Single();

            if (mainScreen.ComponentsLoaded)
                startAnimation();
            else
                mainScreen.OnComponentsLoaded += _ => startAnimation();
        }

        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        private void startAnimation()
        {
            // SLOP-RESULTS-V2: only animate on entry, not again when a skin change reloads the components.
            if (animationStarted) return;

            animationStarted = true;
            StartAnimating();
        }

        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e))
                return true;

            // SLOP-RESULTS-V2: same safety as the old ResultsScreen, so nothing keeps gameplay alive through the hit events' hit objects.
            score.HitEvents.Clear();
            return false;
        }

        /// <summary>
        /// Returns this screen in place of a plain <see cref="SoloResultsScreen"/> while a legacy skin is active and <see cref="OsuSetting.SlopLegacyResultsScreen"/> is enabled.
        /// Any other results screen (multiplayer, playlists, spectator…) is returned unchanged.
        /// </summary>
        // SLOP-RESULTS-V2: temporary until upstream finishes SoloResultsScreenV2. Remove once upstream's results screen is merged.
        public static OsuScreen ReplaceSoloResults(ResultsScreen results, ISkinSource skin, OsuConfigManager config)
        {
            // SLOP-RESULTS-V2: exact type, so subclasses such as SpectatorResultsScreen keep their own screen.
            if (results.GetType() != typeof(SoloResultsScreen) || results.Score == null)
                return results;

            // SLOP-RESULTS-V2: upstream only has a finished-looking layout for legacy skins, so lazer skins keep the old screen.
            if (!config.Get<bool>(OsuSetting.SlopLegacyResultsScreen) || !skin.UsesLegacyLayout())
                return results;

            return new SlopSoloResultsScreenV2(results.Score)
            {
                AllowRetry = results.AllowRetry,
                AllowWatchingReplay = results.AllowWatchingReplay,
                IsLocalPlay = results.IsLocalPlay,
            };
        }
    }
}
