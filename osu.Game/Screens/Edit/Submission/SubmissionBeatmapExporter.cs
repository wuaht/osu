// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Screens.Edit.Submission
{
    public class SubmissionBeatmapExporter : LegacyBeatmapExporter
    {
        private readonly uint? beatmapSetId;
        private readonly HashSet<int>? allocatedBeatmapIds;

        /// <summary>
        /// The hashes of the files of difficulties which aren't uploaded (e.g. the hitsound difficulty).
        /// They are still exported, such that they are kept when the local beatmap set is updated from the package, but don't get an online ID.
        /// </summary>
        private readonly IReadOnlySet<string> excludedBeatmapHashes;

        private bool exportingExcludedBeatmap;

        public SubmissionBeatmapExporter(Storage storage, PutBeatmapSetResponse putBeatmapSetResponse, IReadOnlySet<string>? excludedBeatmapHashes = null)
            : base(storage)
        {
            beatmapSetId = putBeatmapSetResponse.BeatmapSetId;
            allocatedBeatmapIds = putBeatmapSetResponse.BeatmapIds.Select(id => (int)id).ToHashSet();
            this.excludedBeatmapHashes = excludedBeatmapHashes ?? new HashSet<string>();
        }

        protected override Stream? GetFileContents(BeatmapSetInfo model, INamedFileUsage file)
        {
            exportingExcludedBeatmap = excludedBeatmapHashes.Contains(file.File.Hash);
            return base.GetFileContents(model, file);
        }

        protected override void MutateBeatmap(BeatmapSetInfo beatmapSet, IBeatmap playableBeatmap)
        {
            base.MutateBeatmap(beatmapSet, playableBeatmap);

            if (beatmapSetId != null && allocatedBeatmapIds != null)
            {
                playableBeatmap.BeatmapInfo.BeatmapSet = beatmapSet;
                playableBeatmap.BeatmapInfo.BeatmapSet!.OnlineID = (int)beatmapSetId;

                // difficulties which aren't uploaded don't exist online, so must not have an online ID (an ID from an earlier upload is removed by the server).
                if (exportingExcludedBeatmap)
                {
                    playableBeatmap.BeatmapInfo.OnlineID = -1;
                    return;
                }

                if (allocatedBeatmapIds.Contains(playableBeatmap.BeatmapInfo.OnlineID))
                {
                    allocatedBeatmapIds.Remove(playableBeatmap.BeatmapInfo.OnlineID);
                    return;
                }

                if (playableBeatmap.BeatmapInfo.OnlineID > 0)
                    throw new InvalidOperationException($@"Difficulty ""{playableBeatmap.BeatmapInfo.DifficultyName}"" has BeatmapID {playableBeatmap.BeatmapInfo.OnlineID} that has not been assigned to it by the server!");

                if (allocatedBeatmapIds.Count == 0)
                    throw new InvalidOperationException(@"Ran out of new beatmap IDs to assign to unsubmitted beatmaps!");

                int newId = allocatedBeatmapIds.First();
                allocatedBeatmapIds.Remove(newId);
                playableBeatmap.BeatmapInfo.OnlineID = newId;
            }
        }
    }
}
