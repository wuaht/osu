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

        /// <summary>
        /// "Imported "{0}"."
        /// </summary>
        public static LocalisableString GuestDifficultyImported(string name) => new TranslatableString(getKey(@"guest_difficulty_imported"), @"Imported ""{0}"".", name);

        /// <summary>
        /// "The dropped .osu file couldn't be read."
        /// </summary>
        public static LocalisableString GuestDifficultyReadFailed => new TranslatableString(getKey(@"guest_difficulty_read_failed"), @"The dropped .osu file couldn't be read.");

        /// <summary>
        /// "The dropped .osu file couldn't be parsed."
        /// </summary>
        public static LocalisableString GuestDifficultyParseFailed => new TranslatableString(getKey(@"guest_difficulty_parse_failed"), @"The dropped .osu file couldn't be parsed.");

        /// <summary>
        /// "The dropped difficulty doesn't belong to this beatmap set."
        /// </summary>
        public static LocalisableString GuestDifficultyWrongSet => new TranslatableString(getKey(@"guest_difficulty_wrong_set"), @"The dropped difficulty doesn't belong to this beatmap set.");

        /// <summary>
        /// "The dropped difficulty is for a different game mode."
        /// </summary>
        public static LocalisableString GuestDifficultyWrongRuleset => new TranslatableString(getKey(@"guest_difficulty_wrong_ruleset"), @"The dropped difficulty is for a different game mode.");

        /// <summary>
        /// "Replace this difficulty?"
        /// </summary>
        public static LocalisableString GuestDifficultyNameMismatchHeader => new TranslatableString(getKey(@"guest_difficulty_name_mismatch_header"), @"Replace this difficulty?");

        /// <summary>
        /// "The dropped file is the difficulty "{0}", but "{1}" is open. Replace the content of "{1}" with it anyway?"
        /// </summary>
        public static LocalisableString GuestDifficultyNameMismatchBody(string dropped, string open) => new TranslatableString(getKey(@"guest_difficulty_name_mismatch_body"),
            @"The dropped file is the difficulty ""{0}"", but ""{1}"" is open. Replace the content of ""{1}"" with it anyway?", dropped, open);

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
