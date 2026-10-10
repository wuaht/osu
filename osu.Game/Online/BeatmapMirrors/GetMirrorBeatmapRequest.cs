// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.BeatmapMirrors
{
    /// <summary>
    /// Looks up a beatmap difficulty by its online ID on a <see cref="BeatmapMirror"/>.
    /// </summary>
    public class GetMirrorBeatmapRequest : BeatmapMirrorJsonRequest<APIBeatmap>
    {
        public readonly int OnlineID;

        public GetMirrorBeatmapRequest(BeatmapMirror mirror, int onlineId)
            : base(mirror)
        {
            OnlineID = onlineId;
        }

        protected override string Target => $@"b/{OnlineID}";
    }
}
