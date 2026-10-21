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
        /// "Loop music"
        /// </summary>
        public static LocalisableString LoopMusic => new TranslatableString(getKey(@"loop_music"), @"Loop music");

        /// <summary>
        /// "When the end of the track is reached during playback in the editor, playback continues from the start."
        /// </summary>
        public static LocalisableString LoopMusicDescription => new TranslatableString(getKey(@"loop_music_description"),
            @"When the end of the track is reached during playback in the editor, playback continues from the start.");

        /// <summary>
        /// "Set muted object ends to Soft:C999"
        /// </summary>
        public static LocalisableString MutedEndsSoftC999 => new TranslatableString(getKey(@"muted_ends_soft_c999"), @"Set muted object ends to Soft:C999");

        /// <summary>
        /// "Sets a custom sample set in addition to changing volume when using the object end mute hotkey."
        /// </summary>
        public static LocalisableString MutedEndsSoftC999Description => new TranslatableString(getKey(@"muted_ends_soft_c999_description"),
            @"Sets a custom sample set in addition to changing volume when using the object end mute hotkey.");

        /// <summary>
        /// "Zoom out playfield"
        /// </summary>
        public static LocalisableString ZoomOutPlayfield => new TranslatableString(getKey(@"zoom_out_playfield"), @"Zoom out playfield");

        /// <summary>
        /// "Shrinks the playfield of the compose screen, leaving room to work with objects outside of it."
        /// </summary>
        public static LocalisableString ZoomOutPlayfieldDescription => new TranslatableString(getKey(@"zoom_out_playfield_description"),
            @"Shrinks the playfield of the compose screen, leaving room to work with objects outside of it.");

        /// <summary>
        /// "Show grid"
        /// </summary>
        public static LocalisableString ShowGrid => new TranslatableString(getKey(@"show_grid"), @"Show grid");

        /// <summary>
        /// "Shows the positional snap grid in the compose screen. Grid snapping keeps working while the grid is hidden."
        /// </summary>
        public static LocalisableString ShowGridDescription => new TranslatableString(getKey(@"show_grid_description"),
            @"Shows the positional snap grid in the compose screen. Grid snapping keeps working while the grid is hidden.");

        /// <summary>
        /// "Rotate around the selection"
        /// </summary>
        public static LocalisableString RotateAroundSelection => new TranslatableString(getKey(@"rotate_around_selection"), @"Rotate around the selection");

        /// <summary>
        /// "When disabled, the rotation hotkeys and ctrl+shift+scroll rotate the selection around the centre of the playfield."
        /// </summary>
        public static LocalisableString RotateAroundSelectionDescription => new TranslatableString(getKey(@"rotate_around_selection_description"),
            @"When disabled, the rotation hotkeys and ctrl+shift+scroll rotate the selection around the centre of the playfield.");

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
        /// "Expand hitsound toolbox on hover"
        /// </summary>
        public static LocalisableString HitsoundToolboxExpandOnHover => new TranslatableString(getKey(@"hitsound_toolbox_expand_on_hover"), @"Expand hitsound toolbox on hover");

        /// <summary>
        /// "While the sidebars are contracted, the toolbox of the hitsound editor expands while hovering it, like the toolboxes of the compose screen."
        /// </summary>
        public static LocalisableString HitsoundToolboxExpandOnHoverDescription => new TranslatableString(getKey(@"hitsound_toolbox_expand_on_hover_description"),
            @"While the sidebars are contracted, the toolbox of the hitsound editor expands while hovering it, like the toolboxes of the compose screen.");

        /// <summary>
        /// "Sync hitsounds with the hitsound difficulty"
        /// </summary>
        public static LocalisableString AutoApplyHitsoundDifficulty => new TranslatableString(getKey(@"auto_apply_hitsound_difficulty"),
            @"Sync hitsounds with the hitsound difficulty");

        /// <summary>
        /// "Objects which are placed or moved in other difficulties get the hitsounds of the hitsound difficulty at their time. Leaving the hitsound difficulty after saving it copies its hitsounds to the other difficulties, except those deselected under "Copy hitsounds" in the hitsound editor."
        /// </summary>
        public static LocalisableString AutoApplyHitsoundDifficultyDescription => new TranslatableString(getKey(@"auto_apply_hitsound_difficulty_description"),
            @"Objects which are placed or moved in other difficulties get the hitsounds of the hitsound difficulty at their time. Leaving the hitsound difficulty after saving it copies its hitsounds to the other difficulties, except those deselected under ""Copy hitsounds"" in the hitsound editor.");

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
        /// "Onsets"
        /// </summary>
        public static LocalisableString WaveformStyleOnsets => new TranslatableString(getKey(@"waveform_style_onsets"), @"Onsets");

        /// <summary>
        /// "Online"
        /// </summary>
        public static LocalisableString OnlineHeader => new TranslatableString(getKey(@"online_header"), @"Online");

        /// <summary>
        /// "Always use argon cursor trail"
        /// </summary>
        public static LocalisableString ForceArgonCursorTrail => new TranslatableString(getKey(@"force_argon_cursor_trail"), @"Always use argon cursor trail");

        /// <summary>
        /// "Replaces the cursor trail of the current skin with the argon cursor trail."
        /// </summary>
        public static LocalisableString ForceArgonCursorTrailDescription => new TranslatableString(getKey(@"force_argon_cursor_trail_description"),
            @"Replaces the cursor trail of the current skin with the argon cursor trail.");

        /// <summary>
        /// "Use combo colour for slider borders"
        /// </summary>
        public static LocalisableString SliderBorderComboColour => new TranslatableString(getKey(@"slider_border_combo_colour"), @"Use combo colour for slider borders");

        /// <summary>
        /// "Colours slider borders with the combo colour of the slider instead of the border colour of the skin."
        /// </summary>
        public static LocalisableString SliderBorderComboColourDescription => new TranslatableString(getKey(@"slider_border_combo_colour_description"),
            @"Colours slider borders with the combo colour of the slider instead of the border colour of the skin.");

        /// <summary>
        /// "Gameplay"
        /// </summary>
        public static LocalisableString GameplayHeader => new TranslatableString(getKey(@"gameplay_header"), @"Gameplay");

        /// <summary>
        /// "Skip breaks"
        /// </summary>
        public static LocalisableString SkipBreaks => new TranslatableString(getKey(@"skip_breaks"), @"Skip breaks");

        /// <summary>
        /// "Displays a skip button during breaks, which jumps to shortly before the next objects."
        /// </summary>
        public static LocalisableString SkipBreaksDescription => new TranslatableString(getKey(@"skip_breaks_description"),
            @"Displays a skip button during breaks, which jumps to shortly before the next objects.");

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

        /// <summary>
        /// "Colour effects"
        /// </summary>
        public static LocalisableString ColourEffectsHeader => new TranslatableString(getKey(@"colour_effects_header"), @"Colour effects");

        /// <summary>
        /// "Chroma cursor"
        /// </summary>
        public static LocalisableString CursorColourEffect => new TranslatableString(getKey(@"cursor_colour_effect"), @"Chroma cursor");

        /// <summary>
        /// "The gameplay cursor smoothly cycles through rainbow or gradient colours."
        /// </summary>
        public static LocalisableString CursorColourEffectDescription => new TranslatableString(getKey(@"cursor_colour_effect_description"), @"The gameplay cursor smoothly cycles through rainbow or gradient colours.");

        /// <summary>
        /// "Chroma cursor trail"
        /// </summary>
        public static LocalisableString CursorTrailColourEffect => new TranslatableString(getKey(@"cursor_trail_colour_effect"), @"Chroma cursor trail");

        /// <summary>
        /// "The cursor trail smoothly cycles through rainbow or gradient colours."
        /// </summary>
        public static LocalisableString CursorTrailColourEffectDescription => new TranslatableString(getKey(@"cursor_trail_colour_effect_description"), @"The cursor trail smoothly cycles through rainbow or gradient colours.");

        /// <summary>
        /// "Chroma combo colours"
        /// </summary>
        public static LocalisableString ComboColourEffect => new TranslatableString(getKey(@"combo_colour_effect"), @"Chroma combo colours");

        /// <summary>
        /// "Combo colours of hit objects are replaced by smoothly cycling rainbow or gradient colours."
        /// </summary>
        public static LocalisableString ComboColourEffectDescription => new TranslatableString(getKey(@"combo_colour_effect_description"), @"Combo colours of hit objects are replaced by smoothly cycling rainbow or gradient colours.");

        /// <summary>
        /// "Style"
        /// </summary>
        public static LocalisableString ColourEffectMode => new TranslatableString(getKey(@"colour_effect_mode"), @"Style");

        /// <summary>
        /// "Gradient colours"
        /// </summary>
        public static LocalisableString ColourEffectGradient => new TranslatableString(getKey(@"colour_effect_gradient"), @"Gradient colours");

        /// <summary>
        /// "The colours which are cycled through, in order. Right-click a colour to remove it."
        /// </summary>
        public static LocalisableString ColourEffectGradientDescription => new TranslatableString(getKey(@"colour_effect_gradient_description"), @"The colours which are cycled through, in order. Right-click a colour to remove it.");

        /// <summary>
        /// "Colour effect speed"
        /// </summary>
        public static LocalisableString ColourEffectSpeed => new TranslatableString(getKey(@"colour_effect_speed"), @"Colour effect speed");

        /// <summary>
        /// "How fast all rainbow and gradient colours cycle. All effects share this speed, so they stay in sync."
        /// </summary>
        public static LocalisableString ColourEffectSpeedDescription => new TranslatableString(getKey(@"colour_effect_speed_description"), @"How fast all rainbow and gradient colours cycle. All effects share this speed, so they stay in sync.");

        /// <summary>
        /// "Rainbow"
        /// </summary>
        public static LocalisableString ColourEffectModeRainbow => new TranslatableString(getKey(@"colour_effect_mode_rainbow"), @"Rainbow");

        /// <summary>
        /// "Gradient"
        /// </summary>
        public static LocalisableString ColourEffectModeGradient => new TranslatableString(getKey(@"colour_effect_mode_gradient"), @"Gradient");

        /// <summary>
        /// "Combo colours"
        /// </summary>
        public static LocalisableString ColourEffectModeComboColours => new TranslatableString(getKey(@"colour_effect_mode_combo_colours"), @"Combo colours");

        /// <summary>
        /// "Rainbow saturation"
        /// </summary>
        public static LocalisableString ColourEffectRainbowSaturation => new TranslatableString(getKey(@"colour_effect_rainbow_saturation"), @"Rainbow saturation");

        /// <summary>
        /// "How saturated rainbow colours are. Lower values make dark colours like blue and purple brighter."
        /// </summary>
        public static LocalisableString ColourEffectRainbowSaturationDescription => new TranslatableString(getKey(@"colour_effect_rainbow_saturation_description"), @"How saturated rainbow colours are. Lower values make dark colours like blue and purple brighter.");

        /// <summary>
        /// "Hit objects"
        /// </summary>
        public static LocalisableString HitObjectsHeader => new TranslatableString(getKey(@"hit_objects_header"), @"Hit objects");

        /// <summary>
        /// "Show 300 judgements"
        /// </summary>
        public static LocalisableString ShowGreatJudgements => new TranslatableString(getKey(@"show_great_judgements"), @"Show 300 judgements");

        /// <summary>
        /// "Show slider ends"
        /// </summary>
        public static LocalisableString ShowSliderEnds => new TranslatableString(getKey(@"show_slider_ends"), @"Show slider ends");

        /// <summary>
        /// "Slider end circles are still shown in the editor."
        /// </summary>
        public static LocalisableString ShowSliderEndsDescription => new TranslatableString(getKey(@"show_slider_ends_description"), @"Slider end circles are still shown in the editor.");

        /// <summary>
        /// "Show combo numbers"
        /// </summary>
        public static LocalisableString ShowComboNumbers => new TranslatableString(getKey(@"show_combo_numbers"), @"Show combo numbers");

        /// <summary>
        /// "Hide follow points"
        /// </summary>
        public static LocalisableString HideFollowPoints => new TranslatableString(getKey(@"hide_follow_points"), @"Hide follow points");

        /// <summary>
        /// "Hides follow points in gameplay and in the editor."
        /// </summary>
        public static LocalisableString HideFollowPointsDescription => new TranslatableString(getKey(@"hide_follow_points_description"), @"Hides follow points in gameplay and in the editor.");

        /// <summary>
        /// "Disable slider rendering"
        /// </summary>
        public static LocalisableString HideSliderBodies => new TranslatableString(getKey(@"hide_slider_bodies"), @"Disable slider rendering");

        /// <summary>
        /// "Hides slider bodies. Makes some 2B/Aspire maps easier to read."
        /// </summary>
        public static LocalisableString HideSliderBodiesDescription => new TranslatableString(getKey(@"hide_slider_bodies_description"), @"Hides slider bodies. Makes some 2B/Aspire maps easier to read.");

        /// <summary>
        /// "Don't tint approach circles"
        /// </summary>
        public static LocalisableString WhiteApproachCircles => new TranslatableString(getKey(@"white_approach_circles"), @"Don't tint approach circles");

        /// <summary>
        /// "Makes approach circles white instead of using combo colours."
        /// </summary>
        public static LocalisableString WhiteApproachCirclesDescription => new TranslatableString(getKey(@"white_approach_circles_description"), @"Makes approach circles white instead of using combo colours.");

        /// <summary>
        /// "Animations"
        /// </summary>
        public static LocalisableString AnimationsHeader => new TranslatableString(getKey(@"animations_header"), @"Animations");

        /// <summary>
        /// "Disable kiai effects"
        /// </summary>
        public static LocalisableString DisableKiaiEffects => new TranslatableString(getKey(@"disable_kiai_effects"), @"Disable kiai effects");

        /// <summary>
        /// "Disables all kiai time effects during gameplay."
        /// </summary>
        public static LocalisableString DisableKiaiEffectsDescription => new TranslatableString(getKey(@"disable_kiai_effects_description"), @"Disables all kiai time effects during gameplay.");

        /// <summary>
        /// "Increase framerate of hitcircleoverlay animation"
        /// </summary>
        public static LocalisableString AnimateHitCircleOverlay => new TranslatableString(getKey(@"animate_hit_circle_overlay"), @"Increase framerate of hitcircleoverlay animation");

        /// <summary>
        /// "Uses the skin's AnimationFramerate for hitcircleoverlay animations instead of a fixed 2 frames per second. Only applies to legacy skins."
        /// </summary>
        public static LocalisableString AnimateHitCircleOverlayDescription => new TranslatableString(getKey(@"animate_hit_circle_overlay_description"), @"Uses the skin's AnimationFramerate for hitcircleoverlay animations instead of a fixed 2 frames per second. Only applies to legacy skins.");

        /// <summary>
        /// "Disable slider fade-out animation"
        /// </summary>
        public static LocalisableString InstafadeSliders => new TranslatableString(getKey(@"instafade_sliders"), @"Disable slider fade-out animation");

        /// <summary>
        /// "Disable follow circle animations"
        /// </summary>
        public static LocalisableString DisableFollowCircleAnimations => new TranslatableString(getKey(@"disable_follow_circle_animations"), @"Disable follow circle animations");

        /// <summary>
        /// "Disables pulsing on slider ticks and the fade in/out."
        /// </summary>
        public static LocalisableString DisableFollowCircleAnimationsDescription => new TranslatableString(getKey(@"disable_follow_circle_animations_description"), @"Disables pulsing on slider ticks and the fade in/out.");

        /// <summary>
        /// "Disable reverse arrow animations"
        /// </summary>
        public static LocalisableString DisableReverseArrowAnimations => new TranslatableString(getKey(@"disable_reverse_arrow_animations"), @"Disable reverse arrow animations");

        /// <summary>
        /// "Disables pulsing and rotating."
        /// </summary>
        public static LocalisableString DisableReverseArrowAnimationsDescription => new TranslatableString(getKey(@"disable_reverse_arrow_animations_description"), @"Disables pulsing and rotating.");

        /// <summary>
        /// "Disable fade-out on numbers"
        /// </summary>
        public static LocalisableString InstafadeNumbers => new TranslatableString(getKey(@"instafade_numbers"), @"Disable fade-out on numbers");

        /// <summary>
        /// "Force disable cursor expand"
        /// </summary>
        public static LocalisableString ForceDisableCursorExpand => new TranslatableString(getKey(@"force_disable_cursor_expand"), @"Force disable cursor expand");

        /// <summary>
        /// "Prevents the cursor from expanding on click, even if the skin enables it."
        /// </summary>
        public static LocalisableString ForceDisableCursorExpandDescription => new TranslatableString(getKey(@"force_disable_cursor_expand_description"), @"Prevents the cursor from expanding on click, even if the skin enables it.");

        /// <summary>
        /// "Kill player upon failing"
        /// </summary>
        public static LocalisableString KillOnFail => new TranslatableString(getKey(@"kill_on_fail"), @"Kill player upon failing");

        /// <summary>
        /// "Disable to act like you're playing in multiplayer."
        /// </summary>
        public static LocalisableString KillOnFailDescription => new TranslatableString(getKey(@"kill_on_fail_description"), @"Disable to act like you're playing in multiplayer.");

        /// <summary>
        /// "Disable multiplayer fail effects"
        /// </summary>
        public static LocalisableString DisableSoftFailEffects => new TranslatableString(getKey(@"disable_soft_fail_effects"), @"Disable multiplayer fail effects");

        /// <summary>
        /// "Disables the dimming effect and notification on fail when the above option is disabled."
        /// </summary>
        public static LocalisableString DisableSoftFailEffectsDescription => new TranslatableString(getKey(@"disable_soft_fail_effects_description"), @"Disables the dimming effect and notification on fail when the above option is disabled.");

        /// <summary>
        /// "Failed"
        /// </summary>
        public static LocalisableString SoftFailNotification => new TranslatableString(getKey(@"soft_fail_notification"), @"Failed");

        /// <summary>
        /// "Ignore beatmap volume"
        /// </summary>
        public static LocalisableString IgnoreBeatmapVolume => new TranslatableString(getKey(@"ignore_beatmap_volume"), @"Ignore beatmap volume");

        /// <summary>
        /// "Plays all hitsounds at full volume instead of the volume set by the beatmap."
        /// </summary>
        public static LocalisableString IgnoreBeatmapVolumeDescription => new TranslatableString(getKey(@"ignore_beatmap_volume_description"), @"Plays all hitsounds at full volume instead of the volume set by the beatmap.");

        /// <summary>
        /// "Never"
        /// </summary>
        public static LocalisableString IgnoreBeatmapVolumeNever => new TranslatableString(getKey(@"ignore_beatmap_volume_never"), @"Never");

        /// <summary>
        /// "If beatmap hitsounds are disabled"
        /// </summary>
        public static LocalisableString IgnoreBeatmapVolumeIfHitsoundsDisabled => new TranslatableString(getKey(@"ignore_beatmap_volume_if_hitsounds_disabled"), @"If beatmap hitsounds are disabled");

        /// <summary>
        /// "Always"
        /// </summary>
        public static LocalisableString IgnoreBeatmapVolumeAlways => new TranslatableString(getKey(@"ignore_beatmap_volume_always"), @"Always");

        /// <summary>
        /// "Mute hitsounds"
        /// </summary>
        public static LocalisableString MuteHitsounds => new TranslatableString(getKey(@"mute_hitsounds"), @"Mute hitsounds");

        /// <summary>
        /// "Never"
        /// </summary>
        public static LocalisableString MuteHitsoundsNever => new TranslatableString(getKey(@"mute_hitsounds_never"), @"Never");

        /// <summary>
        /// "osu!mania only"
        /// </summary>
        public static LocalisableString MuteHitsoundsManiaOnly => new TranslatableString(getKey(@"mute_hitsounds_mania_only"), @"osu!mania only");

        /// <summary>
        /// "Always"
        /// </summary>
        public static LocalisableString MuteHitsoundsAlways => new TranslatableString(getKey(@"mute_hitsounds_always"), @"Always");

        /// <summary>
        /// "Always use minimum flashlight size"
        /// </summary>
        public static LocalisableString FlashlightMinSize => new TranslatableString(getKey(@"flashlight_min_size"), @"Always use minimum flashlight size");

        /// <summary>
        /// "Makes the flashlight size always use the minimum radius (200x combo)."
        /// </summary>
        public static LocalisableString FlashlightMinSizeDescription => new TranslatableString(getKey(@"flashlight_min_size_description"), @"Makes the flashlight size always use the minimum radius (200x combo).");

        /// <summary>
        /// "Don't reset flashlight size on combo break"
        /// </summary>
        public static LocalisableString FlashlightIgnoreComboBreaks => new TranslatableString(getKey(@"flashlight_ignore_combo_breaks"), @"Don't reset flashlight size on combo break");

        /// <summary>
        /// "The flashlight keeps shrinking as if no combo break happened."
        /// </summary>
        public static LocalisableString FlashlightIgnoreComboBreaksDescription => new TranslatableString(getKey(@"flashlight_ignore_combo_breaks_description"), @"The flashlight keeps shrinking as if no combo break happened.");

        /// <summary>
        /// "Instant combo counter"
        /// </summary>
        public static LocalisableString InstantComboCounter => new TranslatableString(getKey(@"instant_combo_counter"), @"Instant combo counter");

        /// <summary>
        /// "Disables the combo counter increase and decrease animations."
        /// </summary>
        public static LocalisableString InstantComboCounterDescription => new TranslatableString(getKey(@"instant_combo_counter_description"), @"Disables the combo counter increase and decrease animations.");

        /// <summary>
        /// "Instant score and accuracy display"
        /// </summary>
        public static LocalisableString InstantScoreDisplay => new TranslatableString(getKey(@"instant_score_display"), @"Instant score and accuracy display");

        /// <summary>
        /// "Disables the score and accuracy increase and decrease animations."
        /// </summary>
        public static LocalisableString InstantScoreDisplayDescription => new TranslatableString(getKey(@"instant_score_display_description"), @"Disables the score and accuracy increase and decrease animations.");

        /// <summary>
        /// "Fast background dim"
        /// </summary>
        public static LocalisableString FastBackgroundDim => new TranslatableString(getKey(@"fast_background_dim"), @"Fast background dim");

        /// <summary>
        /// "Makes background dim changes much faster."
        /// </summary>
        public static LocalisableString FastBackgroundDimDescription => new TranslatableString(getKey(@"fast_background_dim_description"), @"Makes background dim changes much faster.");

        /// <summary>
        /// "Cursor trail style"
        /// </summary>
        public static LocalisableString CursorTrailStyle => new TranslatableString(getKey(@"cursor_trail_style"), @"Cursor trail style");

        /// <summary>
        /// "Disabled"
        /// </summary>
        public static LocalisableString CursorTrailStyleDisabled => new TranslatableString(getKey(@"cursor_trail_style_disabled"), @"Disabled");

        /// <summary>
        /// "Default"
        /// </summary>
        public static LocalisableString CursorTrailStyleDefault => new TranslatableString(getKey(@"cursor_trail_style_default"), @"Default");

        /// <summary>
        /// "SDF"
        /// </summary>
        public static LocalisableString CursorTrailStyleSdf => new TranslatableString(getKey(@"cursor_trail_style_sdf"), @"SDF");

        /// <summary>
        /// "Atrias"
        /// </summary>
        public static LocalisableString CursorTrailStyleAtrias => new TranslatableString(getKey(@"cursor_trail_style_atrias"), @"Atrias");

        /// <summary>
        /// "Smoother cursor trail"
        /// </summary>
        public static LocalisableString SmootherCursorTrail => new TranslatableString(getKey(@"smoother_cursor_trail"), @"Smoother cursor trail");

        /// <summary>
        /// "Halves the required movement to make new trail points."
        /// </summary>
        public static LocalisableString SmootherCursorTrailDescription => new TranslatableString(getKey(@"smoother_cursor_trail_description"), @"Halves the required movement to make new trail points.");

        /// <summary>
        /// "Mania judgement counter"
        /// </summary>
        public static LocalisableString ManiaJudgementCounter => new TranslatableString(getKey(@"mania_judgement_counter"), @"Mania judgement counter");

        /// <summary>
        /// "Disabled"
        /// </summary>
        public static LocalisableString ManiaJudgementCounterDisabled => new TranslatableString(getKey(@"mania_judgement_counter_disabled"), @"Disabled");

        /// <summary>
        /// "Bottom left"
        /// </summary>
        public static LocalisableString ManiaJudgementCounterBottomLeft => new TranslatableString(getKey(@"mania_judgement_counter_bottom_left"), @"Bottom left");

        /// <summary>
        /// "Stage left"
        /// </summary>
        public static LocalisableString ManiaJudgementCounterStageLeft => new TranslatableString(getKey(@"mania_judgement_counter_stage_left"), @"Stage left");

        /// <summary>
        /// "Disable Mania judgement animations"
        /// </summary>
        public static LocalisableString NoManiaJudgementAnimations => new TranslatableString(getKey(@"no_mania_judgement_animations"), @"Disable Mania judgement animations");

        /// <summary>
        /// "Removes the scaling animation from Mania judgements"
        /// </summary>
        public static LocalisableString NoManiaJudgementAnimationsDescription => new TranslatableString(getKey(@"no_mania_judgement_animations_description"), @"Removes the scaling animation from Mania judgements");

        /// <summary>
        /// "Disable Taiko judgement animations"
        /// </summary>
        public static LocalisableString NoTaikoJudgementAnimations => new TranslatableString(getKey(@"no_taiko_judgement_animations"), @"Disable Taiko judgement animations");

        /// <summary>
        /// "Removes the scaling animation from Taiko judgements"
        /// </summary>
        public static LocalisableString NoTaikoJudgementAnimationsDescription => new TranslatableString(getKey(@"no_taiko_judgement_animations_description"), @"Removes the scaling animation from Taiko judgements");

        /// <summary>
        /// "Skins"
        /// </summary>
        public static LocalisableString SkinsHeader => new TranslatableString(getKey(@"skins_header"), @"Skins");

        /// <summary>
        /// "Taiko skin"
        /// </summary>
        public static LocalisableString TaikoSkin => new TranslatableString(getKey(@"taiko_skin"), @"Taiko skin");

        /// <summary>
        /// "Catch skin"
        /// </summary>
        public static LocalisableString CatchSkin => new TranslatableString(getKey(@"catch_skin"), @"Catch skin");

        /// <summary>
        /// "Mania skin"
        /// </summary>
        public static LocalisableString ManiaSkin => new TranslatableString(getKey(@"mania_skin"), @"Mania skin");

        /// <summary>
        /// "&lt;Use main skin&gt;"
        /// </summary>
        public static LocalisableString UseMainSkin => new TranslatableString(getKey(@"use_main_skin"), @"<Use main skin>");

        /// <summary>
        /// "Only autosave maps you own"
        /// </summary>
        public static LocalisableString OnlyAutosaveOwnMaps => new TranslatableString(getKey(@"only_autosave_own_maps"), @"Only autosave maps you own");

        /// <summary>
        /// "Only autosaves maps if you made them or if your name is in the diff name. Also includes any names listed as a My Maps alias"
        /// </summary>
        public static LocalisableString OnlyAutosaveOwnMapsDescription => new TranslatableString(getKey(@"only_autosave_own_maps_description"), @"Only autosaves maps if you made them or if your name is in the diff name. Also includes any names listed as a My Maps alias");

        /// <summary>
        /// "Write autosaves directly to backups"
        /// </summary>
        public static LocalisableString AutosaveToBackups => new TranslatableString(getKey(@"autosave_to_backups"), @"Write autosaves directly to backups");

        /// <summary>
        /// "Instead of autosaves overwriting the main map file, this writes them to the Backups folder instead"
        /// </summary>
        public static LocalisableString AutosaveToBackupsDescription => new TranslatableString(getKey(@"autosave_to_backups_description"), @"Instead of autosaves overwriting the main map file, this writes them to the Backups folder instead");

        /// <summary>
        /// "My Maps aliases (comma-separated)"
        /// </summary>
        public static LocalisableString MyMapsAliases => new TranslatableString(getKey(@"my_maps_aliases"), @"My Maps aliases (comma-separated)");

        /// <summary>
        /// "Never automatically insert breaks"
        /// </summary>
        public static LocalisableString NoAutomaticBreaks => new TranslatableString(getKey(@"no_automatic_breaks"), @"Never automatically insert breaks");

        /// <summary>
        /// "Disables the editor automatically adding breaks into long gaps"
        /// </summary>
        public static LocalisableString NoAutomaticBreaksDescription => new TranslatableString(getKey(@"no_automatic_breaks_description"), @"Disables the editor automatically adding breaks into long gaps");

        /// <summary>
        /// "Draw screen borders"
        /// </summary>
        public static LocalisableString DrawScreenBorders => new TranslatableString(getKey(@"draw_screen_borders"), @"Draw screen borders");

        /// <summary>
        /// "Shows the bounds of the screen relative to the playfield"
        /// </summary>
        public static LocalisableString DrawScreenBordersDescription => new TranslatableString(getKey(@"draw_screen_borders_description"), @"Shows the bounds of the screen relative to the playfield");

        /// <summary>
        /// "Always hide FPS"
        /// </summary>
        public static LocalisableString AlwaysHideFps => new TranslatableString(getKey(@"always_hide_fps"), @"Always hide FPS");

        /// <summary>
        /// "Always hides the FPS counter in the editor, even if it's enabled everywhere else"
        /// </summary>
        public static LocalisableString AlwaysHideFpsDescription => new TranslatableString(getKey(@"always_hide_fps_description"), @"Always hides the FPS counter in the editor, even if it's enabled everywhere else");

        /// <summary>
        /// "Show star count in difficulty selector"
        /// </summary>
        public static LocalisableString DifficultyStars => new TranslatableString(getKey(@"difficulty_stars"), @"Show star count in difficulty selector");

        /// <summary>
        /// "Shows the star count for each difficulty in the difficulty switcher"
        /// </summary>
        public static LocalisableString DifficultyStarsDescription => new TranslatableString(getKey(@"difficulty_stars_description"), @"Shows the star count for each difficulty in the difficulty switcher");

        /// <summary>
        /// "Always ignore beatmap hitsounds"
        /// </summary>
        public static LocalisableString AlwaysIgnoreBeatmapSamples => new TranslatableString(getKey(@"always_ignore_beatmap_samples"), @"Always ignore beatmap hitsounds");

        /// <summary>
        /// "Forces the editor to use your skin's samples. Should only be used temporarily for most people"
        /// </summary>
        public static LocalisableString AlwaysIgnoreBeatmapSamplesDescription => new TranslatableString(getKey(@"always_ignore_beatmap_samples_description"), @"Forces the editor to use your skin's samples. Should only be used temporarily for most people");

        /// <summary>
        /// "Bezier point insertion preview"
        /// </summary>
        public static LocalisableString PointInsertPreview => new TranslatableString(getKey(@"point_insert_preview"), @"Bezier point insertion preview");

        /// <summary>
        /// "Preview the insertion order of a new control point and the new path"
        /// </summary>
        public static LocalisableString PointInsertPreviewDescription => new TranslatableString(getKey(@"point_insert_preview_description"), @"Preview the insertion order of a new control point and the new path");

        /// <summary>
        /// "Slider curve color"
        /// </summary>
        public static LocalisableString SliderCurveColour => new TranslatableString(getKey(@"slider_curve_colour"), @"Slider curve color");

        /// <summary>
        /// "Disabled"
        /// </summary>
        public static LocalisableString SliderCurveColourDisabled => new TranslatableString(getKey(@"slider_curve_colour_disabled"), @"Disabled");

        /// <summary>
        /// "Anchor"
        /// </summary>
        public static LocalisableString SliderCurveColourAnchor => new TranslatableString(getKey(@"slider_curve_colour_anchor"), @"Anchor");

        /// <summary>
        /// "Line"
        /// </summary>
        public static LocalisableString SliderCurveColourLine => new TranslatableString(getKey(@"slider_curve_colour_line"), @"Line");

        /// <summary>
        /// "Anchor and line"
        /// </summary>
        public static LocalisableString SliderCurveColourAnchorAndLine => new TranslatableString(getKey(@"slider_curve_colour_anchor_and_line"), @"Anchor and line");

        /// <summary>
        /// "Disable Alt for distance snap"
        /// </summary>
        public static LocalisableString DisableAltDistanceSnap => new TranslatableString(getKey(@"disable_alt_distance_snap"), @"Disable Alt for distance snap");

        /// <summary>
        /// "Disables holding Alt to temporarily toggle distance snap"
        /// </summary>
        public static LocalisableString DisableAltDistanceSnapDescription => new TranslatableString(getKey(@"disable_alt_distance_snap_description"), @"Disables holding Alt to temporarily toggle distance snap");

        /// <summary>
        /// "Select slider hitsound on first click"
        /// </summary>
        public static LocalisableString SliderPartOnFirstClick => new TranslatableString(getKey(@"slider_part_on_first_click"), @"Select slider hitsound on first click");

        /// <summary>
        /// "Clicking the head or tail of an unselected slider directly selects that part for hitsounding"
        /// </summary>
        public static LocalisableString SliderPartOnFirstClickDescription => new TranslatableString(getKey(@"slider_part_on_first_click_description"), @"Clicking the head or tail of an unselected slider directly selects that part for hitsounding");

        /// <summary>
        /// "Don't stretch audio when changing speed"
        /// </summary>
        public static LocalisableString EditorNoAudioStretch => new TranslatableString(getKey(@"editor_no_audio_stretch"), @"Don't stretch audio when changing speed");

        /// <summary>
        /// "Allows audio pitch changes in order to make timing more precise"
        /// </summary>
        public static LocalisableString EditorNoAudioStretchDescription => new TranslatableString(getKey(@"editor_no_audio_stretch_description"), @"Allows audio pitch changes in order to make timing more precise");

        /// <summary>
        /// "Preview mods"
        /// </summary>
        public static LocalisableString EditorPreviewMods => new TranslatableString(getKey(@"editor_preview_mods"), @"Preview mods");

        /// <summary>
        /// "Loads the map with the currently selected mods. Only supports DT/HT/HD for now"
        /// </summary>
        public static LocalisableString EditorPreviewModsDescription => new TranslatableString(getKey(@"editor_preview_mods_description"), @"Loads the map with the currently selected mods. Only supports DT/HT/HD for now");

        /// <summary>
        /// "Show play mode song select buttons"
        /// </summary>
        public static LocalisableString EditorSongSelectShowPlayButtons => new TranslatableString(getKey(@"editor_song_select_show_play_buttons"), @"Show play mode song select buttons");

        /// <summary>
        /// "Shows the mods, random and options buttons in the editor song select. Useful together with preview mods."
        /// </summary>
        public static LocalisableString EditorSongSelectShowPlayButtonsDescription => new TranslatableString(getKey(@"editor_song_select_show_play_buttons_description"), @"Shows the mods, random and options buttons in the editor song select. Useful together with preview mods.");

        /// <summary>
        /// "Show waveform under Mania playfield"
        /// </summary>
        public static LocalisableString EditorManiaPlayfieldWaveform => new TranslatableString(getKey(@"editor_mania_playfield_waveform"), @"Show waveform under Mania playfield");

        /// <summary>
        /// "Requires the timeline waveform to be visible. Shows the waveform under the playfield instead of the timeline in Mania compose mode"
        /// </summary>
        public static LocalisableString EditorManiaPlayfieldWaveformDescription => new TranslatableString(getKey(@"editor_mania_playfield_waveform_description"), @"Requires the timeline waveform to be visible. Shows the waveform under the playfield instead of the timeline in Mania compose mode");

        /// <summary>
        /// "Show autoplay cursor"
        /// </summary>
        public static LocalisableString EditorShowAutoplayCursor => new TranslatableString(getKey(@"editor_show_autoplay_cursor"), @"Show autoplay cursor");

        /// <summary>
        /// "Shows the cursor of the autoplay replay on the playfield. Only supported in osu!"
        /// </summary>
        public static LocalisableString EditorShowAutoplayCursorDescription => new TranslatableString(getKey(@"editor_show_autoplay_cursor_description"), @"Shows the cursor of the autoplay replay on the playfield. Only supported in osu!");

        /// <summary>
        /// "Autoplay cursor skin"
        /// </summary>
        public static LocalisableString EditorAutoplayCursorSkin => new TranslatableString(getKey(@"editor_autoplay_cursor_skin"), @"Autoplay cursor skin");

        /// <summary>
        /// "Skin whose cursor is used for the autoplay cursor in the editor."
        /// </summary>
        public static LocalisableString EditorAutoplayCursorSkinDescription => new TranslatableString(getKey(@"editor_autoplay_cursor_skin_description"), @"Skin whose cursor is used for the autoplay cursor in the editor.");

        /// <summary>
        /// "Same as editor skin"
        /// </summary>
        public static LocalisableString SameAsEditorSkin => new TranslatableString(getKey(@"same_as_editor_skin"), @"Same as editor skin");

        /// <summary>
        /// "Editor autoplay engine"
        /// </summary>
        public static LocalisableString EditorAutoplayEngine => new TranslatableString(getKey(@"editor_autoplay_engine"), @"Editor autoplay engine");

        /// <summary>
        /// "The autoplay engine which plays the map in the editor. Realistic moves the cursor like a player would, cutting corners on sliders"
        /// </summary>
        public static LocalisableString EditorAutoplayEngineDescription => new TranslatableString(getKey(@"editor_autoplay_engine_description"), @"The autoplay engine which plays the map in the editor. Realistic moves the cursor like a player would, cutting corners on sliders");

        /// <summary>
        /// "Text editor"
        /// </summary>
        public static LocalisableString EditorTextEditor => new TranslatableString(getKey(@"editor_text_editor"), @"Text editor");

        /// <summary>
        /// "The program used by "Open .osu in text editor" in the editor's file menu"
        /// </summary>
        public static LocalisableString EditorTextEditorDescription => new TranslatableString(getKey(@"editor_text_editor_description"), @"The program used by ""Open .osu in text editor"" in the editor's file menu");

        /// <summary>
        /// "Open .osu in text editor"
        /// </summary>
        public static LocalisableString OpenInTextEditor => new TranslatableString(getKey(@"open_in_text_editor"), @"Open .osu in text editor");

        /// <summary>
        /// "Minimal replay frames"
        /// </summary>
        public static LocalisableString MinimalReplayFrames => new TranslatableString(getKey(@"minimal_replay_frames"), @"Minimal replay frames");

        /// <summary>
        /// "Records the bare minimum amount of frames in replays"
        /// </summary>
        public static LocalisableString MinimalReplayFramesDescription => new TranslatableString(getKey(@"minimal_replay_frames_description"), @"Records the bare minimum amount of frames in replays");

        /// <summary>
        /// "User interface"
        /// </summary>
        public static LocalisableString UserInterfaceHeader => new TranslatableString(getKey(@"user_interface_header"), @"User interface");

        /// <summary>
        /// "Skip intro"
        /// </summary>
        public static LocalisableString SkipIntro => new TranslatableString(getKey(@"skip_intro"), @"Skip intro");

        /// <summary>
        /// "Jumps directly to the menu on startup"
        /// </summary>
        public static LocalisableString SkipIntroDescription => new TranslatableString(getKey(@"skip_intro_description"), @"Jumps directly to the menu on startup");

        /// <summary>
        /// "Instant menu navigation"
        /// </summary>
        public static LocalisableString InstantScreenTransitions => new TranslatableString(getKey(@"instant_screen_transitions"), @"Instant menu navigation");

        /// <summary>
        /// "Disables fade transitions between screens"
        /// </summary>
        public static LocalisableString InstantScreenTransitionsDescription => new TranslatableString(getKey(@"instant_screen_transitions_description"), @"Disables fade transitions between screens");

        /// <summary>
        /// "Backup beatmaps on update"
        /// </summary>
        public static LocalisableString BackupOnUpdate => new TranslatableString(getKey(@"backup_on_update"), @"Backup beatmaps on update");

        /// <summary>
        /// "Backs up the .osu files when updating a map"
        /// </summary>
        public static LocalisableString BackupOnUpdateDescription => new TranslatableString(getKey(@"backup_on_update_description"), @"Backs up the .osu files when updating a map");

        /// <summary>
        /// "Custom colours"
        /// </summary>
        public static LocalisableString CustomColoursHeader => new TranslatableString(getKey(@"custom_colours_header"), @"Custom colours");

        /// <summary>
        /// "Strain graph low"
        /// </summary>
        public static LocalisableString StrainGraphLowColour => new TranslatableString(getKey(@"strain_graph_low_colour"), @"Strain graph low");

        /// <summary>
        /// "Strain graph high"
        /// </summary>
        public static LocalisableString StrainGraphHighColour => new TranslatableString(getKey(@"strain_graph_high_colour"), @"Strain graph high");

        /// <summary>
        /// "Strain graph active alpha"
        /// </summary>
        public static LocalisableString StrainGraphActiveAlpha => new TranslatableString(getKey(@"strain_graph_active_alpha"), @"Strain graph active alpha");

        /// <summary>
        /// "Strain graph inactive alpha"
        /// </summary>
        public static LocalisableString StrainGraphInactiveAlpha => new TranslatableString(getKey(@"strain_graph_inactive_alpha"), @"Strain graph inactive alpha");

        /// <summary>
        /// "Mania snap 1/2"
        /// </summary>
        public static LocalisableString ManiaSnapColour2 => new TranslatableString(getKey(@"mania_snap_colour_2"), @"Mania snap 1/2");

        /// <summary>
        /// "Mania snap 1/3"
        /// </summary>
        public static LocalisableString ManiaSnapColour3 => new TranslatableString(getKey(@"mania_snap_colour_3"), @"Mania snap 1/3");

        /// <summary>
        /// "Mania snap 1/4"
        /// </summary>
        public static LocalisableString ManiaSnapColour4 => new TranslatableString(getKey(@"mania_snap_colour_4"), @"Mania snap 1/4");

        /// <summary>
        /// "Mania snap 1/6"
        /// </summary>
        public static LocalisableString ManiaSnapColour6 => new TranslatableString(getKey(@"mania_snap_colour_6"), @"Mania snap 1/6");

        /// <summary>
        /// "Mania snap 1/8"
        /// </summary>
        public static LocalisableString ManiaSnapColour8 => new TranslatableString(getKey(@"mania_snap_colour_8"), @"Mania snap 1/8");

        /// <summary>
        /// "Mania snap 1/12"
        /// </summary>
        public static LocalisableString ManiaSnapColour12 => new TranslatableString(getKey(@"mania_snap_colour_12"), @"Mania snap 1/12");

        /// <summary>
        /// "Mania snap 1/16"
        /// </summary>
        public static LocalisableString ManiaSnapColour16 => new TranslatableString(getKey(@"mania_snap_colour_16"), @"Mania snap 1/16");

        /// <summary>
        /// "Mania snap other"
        /// </summary>
        public static LocalisableString ManiaSnapColourOther => new TranslatableString(getKey(@"mania_snap_colour_other"), @"Mania snap other");

        /// <summary>
        /// "Used by timing-based note colouring"
        /// </summary>
        public static LocalisableString ManiaSnapColourDescription => new TranslatableString(getKey(@"mania_snap_colour_description"), @"Used by timing-based note colouring");

        /// <summary>
        /// "Input overlay key K1"
        /// </summary>
        public static LocalisableString KeyOverlayColourK1 => new TranslatableString(getKey(@"key_overlay_colour_k1"), @"Input overlay key K1");

        /// <summary>
        /// "Input overlay key K2"
        /// </summary>
        public static LocalisableString KeyOverlayColourK2 => new TranslatableString(getKey(@"key_overlay_colour_k2"), @"Input overlay key K2");

        /// <summary>
        /// "Input overlay key M1"
        /// </summary>
        public static LocalisableString KeyOverlayColourM1 => new TranslatableString(getKey(@"key_overlay_colour_m1"), @"Input overlay key M1");

        /// <summary>
        /// "Input overlay key M2"
        /// </summary>
        public static LocalisableString KeyOverlayColourM2 => new TranslatableString(getKey(@"key_overlay_colour_m2"), @"Input overlay key M2");

        /// <summary>
        /// "Autoplay"
        /// </summary>
        public static LocalisableString AutoplayHeader => new TranslatableString(getKey(@"autoplay_header"), @"Autoplay");

        /// <summary>
        /// "Autoplay engine"
        /// </summary>
        public static LocalisableString AutoplayEngine => new TranslatableString(getKey(@"autoplay_engine"), @"Autoplay engine");

        /// <summary>
        /// "Auto+ settings only apply when the Auto+ engine is selected"
        /// </summary>
        public static LocalisableString AutoplayEngineDescription => new TranslatableString(getKey(@"autoplay_engine_description"), @"Auto+ settings only apply when the Auto+ engine is selected");

        /// <summary>
        /// "Save Autoplay replays"
        /// </summary>
        public static LocalisableString SaveAutoplayReplays => new TranslatableString(getKey(@"save_autoplay_replays"), @"Save Autoplay replays");

        /// <summary>
        /// "Follow sliders"
        /// </summary>
        public static LocalisableString AutoPlusFollowSliders => new TranslatableString(getKey(@"auto_plus_follow_sliders"), @"Follow sliders");

        /// <summary>
        /// "Makes Auto+ follow sliders."
        /// </summary>
        public static LocalisableString AutoPlusFollowSlidersDescription => new TranslatableString(getKey(@"auto_plus_follow_sliders_description"), @"Makes Auto+ follow sliders.");

        /// <summary>
        /// "Follow slider ends"
        /// </summary>
        public static LocalisableString AutoPlusFollowSliderEnds => new TranslatableString(getKey(@"auto_plus_follow_slider_ends"), @"Follow slider ends");

        /// <summary>
        /// "Makes Auto+ follow visible slider ends."
        /// </summary>
        public static LocalisableString AutoPlusFollowSliderEndsDescription => new TranslatableString(getKey(@"auto_plus_follow_slider_ends_description"), @"Makes Auto+ follow visible slider ends.");

        /// <summary>
        /// "Use rotation force"
        /// </summary>
        public static LocalisableString AutoPlusRotation => new TranslatableString(getKey(@"auto_plus_rotation"), @"Use rotation force");

        /// <summary>
        /// "From osu!fx. Makes the cursor rotate around the points more, best with disabled angular stream interpolation"
        /// </summary>
        public static LocalisableString AutoPlusRotationDescription => new TranslatableString(getKey(@"auto_plus_rotation_description"), @"From osu!fx. Makes the cursor rotate around the points more, best with disabled angular stream interpolation");

        /// <summary>
        /// "Bounce off border"
        /// </summary>
        public static LocalisableString AutoPlusBounceBorder => new TranslatableString(getKey(@"auto_plus_bounce_border"), @"Bounce off border");

        /// <summary>
        /// "From osu!fx. Reverse cursor position when going offscreen"
        /// </summary>
        public static LocalisableString AutoPlusBounceBorderDescription => new TranslatableString(getKey(@"auto_plus_bounce_border_description"), @"From osu!fx. Reverse cursor position when going offscreen");

        /// <summary>
        /// "Alternate buttons"
        /// </summary>
        public static LocalisableString AutoPlusAlternateButtons => new TranslatableString(getKey(@"auto_plus_alternate_buttons"), @"Alternate buttons");

        /// <summary>
        /// "Use cubic spline interpolation"
        /// </summary>
        public static LocalisableString AutoPlusCubicSplineInterpolation => new TranslatableString(getKey(@"auto_plus_cubic_spline_interpolation"), @"Use cubic spline interpolation");

        /// <summary>
        /// "Use cubic spline interpolation for cursor dancing."
        /// </summary>
        public static LocalisableString AutoPlusCubicSplineInterpolationDescription => new TranslatableString(getKey(@"auto_plus_cubic_spline_interpolation_description"), @"Use cubic spline interpolation for cursor dancing.");

        /// <summary>
        /// "Use half-circle interpolation"
        /// </summary>
        public static LocalisableString AutoPlusHalfCircleInterpolation => new TranslatableString(getKey(@"auto_plus_half_circle_interpolation"), @"Use half-circle interpolation");

        /// <summary>
        /// "Use half-circle stream interpolation"
        /// </summary>
        public static LocalisableString AutoPlusHalfCircleStreamInterpolation => new TranslatableString(getKey(@"auto_plus_half_circle_stream_interpolation"), @"Use half-circle stream interpolation");

        /// <summary>
        /// "Use angular stream interpolation"
        /// </summary>
        public static LocalisableString AutoPlusAngularStreamInterpolation => new TranslatableString(getKey(@"auto_plus_angular_stream_interpolation"), @"Use angular stream interpolation");

        /// <summary>
        /// "Interpolation frequency"
        /// </summary>
        public static LocalisableString AutoPlusInterpolationFrequency => new TranslatableString(getKey(@"auto_plus_interpolation_frequency"), @"Interpolation frequency");

        /// <summary>
        /// "Slider speed factor"
        /// </summary>
        public static LocalisableString AutoPlusSliderSpeedFactor => new TranslatableString(getKey(@"auto_plus_slider_speed_factor"), @"Slider speed factor");

        /// <summary>
        /// "Stream speed factor"
        /// </summary>
        public static LocalisableString AutoPlusStreamSpeedFactor => new TranslatableString(getKey(@"auto_plus_stream_speed_factor"), @"Stream speed factor");

        /// <summary>
        /// "Stream half-circle border angle"
        /// </summary>
        public static LocalisableString AutoPlusStreamHalfCircleBorderAngle => new TranslatableString(getKey(@"auto_plus_stream_half_circle_border_angle"), @"Stream half-circle border angle");

        /// <summary>
        /// "(0, 0) interpolation"
        /// </summary>
        public static LocalisableString AutoPlusZeroInterpolation => new TranslatableString(getKey(@"auto_plus_zero_interpolation"), @"(0, 0) interpolation");

        /// <summary>
        /// "Put the cursor to the top-left corner of the screen whenever possible."
        /// </summary>
        public static LocalisableString AutoPlusZeroInterpolationDescription => new TranslatableString(getKey(@"auto_plus_zero_interpolation_description"), @"Put the cursor to the top-left corner of the screen whenever possible.");

        /// <summary>
        /// "TAG mode"
        /// </summary>
        public static LocalisableString AutoPlusTag => new TranslatableString(getKey(@"auto_plus_tag"), @"TAG mode");

        /// <summary>
        /// "Creates multiple cursors for Auto+ to simulate TAG multiplayer"
        /// </summary>
        public static LocalisableString AutoPlusTagDescription => new TranslatableString(getKey(@"auto_plus_tag_description"), @"Creates multiple cursors for Auto+ to simulate TAG multiplayer");

        /// <summary>
        /// "Cursor count"
        /// </summary>
        public static LocalisableString AutoPlusTagCount => new TranslatableString(getKey(@"auto_plus_tag_count"), @"Cursor count");

        /// <summary>
        /// "0 to use the combo colour count"
        /// </summary>
        public static LocalisableString AutoPlusTagCountDescription => new TranslatableString(getKey(@"auto_plus_tag_count_description"), @"0 to use the combo colour count");

        /// <summary>
        /// "Split objects by"
        /// </summary>
        public static LocalisableString AutoPlusTagSplit => new TranslatableString(getKey(@"auto_plus_tag_split"), @"Split objects by");

        /// <summary>
        /// "Cursor colours"
        /// </summary>
        public static LocalisableString AutoPlusTagColours => new TranslatableString(getKey(@"auto_plus_tag_colours"), @"Cursor colours");

        /// <summary>
        /// "Allow opening overlays during gameplay"
        /// </summary>
        public static LocalisableString AllowOverlaysDuringGameplay => new TranslatableString(getKey(@"allow_overlays_during_gameplay"), @"Allow opening overlays during gameplay");

        /// <summary>
        /// "The settings, chat and other overlays can be opened with their hotkeys while playing, without pausing first."
        /// </summary>
        public static LocalisableString AllowOverlaysDuringGameplayDescription => new TranslatableString(getKey(@"allow_overlays_during_gameplay_description"), @"The settings, chat and other overlays can be opened with their hotkeys while playing, without pausing first.");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
