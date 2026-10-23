// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// Which divisors hit object nodes may be snapped to before they are marked as unsnapped in the timeline of the editor.
    /// </summary>
    public enum EditorUnsnapMode
    {
        /// <summary>
        /// The divisors of the unsnap divisors setting.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.UnsnapModeEnabled))]
        Enabled,

        /// <summary>
        /// Only the beat divisor currently selected in the editor.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.UnsnapModeCurrentDivisor))]
        CurrentDivisor,

        /// <summary>
        /// Nodes are never marked as unsnapped.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.UnsnapModeDisabled))]
        Disabled,
    }
}
