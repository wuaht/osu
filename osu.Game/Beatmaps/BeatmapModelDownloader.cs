// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.BeatmapMirrors;

namespace osu.Game.Beatmaps
{
    public class BeatmapModelDownloader : ModelDownloader<BeatmapSetInfo, IBeatmapSetInfo>
    {
        private readonly BeatmapMirrorProvider? mirrors;

        protected override ArchiveDownloadRequest<IBeatmapSetInfo> CreateDownloadRequest(IBeatmapSetInfo set, bool minimiseDownloadSize)
        {
            if (mirrors?.IsActive == true)
                return mirrors.CreateDownloadRequest(set, minimiseDownloadSize);

            return new DownloadBeatmapSetRequest(set, minimiseDownloadSize);
        }

        public override ArchiveDownloadRequest<IBeatmapSetInfo>? GetExistingDownload(IBeatmapSetInfo model)
            => CurrentDownloads.Find(r => r.Model.OnlineID == model.OnlineID);

        public bool Download(IBeatmapSetInfo model, bool withoutVideo) => Download(model, withoutVideo, null);

        public void DownloadAsUpdate(BeatmapSetInfo originalModel, bool withoutVideo) => Download(originalModel, withoutVideo, originalModel);

        /// <param name="beatmapImporter">The importer for downloaded beatmap sets.</param>
        /// <param name="api">The API to perform the downloads with.</param>
        /// <param name="mirrors">Provides the beatmap mirror to download from while the official servers can't be used. If <c>null</c>, the official servers are always used.</param>
        public BeatmapModelDownloader(IModelImporter<BeatmapSetInfo> beatmapImporter, IAPIProvider api, BeatmapMirrorProvider? mirrors = null)
            : base(beatmapImporter, api)
        {
            this.mirrors = mirrors;
        }
    }
}
