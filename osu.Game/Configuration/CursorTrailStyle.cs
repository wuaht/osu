// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// The style used to draw the gameplay cursor trail.
    /// </summary>
    public enum CursorTrailStyle
    {
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.CursorTrailStyleDisabled))]
        Disabled,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.CursorTrailStyleDefault))]
        Default,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.CursorTrailStyleSdf))]
        SDF,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.CursorTrailStyleAtrias))]
        Atrias,
    }
}
