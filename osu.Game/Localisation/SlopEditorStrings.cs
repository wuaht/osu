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
        /// "Failed to add "{0}" from the dropped .osu file."
        /// </summary>
        public static LocalisableString GuestDifficultyAddFailed(string name) => new TranslatableString(getKey(@"guest_difficulty_add_failed"),
            @"Failed to add ""{0}"" from the dropped .osu file.", name);

        /// <summary>
        /// "Failed to replace "{0}" with the dropped .osu file."
        /// </summary>
        public static LocalisableString GuestDifficultyReplaceFailed(string name) => new TranslatableString(getKey(@"guest_difficulty_replace_failed"),
            @"Failed to replace ""{0}"" with the dropped .osu file.", name);

        /// <summary>
        /// "Zoom timeline in"
        /// </summary>
        public static LocalisableString TimelineZoomIn => new TranslatableString(getKey(@"timeline_zoom_in"), @"Zoom timeline in");

        /// <summary>
        /// "Zoom timeline out"
        /// </summary>
        public static LocalisableString TimelineZoomOut => new TranslatableString(getKey(@"timeline_zoom_out"), @"Zoom timeline out");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
