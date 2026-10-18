// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// The title next to the ring of the <see cref="VisualizerStyle.Ncs"/> visualizer.
    /// </summary>
    public enum NcsTitle
    {
        /// <summary>
        /// The name of this client.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.VisualizerBrandingSlop))]
        Slop,

        /// <summary>
        /// "NCS", in the style of the NoCopyrightSounds channel.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.VisualizerStyleNcs))]
        Ncs,
    }
}
