// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// The logo in the centre of the <see cref="VisualizerStyle.TrapNation"/> visualizer.
    /// </summary>
    public enum TrapNationLogo
    {
        /// <summary>
        /// The logo of this client.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.VisualizerBrandingSlop))]
        Slop,

        /// <summary>
        /// A triangle emblem in the style of the Trap Nation channel.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.VisualizerStyleTrapNation))]
        TrapNation,
    }
}
