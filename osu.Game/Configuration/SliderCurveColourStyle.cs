// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// How the curve types of slider paths are highlighted in the editor.
    /// </summary>
    public enum SliderCurveColourStyle
    {
        /// <summary>
        /// Curve types are not highlighted.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.SliderCurveColourDisabled))]
        Disabled,

        /// <summary>
        /// Control points are coloured by the curve type of their segment.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.SliderCurveColourAnchor))]
        Anchor,

        /// <summary>
        /// The lines between control points are coloured by the curve type of their segment.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.SliderCurveColourLine))]
        Line,
    }
}
