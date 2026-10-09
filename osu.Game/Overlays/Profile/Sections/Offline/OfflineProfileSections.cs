// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Overlays.Profile.Sections.Ranks;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Overlays.Profile.Sections.Offline
{
    /// <summary>
    /// The best performance of an offline profile, like <see cref="RanksSection"/>.
    /// </summary>
    public partial class OfflineRanksSection : ProfileSection
    {
        public override LocalisableString Title => UsersStrings.ShowExtraTopRanksTitle;

        public override string Identifier => @"top_ranks";

        public OfflineRanksSection(OfflineProfileStatistics statistics)
        {
            Children = new[]
            {
                new OfflineScoresSubsection(User, UsersStrings.ShowExtraTopRanksBestTitle, statistics.TopPlays, statistics.IncludesUnrankedBeatmaps, true),
            };
        }
    }

    /// <summary>
    /// The recent plays of an offline profile, like <see cref="HistoricalSection"/>.
    /// </summary>
    public partial class OfflineHistoricalSection : ProfileSection
    {
        public override LocalisableString Title => UsersStrings.ShowExtraHistoricalTitle;

        public override string Identifier => @"historical";

        public OfflineHistoricalSection(OfflineProfileStatistics statistics)
        {
            Children = new[]
            {
                new OfflineScoresSubsection(User, OfflineProfileStrings.RecentPlays, statistics.RecentPlays, statistics.IncludesUnrankedBeatmaps, false),
            };
        }
    }

    /// <summary>
    /// Lists local scores like <see cref="PaginatedScoreContainer"/>.
    /// </summary>
    public partial class OfflineScoresSubsection : ProfileSubsection
    {
        private readonly IReadOnlyList<ScoreInfo> scores;
        private readonly bool includeUnrankedBeatmaps;
        private readonly bool weighted;

        /// <param name="user">The user whose scores are listed.</param>
        /// <param name="headerText">The header of the list.</param>
        /// <param name="scores">The scores to list.</param>
        /// <param name="includeUnrankedBeatmaps">Whether scores on unranked beatmaps award performance.</param>
        /// <param name="weighted">Whether the scores are weighted by their position in the list (like the best performance of osu! accounts).</param>
        public OfflineScoresSubsection(Bindable<UserProfileData?> user, LocalisableString headerText, IReadOnlyList<ScoreInfo> scores, bool includeUnrankedBeatmaps, bool weighted)
            : base(user, headerText, CounterVisibilityState.AlwaysVisible)
        {
            this.scores = scores;
            this.includeUnrankedBeatmaps = includeUnrankedBeatmaps;
            this.weighted = weighted;
        }

        protected override Drawable CreateContent()
        {
            if (scores.Count == 0)
            {
                return new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 15),
                    Text = OfflineProfileStrings.NoScores,
                };
            }

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = scores.Select((score, index) => weighted
                    ? new DrawableProfileWeightedScore(CreateSoloScoreInfo(score, includeUnrankedBeatmaps), Math.Pow(OfflineProfileStatistics.WEIGHT_FACTOR, index))
                    {
                        Ruleset = score.Ruleset,
                        ShowPerformanceOnAnyBeatmap = includeUnrankedBeatmaps,
                    }
                    : new DrawableProfileScore(CreateSoloScoreInfo(score, includeUnrankedBeatmaps))
                    {
                        Ruleset = score.Ruleset,
                        ShowPerformanceOnAnyBeatmap = includeUnrankedBeatmaps,
                    }).ToArray(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            SetCount(scores.Count);
        }

        /// <summary>
        /// Converts a local score to the format of scores on osu! profiles.
        /// </summary>
        /// <param name="score">The score.</param>
        /// <param name="includeUnrankedBeatmaps">Whether scores on unranked beatmaps award performance.</param>
        public static SoloScoreInfo CreateSoloScoreInfo(ScoreInfo score, bool includeUnrankedBeatmaps)
        {
            var solo = SoloScoreInfo.ForSubmission(score);

            var beatmap = score.BeatmapInfo!;
            var metadata = beatmap.Metadata;

            solo.EndedAt = score.Date;
            solo.BeatmapID = beatmap.OnlineID;
            solo.Ranked = OfflineProfileStatistics.AwardsPerformance(score, includeUnrankedBeatmaps);
            solo.Processed = true;
            solo.Beatmap = new APIBeatmap
            {
                OnlineID = beatmap.OnlineID,
                DifficultyName = beatmap.DifficultyName,
                StarRating = beatmap.StarRating,
                Status = beatmap.Status,
                RulesetID = beatmap.Ruleset.OnlineID,
                Length = beatmap.Length,
                BPM = beatmap.BPM,
                BeatmapSet = new APIBeatmapSet
                {
                    OnlineID = beatmap.BeatmapSet?.OnlineID ?? -1,
                    Status = beatmap.Status,
                    Title = metadata.Title,
                    TitleUnicode = metadata.TitleUnicode,
                    Artist = metadata.Artist,
                    ArtistUnicode = metadata.ArtistUnicode,
                    Author = new APIUser { Username = metadata.Author.Username, Id = metadata.Author.OnlineID },
                },
            };

            return solo;
        }
    }
}
