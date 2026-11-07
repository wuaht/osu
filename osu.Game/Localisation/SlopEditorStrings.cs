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
        /// "Mute object ends"
        /// </summary>
        public static LocalisableString MuteObjectEnds => new TranslatableString(getKey(@"mute_object_ends"), @"Mute object ends");

        /// <summary>
        /// "Toggled muted ends for {0} object(s)"
        /// </summary>
        public static LocalisableString ToggledMutedEnds(int count) => new TranslatableString(getKey(@"toggled_muted_ends"), @"Toggled muted ends for {0} object(s)", count);

        /// <summary>
        /// "Increase slider velocity"
        /// </summary>
        public static LocalisableString IncreaseSliderVelocity => new TranslatableString(getKey(@"increase_slider_velocity"), @"Increase slider velocity");

        /// <summary>
        /// "Decrease slider velocity"
        /// </summary>
        public static LocalisableString DecreaseSliderVelocity => new TranslatableString(getKey(@"decrease_slider_velocity"), @"Decrease slider velocity");

        /// <summary>
        /// "Slider velocity"
        /// </summary>
        public static LocalisableString SliderVelocity => new TranslatableString(getKey(@"slider_velocity"), @"Slider velocity");

        /// <summary>
        /// "Toggle note lock"
        /// </summary>
        public static LocalisableString ToggleNoteLock => new TranslatableString(getKey(@"toggle_note_lock"), @"Toggle note lock");

        /// <summary>
        /// "Note lock"
        /// </summary>
        public static LocalisableString NoteLock => new TranslatableString(getKey(@"note_lock"), @"Note lock");

        /// <summary>
        /// "Copied hitsounds of the selected object"
        /// </summary>
        public static LocalisableString SampleNoteCopied => new TranslatableString(getKey(@"sample_note_copied"), @"Copied hitsounds of the selected object");

        /// <summary>
        /// "Pasted hitsounds"
        /// </summary>
        public static LocalisableString SampleNotePasted => new TranslatableString(getKey(@"sample_note_pasted"), @"Pasted hitsounds");

        /// <summary>
        /// "No hitsounds copied. Copy a single object while note lock is enabled."
        /// </summary>
        public static LocalisableString NoSampleNote => new TranslatableString(getKey(@"no_sample_note"), @"No hitsounds copied. Copy a single object while note lock is enabled.");

        /// <summary>
        /// "Increase bezier degree"
        /// </summary>
        public static LocalisableString IncreaseBezierDegree => new TranslatableString(getKey(@"increase_bezier_degree"), @"Increase bezier degree");

        /// <summary>
        /// "Toggle zoom out"
        /// </summary>
        public static LocalisableString ToggleZoomOut => new TranslatableString(getKey(@"toggle_zoom_out"), @"Toggle zoom out");

        /// <summary>
        /// "Zoom out"
        /// </summary>
        public static LocalisableString ZoomOut => new TranslatableString(getKey(@"zoom_out"), @"Zoom out");

        /// <summary>
        /// "Toggle grid display"
        /// </summary>
        public static LocalisableString ToggleGridDisplay => new TranslatableString(getKey(@"toggle_grid_display"), @"Toggle grid display");

        /// <summary>
        /// "Grid display"
        /// </summary>
        public static LocalisableString GridDisplay => new TranslatableString(getKey(@"grid_display"), @"Grid display");

        /// <summary>
        /// "Rotate selected sliders randomly"
        /// </summary>
        public static LocalisableString RandomSliderRotation => new TranslatableString(getKey(@"random_slider_rotation"), @"Rotate selected sliders randomly");

        /// <summary>
        /// "Randomise selected slider shape"
        /// </summary>
        public static LocalisableString RandomSliderShape => new TranslatableString(getKey(@"random_slider_shape"), @"Randomise selected slider shape");

        /// <summary>
        /// "Switch to perfect curve"
        /// </summary>
        public static LocalisableString SwitchToPerfectCurve => new TranslatableString(getKey(@"switch_to_perfect_curve"), @"Switch to perfect curve");

        /// <summary>
        /// "Switch to bezier curve"
        /// </summary>
        public static LocalisableString SwitchToBezierCurve => new TranslatableString(getKey(@"switch_to_bezier_curve"), @"Switch to bezier curve");

        /// <summary>
        /// "Switch to linear curve"
        /// </summary>
        public static LocalisableString SwitchToLinearCurve => new TranslatableString(getKey(@"switch_to_linear_curve"), @"Switch to linear curve");

        /// <summary>
        /// "Switch to catmull curve"
        /// </summary>
        public static LocalisableString SwitchToCatmullCurve => new TranslatableString(getKey(@"switch_to_catmull_curve"), @"Switch to catmull curve");

        /// <summary>
        /// "Slider curve"
        /// </summary>
        public static LocalisableString SliderCurve => new TranslatableString(getKey(@"slider_curve"), @"Slider curve");

        /// <summary>
        /// "Automatic"
        /// </summary>
        public static LocalisableString AutomaticCurve => new TranslatableString(getKey(@"automatic_curve"), @"Automatic");

        /// <summary>
        /// "Continue with same distance snap"
        /// </summary>
        public static LocalisableString ContinueDistanceSnap => new TranslatableString(getKey(@"continue_distance_snap"), @"Continue with same distance snap");

        /// <summary>
        /// "Toggle rotation hotkey origin"
        /// </summary>
        public static LocalisableString ToggleRotationHotkeyOrigin => new TranslatableString(getKey(@"toggle_rotation_hotkey_origin"), @"Toggle rotation hotkey origin");

        /// <summary>
        /// "Zoom timeline in"
        /// </summary>
        public static LocalisableString TimelineZoomIn => new TranslatableString(getKey(@"timeline_zoom_in"), @"Zoom timeline in");

        /// <summary>
        /// "Zoom timeline out"
        /// </summary>
        public static LocalisableString TimelineZoomOut => new TranslatableString(getKey(@"timeline_zoom_out"), @"Zoom timeline out");

        /// <summary>
        /// "Rotation hotkey origin"
        /// </summary>
        public static LocalisableString RotationHotkeyOrigin => new TranslatableString(getKey(@"rotation_hotkey_origin"), @"Rotation hotkey origin");

        /// <summary>
        /// "Selection"
        /// </summary>
        public static LocalisableString SelectionOrigin => new TranslatableString(getKey(@"selection_origin"), @"Selection");

        /// <summary>
        /// "Playfield"
        /// </summary>
        public static LocalisableString PlayfieldOrigin => new TranslatableString(getKey(@"playfield_origin"), @"Playfield");

        /// <summary>
        /// "Toggle angled flip control"
        /// </summary>
        public static LocalisableString ToggleAngledFlipControl => new TranslatableString(getKey(@"toggle_angled_flip_control"), @"Toggle angled flip control");

        /// <summary>
        /// "Rotate by custom angle clockwise"
        /// </summary>
        public static LocalisableString RotateByCustomAngleClockwise => new TranslatableString(getKey(@"rotate_by_custom_angle_clockwise"), @"Rotate by custom angle clockwise");

        /// <summary>
        /// "Rotate by custom angle counterclockwise"
        /// </summary>
        public static LocalisableString RotateByCustomAngleCounterclockwise => new TranslatableString(getKey(@"rotate_by_custom_angle_counterclockwise"), @"Rotate by custom angle counterclockwise");

        /// <summary>
        /// "Line angle (degrees)"
        /// </summary>
        public static LocalisableString AngledFlipLineAngle => new TranslatableString(getKey(@"angled_flip_line_angle"), @"Line angle (degrees)");

        /// <summary>
        /// "The angle of the line to mirror the selection over. Positive angles are clockwise from the horizontal."
        /// </summary>
        public static LocalisableString AngledFlipLineAngleDescription => new TranslatableString(getKey(@"angled_flip_line_angle_description"),
            @"The angle of the line to mirror the selection over. Positive angles are clockwise from the horizontal.");

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
        /// "Use previous slider velocity"
        /// </summary>
        public static LocalisableString UsePreviousSliderVelocity => new TranslatableString(getKey(@"use_previous_slider_velocity"), @"Use previous slider velocity");

        /// <summary>
        /// "Copy selection in .osu format"
        /// </summary>
        public static LocalisableString CopyAsOsuFormat => new TranslatableString(getKey(@"copy_as_osu_format"), @"Copy selection in .osu format");

        /// <summary>
        /// "Set to previous slider velocity"
        /// </summary>
        public static LocalisableString SetToPreviousSliderVelocity => new TranslatableString(getKey(@"set_to_previous_slider_velocity"), @"Set to previous slider velocity");

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
        /// "Custom slider colours"
        /// </summary>
        public static LocalisableString CustomSliderColours => new TranslatableString(getKey(@"custom_slider_colours"), @"Custom slider colours");

        /// <summary>
        /// "Overrides the slider track and border colours of the skin for this beatmap."
        /// </summary>
        public static LocalisableString CustomSliderColoursDescription => new TranslatableString(getKey(@"custom_slider_colours_description"),
            @"Overrides the slider track and border colours of the skin for this beatmap.");

        /// <summary>
        /// "Slider track"
        /// </summary>
        public static LocalisableString SliderTrackColour => new TranslatableString(getKey(@"slider_track_colour"), @"Slider track");

        /// <summary>
        /// "Slider border"
        /// </summary>
        public static LocalisableString SliderBorderColour => new TranslatableString(getKey(@"slider_border_colour"), @"Slider border");

        /// <summary>
        /// "Lock colour"
        /// </summary>
        public static LocalisableString LockColour => new TranslatableString(getKey(@"lock_colour"), @"Lock colour");

        /// <summary>
        /// "Unlock colour"
        /// </summary>
        public static LocalisableString UnlockColour => new TranslatableString(getKey(@"unlock_colour"), @"Unlock colour");

        /// <summary>
        /// "Lock all colours"
        /// </summary>
        public static LocalisableString LockAllColours => new TranslatableString(getKey(@"lock_all_colours"), @"Lock all colours");

        /// <summary>
        /// "Unlock all colours"
        /// </summary>
        public static LocalisableString UnlockAllColours => new TranslatableString(getKey(@"unlock_all_colours"), @"Unlock all colours");

        /// <summary>
        /// "Random combo colours"
        /// </summary>
        public static LocalisableString RandomComboColours => new TranslatableString(getKey(@"random_combo_colours"), @"Random combo colours");

        /// <summary>
        /// "Right-click a combo colour to lock it, so that it is kept when getting random colours from the background."
        /// </summary>
        public static LocalisableString RandomComboColoursDescription => new TranslatableString(getKey(@"random_combo_colours_description"),
            @"Right-click a combo colour to lock it, so that it is kept when getting random colours from the background.");

        /// <summary>
        /// "Get random colours"
        /// </summary>
        public static LocalisableString GetRandomColours => new TranslatableString(getKey(@"get_random_colours"), @"Get random colours");

        /// <summary>
        /// "Align background with playfield"
        /// </summary>
        public static LocalisableString AlignBackground => new TranslatableString(getKey(@"align_background"), @"Align background with playfield");

        /// <summary>
        /// "Use as template for new set"
        /// </summary>
        public static LocalisableString UseAsTemplateForNewSet => new TranslatableString(getKey(@"use_as_template_for_new_set"), @"Use as template for new set");

        /// <summary>
        /// "Failed to create a new beatmap set from this difficulty."
        /// </summary>
        public static LocalisableString UseAsTemplateFailed => new TranslatableString(getKey(@"use_as_template_failed"), @"Failed to create a new beatmap set from this difficulty.");

        /// <summary>
        /// "Equalize slider velocity"
        /// </summary>
        public static LocalisableString EqualizeSliderVelocity => new TranslatableString(getKey(@"equalize_slider_velocity"), @"Equalize slider velocity");

        /// <summary>
        /// "Sets the slider velocity of objects in this section so that they keep the speed from before the BPM change. Useful for lower difficulties."
        /// </summary>
        public static LocalisableString EqualizeSliderVelocityDescription => new TranslatableString(getKey(@"equalize_slider_velocity_description"),
            @"Sets the slider velocity of objects in this section so that they keep the speed from before the BPM change. Useful for lower difficulties.");

        /// <summary>
        /// "Property transformer"
        /// </summary>
        public static LocalisableString PropertyTransformer => new TranslatableString(getKey(@"property_transformer"), @"Property transformer");

        /// <summary>
        /// "Property transformer..."
        /// </summary>
        public static LocalisableString PropertyTransformerEllipsis => new TranslatableString(getKey(@"property_transformer_ellipsis"), @"Property transformer...");

        /// <summary>
        /// "BPM"
        /// </summary>
        public static LocalisableString PropertyTransformerBpm => new TranslatableString(getKey(@"property_transformer_bpm"), @"BPM");

        /// <summary>
        /// "Slider velocity"
        /// </summary>
        public static LocalisableString PropertyTransformerSliderVelocity => new TranslatableString(getKey(@"property_transformer_slider_velocity"), @"Slider velocity");

        /// <summary>
        /// "Volume"
        /// </summary>
        public static LocalisableString PropertyTransformerVolume => new TranslatableString(getKey(@"property_transformer_volume"), @"Volume");

        /// <summary>
        /// "Apply"
        /// </summary>
        public static LocalisableString PropertyTransformerApply => new TranslatableString(getKey(@"property_transformer_apply"), @"Apply");

        /// <summary>
        /// "Test in other mode"
        /// </summary>
        public static LocalisableString TestInOtherMode => new TranslatableString(getKey(@"test_in_other_mode"), @"Test in other mode");

        /// <summary>
        /// "Slider generator"
        /// </summary>
        public static LocalisableString SliderGenerator => new TranslatableString(getKey(@"slider_generator"), @"Slider generator");

        /// <summary>
        /// "Slider"
        /// </summary>
        public static LocalisableString SliderGeneratorButton => new TranslatableString(getKey(@"slider_generator_button"), @"Slider");

        /// <summary>
        /// "Segments"
        /// </summary>
        public static LocalisableString SliderGeneratorSegments => new TranslatableString(getKey(@"slider_generator_segments"), @"Segments");

        /// <summary>
        /// "Arc chance"
        /// </summary>
        public static LocalisableString SliderGeneratorArcChance => new TranslatableString(getKey(@"slider_generator_arc_chance"), @"Arc chance");

        /// <summary>
        /// "Length (beats)"
        /// </summary>
        public static LocalisableString SliderGeneratorLength => new TranslatableString(getKey(@"slider_generator_length"), @"Length (beats)");

        /// <summary>
        /// "Rotation"
        /// </summary>
        public static LocalisableString SliderGeneratorRotation => new TranslatableString(getKey(@"slider_generator_rotation"), @"Rotation");

        /// <summary>
        /// "Seed"
        /// </summary>
        public static LocalisableString SliderGeneratorSeed => new TranslatableString(getKey(@"slider_generator_seed"), @"Seed");

        /// <summary>
        /// "New seed"
        /// </summary>
        public static LocalisableString SliderGeneratorNewSeed => new TranslatableString(getKey(@"slider_generator_new_seed"), @"New seed");

        /// <summary>
        /// "Create"
        /// </summary>
        public static LocalisableString SliderGeneratorCreate => new TranslatableString(getKey(@"slider_generator_create"), @"Create");

        /// <summary>
        /// "Preserve beat spacing"
        /// </summary>
        public static LocalisableString TimingCopierPreserveBeatSpacing => new TranslatableString(getKey(@"timing_copier_preserve_beat_spacing"), @"Preserve beat spacing");

        /// <summary>
        /// "Resnap"
        /// </summary>
        public static LocalisableString TimingCopierResnap => new TranslatableString(getKey(@"timing_copier_resnap"), @"Resnap");

        /// <summary>
        /// "Keep objects fixed"
        /// </summary>
        public static LocalisableString TimingCopierKeepObjectsFixed => new TranslatableString(getKey(@"timing_copier_keep_objects_fixed"), @"Keep objects fixed");

        /// <summary>
        /// "Timing (replaces the timing of other difficulties)"
        /// </summary>
        public static LocalisableString SyncTimingOptionTiming => new TranslatableString(getKey(@"sync_timing_option_timing"), @"Timing (replaces the timing of other difficulties)");

        /// <summary>
        /// "Object handling"
        /// </summary>
        public static LocalisableString SyncTimingObjectHandling => new TranslatableString(getKey(@"sync_timing_object_handling"), @"Object handling");

        /// <summary>
        /// "Beat divisors"
        /// </summary>
        public static LocalisableString BeatDivisors => new TranslatableString(getKey(@"beat_divisors"), @"Beat divisors");

        /// <summary>
        /// "Comma-separated fractions of a beat. Objects snap to the closest tick of any of them."
        /// </summary>
        public static LocalisableString BeatDivisorsHint => new TranslatableString(getKey(@"beat_divisors_hint"), @"Comma-separated fractions of a beat. Objects snap to the closest tick of any of them.");

        /// <summary>
        /// "Clean up map"
        /// </summary>
        public static LocalisableString CleanUpMap => new TranslatableString(getKey(@"clean_up_map"), @"Clean up map");

        /// <summary>
        /// "This tidies up the current difficulty. Redundant control points are always removed."
        /// </summary>
        public static LocalisableString CleanUpMapBody => new TranslatableString(getKey(@"clean_up_map_body"), @"This tidies up the current difficulty. Redundant control points are always removed.");

        /// <summary>
        /// "Resnap objects"
        /// </summary>
        public static LocalisableString CleanUpResnapObjects => new TranslatableString(getKey(@"clean_up_resnap_objects"), @"Resnap objects");

        /// <summary>
        /// "Resnap bookmarks"
        /// </summary>
        public static LocalisableString CleanUpResnapBookmarks => new TranslatableString(getKey(@"clean_up_resnap_bookmarks"), @"Resnap bookmarks");

        /// <summary>
        /// "Remove hitsounds"
        /// </summary>
        public static LocalisableString CleanUpRemoveHitsounds => new TranslatableString(getKey(@"clean_up_remove_hitsounds"), @"Remove hitsounds");

        /// <summary>
        /// "Remove muting"
        /// </summary>
        public static LocalisableString CleanUpRemoveMuting => new TranslatableString(getKey(@"clean_up_remove_muting"), @"Remove muting");

        /// <summary>
        /// "Mute unclickable hitsounds"
        /// </summary>
        public static LocalisableString CleanUpMuteUnclickableHitsounds => new TranslatableString(getKey(@"clean_up_mute_unclickable_hitsounds"), @"Mute unclickable hitsounds");

        /// <summary>
        /// "Mutes slider repeats, slider tails and spinner ends. Ignored when removing muting."
        /// </summary>
        public static LocalisableString CleanUpMuteUnclickableHitsoundsHint => new TranslatableString(getKey(@"clean_up_mute_unclickable_hitsounds_hint"), @"Mutes slider repeats, slider tails and spinner ends. Ignored when removing muting.");

        /// <summary>
        /// "Show the timing point list and settings"
        /// </summary>
        public static LocalisableString ShowTimingPointPanels => new TranslatableString(getKey(@"show_timing_point_panels"), @"Show the timing point list and settings");

        /// <summary>
        /// "Hide the timing point list and settings"
        /// </summary>
        public static LocalisableString HideTimingPointPanels => new TranslatableString(getKey(@"hide_timing_point_panels"), @"Hide the timing point list and settings");

        /// <summary>
        /// "Remove redundant timing points: the ones which lie on a bar line of the previous timing point with the same BPM and meter, moving no beat by more than {0} ms. Ctrl+scroll to change the leeway."
        /// </summary>
        public static LocalisableString RemoveRedundantTimingPoints(double leeway) => new TranslatableString(getKey(@"remove_redundant_timing_points"),
            @"Remove redundant timing points: the ones which lie on a bar line of the previous timing point with the same BPM and meter, moving no beat by more than {0} ms. Ctrl+scroll to change the leeway.", leeway);

        /// <summary>
        /// The controls of the bar view of the timing screen, and how to time a song with a varying tempo.
        /// </summary>
        public static LocalisableString TimingGuide => new TranslatableString(getKey(@"timing_guide"), @"Every row is one bar, so the beats of all bars line up. The ends of the neighbouring bars are shown darkened at the sides.
Click: place a timing point such that the audio under the cursor lies on the closest beat. The BPM of the previous timing point is recalculated to end on a whole beat.
Drag: grab the audio under the cursor and drop it onto the beat line it belongs to. The BPMs of the timing points around it are recalculated.
Drag a timing point: move it onto another beat, or onto a division of a beat with the beat divisor. Ctrl + drag it to only change its offset.
Ctrl + scroll: nudge the timing point of the bar under the cursor by 1 ms (Ctrl + Alt: 10 ms).
Hold right click: listen to the audio at the cursor. Click a timing point or the number of its bar to select it in the list. Ctrl + right click: remove a timing point, or jump the playback there. Scroll to move through the bars. Ctrl + Shift + scroll: zoom into the bars, Shift + scroll: move sideways while zoomed in.
With the onsets waveform style, clicks and drags snap to the exact millisecond of the onset under the cursor. Hold Shift to not snap.

How to time a song with a varying tempo:
1. Place the first timing point on a clear downbeat, e.g. the first kick or snare of the song, and check it by ear with the metronome.
2. Play or scroll on until the onsets start drifting away from the beat lines.
3. Drag a clear onset just before the drift onto its beat line. This places a timing point and recalculates the tempo before it.
4. Repeat until the onsets of all bars form straight columns.
5. Open the timing point list and settings (the button on the right) to type in exact values or change the meter.
6. Clean up with the broom button, which removes the timing points that continue the previous one unchanged.");

        /// <summary>
        /// "Media reencoder"
        /// </summary>
        public static LocalisableString MediaReencoder => new TranslatableString(getKey(@"media_reencoder"), @"Media reencoder");

        /// <summary>
        /// "Converts the video or audio track into an osu!-compatible format and replaces it. Requires ffmpeg on PATH."
        /// </summary>
        public static LocalisableString MediaReencoderBody => new TranslatableString(getKey(@"media_reencoder_body"),
            @"Converts the video or audio track into an osu!-compatible format and replaces it. Requires ffmpeg on PATH.");

        /// <summary>
        /// "Reencode..."
        /// </summary>
        public static LocalisableString MediaReencoderButton => new TranslatableString(getKey(@"media_reencoder_button"), @"Reencode...");

        /// <summary>
        /// "Media type"
        /// </summary>
        public static LocalisableString MediaReencoderMediaType => new TranslatableString(getKey(@"media_reencoder_media_type"), @"Media type");

        /// <summary>
        /// "Encoder"
        /// </summary>
        public static LocalisableString MediaReencoderEncoder => new TranslatableString(getKey(@"media_reencoder_encoder"), @"Encoder");

        /// <summary>
        /// "Resize"
        /// </summary>
        public static LocalisableString MediaReencoderResize => new TranslatableString(getKey(@"media_reencoder_resize"), @"Resize");

        /// <summary>
        /// "Quality"
        /// </summary>
        public static LocalisableString MediaReencoderQuality => new TranslatableString(getKey(@"media_reencoder_quality"), @"Quality");

        /// <summary>
        /// "Lower is higher quality."
        /// </summary>
        public static LocalisableString MediaReencoderQualityHint => new TranslatableString(getKey(@"media_reencoder_quality_hint"), @"Lower is higher quality.");

        /// <summary>
        /// "Convert"
        /// </summary>
        public static LocalisableString MediaReencoderConvert => new TranslatableString(getKey(@"media_reencoder_convert"), @"Convert");

        /// <summary>
        /// "Reencoding {0}..."
        /// </summary>
        public static LocalisableString MediaReencoderProgress(string filename) => new TranslatableString(getKey(@"media_reencoder_progress"), @"Reencoding {0}...", filename);

        /// <summary>
        /// "Reencoded {0}."
        /// </summary>
        public static LocalisableString MediaReencoderComplete(string filename) => new TranslatableString(getKey(@"media_reencoder_complete"), @"Reencoded {0}.", filename);

        /// <summary>
        /// "Reencoding requires ffmpeg. Install it by running "{0}" in a terminal, then restart the game. Click here to open the ffmpeg download page."
        /// </summary>
        public static LocalisableString MediaReencoderFFmpegRequired(string installCommand) => new TranslatableString(getKey(@"media_reencoder_ffmpeg_required"),
            @"Reencoding requires ffmpeg. Install it by running ""{0}"" in a terminal, then restart the game. Click here to open the ffmpeg download page.", installCommand);

        /// <summary>
        /// "Pattern gallery"
        /// </summary>
        public static LocalisableString PatternGallery => new TranslatableString(getKey(@"pattern_gallery"), @"Pattern gallery");

        /// <summary>
        /// "Save selection as pattern"
        /// </summary>
        public static LocalisableString SavePattern => new TranslatableString(getKey(@"save_pattern"), @"Save selection as pattern");

        /// <summary>
        /// "Insert most recent pattern"
        /// </summary>
        public static LocalisableString InsertRecentPattern => new TranslatableString(getKey(@"insert_recent_pattern"), @"Insert most recent pattern");

        /// <summary>
        /// "Pattern name"
        /// </summary>
        public static LocalisableString PatternName => new TranslatableString(getKey(@"pattern_name"), @"Pattern name");

        /// <summary>
        /// "Search (e.g. jump c&gt;3 b&lt;=4)"
        /// </summary>
        public static LocalisableString PatternSearch => new TranslatableString(getKey(@"pattern_search"), @"Search (e.g. jump c>3 b<=4)");

        /// <summary>
        /// "Save selection"
        /// </summary>
        public static LocalisableString PatternSaveSelection => new TranslatableString(getKey(@"pattern_save_selection"), @"Save selection");

        /// <summary>
        /// "Mode"
        /// </summary>
        public static LocalisableString PatternModeToggle => new TranslatableString(getKey(@"pattern_mode_toggle"), @"Mode");

        /// <summary>
        /// "Scale"
        /// </summary>
        public static LocalisableString PatternScaleToggle => new TranslatableString(getKey(@"pattern_scale_toggle"), @"Scale");

        /// <summary>
        /// "Snap"
        /// </summary>
        public static LocalisableString PatternSnapToggle => new TranslatableString(getKey(@"pattern_snap_toggle"), @"Snap");

        /// <summary>
        /// "SV"
        /// </summary>
        public static LocalisableString PatternSliderVelocityToggle => new TranslatableString(getKey(@"pattern_sv_toggle"), @"SV");

        /// <summary>
        /// "Show only patterns of the current mode."
        /// </summary>
        public static LocalisableString PatternCurrentModeOnly => new TranslatableString(getKey(@"pattern_current_mode_only"), @"Show only patterns of the current mode.");

        /// <summary>
        /// "Rescale the pattern to the BPM it is inserted at."
        /// </summary>
        public static LocalisableString PatternScaleToTiming => new TranslatableString(getKey(@"pattern_scale_to_timing"), @"Rescale the pattern to the BPM it is inserted at.");

        /// <summary>
        /// "Snap inserted objects to the current beat divisor."
        /// </summary>
        public static LocalisableString PatternSnapToDivisor => new TranslatableString(getKey(@"pattern_snap_to_divisor"), @"Snap inserted objects to the current beat divisor.");

        /// <summary>
        /// "Keep the slider lengths the pattern was saved with."
        /// </summary>
        public static LocalisableString PatternPreserveSliderVelocity => new TranslatableString(getKey(@"pattern_preserve_slider_velocity"), @"Keep the slider lengths the pattern was saved with.");

        /// <summary>
        /// "import from Mapping Tools"
        /// </summary>
        public static LocalisableString PatternImportMappingTools => new TranslatableString(getKey(@"pattern_import_mapping_tools"), @"import from Mapping Tools");

        /// <summary>
        /// "Insert"
        /// </summary>
        public static LocalisableString PatternInsert => new TranslatableString(getKey(@"pattern_insert"), @"Insert");

        /// <summary>
        /// "Double-click to insert at the current time"
        /// </summary>
        public static LocalisableString PatternInsertTooltip => new TranslatableString(getKey(@"pattern_insert_tooltip"), @"Double-click to insert at the current time");

        /// <summary>
        /// "Overwrite"
        /// </summary>
        public static LocalisableString PatternOverwrite => new TranslatableString(getKey(@"pattern_overwrite"), @"Overwrite");

        /// <summary>
        /// "Delete"
        /// </summary>
        public static LocalisableString PatternDelete => new TranslatableString(getKey(@"pattern_delete"), @"Delete");

        /// <summary>
        /// "undo"
        /// </summary>
        public static LocalisableString PatternUndo => new TranslatableString(getKey(@"pattern_undo"), @"undo");

        /// <summary>
        /// "Select some objects to save them as a pattern."
        /// </summary>
        public static LocalisableString PatternNothingSelected => new TranslatableString(getKey(@"pattern_nothing_selected"), @"Select some objects to save them as a pattern.");

        /// <summary>
        /// "Patterns can only be saved in the four built-in modes."
        /// </summary>
        public static LocalisableString PatternUnsupportedRuleset => new TranslatableString(getKey(@"pattern_unsupported_ruleset"), @"Patterns can only be saved in the four built-in modes.");

        /// <summary>
        /// "This pattern could not be inserted into this mode."
        /// </summary>
        public static LocalisableString PatternOpenFailed => new TranslatableString(getKey(@"pattern_open_failed"), @"This pattern could not be inserted into this mode.");

        /// <summary>
        /// "No patterns yet. Select objects and save them."
        /// </summary>
        public static LocalisableString PatternEmpty => new TranslatableString(getKey(@"pattern_empty"), @"No patterns yet. Select objects and save them.");

        /// <summary>
        /// "{0} of {1} patterns"
        /// </summary>
        public static LocalisableString PatternCount(int shown, int total) => new TranslatableString(getKey(@"pattern_count"), @"{0} of {1} patterns", shown, total);

        /// <summary>
        /// "Imported {0} patterns."
        /// </summary>
        public static LocalisableString PatternsImported(int count) => new TranslatableString(getKey(@"patterns_imported"), @"Imported {0} patterns.", count);

        /// <summary>
        /// "Deleted “{0}”."
        /// </summary>
        public static LocalisableString PatternDeleted(string name) => new TranslatableString(getKey(@"pattern_deleted"), @"Deleted “{0}”.", name);

        /// <summary>
        /// "{0} beats at {1} bpm · cs {2} · used {3}x"
        /// </summary>
        public static LocalisableString PatternUsage(string beats, string bpm, string circleSize, int uses) => new TranslatableString(getKey(@"pattern_usage"), @"{0} beats at {1} bpm · cs {2} · used {3}x", beats, bpm, circleSize, uses);

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
