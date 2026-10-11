// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests;

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
    }
}
