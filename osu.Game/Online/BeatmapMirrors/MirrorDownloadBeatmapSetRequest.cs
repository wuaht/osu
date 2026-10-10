// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Online.API;

namespace osu.Game.Online.BeatmapMirrors
{
    /// <summary>
    /// Downloads a beatmap set from a <see cref="BeatmapMirror"/>.
    /// </summary>
    public class MirrorDownloadBeatmapSetRequest : ArchiveDownloadRequest<IBeatmapSetInfo>
    {
        public readonly BeatmapMirror Mirror;

        private readonly bool noVideo;

        public MirrorDownloadBeatmapSetRequest(IBeatmapSetInfo set, BeatmapMirror mirror, bool noVideo)
            : base(set)
        {
            Mirror = mirror;
            this.noVideo = noVideo;
        }

        protected override WebRequest CreateWebRequest()
        {
            var req = base.CreateWebRequest();
            req.Timeout = 60000;
            return req;
        }

        protected override string FileExtension => @".osz";

        protected override string Target => Uri;

        protected override string Uri => Mirror.GetDownloadUrl(Model.OnlineID, noVideo);

        protected override bool TargetsOsuServer => false;

        public override string ToString() => $@"{GetType().Name} ({Uri})";
    }
}
