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

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
