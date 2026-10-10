// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Online.API;

namespace osu.Game.Online.BeatmapMirrors
{
    /// <summary>
    /// Decides whether beatmaps are searched, looked up and downloaded through the selected <see cref="BeatmapMirror"/> instead of the official servers,
    /// and creates the requests for doing so.
    /// </summary>
    public class BeatmapMirrorProvider
    {
        private readonly IAPIProvider api;
        private readonly Bindable<BeatmapMirror> mirror;

        public BeatmapMirrorProvider(IAPIProvider api, OsuConfigManager config)
        {
            this.api = api;

            mirror = config.GetBindable<BeatmapMirror>(OsuSetting.SlopBeatmapMirror);
        }

        /// <summary>
        /// The mirror selected by the user.
        /// </summary>
        public BeatmapMirror Mirror => mirror.Value;

        /// <summary>
        /// Whether mirrors should be used instead of the official servers.
        /// This is the case while not logged in, or while connected to the development server (which doesn't have the official beatmaps).
        /// </summary>
        public bool IsActive => !api.IsLoggedIn || api.Endpoints is DevelopmentEndpointConfiguration;

        /// <summary>
        /// Creates a request which downloads the given beatmap set from the selected mirror.
        /// </summary>
        public ArchiveDownloadRequest<IBeatmapSetInfo> CreateDownloadRequest(IBeatmapSetInfo beatmapSet, bool noVideo)
            => new MirrorDownloadBeatmapSetRequest(beatmapSet, Mirror, noVideo);

        /// <summary>
        /// Performs a request to the metadata API of the selected mirror, falling back to other mirrors if it has none or the request fails.
        /// </summary>
        /// <param name="createRequest">Creates the request for a mirror.</param>
        /// <param name="onSuccess">Invoked with the response. Runs on the update thread.</param>
        /// <param name="onFailure">Invoked if the request failed on all mirrors. Runs on the update thread.</param>
        /// <param name="supportedMirrors">The mirrors which provide the requested data, or <c>null</c> if all mirrors with a metadata API do.</param>
        /// <returns>The lookup, which can be used to cancel it.</returns>
        public BeatmapMirrorLookup<T> PerformLookup<T>(Func<BeatmapMirror, BeatmapMirrorJsonRequest<T>> createRequest, Action<T> onSuccess, Action<Exception> onFailure,
                                                       IReadOnlyCollection<BeatmapMirror>? supportedMirrors = null)
            where T : class
        {
            var mirrors = Mirror.GetMetadataMirrors().Where(m => supportedMirrors == null || supportedMirrors.Contains(m)).ToArray();
            var lookup = new BeatmapMirrorLookup<T>(api, mirrors, createRequest, onSuccess, onFailure);
            lookup.Start();
            return lookup;
        }
    }
}
