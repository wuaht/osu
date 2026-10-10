// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Online.BeatmapMirrors
{
    /// <summary>
    /// Third-party servers which beatmaps can be downloaded from while the official servers can't be used.
    /// </summary>
    public enum BeatmapMirror
    {
        /// <summary>
        /// https://catboy.best
        /// </summary>
        [Description("Mino (catboy.best)")]
        Mino,

        /// <summary>
        /// https://osu.direct
        /// </summary>
        [Description("osu.direct")]
        OsuDirect,

        /// <summary>
        /// https://osu.sayobot.cn
        /// </summary>
        [Description("Sayobot")]
        Sayobot,

        /// <summary>
        /// https://beatconnect.io
        /// </summary>
        [Description("Beatconnect")]
        Beatconnect,

        /// <summary>
        /// https://nerinyan.moe
        /// </summary>
        [Description("NeriNyan")]
        NeriNyan,

        /// <summary>
        /// https://osudl.org (only mirrors ranked, approved and loved beatmaps)
        /// </summary>
        [Description("osu!dl")]
        OsuDl,
    }
}
