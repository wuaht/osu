// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopSongSelectStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopSongSelect";

        /// <summary>
        /// "Refresh mappers"
        /// </summary>
        public static LocalisableString RefreshMappers => new TranslatableString(getKey(@"refresh_mappers"), @"Refresh mappers");

        /// <summary>
        /// "beatmap editor"
        /// </summary>
        public static LocalisableString EditorTitle => new TranslatableString(getKey(@"editor_title"), @"beatmap editor");

        /// <summary>
        /// "select a beatmap to edit"
        /// </summary>
        public static LocalisableString EditorSubtitle => new TranslatableString(getKey(@"editor_subtitle"), @"select a beatmap to edit");

        /// <summary>
        /// "Edit mode"
        /// </summary>
        public static LocalisableString EditorDetailsHeader => new TranslatableString(getKey(@"editor_details_header"), @"Edit mode");

        /// <summary>
        /// "Clicking a beatmap opens it in the editor"
        /// </summary>
        public static LocalisableString EditorDetailsHint => new TranslatableString(getKey(@"editor_details_hint"), @"Clicking a beatmap opens it in the editor");

        /// <summary>
        /// "New beatmap"
        /// </summary>
        public static LocalisableString NewBeatmap => new TranslatableString(getKey(@"new_beatmap"), @"New beatmap");

        /// <summary>
        /// "Mappers"
        /// </summary>
        public static LocalisableString Mappers => new TranslatableString(getKey(@"mappers"), @"Mappers");

        /// <summary>
        /// "Added locally"
        /// </summary>
        public static LocalisableString AddedLocally => new TranslatableString(getKey(@"added_locally"), @"Added locally");

        /// <summary>
        /// "Last local update"
        /// </summary>
        public static LocalisableString LastLocalUpdate => new TranslatableString(getKey(@"last_local_update"), @"Last local update");

        /// <summary>
        /// "Last online update"
        /// </summary>
        public static LocalisableString LastOnlineUpdate => new TranslatableString(getKey(@"last_online_update"), @"Last online update");

        /// <summary>
        /// "Beatmap / set ID"
        /// </summary>
        public static LocalisableString OnlineIds => new TranslatableString(getKey(@"beatmap_set_ids"), @"Beatmap / set ID");

        /// <summary>
        /// "{0} / {1}"
        /// </summary>
        public static LocalisableString OnlineIdsValue(int beatmapId, int beatmapSetId) => new TranslatableString(getKey(@"beatmap_set_ids_value"), @"{0} / {1}", beatmapId, beatmapSetId);

        /// <summary>
        /// "Copy beatmap ID"
        /// </summary>
        public static LocalisableString CopyBeatmapId => new TranslatableString(getKey(@"copy_beatmap_id"), @"Copy beatmap ID");

        /// <summary>
        /// "Copy beatmap set ID"
        /// </summary>
        public static LocalisableString CopyBeatmapSetId => new TranslatableString(getKey(@"copy_beatmap_set_id"), @"Copy beatmap set ID");

        /// <summary>
        /// "Right click to set the current mods as the default for this ruleset. Hold Ctrl to also deselect the default mods."
        /// </summary>
        public static LocalisableString DeselectAllModsTooltip => new TranslatableString(getKey(@"deselect_all_mods_tooltip"),
            @"Right click to set the current mods as the default for this ruleset. Hold Ctrl to also deselect the default mods.");

        /// <summary>
        /// "Set {1} as the default mods for {0}"
        /// </summary>
        public static LocalisableString DefaultModsSet(string ruleset, string mods) => new TranslatableString(getKey(@"default_mods_set"), @"Set {1} as the default mods for {0}", ruleset, mods);

        /// <summary>
        /// "Cleared the default mods for {0}"
        /// </summary>
        public static LocalisableString DefaultModsCleared(string ruleset) => new TranslatableString(getKey(@"default_mods_cleared"), @"Cleared the default mods for {0}", ruleset);

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
