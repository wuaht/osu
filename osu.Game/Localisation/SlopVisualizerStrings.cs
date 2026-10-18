// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopVisualizerStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopVisualizer";

        /// <summary>
        /// "visualizer"
        /// </summary>
        public static LocalisableString HeaderTitle => new TranslatableString(getKey(@"header_title"), @"visualizer");

        /// <summary>
        /// "watch your music come alive"
        /// </summary>
        public static LocalisableString HeaderDescription => new TranslatableString(getKey(@"header_description"), @"watch your music come alive");

        /// <summary>
        /// "Toggle visualizer"
        /// </summary>
        public static LocalisableString ToggleVisualizer => new TranslatableString(getKey(@"toggle_visualizer"), @"Toggle visualizer");

        /// <summary>
        /// "Switch to {0}"
        /// </summary>
        public static LocalisableString SwitchToStyle(LocalisableString style) => new TranslatableString(getKey(@"switch_to_style"), @"Switch to {0}", style);

        /// <summary>
        /// "Shuffle"
        /// </summary>
        public static LocalisableString Shuffle => new TranslatableString(getKey(@"shuffle"), @"Shuffle");

        /// <summary>
        /// "Playlist"
        /// </summary>
        public static LocalisableString Playlist => new TranslatableString(getKey(@"playlist"), @"Playlist");

        /// <summary>
        /// "Previous track"
        /// </summary>
        public static LocalisableString PreviousTrack => new TranslatableString(getKey(@"previous_track"), @"Previous track");

        /// <summary>
        /// "Next track"
        /// </summary>
        public static LocalisableString NextTrack => new TranslatableString(getKey(@"next_track"), @"Next track");

        /// <summary>
        /// "Close"
        /// </summary>
        public static LocalisableString Close => new TranslatableString(getKey(@"close"), @"Close");

        /// <summary>
        /// "Nothing to play"
        /// </summary>
        public static LocalisableString NothingToPlay => new TranslatableString(getKey(@"nothing_to_play"), @"Nothing to play");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
