// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// Turns raw FFT magnitudes into perceptually scaled, smoothed values suitable for driving visualizers.
    /// </summary>
    /// <remarks>
    /// All smoothing is time-based, so the output does not depend on the frame rate the analyser is updated at.
    /// Levels are normalised against an adaptive peak, so quiet and loud tracks both use the full output range.
    /// </remarks>
    public class SpectrumAnalyser
    {
        /// <summary>
        /// The first (fractional) FFT bin covered by <see cref="Bands"/>. With a 512 sample FFT at 44.1 kHz this is roughly 43 Hz.
        /// </summary>
        public const float FIRST_BIN = 0.5f;

        /// <summary>
        /// The last FFT bin covered by <see cref="Bands"/>. With a 512 sample FFT at 44.1 kHz this is roughly 9.5 kHz.
        /// </summary>
        public const float LAST_BIN = 110;

        /// <summary>
        /// The FFT bins (inclusive) which make up <see cref="Bass"/>.
        /// </summary>
        public const int BASS_BINS = 4;

        private const double band_attack_time = 15;
        private const double band_release_time = 120;
        private const double bass_attack_time = 10;
        private const double bass_release_time = 100;
        private const double loudness_time = 300;

        private const double peak_decay_time = 8000;
        private const float peak_floor = 0.02f;

        private const float band_range_db = 42;
        private const float bass_range_db = 24;
        private const float contrast = 1.5f;

        /// <summary>
        /// Magnitudes below this are considered silence.
        /// </summary>
        private const float noise_gate = 1e-4f;

        private const double beat_mean_time = 1000;
        private const double beat_refractory_time = 120;
        private const double beat_decay_time = 150;
        private const float beat_threshold_ratio = 1.3f;
        private const float beat_threshold_minimum = 0.08f;

        /// <summary>
        /// The number of output bands.
        /// </summary>
        public int BandCount => bands.Length;

        /// <summary>
        /// Smoothed band levels in the range [0, 1], logarithmically spaced from low to high frequencies.
        /// </summary>
        public ReadOnlySpan<float> Bands => bands;

        /// <summary>
        /// Smoothed bass level in the range [0, 1].
        /// </summary>
        public float Bass { get; private set; }

        /// <summary>
        /// Slowly smoothed overall loudness in the range [0, 1].
        /// </summary>
        public float Loudness { get; private set; }

        /// <summary>
        /// An impulse which jumps to 1 on every detected beat and decays back to 0 afterwards.
        /// </summary>
        public float Beat { get; private set; }

        /// <summary>
        /// Whether a beat was detected in the most recent <see cref="Update"/>.
        /// </summary>
        public bool BeatOnset { get; private set; }

        private readonly float[] bands;
        private readonly float[] bandStarts;
        private readonly float[] bandEnds;
        private readonly float[] bandTilts;
        private readonly float[] rawBands;

        private float bandPeak = peak_floor;
        private float bassPeak = peak_floor;

        private float bassMean;
        private double timeSinceBeat = double.PositiveInfinity;

        public SpectrumAnalyser(int bandCount)
        {
            if (bandCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(bandCount), bandCount, "At least one band is required.");

            bands = new float[bandCount];
            rawBands = new float[bandCount];
            bandStarts = new float[bandCount];
            bandEnds = new float[bandCount];
            bandTilts = new float[bandCount];

            float ratio = LAST_BIN / FIRST_BIN;

            for (int i = 0; i < bandCount; i++)
            {
                bandStarts[i] = FIRST_BIN * MathF.Pow(ratio, (float)i / bandCount);
                bandEnds[i] = FIRST_BIN * MathF.Pow(ratio, (float)(i + 1) / bandCount);

                // music has a falling spectrum; tilting by +3 dB per octave keeps the highs visible.
                float centre = MathF.Sqrt(bandStarts[i] * bandEnds[i]);
                bandTilts[i] = MathF.Sqrt(centre / FIRST_BIN);
            }
        }

        /// <summary>
        /// Resets all state to silence.
        /// </summary>
        public void Reset()
        {
            Array.Clear(bands);
            Bass = Loudness = Beat = 0;
            BeatOnset = false;
            bandPeak = bassPeak = peak_floor;
            bassMean = 0;
            timeSinceBeat = double.PositiveInfinity;
        }

        /// <summary>
        /// Advances the analysis.
        /// </summary>
        /// <param name="amplitudes">Linear FFT magnitudes, indexed by bin.</param>
        /// <param name="elapsed">The time since the last update, in milliseconds.</param>
        public void Update(ReadOnlySpan<float> amplitudes, double elapsed)
        {
            elapsed = Math.Max(0, elapsed);

            // levels

            float frameMax = 0;

            for (int i = 0; i < bands.Length; i++)
            {
                rawBands[i] = sampleBand(amplitudes, bandStarts[i], bandEnds[i]) * bandTilts[i];
                frameMax = Math.Max(frameMax, rawBands[i]);
            }

            bandPeak = trackPeak(bandPeak, frameMax, elapsed);

            float loudnessSum = 0;

            for (int i = 0; i < bands.Length; i++)
            {
                float target = normalise(rawBands[i], bandPeak, band_range_db);
                bands[i] = follow(bands[i], target, elapsed, band_attack_time, band_release_time);
                loudnessSum += target;
            }

            Loudness = interpolate(Loudness, loudnessSum / bands.Length, elapsed, loudness_time);

            // bass

            float rawBass = 0;
            for (int i = 0; i < Math.Min(BASS_BINS, amplitudes.Length); i++)
                rawBass = Math.Max(rawBass, amplitudes[i]);

            bassPeak = trackPeak(bassPeak, rawBass, elapsed);

            float bassTarget = normalise(rawBass, bassPeak, bass_range_db);
            Bass = follow(Bass, bassTarget, elapsed, bass_attack_time, bass_release_time);

            // beat

            timeSinceBeat += elapsed;

            BeatOnset = timeSinceBeat >= beat_refractory_time
                        && bassTarget > bassMean * beat_threshold_ratio
                        && bassTarget - bassMean > beat_threshold_minimum;

            bassMean = interpolate(bassMean, bassTarget, elapsed, beat_mean_time);

            if (BeatOnset)
            {
                timeSinceBeat = 0;
                Beat = 1;
            }
            else
                Beat *= (float)Math.Exp(-elapsed / beat_decay_time);
        }

        /// <summary>
        /// Samples the magnitude of the (fractional) bin range [<paramref name="start"/>, <paramref name="end"/>).
        /// </summary>
        private static float sampleBand(ReadOnlySpan<float> amplitudes, float start, float end)
        {
            if (amplitudes.Length == 0)
                return 0;

            // narrow bands (common at low frequencies) would otherwise repeat the same bin, so interpolate between neighbouring bins instead.
            if (end - start < 1)
            {
                float position = Math.Clamp((start + end) / 2, 0, amplitudes.Length - 1);
                int lower = (int)position;
                int upper = Math.Min(lower + 1, amplitudes.Length - 1);
                float t = position - lower;
                return amplitudes[lower] + (amplitudes[upper] - amplitudes[lower]) * t;
            }

            int first = Math.Clamp((int)MathF.Floor(start), 0, amplitudes.Length - 1);
            int last = Math.Clamp((int)MathF.Ceiling(end) - 1, first, amplitudes.Length - 1);

            float max = 0;
            for (int i = first; i <= last; i++)
                max = Math.Max(max, amplitudes[i]);

            return max;
        }

        /// <summary>
        /// Maps a magnitude to [0, 1] on a decibel scale spanning <paramref name="rangeDb"/> below <paramref name="peak"/>.
        /// </summary>
        private static float normalise(float value, float peak, float rangeDb)
        {
            if (value < noise_gate)
                return 0;

            float db = 20 * MathF.Log10(value / peak);
            float level = Math.Clamp(1 + db / rangeDb, 0, 1);

            return MathF.Pow(level, contrast);
        }

        private static float trackPeak(float peak, float value, double elapsed)
        {
            peak *= (float)Math.Exp(-elapsed / peak_decay_time);
            return Math.Max(peak_floor, Math.Max(peak, value));
        }

        private static float follow(float current, float target, double elapsed, double attackTime, double releaseTime)
            => interpolate(current, target, elapsed, target > current ? attackTime : releaseTime);

        /// <summary>
        /// Frame-rate independent exponential interpolation towards <paramref name="target"/> with time constant <paramref name="time"/>.
        /// </summary>
        private static float interpolate(float current, float target, double elapsed, double time)
            => current + (target - current) * (float)(1 - Math.Exp(-elapsed / time));
    }
}
