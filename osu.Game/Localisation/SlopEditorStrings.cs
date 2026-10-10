// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopEditorStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopEditor";

        /// <summary>
        /// "Mute music"
        /// </summary>
        public static LocalisableString MuteMusic => new TranslatableString(getKey(@"mute_music"), @"Mute music");

        /// <summary>
        /// "Mute effects"
        /// </summary>
        public static LocalisableString MuteEffects => new TranslatableString(getKey(@"mute_effects"), @"Mute effects");

        /// <summary>
        /// "Music volume"
        /// </summary>
        public static LocalisableString MusicVolume => new TranslatableString(getKey(@"music_volume"), @"Music volume");

        /// <summary>
        /// "Effects volume"
        /// </summary>
        public static LocalisableString EffectsVolume => new TranslatableString(getKey(@"effects_volume"), @"Effects volume");

        /// <summary>
        /// "Muted"
        /// </summary>
        public static LocalisableString Muted => new TranslatableString(getKey(@"muted"), @"Muted");

        /// <summary>
        /// "Snap objects to anchor"
        /// </summary>
        public static LocalisableString SnapObjectsToAnchor => new TranslatableString(getKey(@"snap_objects_to_anchor"), @"Snap objects to anchor");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
