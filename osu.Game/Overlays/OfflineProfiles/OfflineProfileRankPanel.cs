// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Users;
using Realms;

namespace osu.Game.Overlays.OfflineProfiles
{
    /// <summary>
    /// The user card of an <see cref="OfflineProfile"/>, showing its rank among all offline profiles.
    /// </summary>
    public partial class OfflineProfileRankPanel : UserRankPanel
    {
        private readonly OfflineProfile profile;

        [Resolved]
        private OfflineProfileManager manager { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        /// <summary>
        /// The calculated statistics by ruleset short name.
        /// </summary>
        private readonly Dictionary<string, UserStatistics> statistics = new Dictionary<string, UserStatistics>();

        private CancellationTokenSource? calculationCancellation;

        private IDisposable? scoresSubscription;

        public OfflineProfileRankPanel(OfflineProfile profile)
            : base(profile.CreateUser())
        {
            this.profile = profile;
        }

        protected override Drawable CreateLayout()
        {
            var layout = base.CreateLayout();

            Details.Add(new OfflineProfilePill
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            });

            return layout;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => Scheduler.AddOnce(calculate));

            // also invoked initially.
            scoresSubscription = realm.RegisterForNotifications(r => r.All<ScoreInfo>().Filter($"{nameof(ScoreInfo.User)}.{nameof(RealmUser.OnlineID)} == $0", profile.UserID),
                (_, _) =>
                {
                    // statistics of other rulesets are recalculated when they are displayed, the current ones stay displayed until recalculated.
                    foreach (string shortName in statistics.Keys.Where(k => k != ruleset.Value.ShortName).ToArray())
                        statistics.Remove(shortName);

                    Scheduler.AddOnce(calculate);
                });
        }

        protected override UserStatistics? GetStatistics(RulesetInfo ruleset) => statistics.GetValueOrDefault(ruleset.ShortName);

        private void calculate()
        {
            calculationCancellation?.Cancel();

            var cancellation = calculationCancellation = new CancellationTokenSource();
            var calculatedRuleset = ruleset.Value;

            manager.CalculateRankingAsync(calculatedRuleset, cancellation.Token).ContinueWith(task => Schedule(() =>
            {
                if (cancellation.IsCancellationRequested)
                    return;

                if (!task.IsCompletedSuccessfully)
                    Logger.Error(task.Exception, $"Failed to calculate the statistics of offline profile {profile.Username}");

                // empty statistics if they can't be calculated, so that the card doesn't keep loading.
                statistics[calculatedRuleset.ShortName] = (task.IsCompletedSuccessfully ? task.GetResultSafely().GetEntry(profile)?.Statistics.Statistics : null) ?? new UserStatistics();

                if (calculatedRuleset.Equals(ruleset.Value))
                    UpdateDisplay();
            }), CancellationToken.None);
        }

        protected override void Dispose(bool isDisposing)
        {
            calculationCancellation?.Cancel();
            scoresSubscription?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
