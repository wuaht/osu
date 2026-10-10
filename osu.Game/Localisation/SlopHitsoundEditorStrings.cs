// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopHitsoundEditorStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopHitsoundEditor";

        /// <summary>
        /// "hitsound"
        /// </summary>
        public static LocalisableString HitsoundScreen => new TranslatableString(getKey(@"hitsound_screen"), @"hitsound");

        /// <summary>
        /// "Hitsound mode"
        /// </summary>
        public static LocalisableString HitsoundEditorMode => new TranslatableString(getKey(@"hitsound_editor_mode"), @"Hitsound mode");

        /// <summary>
        /// "Hitsounds"
        /// </summary>
        public static LocalisableString Hitsounds => new TranslatableString(getKey(@"hitsounds"), @"Hitsounds");

        /// <summary>
        /// "Hitnormal bank"
        /// </summary>
        public static LocalisableString HitnormalBank => new TranslatableString(getKey(@"hitnormal_bank"), @"Hitnormal bank");

        /// <summary>
        /// "Additions"
        /// </summary>
        public static LocalisableString Additions => new TranslatableString(getKey(@"additions"), @"Additions");

        /// <summary>
        /// "Addition bank"
        /// </summary>
        public static LocalisableString AdditionBank => new TranslatableString(getKey(@"addition_bank"), @"Addition bank");

        /// <summary>
        /// "Auto"
        /// </summary>
        public static LocalisableString Auto => new TranslatableString(getKey(@"auto"), @"Auto");

        /// <summary>
        /// "Volume"
        /// </summary>
        public static LocalisableString Volume => new TranslatableString(getKey(@"volume"), @"Volume");

        /// <summary>
        /// "Ramp volume"
        /// </summary>
        public static LocalisableString RampVolume => new TranslatableString(getKey(@"ramp_volume"), @"Ramp volume");

        /// <summary>
        /// "Custom sample index"
        /// </summary>
        public static LocalisableString CustomIndex => new TranslatableString(getKey(@"custom_index"), @"Custom sample index");

        /// <summary>
        /// "Plays the beatmap's own samples, e.g. normal-hitclap2.wav for index 2."
        /// </summary>
        public static LocalisableString CustomIndexHint => new TranslatableString(getKey(@"custom_index_hint"), @"Plays the beatmap's own samples, e.g. normal-hitclap2.wav for index 2.");

        /// <summary>
        /// "Skin (0)"
        /// </summary>
        public static LocalisableString CustomIndexSkin => new TranslatableString(getKey(@"custom_index_skin"), @"Skin (0)");

        /// <summary>
        /// "Beatmap (1)"
        /// </summary>
        public static LocalisableString CustomIndexBeatmap => new TranslatableString(getKey(@"custom_index_beatmap"), @"Beatmap (1)");

        /// <summary>
        /// "Clear additions"
        /// </summary>
        public static LocalisableString ClearAdditions => new TranslatableString(getKey(@"clear_additions"), @"Clear additions");

        /// <summary>
        /// "Nothing selected"
        /// </summary>
        public static LocalisableString NothingSelected => new TranslatableString(getKey(@"nothing_selected"), @"Nothing selected");

        /// <summary>
        /// "At playhead"
        /// </summary>
        public static LocalisableString EditingAtPlayhead => new TranslatableString(getKey(@"editing_at_playhead"), @"At playhead");

        /// <summary>
        /// "Hitsound difficulty"
        /// </summary>
        public static LocalisableString HitsoundDifficulty => new TranslatableString(getKey(@"hitsound_difficulty"), @"Hitsound difficulty");

        /// <summary>
        /// "Hitsound difficulty mode"
        /// </summary>
        public static LocalisableString HitsoundDifficultyMode => new TranslatableString(getKey(@"hitsound_difficulty_mode"), @"Hitsound difficulty mode");

        /// <summary>
        /// "HS mode"
        /// </summary>
        public static LocalisableString HitsoundDifficultyModeShort => new TranslatableString(getKey(@"hitsound_difficulty_mode_short"), @"HS mode");

        /// <summary>
        /// "Clicking empty space adds hitsounds, and removing a hitnormal deletes its objects. On for difficulties named like hitsound difficulties."
        /// </summary>
        public static LocalisableString HitsoundDifficultyModeHint => new TranslatableString(getKey(@"hitsound_difficulty_mode_hint"), @"Clicking empty space adds hitsounds, and removing a hitnormal deletes its objects. On for difficulties named like hitsound difficulties.");

        /// <summary>
        /// "Starting hitnormal bank"
        /// </summary>
        public static LocalisableString StartingBank => new TranslatableString(getKey(@"starting_bank"), @"Starting hitnormal bank");

        /// <summary>
        /// "Starting volume"
        /// </summary>
        public static LocalisableString StartingVolume => new TranslatableString(getKey(@"starting_volume"), @"Starting volume");

        /// <summary>
        /// "The hitnormal bank and volume which the new hitsound difficulty uses where this difficulty uses its most common bank and volume. Filled in with the most common ones of this difficulty."
        /// </summary>
        public static LocalisableString StartingHitsoundsHint => new TranslatableString(getKey(@"starting_hitsounds_hint"),
            @"The hitnormal bank and volume which the new hitsound difficulty uses where this difficulty uses its most common bank and volume. Filled in with the most common ones of this difficulty.");

        /// <summary>
        /// "Remove all objects"
        /// </summary>
        public static LocalisableString RemoveAllObjects => new TranslatableString(getKey(@"remove_all_objects"), @"Remove all objects");

        /// <summary>
        /// "The new hitsound difficulty starts empty, instead of with the hitsounds of all difficulties. The first hitsound placed uses the starting bank and volume."
        /// </summary>
        public static LocalisableString RemoveAllObjectsHint => new TranslatableString(getKey(@"remove_all_objects_hint"),
            @"The new hitsound difficulty starts empty, instead of with the hitsounds of all difficulties. The first hitsound placed uses the starting bank and volume.");

        /// <summary>
        /// "Create hitsound difficulty"
        /// </summary>
        public static LocalisableString CreateHitsoundDifficulty => new TranslatableString(getKey(@"create_hitsound_difficulty"), @"Create hitsound difficulty");

        /// <summary>
        /// "Copy hitsounds"
        /// </summary>
        public static LocalisableString CopyHitsounds => new TranslatableString(getKey(@"copy_hitsounds"), @"Copy hitsounds");

        /// <summary>
        /// "No other difficulties."
        /// </summary>
        public static LocalisableString NoOtherDifficulties => new TranslatableString(getKey(@"no_other_difficulties"), @"No other difficulties.");

        /// <summary>
        /// "Banks"
        /// </summary>
        public static LocalisableString CopySampleSets => new TranslatableString(getKey(@"copy_sample_sets"), @"Banks");

        /// <summary>
        /// "Additions"
        /// </summary>
        public static LocalisableString CopyAdditions => new TranslatableString(getKey(@"copy_additions"), @"Additions");

        /// <summary>
        /// "Volumes"
        /// </summary>
        public static LocalisableString CopyVolumes => new TranslatableString(getKey(@"copy_volumes"), @"Volumes");

        /// <summary>
        /// "Keep muted"
        /// </summary>
        public static LocalisableString PreserveMutedVolumes => new TranslatableString(getKey(@"preserve_muted_volumes"), @"Keep muted");

        /// <summary>
        /// "Hitsounds at the minimum volume keep their volume, e.g. muted slider ends."
        /// </summary>
        public static LocalisableString PreserveMutedVolumesHint => new TranslatableString(getKey(@"preserve_muted_volumes_hint"), @"Hitsounds at the minimum volume keep their volume, e.g. muted slider ends.");

        /// <summary>
        /// "Indices"
        /// </summary>
        public static LocalisableString CopyCustomIndices => new TranslatableString(getKey(@"copy_custom_indices"), @"Indices");

        /// <summary>
        /// "Slider bodies"
        /// </summary>
        public static LocalisableString CopySliderBodies => new TranslatableString(getKey(@"copy_slider_bodies"), @"Slider bodies");

        /// <summary>
        /// "Overwrite all"
        /// </summary>
        public static LocalisableString OverwriteUnmatched => new TranslatableString(getKey(@"overwrite_unmatched"), @"Overwrite all");

        /// <summary>
        /// "Clears the additions of hitsounds in the target which have no counterpart in the source."
        /// </summary>
        public static LocalisableString OverwriteUnmatchedHint => new TranslatableString(getKey(@"overwrite_unmatched_hint"), @"Clears the additions of hitsounds in the target which have no counterpart in the source.");

        /// <summary>
        /// "Mute slider ends"
        /// </summary>
        public static LocalisableString MuteUnmatchedSliderEnds => new TranslatableString(getKey(@"mute_unmatched_slider_ends"), @"Mute slider ends");

        /// <summary>
        /// "Mutes slider ends in the target which have no counterpart in the source."
        /// </summary>
        public static LocalisableString MuteUnmatchedSliderEndsHint => new TranslatableString(getKey(@"mute_unmatched_slider_ends_hint"), @"Mutes slider ends in the target which have no counterpart in the source.");

        /// <summary>
        /// "Leniency"
        /// </summary>
        public static LocalisableString Leniency => new TranslatableString(getKey(@"leniency"), @"Leniency");

        /// <summary>
        /// "How far apart hitsounds may be to count as the same."
        /// </summary>
        public static LocalisableString LeniencyHint => new TranslatableString(getKey(@"leniency_hint"), @"How far apart hitsounds may be to count as the same.");

        /// <summary>
        /// "Copy to selected"
        /// </summary>
        public static LocalisableString CopyToDifficulties => new TranslatableString(getKey(@"copy_to_difficulties"), @"Copy to selected");

        /// <summary>
        /// "Import from"
        /// </summary>
        public static LocalisableString ImportSource => new TranslatableString(getKey(@"import_source"), @"Import from");

        /// <summary>
        /// "Import"
        /// </summary>
        public static LocalisableString Import => new TranslatableString(getKey(@"import"), @"Import");

        /// <summary>
        /// "Copy hitsounds?"
        /// </summary>
        public static LocalisableString CopyConfirmationHeader => new TranslatableString(getKey(@"copy_confirmation_header"), @"Copy hitsounds?");

        /// <summary>
        /// "Hide unused lanes"
        /// </summary>
        public static LocalisableString HideUnusedLanes => new TranslatableString(getKey(@"hide_unused_lanes"), @"Hide unused lanes");

        /// <summary>
        /// "Left click a lane to add a hitsound, and a hitsound to select or deselect it. Right click to remove it. Drag to paint along a lane.
        /// Drag in the volume area to draw volumes (hold Shift for 1% steps).
        /// Shift/Ctrl + drag to select. Ctrl+C/Ctrl+V copy and paste hitsound patterns at the playhead.
        /// W/E/R toggle whistle/finish/clap, Shift+W/E/R set the hitnormal bank (normal/soft/drum), Alt+Q/W/E/R set the addition bank (auto/normal/soft/drum).
        /// Alt + scroll to zoom."
        /// </summary>
        public static LocalisableString ControlsDescription => new TranslatableString(getKey(@"controls_description"), @"Left click a lane to add a hitsound, and a hitsound to select or deselect it. Right click (or right drag) to remove. Drag to paint along a lane.
With a selection, clicking empty space deselects. Drag on empty space to select a rectangle of lanes. Ctrl/Shift + drag adds a rectangle to the selection (also where dragging would create hitsounds).
Ctrl + click toggles a hitsound in the selection, Shift + click selects all hitsounds up to it. Escape deselects.
Volume area: drag to draw (hold Ctrl for 1% steps), right drag to draw a straight line, Shift + drag to level to the starting volume, Alt + drag to reset to the volume most hitsounds use.
Ctrl+C/Ctrl+V copy and paste hitsound patterns at the playhead.
W/E/R toggle whistle/finish/clap, Shift+W/E/R set the hitnormal bank (normal/soft/drum), Alt+Q/W/E/R set the addition bank (auto/normal/soft/drum).
Alt + scroll to zoom. Ctrl + click a lane name to preview the skin's sample instead of the beatmap's.");

        /// <summary>
        /// "Normal"
        /// </summary>
        public static LocalisableString BankNormal => new TranslatableString(getKey(@"bank_normal"), @"Normal");

        /// <summary>
        /// "Soft"
        /// </summary>
        public static LocalisableString BankSoft => new TranslatableString(getKey(@"bank_soft"), @"Soft");

        /// <summary>
        /// "Drum"
        /// </summary>
        public static LocalisableString BankDrum => new TranslatableString(getKey(@"bank_drum"), @"Drum");

        /// <summary>
        /// "Hitnormal"
        /// </summary>
        public static LocalisableString SampleHitnormal => new TranslatableString(getKey(@"sample_hitnormal"), @"Hitnormal");

        /// <summary>
        /// "Whistle"
        /// </summary>
        public static LocalisableString SampleWhistle => new TranslatableString(getKey(@"sample_whistle"), @"Whistle");

        /// <summary>
        /// "Finish"
        /// </summary>
        public static LocalisableString SampleFinish => new TranslatableString(getKey(@"sample_finish"), @"Finish");

        /// <summary>
        /// "Clap"
        /// </summary>
        public static LocalisableString SampleClap => new TranslatableString(getKey(@"sample_clap"), @"Clap");

        /// <summary>
        /// "Object"
        /// </summary>
        public static LocalisableString KindObject => new TranslatableString(getKey(@"kind_object"), @"Object");

        /// <summary>
        /// "Head"
        /// </summary>
        public static LocalisableString KindHead => new TranslatableString(getKey(@"kind_head"), @"Head");

        /// <summary>
        /// "Repeat"
        /// </summary>
        public static LocalisableString KindRepeat => new TranslatableString(getKey(@"kind_repeat"), @"Repeat");

        /// <summary>
        /// "Tail"
        /// </summary>
        public static LocalisableString KindTail => new TranslatableString(getKey(@"kind_tail"), @"Tail");

        /// <summary>
        /// "Hold start"
        /// </summary>
        public static LocalisableString KindHoldStart => new TranslatableString(getKey(@"kind_hold_start"), @"Hold start");

        /// <summary>
        /// "End"
        /// </summary>
        public static LocalisableString KindEnd => new TranslatableString(getKey(@"kind_end"), @"End");

        /// <summary>
        /// "Click to preview (hold Ctrl for the skin's sample)"
        /// </summary>
        public static LocalisableString PlaySample => new TranslatableString(getKey(@"play_sample"), @"Click to preview (hold Ctrl for the skin's sample)");

        /// <summary>
        /// "Commonly used for: {0}"
        /// </summary>
        public static LocalisableString CommonlyUsedFor(string sounds) => new TranslatableString(getKey(@"commonly_used_for"), @"Commonly used for: {0}", sounds);

        /// <summary>
        /// "Which hitsounds are commonly used for which sounds:"
        /// </summary>
        public static LocalisableString SoundGuideHeader => new TranslatableString(getKey(@"sound_guide_header"), @"Which hitsounds are commonly used for which sounds:");

        /// <summary>
        /// "alternatively {0}"
        /// </summary>
        public static LocalisableString SoundGuideAlternatively(string lanes) => new TranslatableString(getKey(@"sound_guide_alternatively"), @"alternatively {0}", lanes);

        /// <summary>
        /// "Cymbal crash and splash"
        /// </summary>
        public static LocalisableString SoundCymbalCrash => new TranslatableString(getKey(@"sound_cymbal_crash"), @"Cymbal crash and splash");

        /// <summary>
        /// "Snare"
        /// </summary>
        public static LocalisableString SoundSnare => new TranslatableString(getKey(@"sound_snare"), @"Snare");

        /// <summary>
        /// "Kick"
        /// </summary>
        public static LocalisableString SoundKick => new TranslatableString(getKey(@"sound_kick"), @"Kick");

        /// <summary>
        /// "Melody"
        /// </summary>
        public static LocalisableString SoundMelody => new TranslatableString(getKey(@"sound_melody"), @"Melody");

        /// <summary>
        /// "Claps and snaps"
        /// </summary>
        public static LocalisableString SoundClaps => new TranslatableString(getKey(@"sound_claps"), @"Claps and snaps");

        /// <summary>
        /// "Hi-hat"
        /// </summary>
        public static LocalisableString SoundHiHat => new TranslatableString(getKey(@"sound_hi_hat"), @"Hi-hat");

        /// <summary>
        /// "Cymbal ride"
        /// </summary>
        public static LocalisableString SoundCymbalRide => new TranslatableString(getKey(@"sound_cymbal_ride"), @"Cymbal ride");

        /// <summary>
        /// "Tom"
        /// </summary>
        public static LocalisableString SoundTom => new TranslatableString(getKey(@"sound_tom"), @"Tom");

        /// <summary>
        /// "Rim"
        /// </summary>
        public static LocalisableString SoundRim => new TranslatableString(getKey(@"sound_rim"), @"Rim");

        /// <summary>
        /// "Drumsticks"
        /// </summary>
        public static LocalisableString SoundDrumsticks => new TranslatableString(getKey(@"sound_drumsticks"), @"Drumsticks");

        /// <summary>
        /// "Cowbell"
        /// </summary>
        public static LocalisableString SoundCowbell => new TranslatableString(getKey(@"sound_cowbell"), @"Cowbell");

        /// <summary>
        /// "Show the custom sample sets of this sample"
        /// </summary>
        public static LocalisableString ExpandCustomSampleSets => new TranslatableString(getKey(@"expand_custom_sample_sets"), @"Show the custom sample sets of this sample");

        /// <summary>
        /// "Hide the custom sample sets of this sample"
        /// </summary>
        public static LocalisableString CollapseCustomSampleSets => new TranslatableString(getKey(@"collapse_custom_sample_sets"), @"Hide the custom sample sets of this sample");

        /// <summary>
        /// "Rename"
        /// </summary>
        public static LocalisableString RenameLane => new TranslatableString(getKey(@"rename_lane"), @"Rename");

        /// <summary>
        /// "Reset name"
        /// </summary>
        public static LocalisableString ResetLaneName => new TranslatableString(getKey(@"reset_lane_name"), @"Reset name");

        /// <summary>
        /// "Double click to show or hide the custom sample sets."
        /// </summary>
        public static LocalisableString ExpandLaneHint => new TranslatableString(getKey(@"expand_lane_hint"), @"Double click to show or hide the custom sample sets.");

        /// <summary>
        /// "Double click or right click to rename."
        /// </summary>
        public static LocalisableString RenameLaneHint => new TranslatableString(getKey(@"rename_lane_hint"), @"Double click or right click to rename.");

        /// <summary>
        /// "Mute lane"
        /// </summary>
        public static LocalisableString MuteLane => new TranslatableString(getKey(@"mute_lane"), @"Mute lane");

        /// <summary>
        /// "Unmute lane"
        /// </summary>
        public static LocalisableString UnmuteLane => new TranslatableString(getKey(@"unmute_lane"), @"Unmute lane");

        /// <summary>
        /// "Zoom in"
        /// </summary>
        public static LocalisableString ZoomIn => new TranslatableString(getKey(@"zoom_in"), @"Zoom in");

        /// <summary>
        /// "Zoom out"
        /// </summary>
        public static LocalisableString ZoomOut => new TranslatableString(getKey(@"zoom_out"), @"Zoom out");

        /// <summary>
        /// "Volume: {0}%"
        /// </summary>
        public static LocalisableString VolumeValue(int volume) => new TranslatableString(getKey(@"volume_value"), @"Volume: {0}%", volume);

        /// <summary>
        /// "Index: {0}"
        /// </summary>
        public static LocalisableString CustomIndexValue(int index) => new TranslatableString(getKey(@"custom_index_value"), @"Index: {0}", index);

        /// <summary>
        /// "Custom #{0}"
        /// </summary>
        public static LocalisableString CustomIndexCustom(int index) => new TranslatableString(getKey(@"custom_index_custom"), @"Custom #{0}", index);

        /// <summary>
        /// "{0} selected"
        /// </summary>
        public static LocalisableString SelectedCount(int count) => new TranslatableString(getKey(@"selected_count"), @"{0} selected", count);

        /// <summary>
        /// "{0} hitsounds couldn't be pasted, as there are not enough hit objects at their time."
        /// </summary>
        public static LocalisableString PasteDropped(int count) => new TranslatableString(getKey(@"paste_dropped"), @"{0} hitsounds couldn't be pasted, as there are not enough hit objects at their time.", count);

        /// <summary>
        /// "Changed {0} hitsounds in {1} difficulties."
        /// </summary>
        public static LocalisableString CopyResult(int hitsounds, int difficulties) => new TranslatableString(getKey(@"copy_result"), @"Changed {0} hitsounds in {1} difficulties.", hitsounds, difficulties);

        /// <summary>
        /// "{0} hitsounds couldn't be copied, as there are not enough hit objects at their time."
        /// </summary>
        public static LocalisableString CopyDropped(int count) => new TranslatableString(getKey(@"copy_dropped"), @"{0} hitsounds couldn't be copied, as there are not enough hit objects at their time.", count);

        /// <summary>
        /// "Copying to {0} difficulties failed. See the log for details."
        /// </summary>
        public static LocalisableString CopyFailed(int count) => new TranslatableString(getKey(@"copy_failed"), @"Copying to {0} difficulties failed. See the log for details.", count);

        /// <summary>
        /// "Imported {0} hitsounds."
        /// </summary>
        public static LocalisableString ImportResult(int count) => new TranslatableString(getKey(@"import_result"), @"Imported {0} hitsounds.", count);

        /// <summary>
        /// "Created "{0}" with {1} hitsounds."
        /// </summary>
        public static LocalisableString HitsoundDifficultyCreated(string name, int count) => new TranslatableString(getKey(@"hitsound_difficulty_created"), @"Created ""{0}"" with {1} hitsounds.", name, count);

        /// <summary>
        /// "This overwrites the hitsounds of {0} difficulties and saves them. Your current difficulty is saved too."
        /// </summary>
        public static LocalisableString CopyConfirmationBody(int count) => new TranslatableString(getKey(@"copy_confirmation_body"), @"This overwrites the hitsounds of {0} difficulties and saves them. Your current difficulty is saved too.", count);

        /// <summary>
        /// "Importing the hitsounds of {0} failed. See the log for details."
        /// </summary>
        public static LocalisableString ImportFailed(string difficulty) => new TranslatableString(getKey(@"import_failed"), @"Importing the hitsounds of {0} failed. See the log for details.", difficulty);

        /// <summary>
        /// "Removes the additions of the hitsounds (Delete)."
        /// </summary>
        public static LocalisableString ClearAdditionsTooltip => new TranslatableString(getKey(@"clear_additions_tooltip"), @"Removes the additions of the hitsounds (Delete).");

        /// <summary>
        /// "Delete hitsounds"
        /// </summary>
        public static LocalisableString DeleteHitsounds => new TranslatableString(getKey(@"delete_hitsounds"), @"Delete hitsounds");

        /// <summary>
        /// "Deletes the hitsounds (Delete)."
        /// </summary>
        public static LocalisableString DeleteHitsoundsTooltip => new TranslatableString(getKey(@"delete_hitsounds_tooltip"), @"Deletes the hitsounds (Delete).");

        /// <summary>
        /// "Fades the volume from the first to the last selected hitsound."
        /// </summary>
        public static LocalisableString RampVolumeTooltip => new TranslatableString(getKey(@"ramp_volume_tooltip"), @"Fades the volume from the first to the last selected hitsound.");

        /// <summary>
        /// "Combines the hitsounds of all difficulties into a new difficulty."
        /// </summary>
        public static LocalisableString CreateHitsoundDifficultyTooltip => new TranslatableString(getKey(@"create_hitsound_difficulty_tooltip"), @"Combines the hitsounds of all difficulties into a new difficulty.");

        /// <summary>
        /// "Difficulties"
        /// </summary>
        public static LocalisableString Difficulties => new TranslatableString(getKey(@"difficulties"), @"Difficulties");

        /// <summary>
        /// "Copy"
        /// </summary>
        public static LocalisableString CopyWhat => new TranslatableString(getKey(@"copy_what"), @"Copy");

        /// <summary>
        /// "Options"
        /// </summary>
        public static LocalisableString CopyOptions => new TranslatableString(getKey(@"copy_options"), @"Options");

        /// <summary>
        /// "Copies the hitnormal banks."
        /// </summary>
        public static LocalisableString CopySampleSetsTooltip => new TranslatableString(getKey(@"copy_sample_sets_tooltip"), @"Copies the hitnormal banks.");

        /// <summary>
        /// "Copies the custom sample indices."
        /// </summary>
        public static LocalisableString CopyCustomIndicesTooltip => new TranslatableString(getKey(@"copy_custom_indices_tooltip"), @"Copies the custom sample indices.");

        /// <summary>
        /// "Copies the slide and whistle of sliders starting at the same time."
        /// </summary>
        public static LocalisableString CopySliderBodiesTooltip => new TranslatableString(getKey(@"copy_slider_bodies_tooltip"), @"Copies the slide and whistle of sliders starting at the same time.");

        /// <summary>
        /// "Saves the selected difficulties. This can't be undone."
        /// </summary>
        public static LocalisableString CopyToDifficultiesTooltip => new TranslatableString(getKey(@"copy_to_difficulties_tooltip"), @"Saves the selected difficulties. This can't be undone.");

        /// <summary>
        /// "Copies the hitsounds of the chosen difficulty onto this one. This can be undone."
        /// </summary>
        public static LocalisableString ImportTooltip => new TranslatableString(getKey(@"import_tooltip"), @"Copies the hitsounds of the chosen difficulty onto this one. This can be undone.");

        /// <summary>
        /// "Show all lanes"
        /// </summary>
        public static LocalisableString ShowAllLanes => new TranslatableString(getKey(@"show_all_lanes"), @"Show all lanes");

        /// <summary>
        /// "Expand sidebars"
        /// </summary>
        public static LocalisableString ExpandSidebars => new TranslatableString(getKey(@"expand_sidebars"), @"Expand sidebars");

        /// <summary>
        /// "None"
        /// </summary>
        public static LocalisableString None => new TranslatableString(getKey(@"none"), @"None");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
