// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// When beatmap sample volumes should be ignored during gameplay.
    /// </summary>
    public enum IgnoreBeatmapVolumeMode
    {
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.IgnoreBeatmapVolumeNever))]
        Never,

        /// <summary>
        /// Only ignore the beatmap volume when beatmap hitsounds are disabled.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.IgnoreBeatmapVolumeIfHitsoundsDisabled))]
        IfHitsoundsDisabled,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.IgnoreBeatmapVolumeAlways))]
        Always,
    }
}
