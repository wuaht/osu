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

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
