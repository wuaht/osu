// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Online.API.Requests;
using osu.Game.Screens.Edit.Hitsounding;

namespace osu.Game.Screens.Edit.Submission
{
    public class BeatmapSubmissionSettings
    {
        public GetBeatmapSetRequest? LatestOnlineStateRequest { get; set; }

        public Bindable<BeatmapSubmissionTarget> Target { get; } = new Bindable<BeatmapSubmissionTarget>();

        public Bindable<bool> NotifyOnDiscussionReplies { get; } = new Bindable<bool>();

        /// <summary>
        /// The difficulties which aren't uploaded. They are kept locally, and deleted online if they have been uploaded before.
        /// By default, these are the hitsound difficulties, which only exist to edit hitsounds.
        /// </summary>
        public BindableList<BeatmapInfo> ExcludedBeatmaps { get; } = new BindableList<BeatmapInfo>();

        /// <summary>
        /// Excludes the hitsound difficulties of a beatmap set by default, unless the set only consists of them (as a beatmap set needs at least one difficulty).
        /// </summary>
        public void ExcludeHitsoundDifficulties(OsuConfigManager? config, IEnumerable<BeatmapInfo> beatmaps)
        {
            var all = beatmaps.ToList();
            var hitsoundDifficulties = all.Where(b => HitsoundPreferences.IsHitsoundDifficulty(config, b)).ToList();

            if (hitsoundDifficulties.Count < all.Count)
                ExcludedBeatmaps.AddRange(hitsoundDifficulties);
        }
    }
}
