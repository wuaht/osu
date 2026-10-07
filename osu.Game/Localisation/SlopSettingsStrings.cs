// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopSettings";

        /// <summary>
        /// "slop!"
        /// </summary>
        public static LocalisableString SlopSectionHeader => new TranslatableString(getKey(@"slop_section_header"), @"ai slop");

        /// <summary>
        /// "Editor"
        /// </summary>
        public static LocalisableString EditorHeader => new TranslatableString(getKey(@"editor_header"), @"Editor");

        /// <summary>
        /// "Editor skin"
        /// </summary>
        public static LocalisableString EditorSkin => new TranslatableString(getKey(@"editor_skin"), @"Editor skin");

        /// <summary>
        /// "Skin shown exclusively inside the beatmap editor. Gameplay keeps using the regular skin."
        /// </summary>
        public static LocalisableString EditorSkinDescription => new TranslatableString(getKey(@"editor_skin_description"),
            @"Skin shown exclusively inside the beatmap editor. Gameplay keeps using the regular skin.");

        /// <summary>
        /// "Same as gameplay skin"
        /// </summary>
        public static LocalisableString SameAsGameplaySkin => new TranslatableString(getKey(@"same_as_gameplay_skin"), @"Same as gameplay skin");

        /// <summary>
        /// "Use editor skin in test mode"
        /// </summary>
        public static LocalisableString EditorSkinInTestMode => new TranslatableString(getKey(@"editor_skin_in_test_mode"), @"Use editor skin in test mode");

        /// <summary>
        /// "When test playing a beatmap from the editor, use the editor skin instead of the regular skin."
        /// </summary>
        public static LocalisableString EditorSkinInTestModeDescription => new TranslatableString(getKey(@"editor_skin_in_test_mode_description"),
            @"When test playing a beatmap from the editor, use the editor skin instead of the regular skin.");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
