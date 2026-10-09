// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Overlays.Rankings.Tables;
using osu.Game.Rulesets;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Overlays.Profile.Sections.Offline
{
    /// <summary>
    /// The ranking of all offline profiles, like the performance ranking of osu! accounts.
    /// </summary>
    public partial class OfflineRankingSection : ProfileSection
    {
        public override LocalisableString Title => OfflineProfileStrings.Ranking;

        public override string Identifier => @"offline_ranking";

        public OfflineRankingSection(OfflineProfileRanking ranking, RulesetInfo ruleset)
        {
            Children = new[]
            {
                new OfflineRankingSubsection(User, ranking, ruleset),
            };
        }

        private partial class OfflineRankingSubsection : ProfileSubsection
        {
            private readonly OfflineProfileRanking ranking;
            private readonly RulesetInfo ruleset;

            public OfflineRankingSubsection(Bindable<UserProfileData?> user, OfflineProfileRanking ranking, RulesetInfo ruleset)
                : base(user, ruleset.Name, CounterVisibilityState.AlwaysVisible)
            {
                this.ranking = ranking;
                this.ruleset = ruleset;
            }

            protected override Drawable CreateContent()
            {
                var rankedStatistics = ranking.Entries.Where(e => e.Statistics.Statistics.GlobalRank != null).Select(e =>
                {
                    var user = e.Profile.CreateUser();

                    // inactive users are dimmed in the table.
                    user.Active = true;
                    user.PlayMode = ruleset.ShortName;

                    var statistics = e.Statistics.Statistics;
                    statistics.User = user;
                    return statistics;
                }).ToList();

                var content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                };

                if (rankedStatistics.Count > 0)
                    content.Add(new OfflinePerformanceTable(rankedStatistics));

                if (rankedStatistics.Count < ranking.Entries.Count)
                {
                    content.Add(new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 15),
                        Text = OfflineProfileStrings.UnrankedProfiles,
                    });
                }

                return content;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                SetCount(ranking.Entries.Count(e => e.Statistics.Statistics.GlobalRank != null));
            }
        }

        private partial class OfflinePerformanceTable : PerformanceTable
        {
            public OfflinePerformanceTable(IReadOnlyList<UserStatistics> rankings)
                : base(1, rankings)
            {
                // the table is padded for the rankings overlay, while sections are padded already.
                Padding = new MarginPadding();
            }
        }
    }
}
