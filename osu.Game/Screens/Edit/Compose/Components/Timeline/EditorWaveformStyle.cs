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
        /// The waveforms of the low (red), mid (green) and high (blue) frequency bands, where they overlap they are white.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.WaveformStyleThreeBand))]
        ThreeBand,

        /// <summary>
        /// A waveform which isn't normalised, coloured by its frequency content: low frequencies red, mid frequencies green and high frequencies blue.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.WaveformStyleSpectral))]
        Spectral,

        /// <summary>
        /// Where notes and hits start instead of how loud the track is, which stays readable for loud, compressed songs.
        /// The peaks are displayed where timing points snap to in the timing screen.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.WaveformStyleOnsets))]
        Onsets,
    }
}
