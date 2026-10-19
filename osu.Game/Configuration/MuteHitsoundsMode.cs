// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// When hitsounds should be muted during gameplay.
    /// </summary>
    public enum MuteHitsoundsMode
    {
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MuteHitsoundsNever))]
        Never,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MuteHitsoundsManiaOnly))]
        ManiaOnly,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MuteHitsoundsAlways))]
        Always,
    }
}
