// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Beatmaps
{
    public class BeatmapUpdater : IBeatmapUpdater
    {
        private readonly IWorkingBeatmapCache workingBeatmapCache;

        private readonly BeatmapDifficultyCache difficultyCache;

        private readonly BeatmapUpdaterMetadataLookup metadataLookup;

        private const int update_queue_request_concurrency = 4;

        private readonly ThreadedTaskScheduler updateScheduler = new ThreadedTaskScheduler(update_queue_request_concurrency, nameof(BeatmapUpdaterMetadataLookup));

        public BeatmapUpdater(IWorkingBeatmapCache workingBeatmapCache, BeatmapDifficultyCache difficultyCache, IAPIProvider api, Storage storage)
        {
            this.workingBeatmapCache = workingBeatmapCache;
            this.difficultyCache = difficultyCache;

            metadataLookup = new BeatmapUpdaterMetadataLookup(api, storage);
        }

        public void Queue(Live<BeatmapSetInfo> beatmapSet, MetadataLookupScope lookupScope = MetadataLookupScope.LocalCacheFirst)
        {
            Logger.Log($"Queueing change for local beatmap {beatmapSet}");
            Task.Factory.StartNew(() => beatmapSet.PerformRead(b => Process(b, lookupScope)), CancellationToken.None, TaskCreationOptions.HideScheduler | TaskCreationOptions.RunContinuationsAsynchronously,
                updateScheduler);
        }

        public void Process(BeatmapSetInfo beatmapSet, MetadataLookupScope lookupScope = MetadataLookupScope.LocalCacheFirst)
            => process(beatmapSet, lookupScope, null);

        /// <summary>
        /// Processes a beatmap set after a single difficulty of it was saved.
        /// Only the saved difficulty is recalculated, as the star ratings and statistics of the other difficulties only depend on their own (unchanged) files.
        /// Recalculating every difficulty of large sets made saving in the editor lag noticeably.
        /// </summary>
        public void ProcessSaved(BeatmapSetInfo beatmapSet, BeatmapInfo savedBeatmap)
            => process(beatmapSet, MetadataLookupScope.None, savedBeatmap);

        private void process(BeatmapSetInfo beatmapSet, MetadataLookupScope lookupScope, BeatmapInfo? onlyBeatmap)
        {
            beatmapSet.Realm!.Write(_ =>
            {
                // Before we use below, we want to invalidate.
                workingBeatmapCache.Invalidate(beatmapSet);

                if (lookupScope != MetadataLookupScope.None)
                    metadataLookup.Update(beatmapSet, lookupScope == MetadataLookupScope.OnlineFirst);

                foreach (BeatmapInfo beatmap in beatmapSet.Beatmaps)
                {
                    if (onlyBeatmap != null && !beatmap.Equals(onlyBeatmap))
                        continue;

                    var working = workingBeatmapCache.GetWorkingBeatmap(beatmap);

                    difficultyCache.Invalidate(beatmap, working.BeatmapInfo);

                    var ruleset = working.BeatmapInfo.Ruleset.CreateInstance();
                    var calculator = ruleset.CreateDifficultyCalculator(working);

                    beatmap.StarRating = calculator.Calculate().StarRating;
                    beatmap.UpdateStatisticsFromBeatmap(working.Beatmap);
                }

                // And invalidate again afterwards as re-fetching the most up-to-date database metadata will be required.
                workingBeatmapCache.Invalidate(beatmapSet);
            });
        }

        public void ProcessObjectCounts(BeatmapInfo beatmapInfo, MetadataLookupScope lookupScope = MetadataLookupScope.LocalCacheFirst)
        {
            beatmapInfo.Realm!.Write(_ =>
            {
                // Before we use below, we want to invalidate.
                workingBeatmapCache.Invalidate(beatmapInfo);

                var working = workingBeatmapCache.GetWorkingBeatmap(beatmapInfo);
                var beatmap = working.Beatmap;

                beatmapInfo.EndTimeObjectCount = beatmap.HitObjects.Count(h => h is IHasDuration);
                beatmapInfo.TotalObjectCount = beatmap.HitObjects.Count;

                // And invalidate again afterwards as re-fetching the most up-to-date database metadata will be required.
                workingBeatmapCache.Invalidate(beatmapInfo);
            });
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            if (metadataLookup.IsNotNull())
                metadataLookup.Dispose();

            if (updateScheduler.IsNotNull())
                updateScheduler.Dispose();
        }

        #endregion
    }
}
