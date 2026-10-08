// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Configuration
{
    public partial class OsuConfigManager
    {
        /// <summary>
        /// The prefix of all settings files of this client (e.g. <c>slop.game.ini</c>, <c>slop.framework.ini</c>).
        /// </summary>
        /// <remarks>
        /// The settings files without prefix are shared with an official osu!(lazer) installation using the same data folder.
        /// They are only read when this client is started for the first time, after which its settings are independent.
        /// </remarks>
        public const string CLIENT_SETTINGS_PREFIX = @"slop.";
    }
}
