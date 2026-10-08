// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// How the waveform is displayed in the timeline of the editor.
    /// </summary>
    public enum EditorWaveformStyle
    {
        /// <summary>
        /// The waveform of osu!(lazer).
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.WaveformStyleDefault))]
        Default,

        /// <summary>
        /// A white waveform.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.WaveformStyleSimple))]
        Simple,

        /// <summary>
        /// A white waveform with the low (red), mid (green) and high (blue) frequency bands behind it.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.WaveformStyleThreeBand))]
        ThreeBand,

        /// <summary>
        /// A waveform coloured by its frequency content: low frequencies red, mid frequencies green and high frequencies blue.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.WaveformStyleSpectral))]
        Spectral,
    }
}
