// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopSettingsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopSettings";

        /// <summary>
        /// "ai slop"
        /// </summary>
        public static LocalisableString SlopSectionHeader => new TranslatableString(getKey(@"slop_section_header"), @"ai slop");

        /// <summary>
        /// "Graphics"
        /// </summary>
        public static LocalisableString GraphicsHeader => new TranslatableString(getKey(@"graphics_header"), @"Graphics");

        /// <summary>
        /// "Frosted sliders"
        /// </summary>
        public static LocalisableString FrostedSliders => new TranslatableString(getKey(@"frosted_sliders"), @"Frosted sliders");

        /// <summary>
        /// "Slider bodies of legacy and argon skins blur the content behind them, like frosted glass. Hit circles of legacy skins become slightly translucent to match."
        /// </summary>
        public static LocalisableString FrostedSlidersDescription => new TranslatableString(getKey(@"frosted_sliders_description"),
            @"Slider bodies of legacy and argon skins blur the content behind them, like frosted glass. Hit circles of legacy skins become slightly translucent to match.");

        /// <summary>
        /// "Frostiness"
        /// </summary>
        public static LocalisableString Frostiness => new TranslatableString(getKey(@"frostiness"), @"Frostiness");

        /// <summary>
        /// "How transparent frosted slider bodies are. Higher values let more of the blurred content behind them show through."
        /// </summary>
        public static LocalisableString FrostinessDescription => new TranslatableString(getKey(@"frostiness_description"),
            @"How transparent frosted slider bodies are. Higher values let more of the blurred content behind them show through.");

        /// <summary>
        /// "Frost blur"
        /// </summary>
        public static LocalisableString FrostBlur => new TranslatableString(getKey(@"frost_blur"), @"Frost blur");

        /// <summary>
        /// "How strongly frosted slider bodies blur the content behind them. Lower values keep the content more recognisable."
        /// </summary>
        public static LocalisableString FrostBlurDescription => new TranslatableString(getKey(@"frost_blur_description"),
            @"How strongly frosted slider bodies blur the content behind them. Lower values keep the content more recognisable.");

        /// <summary>
        /// "Always colour frosted slider bodies"
        /// </summary>
        public static LocalisableString FrostedSlidersAlwaysColoured => new TranslatableString(getKey(@"frosted_sliders_always_coloured"), @"Always colour frosted slider bodies");

        /// <summary>
        /// "Frosted slider bodies use the combo colour, even if the skin specifies a slider track colour."
        /// </summary>
        public static LocalisableString FrostedSlidersAlwaysColouredDescription => new TranslatableString(getKey(@"frosted_sliders_always_coloured_description"),
            @"Frosted slider bodies use the combo colour, even if the skin specifies a slider track colour.");

        /// <summary>
        /// "Frosted hit circles"
        /// </summary>
        public static LocalisableString FrostedHitCircles => new TranslatableString(getKey(@"frosted_hit_circles"), @"Frosted hit circles");

        /// <summary>
        /// "Blurs the content behind hit circles which are translucent with frosted sliders. Has no effect on opaque hit circles."
        /// </summary>
        public static LocalisableString FrostedHitCirclesDescription => new TranslatableString(getKey(@"frosted_hit_circles_description"),
            @"Blurs the content behind hit circles which are translucent with frosted sliders. Has no effect on opaque hit circles.");

        /// <summary>
        /// "Menu cursor"
        /// </summary>
        public static LocalisableString MenuCursorStyle => new TranslatableString(getKey(@"menu_cursor_style"), @"Menu cursor");

        /// <summary>
        /// "The cursor used in menus and the editor. "Skin" uses the gameplay cursor of the current skin, including its trail. "System" uses the system cursor, which follows the mouse without any delay, but still rotates while dragging and shrinks when clicking. The gameplay cursor is not affected."
        /// </summary>
        public static LocalisableString MenuCursorStyleDescription => new TranslatableString(getKey(@"menu_cursor_style_description"),
            @"The cursor used in menus and the editor. ""Skin"" uses the gameplay cursor of the current skin, including its trail. ""System"" uses the system cursor, which follows the mouse without any delay, but still rotates while dragging and shrinks when clicking. The gameplay cursor is not affected.");

        /// <summary>
        /// "osu!"
        /// </summary>
        public static LocalisableString MenuCursorStyleDefault => new TranslatableString(getKey(@"menu_cursor_style_default"), @"osu!");

        /// <summary>
        /// "Skin"
        /// </summary>
        public static LocalisableString MenuCursorStyleSkin => new TranslatableString(getKey(@"menu_cursor_style_skin"), @"Skin");

        /// <summary>
        /// "System"
        /// </summary>
        public static LocalisableString MenuCursorStyleSystem => new TranslatableString(getKey(@"menu_cursor_style_system"), @"System");

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

        /// <summary>
        /// "Backup on save"
        /// </summary>
        public static LocalisableString BackupOnSave => new TranslatableString(getKey(@"backup_on_save"), @"Backup on save");

        /// <summary>
        /// "Writes a compressed version of the previous file to the Backups folder on every save."
        /// </summary>
        public static LocalisableString BackupOnSaveDescription => new TranslatableString(getKey(@"backup_on_save_description"),
            @"Writes a compressed version of the previous file to the Backups folder on every save.");

        /// <summary>
        /// "Autosave interval"
        /// </summary>
        public static LocalisableString AutosaveInterval => new TranslatableString(getKey(@"autosave_interval"), @"Autosave interval");

        /// <summary>
        /// "Automatically saves the beatmap with the given interval. Highly recommended to be used with "Backup on save". Set to 0 to disable."
        /// </summary>
        public static LocalisableString AutosaveIntervalDescription => new TranslatableString(getKey(@"autosave_interval_description"),
            @"Automatically saves the beatmap with the given interval. Highly recommended to be used with ""Backup on save"". Set to 0 to disable.");

        /// <summary>
        /// "Off"
        /// </summary>
        public static LocalisableString AutosaveOff => new TranslatableString(getKey(@"autosave_off"), @"Off");

        /// <summary>
        /// "{0} min"
        /// </summary>
        public static LocalisableString AutosaveMinutes(int minutes) => new TranslatableString(getKey(@"autosave_minutes"), @"{0} min", minutes);

        /// <summary>
        /// "Beatmap saved (auto save)"
        /// </summary>
        public static LocalisableString BeatmapAutoSaved => new TranslatableString(getKey(@"beatmap_auto_saved"), @"Beatmap saved (auto save)");

        /// <summary>
        /// "Show selection box"
        /// </summary>
        public static LocalisableString ShowSelectionBox => new TranslatableString(getKey(@"show_selection_box"), @"Show selection box");

        /// <summary>
        /// "Shows a box with scale and rotation handles around the selected objects."
        /// </summary>
        public static LocalisableString ShowSelectionBoxDescription => new TranslatableString(getKey(@"show_selection_box_description"),
            @"Shows a box with scale and rotation handles around the selected objects.");

        /// <summary>
        /// "Show selection box buttons"
        /// </summary>
        public static LocalisableString ShowSelectionBoxButtons => new TranslatableString(getKey(@"show_selection_box_buttons"), @"Show selection box buttons");

        /// <summary>
        /// "Shows rotate, flip and reverse buttons next to the selected objects. Their keyboard shortcuts work regardless."
        /// </summary>
        public static LocalisableString ShowSelectionBoxButtonsDescription => new TranslatableString(getKey(@"show_selection_box_buttons_description"),
            @"Shows rotate, flip and reverse buttons next to the selected objects. Their keyboard shortcuts work regardless.");

        /// <summary>
        /// "Show slider length handle"
        /// </summary>
        public static LocalisableString ShowSliderEndDragMarker => new TranslatableString(getKey(@"show_slider_end_drag_marker"), @"Show slider length handle");

        /// <summary>
        /// "Shows the arc at the end of selected sliders, which can be dragged to adjust the slider's length."
        /// </summary>
        public static LocalisableString ShowSliderEndDragMarkerDescription => new TranslatableString(getKey(@"show_slider_end_drag_marker_description"),
            @"Shows the arc at the end of selected sliders, which can be dragged to adjust the slider's length.");

        /// <summary>
        /// "Move objects without delay"
        /// </summary>
        public static LocalisableString ImmediateDrag => new TranslatableString(getKey(@"immediate_drag"), @"Move objects without delay");

        /// <summary>
        /// "Objects and slider anchors start moving as soon as the mouse moves, instead of only after the mouse has moved a few pixels. Allows very small adjustments."
        /// </summary>
        public static LocalisableString ImmediateDragDescription => new TranslatableString(getKey(@"immediate_drag_description"),
            @"Objects and slider anchors start moving as soon as the mouse moves, instead of only after the mouse has moved a few pixels. Allows very small adjustments.");

        /// <summary>
        /// "Anchor shape"
        /// </summary>
        public static LocalisableString AnchorShape => new TranslatableString(getKey(@"anchor_shape"), @"Anchor shape");

        /// <summary>
        /// "The shape of slider anchors and the handles of the selection box."
        /// </summary>
        public static LocalisableString AnchorShapeDescription => new TranslatableString(getKey(@"anchor_shape_description"),
            @"The shape of slider anchors and the handles of the selection box.");

        /// <summary>
        /// "Circle"
        /// </summary>
        public static LocalisableString AnchorShapeCircle => new TranslatableString(getKey(@"anchor_shape_circle"), @"Circle");

        /// <summary>
        /// "Square"
        /// </summary>
        public static LocalisableString AnchorShapeSquare => new TranslatableString(getKey(@"anchor_shape_square"), @"Square");

        /// <summary>
        /// "Snap to visual spacing"
        /// </summary>
        public static LocalisableString VisualSpacingSnap => new TranslatableString(getKey(@"visual_spacing_snap"), @"Snap to visual spacing");

        /// <summary>
        /// "Objects snap to positions which form an equilateral triangle with two other visible objects."
        /// </summary>
        public static LocalisableString VisualSpacingSnapDescription => new TranslatableString(getKey(@"visual_spacing_snap_description"),
            @"Objects snap to positions which form an equilateral triangle with two other visible objects.");

        /// <summary>
        /// "Snap to blankets"
        /// </summary>
        public static LocalisableString BlanketSnap => new TranslatableString(getKey(@"blanket_snap"), @"Snap to blankets");

        /// <summary>
        /// "Objects snap to the centre of curved slider sections (perfect curve), so that the slider perfectly wraps around them."
        /// </summary>
        public static LocalisableString BlanketSnapDescription => new TranslatableString(getKey(@"blanket_snap_description"),
            @"Objects snap to the centre of curved slider sections (perfect curve), so that the slider perfectly wraps around them.");

        /// <summary>
        /// "Snap to straight lines"
        /// </summary>
        public static LocalisableString LineSnap => new TranslatableString(getKey(@"line_snap"), @"Snap to straight lines");

        /// <summary>
        /// "Objects snap to positions which continue a straight line of equally spaced visible objects."
        /// </summary>
        public static LocalisableString LineSnapDescription => new TranslatableString(getKey(@"line_snap_description"),
            @"Objects snap to positions which continue a straight line of equally spaced visible objects.");

        /// <summary>
        /// "Slider blanket snapping"
        /// </summary>
        public static LocalisableString SliderBlanketSnap => new TranslatableString(getKey(@"slider_blanket_snap"), @"Slider blanket snapping");

        /// <summary>
        /// "Sliders snap to positions where their arcs share the centre of the arcs of visible sliders, so that one slider blankets the other."
        /// </summary>
        public static LocalisableString SliderBlanketSnapDescription => new TranslatableString(getKey(@"slider_blanket_snap_description"),
            @"Sliders snap to positions where their arcs share the centre of the arcs of visible sliders, so that one slider blankets the other.");

        /// <summary>
        /// "Rotate around hit circles and slider heads"
        /// </summary>
        public static LocalisableString RotateAroundObjectStarts => new TranslatableString(getKey(@"rotate_around_object_starts"), @"Rotate around hit circles and slider heads");

        /// <summary>
        /// "Rotating with ctrl+shift+scroll rotates around the centre of the hit circles and slider heads, ignoring slider bodies. A single slider rotates around its head."
        /// </summary>
        public static LocalisableString RotateAroundObjectStartsDescription => new TranslatableString(getKey(@"rotate_around_object_starts_description"),
            @"Rotating with ctrl+shift+scroll rotates around the centre of the hit circles and slider heads, ignoring slider bodies. A single slider rotates around its head.");

        /// <summary>
        /// "Change slider velocity on the timeline with alt"
        /// </summary>
        public static LocalisableString TimelineSliderVelocityWithAlt => new TranslatableString(getKey(@"timeline_slider_velocity_with_alt"), @"Change slider velocity on the timeline with alt");

        /// <summary>
        /// "Dragging the end of a slider on the timeline changes its velocity while holding alt instead of shift."
        /// </summary>
        public static LocalisableString TimelineSliderVelocityWithAltDescription => new TranslatableString(getKey(@"timeline_slider_velocity_with_alt_description"),
            @"Dragging the end of a slider on the timeline changes its velocity while holding alt instead of shift.");

        /// <summary>
        /// "Show difficulty strains"
        /// </summary>
        public static LocalisableString ShowDifficultyStrains => new TranslatableString(getKey(@"show_difficulty_strains"), @"Show difficulty strains");

        /// <summary>
        /// "Displays a graph of the difficulty strain over time in the timeline at the bottom of the editor."
        /// </summary>
        public static LocalisableString ShowDifficultyStrainsDescription => new TranslatableString(getKey(@"show_difficulty_strains_description"),
            @"Displays a graph of the difficulty strain over time in the timeline at the bottom of the editor.");

        /// <summary>
        /// "Highlight offscreen objects"
        /// </summary>
        public static LocalisableString ShowOffscreenObjects => new TranslatableString(getKey(@"show_offscreen_objects"), @"Highlight offscreen objects");

        /// <summary>
        /// "Outlines circles and sliders in red which go offscreen on a 4:3 screen, including any part of a slider's body. Offscreen objects aren't allowed in ranked beatmaps."
        /// </summary>
        public static LocalisableString ShowOffscreenObjectsDescription => new TranslatableString(getKey(@"show_offscreen_objects_description"),
            @"Outlines circles and sliders in red which go offscreen on a 4:3 screen, including any part of a slider's body. Offscreen objects aren't allowed in ranked beatmaps.");

        /// <summary>
        /// "FPoSu"
        /// </summary>
        public static LocalisableString FposuHeader => new TranslatableString(getKey(@"fposu_header"), @"FPoSu");

        /// <summary>
        /// "Mouse DPI"
        /// </summary>
        public static LocalisableString FposuMouseDpi => new TranslatableString(getKey(@"fposu_mouse_dpi"), @"Mouse DPI");

        /// <summary>
        /// "The DPI your mouse is set to. Together with cm/360, this determines the sensitivity like in first person shooters."
        /// </summary>
        public static LocalisableString FposuMouseDpiDescription => new TranslatableString(getKey(@"fposu_mouse_dpi_description"),
            @"The DPI your mouse is set to. Together with cm/360, this determines the sensitivity like in first person shooters.");

        /// <summary>
        /// "cm/360"
        /// </summary>
        public static LocalisableString FposuCmPer360 => new TranslatableString(getKey(@"fposu_cm_per_360"), @"cm/360");

        /// <summary>
        /// "How many centimetres the mouse has to be moved for a full turn."
        /// </summary>
        public static LocalisableString FposuCmPer360Description => new TranslatableString(getKey(@"fposu_cm_per_360_description"),
            @"How many centimetres the mouse has to be moved for a full turn.");

        /// <summary>
        /// "Field of view"
        /// </summary>
        public static LocalisableString FposuFov => new TranslatableString(getKey(@"fposu_fov"), @"Field of view");

        /// <summary>
        /// "The horizontal field of view in degrees."
        /// </summary>
        public static LocalisableString FposuFovDescription => new TranslatableString(getKey(@"fposu_fov_description"), @"The horizontal field of view in degrees.");

        /// <summary>
        /// "Distance"
        /// </summary>
        public static LocalisableString FposuDistance => new TranslatableString(getKey(@"fposu_distance"), @"Distance");

        /// <summary>
        /// "How far away the playfield is. Lower values make it appear larger."
        /// </summary>
        public static LocalisableString FposuDistanceDescription => new TranslatableString(getKey(@"fposu_distance_description"),
            @"How far away the playfield is. Lower values make it appear larger.");

        /// <summary>
        /// "Curved playfield"
        /// </summary>
        public static LocalisableString FposuCurved => new TranslatableString(getKey(@"fposu_curved"), @"Curved playfield");

        /// <summary>
        /// "Curves the playfield around you, so that every part of it is equally far away."
        /// </summary>
        public static LocalisableString FposuCurvedDescription => new TranslatableString(getKey(@"fposu_curved_description"),
            @"Curves the playfield around you, so that every part of it is equally far away.");

        /// <summary>
        /// "Invert horizontal mouse movement"
        /// </summary>
        public static LocalisableString FposuInvertHorizontal => new TranslatableString(getKey(@"fposu_invert_horizontal"), @"Invert horizontal mouse movement");

        /// <summary>
        /// "Invert vertical mouse movement"
        /// </summary>
        public static LocalisableString FposuInvertVertical => new TranslatableString(getKey(@"fposu_invert_vertical"), @"Invert vertical mouse movement");

        /// <summary>
        /// "Absolute mode"
        /// </summary>
        public static LocalisableString FposuAbsoluteMode => new TranslatableString(getKey(@"fposu_absolute_mode"), @"Absolute mode");

        /// <summary>
        /// "The camera looks at the cursor instead of being turned by mouse movement. Useful for tablets."
        /// </summary>
        public static LocalisableString FposuAbsoluteModeDescription => new TranslatableString(getKey(@"fposu_absolute_mode_description"),
            @"The camera looks at the cursor instead of being turned by mouse movement. Useful for tablets.");

        /// <summary>
        /// "Background cube"
        /// </summary>
        public static LocalisableString FposuBackgroundCube => new TranslatableString(getKey(@"fposu_background_cube"), @"Background cube");

        /// <summary>
        /// "Skybox"
        /// </summary>
        public static LocalisableString FposuSkybox => new TranslatableString(getKey(@"fposu_skybox"), @"Skybox");

        /// <summary>
        /// "Displays a sky around you instead of the background cube. Skins can provide their own as skybox.png, a cubemap in the horizontal cross layout."
        /// </summary>
        public static LocalisableString FposuSkyboxDescription => new TranslatableString(getKey(@"fposu_skybox_description"),
            @"Displays a sky around you instead of the background cube. Skins can provide their own as skybox.png, a cubemap in the horizontal cross layout.");

        /// <summary>
        /// "Displays a grid around you, which helps with orientation."
        /// </summary>
        public static LocalisableString FposuBackgroundCubeDescription => new TranslatableString(getKey(@"fposu_background_cube_description"),
            @"Displays a grid around you, which helps with orientation.");

        /// <summary>
        /// "Loop music"
        /// </summary>
        public static LocalisableString LoopMusic => new TranslatableString(getKey(@"loop_music"), @"Loop music");

        /// <summary>
        /// "When the end of the track is reached during playback in the editor, playback continues from the start."
        /// </summary>
        public static LocalisableString LoopMusicDescription => new TranslatableString(getKey(@"loop_music_description"),
            @"When the end of the track is reached during playback in the editor, playback continues from the start.");

        /// <summary>
        /// "Frosted hitsound editor lanes"
        /// </summary>
        public static LocalisableString FrostedHitsoundLanes => new TranslatableString(getKey(@"frosted_hitsound_lanes"), @"Frosted hitsound editor lanes");

        /// <summary>
        /// "The lanes of the hitsound editor have a frosted glass background, through which the background of the beatmap shows."
        /// </summary>
        public static LocalisableString FrostedHitsoundLanesDescription => new TranslatableString(getKey(@"frosted_hitsound_lanes_description"),
            @"The lanes of the hitsound editor have a frosted glass background, through which the background of the beatmap shows.");

        /// <summary>
        /// "Timeline waveform"
        /// </summary>
        public static LocalisableString WaveformStyle => new TranslatableString(getKey(@"waveform_style"), @"Timeline waveform");

        /// <summary>
        /// "How the waveform is displayed in the timeline at the top of the editor."
        /// </summary>
        public static LocalisableString WaveformStyleDescription => new TranslatableString(getKey(@"waveform_style_description"),
            @"How the waveform is displayed in the timeline at the top of the editor.");

        /// <summary>
        /// "Default"
        /// </summary>
        public static LocalisableString WaveformStyleDefault => new TranslatableString(getKey(@"waveform_style_default"), @"Default");

        /// <summary>
        /// "Simple"
        /// </summary>
        public static LocalisableString WaveformStyleSimple => new TranslatableString(getKey(@"waveform_style_simple"), @"Simple");

        /// <summary>
        /// "Three-Band"
        /// </summary>
        public static LocalisableString WaveformStyleThreeBand => new TranslatableString(getKey(@"waveform_style_three_band"), @"Three-Band");

        /// <summary>
        /// "FL Studio / MiniMeters"
        /// </summary>
        public static LocalisableString WaveformStyleSpectral => new TranslatableString(getKey(@"waveform_style_spectral"), @"FL Studio / MiniMeters");

        /// <summary>
        /// "Online"
        /// </summary>
        public static LocalisableString OnlineHeader => new TranslatableString(getKey(@"online_header"), @"Online");

        /// <summary>
        /// "Beatmap mirror"
        /// </summary>
        public static LocalisableString BeatmapMirror => new TranslatableString(getKey(@"beatmap_mirror"), @"Beatmap mirror");

        /// <summary>
        /// "The server beatmaps are downloaded and updated from while not logged in or while connected to the development server. Only Mino and osu.direct can be searched, so the beatmap listing and update checks use one of them if another mirror is selected."
        /// </summary>
        public static LocalisableString BeatmapMirrorDescription => new TranslatableString(getKey(@"beatmap_mirror_description"),
            @"The server beatmaps are downloaded and updated from while not logged in or while connected to the development server. Only Mino and osu.direct can be searched, so the beatmap listing and update checks use one of them if another mirror is selected.");

        /// <summary>
        /// "Clear stored mappers"
        /// </summary>
        public static LocalisableString ClearStoredMappers => new TranslatableString(getKey(@"clear_stored_mappers"), @"Clear stored mappers");

        /// <summary>
        /// "Stored mappers cleared."
        /// </summary>
        public static LocalisableString StoredMappersCleared => new TranslatableString(getKey(@"stored_mappers_cleared"), @"Stored mappers cleared.");

        /// <summary>
        /// "BN Tracker server"
        /// </summary>
        public static LocalisableString BnTrackerServer => new TranslatableString(getKey(@"bn_tracker_server"), @"BN Tracker server");

        /// <summary>
        /// "The server which tracks the Beatmap Nominators asked to nominate your beatmap sets, shown on the "request" screen of the beatmap editor. Press enter to apply."
        /// </summary>
        public static LocalisableString BnTrackerServerDescription => new TranslatableString(getKey(@"bn_tracker_server_description"),
            @"The server which tracks the Beatmap Nominators asked to nominate your beatmap sets, shown on the ""request"" screen of the beatmap editor. Press enter to apply.");

        /// <summary>
        /// "Sign out of the BN Tracker ({0})"
        /// </summary>
        public static LocalisableString BnTrackerSignOut(string username) => new TranslatableString(getKey(@"bn_tracker_sign_out"), @"Sign out of the BN Tracker ({0})", username);

        /// <summary>
        /// "Sign out of the BN Tracker"
        /// </summary>
        public static LocalisableString BnTrackerSignOutUnknownUser => new TranslatableString(getKey(@"bn_tracker_sign_out_unknown_user"), @"Sign out of the BN Tracker");

        /// <summary>
        /// "Not signed in to the BN Tracker"
        /// </summary>
        public static LocalisableString BnTrackerNotSignedIn => new TranslatableString(getKey(@"bn_tracker_not_signed_in"), @"Not signed in to the BN Tracker");

        /// <summary>
        /// "Frosted BN Tracker panels"
        /// </summary>
        public static LocalisableString FrostedBnTrackerPanels => new TranslatableString(getKey(@"frosted_bn_tracker_panels"), @"Frosted BN Tracker panels");

        /// <summary>
        /// "The panels of the "request" screen have a frosted glass background, through which the background of the beatmap shows."
        /// </summary>
        public static LocalisableString FrostedBnTrackerPanelsDescription => new TranslatableString(getKey(@"frosted_bn_tracker_panels_description"),
            @"The panels of the ""request"" screen have a frosted glass background, through which the background of the beatmap shows.");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
