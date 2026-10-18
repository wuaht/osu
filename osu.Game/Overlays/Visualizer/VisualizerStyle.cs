// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// The visualizer displayed by the <see cref="VisualizerOverlay"/>.
    /// </summary>
    public enum VisualizerStyle
    {
        /// <summary>
        /// A pulsing logo surrounded by a mirrored, layered spectrum and floating particles.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.VisualizerStyleTrapNation))]
        TrapNation,

        /// <summary>
        /// A ring of glowing dots which is distorted by noise and breathes with the music, next to a title.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.VisualizerStyleNcs))]
        Ncs,
    }
}
