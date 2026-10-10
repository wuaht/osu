// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Online.BeatmapMirrors
{
    public static class BeatmapMirrorExtensions
    {
        /// <summary>
        /// The mirrors which provide a search and lookup API returning osu!-API-v2-compatible beatmap sets, in order of preference.
        /// </summary>
        private static readonly BeatmapMirror[] metadata_mirrors = { BeatmapMirror.Mino, BeatmapMirror.OsuDirect };

        /// <summary>
        /// Returns the URL the beatmap set with the given online ID can be downloaded from.
        /// </summary>
        /// <param name="mirror">The mirror to download from.</param>
        /// <param name="beatmapSetId">The online ID of the beatmap set.</param>
        /// <param name="noVideo">Whether to download the beatmap set without its video, if the mirror supports it.</param>
        public static string GetDownloadUrl(this BeatmapMirror mirror, int beatmapSetId, bool noVideo)
        {
            switch (mirror)
            {
                case BeatmapMirror.Mino:
                    return $@"https://catboy.best/d/{beatmapSetId}{(noVideo ? "n" : "")}";

                case BeatmapMirror.OsuDirect:
                    return $@"https://osu.direct/api/d/{beatmapSetId}{(noVideo ? "?noVideo=1" : "")}";

                case BeatmapMirror.Sayobot:
                    return $@"https://dl.sayobot.cn/beatmaps/download/{(noVideo ? "novideo" : "full")}/{beatmapSetId}";

                case BeatmapMirror.Beatconnect:
                    // beatconnect has no option to exclude the video.
                    return $@"https://beatconnect.io/b/{beatmapSetId}/";

                case BeatmapMirror.NeriNyan:
                    return $@"https://api.nerinyan.moe/d/{beatmapSetId}{(noVideo ? "?noVideo=true" : "")}";

                case BeatmapMirror.OsuDl:
                    return $@"https://osudl.org/s/{beatmapSetId}{(noVideo ? "?video=false" : "")}";

                default:
                    throw new ArgumentOutOfRangeException(nameof(mirror), mirror, null);
            }
        }

        /// <summary>
        /// Returns the base URL of the osu!-API-v2-compatible endpoints of the given mirror, or <c>null</c> if the mirror doesn't provide any.
        /// </summary>
        public static string? GetMetadataApiUrl(this BeatmapMirror mirror)
        {
            switch (mirror)
            {
                case BeatmapMirror.Mino:
                    return @"https://catboy.best/api/v2";

                case BeatmapMirror.OsuDirect:
                    return @"https://osu.direct/api/v2";

                default:
                    return null;
            }
        }

        /// <summary>
        /// Returns the mirrors which beatmap searches and lookups are sent to when the given mirror is selected, in the order they should be tried in.
        /// </summary>
        /// <remarks>
        /// Not every mirror has a search or lookup API, so others are used as a fallback.
        /// </remarks>
        public static IEnumerable<BeatmapMirror> GetMetadataMirrors(this BeatmapMirror mirror)
        {
            if (mirror.GetMetadataApiUrl() != null)
                yield return mirror;

            foreach (var fallback in metadata_mirrors.Where(m => m != mirror))
                yield return fallback;
        }
    }
}
