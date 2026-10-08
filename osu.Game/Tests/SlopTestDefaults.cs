// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;

namespace osu.Game.Tests
{
    /// <summary>
    /// Configuration applied to test games, so that upstream tests run against upstream behaviour.
    /// </summary>
    public static class SlopTestDefaults
    {
        /// <summary>
        /// slop! hides some editor elements by default, which upstream tests rely on being visible.
        /// It also adds snap points, which objects placed by upstream tests may unintentionally snap to.
        /// </summary>
        public static void ApplyUpstreamEditorBehaviour(OsuConfigManager config)
        {
            config.SetValue(OsuSetting.SlopEditorShowSelectionBox, true);
            config.SetValue(OsuSetting.SlopEditorShowSelectionBoxButtons, true);
            config.SetValue(OsuSetting.SlopEditorShowSliderEndDragMarker, true);
            config.SetValue(OsuSetting.SlopEditorVisualSpacingSnap, false);
            config.SetValue(OsuSetting.SlopEditorBlanketSnap, false);
            config.SetValue(OsuSetting.SlopEditorLineSnap, false);
        }
    }
}
