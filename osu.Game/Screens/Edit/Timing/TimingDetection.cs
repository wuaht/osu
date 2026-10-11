// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using ManagedBass;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Logging;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Detects the BPM and offset of a song with a single timing section, in the same way as TimingAnlyz (v0.32.4).
    /// </summary>
    /// <remarks>
    /// An onset envelope is calculated at 1000 Hz (one value per millisecond) from five frequency bands of the audio.
    /// The beat length is found from the peaks of the autocorrelation of the envelope,
    /// and the offset from the peak of the envelope folded over all beats.
    /// </remarks>
    public static class TimingDetection
    {
        /// <summary>
        /// The sample rate of the onset envelope. One sample per millisecond.
        /// </summary>
        public const double ENVELOPE_RATE = 1000;

        /// <summary>
        /// The sample rate the filters of the onset envelope are designed for.
        /// </summary>
        public const double DESIGN_SAMPLE_RATE = 44100;

        /// <summary>
        /// The longest beat length which is searched for in the autocorrelation, in milliseconds (30 BPM).
        /// </summary>
        private const int max_initial_lag = 2000;

        /// <summary>
        /// Peaks of the autocorrelation are only used as the base of a candidate beat length below this lag, in milliseconds (60 BPM).
        /// </summary>
        private const int max_candidate_lag = 1000;

        /// <summary>
        /// Candidate beat lengths shorter than this (above 210 BPM) are replaced with a multiple of them, if there's a candidate for it.
        /// </summary>
        private const double min_beat_length = 60000 / 210.0;

        /// <summary>
        /// The time subtracted from the peak of the folded envelope to get the offset, compensating for the delay of the envelope filters.
        /// </summary>
        /// <remarks>
        /// TimingAnlyz subtracts the 22.5ms separately when printing the offset.
        /// It moves the offset from the steepest rise of the onsets to their start, where beats are timed in ranked beatmaps.
        /// Very sharp onsets (like synthetic clicks) are detected about that much too early.
        /// </remarks>
        public const double ENVELOPE_DELAY = 6.2 + 22.5;

        /// <summary>
        /// The folded envelope is searched for the offset starting at this time, so that the offset stays positive.
        /// </summary>
        private const int offset_search_start = 35;

        private static readonly BiquadCoefficients[] band_filters =
        {
            // < 250 Hz
            new BiquadCoefficients(2, 1, -1.96602499f, 0.967822254f, 0.000449319050f),
            new BiquadCoefficients(2, 1, -1.92228699f, 0.924044251f, 0.000439323077f),
            // ~ 250 - 500 Hz
            new BiquadCoefficients(0, -1, -1.98691809f, 0.988819599f, 0.0211971477f),
            new BiquadCoefficients(0, -1, -1.97186232f, 0.978765249f, 0.0211971477f),
            new BiquadCoefficients(0, -1, -1.96395516f, 0.966656804f, 0.0209600348f),
            new BiquadCoefficients(0, -1, -1.95116699f, 0.955917597f, 0.0209600348f),
            // ~ 500 - 1000 Hz
            new BiquadCoefficients(0, -1, -1.97018325f, 0.977743566f, 0.0420483202f),
            new BiquadCoefficients(0, -1, -1.93076444f, 0.958042085f, 0.0420483202f),
            new BiquadCoefficients(0, -1, -1.92373407f, 0.934358478f, 0.0411380120f),
            new BiquadCoefficients(0, -1, -1.89517128f, 0.913750589f, 0.0411380120f),
            // ~ 1000 - 2000 Hz
            new BiquadCoefficients(0, -1, -1.92596865f, 0.955819964f, 0.0827306286f),
            new BiquadCoefficients(0, -1, -1.81211865f, 0.918312252f, 0.0827306286f),
            new BiquadCoefficients(0, -1, -1.83145261f, 0.872521520f, 0.0793709233f),
            new BiquadCoefficients(0, -1, -1.76369274f, 0.834738493f, 0.0793709233f),
            // > 2000 Hz
            new BiquadCoefficients(-2, 1, -1.66992509f, 0.772546172f, 0.860617816f),
            new BiquadCoefficients(-2, 1, -1.43855608f, 0.526959062f, 0.741378784f),
        };

        /// <summary>
        /// The ranges of <see cref="band_filters"/> which make up each band.
        /// </summary>
        private static readonly (int start, int count)[] bands = { (0, 2), (2, 4), (6, 4), (10, 4), (14, 2) };

        /// <summary>
        /// Smooths the energy of each band.
        /// </summary>
        private static readonly BiquadCoefficients energy_filter = new BiquadCoefficients(0, 0, -1.94799995f, 0.948099971f, 0.0480000004f);

        /// <summary>
        /// Smooths the onset envelope before and after the current time.
        /// </summary>
        private static readonly BiquadCoefficients envelope_filter = new BiquadCoefficients(0, 0, -1.39999998f, 0.479999989f, 0.200000003f);

        /// <summary>
        /// Decodes the given audio file and detects its timing. Blocking.
        /// </summary>
        /// <param name="data">The audio file. Disposed when done.</param>
        /// <param name="cancellationToken">A token to cancel the detection.</param>
        /// <returns>The result, or <see langword="null"/> if the audio could not be decoded.</returns>
        public static TimingDetectionResult? Detect(Stream data, CancellationToken cancellationToken = default)
        {
            float[]? envelope = CalculateEnvelope(data, out double sampleRate, cancellationToken);

            if (envelope == null)
                return null;

            var result = Detect(envelope);
            result.SampleRateSupported = sampleRate == DESIGN_SAMPLE_RATE;
            return result;
        }

        /// <summary>
        /// Detects the timing of an onset envelope (see <see cref="CalculateEnvelope(float[], double)"/>).
        /// </summary>
        public static TimingDetectionResult Detect(float[] envelope)
        {
            var result = new TimingDetectionResult();

            var bpm = DetectBeatLength(envelope);

            if (bpm == null)
                return result;

            result.BeatLength = bpm.Value.beatLength;
            result.Accurate = bpm.Value.accurate;

            if (result.Accurate)
            {
                result.Offset = DetectOffset(envelope, result.BeatLength.Value);
                result.Warnings = FindWarnings(envelope, result.BeatLength.Value, result.Offset.Value);
            }

            return result;
        }

        #region Onset envelope

        /// <summary>
        /// Decodes the given audio file and calculates its onset envelope. Blocking.
        /// </summary>
        /// <param name="data">The audio file. Disposed when done.</param>
        /// <param name="sampleRate">The sample rate of the audio.</param>
        /// <param name="cancellationToken">A token to cancel the calculation.</param>
        /// <returns>The envelope, or <see langword="null"/> if the audio could not be decoded.</returns>
        public static float[]? CalculateEnvelope(Stream data, out double sampleRate, CancellationToken cancellationToken = default)
        {
            sampleRate = 0;

            using (data)
            {
                if (Bass.CurrentDevice < 0)
                {
                    Logger.Log("Failed to detect timing as no bass device is available.");
                    return null;
                }

                using var fileCallbacks = new FileCallbacks(new DataStreamFileProcedures(data));

                // The same flags as the playback track (see TrackBass), with float samples for accuracy.
                int stream = Bass.CreateStream(StreamSystem.NoBuffer, BassFlags.Decode | BassFlags.Prescan | BassFlags.Float, fileCallbacks.Callbacks, fileCallbacks.Handle);

                try
                {
                    if (stream == 0 || !Bass.ChannelGetInfo(stream, out ChannelInfo info) || info.Channels < 1 || info.Frequency <= 0)
                    {
                        Logger.Log($"Failed to detect timing ({Bass.LastError}).");
                        return null;
                    }

                    sampleRate = info.Frequency;

                    const int bytes_per_sample = 4;
                    int channels = info.Channels;

                    long expectedFrames = Math.Max(0, Bass.ChannelGetLength(stream) / (bytes_per_sample * channels));
                    var builder = new EnvelopeBuilder(sampleRate, expectedFrames);

                    float[] buffer = new float[4096 * channels];
                    int read;

                    while ((read = Bass.ChannelGetData(stream, buffer, buffer.Length * bytes_per_sample)) > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        int framesRead = read / (bytes_per_sample * channels);

                        for (int f = 0; f < framesRead; f++)
                        {
                            // the channels are summed, not averaged.
                            float sum = 0;

                            for (int c = 0; c < channels; c++)
                                sum += buffer[f * channels + c];

                            builder.Add(sum);
                        }
                    }

                    if (read < 0 && Bass.LastError != Errors.Ended)
                    {
                        Logger.Log($"Failed to detect timing ({Bass.LastError}).");
                        return null;
                    }

                    return builder.Finish();
                }
                finally
                {
                    if (stream != 0)
                        Bass.StreamFree(stream);
                }
            }
        }

        /// <summary>
        /// Calculates the onset envelope of mono audio samples.
        /// </summary>
        public static float[] CalculateEnvelope(float[] samples, double sampleRate)
        {
            var builder = new EnvelopeBuilder(sampleRate, samples.Length);

            foreach (float sample in samples)
                builder.Add(sample);

            return builder.Finish();
        }

        /// <summary>
        /// Calculates the onset envelope from audio samples as they are decoded, without keeping the audio in memory.
        /// </summary>
        private class EnvelopeBuilder
        {
            private readonly double samplesPerEnvelopeSample;
            private readonly Biquad[][] bandFilters;
            private readonly Biquad[] energyFilters;
            private readonly Biquad[] lowPass;
            private readonly List<float> resampled;

            private long sampleIndex;
            private long nextResampledIndex;

            public EnvelopeBuilder(double sampleRate, long expectedSamples)
            {
                samplesPerEnvelopeSample = 1 / (ENVELOPE_RATE / sampleRate);

                bandFilters = new Biquad[bands.Length][];
                energyFilters = new Biquad[bands.Length];

                for (int b = 0; b < bands.Length; b++)
                {
                    bandFilters[b] = new Biquad[bands[b].count];

                    for (int s = 0; s < bands[b].count; s++)
                        bandFilters[b][s] = new Biquad(band_filters[bands[b].start + s]);

                    energyFilters[b] = new Biquad(energy_filter);
                }

                // the same filter as the lowest band, to avoid aliasing when resampling.
                lowPass = new[] { new Biquad(band_filters[0]), new Biquad(band_filters[1]) };

                resampled = new List<float>((int)Math.Min(int.MaxValue, (long)(expectedSamples / samplesPerEnvelopeSample) + 1));
            }

            public void Add(float sample)
            {
                float product = 1;

                for (int b = 0; b < bands.Length; b++)
                {
                    float filtered = sample;

                    foreach (var filter in bandFilters[b])
                        filtered = filter.Process(filtered);

                    product *= energyFilters[b].Process(filtered * filtered) + 1;
                }

                float value = (float)Math.Log(product);

                foreach (var filter in lowPass)
                    value = filter.Process(value);

                // nearest-neighbour resampling.
                while (resampledSourceIndex(nextResampledIndex) == sampleIndex)
                {
                    resampled.Add(value);
                    nextResampledIndex++;
                }

                sampleIndex++;
            }

            private long resampledSourceIndex(long index) => (long)Math.Floor(index * samplesPerEnvelopeSample + 0.5);

            public float[] Finish()
            {
                // the envelope covers the whole audio. samples past its end are zero.
                long length = (long)Math.Ceiling(sampleIndex / samplesPerEnvelopeSample);

                while (resampled.Count < length)
                    resampled.Add(0);

                return differentiate(resampled.ToArray());
            }

            /// <summary>
            /// Compares the smoothed envelope after each time with the smoothed envelope before it, so that rises (onsets) become peaks.
            /// </summary>
            private static float[] differentiate(float[] envelope)
            {
                int n = envelope.Length;

                if (n < 3)
                    return new float[n];

                float[] before = (float[])envelope.Clone();
                applyFilter(before, envelope_filter);

                float[] after = (float[])envelope.Clone();
                Array.Reverse(after);
                applyFilter(after, envelope_filter);
                Array.Reverse(after);

                float[] result = new float[n];

                result[0] = after[1];
                for (int i = 1; i < n - 1; i++)
                    result[i] = after[i + 1] - before[i - 1];
                result[n - 1] = -before[n - 2];

                return result;
            }

            private static void applyFilter(float[] values, BiquadCoefficients coefficients)
            {
                var filter = new Biquad(coefficients);

                for (int i = 0; i < values.Length; i++)
                    values[i] = filter.Process(values[i]);
            }
        }

        private readonly record struct BiquadCoefficients(float B1, float B2, float A1, float A2, float Gain);

        /// <summary>
        /// A second order filter in direct form II with b0 = 1 and an input gain.
        /// </summary>
        private class Biquad
        {
            private readonly BiquadCoefficients c;
            private float w1, w2;

            public Biquad(BiquadCoefficients coefficients)
            {
                c = coefficients;
            }

            public float Process(float x)
            {
                float w = c.Gain * x - c.A1 * w1 - c.A2 * w2;
                float y = w + c.B1 * w1 + c.B2 * w2;

                w2 = w1;
                w1 = w;

                return y;
            }
        }

        #endregion

        #region Beat length

        /// <summary>
        /// Detects the beat length of an onset envelope.
        /// </summary>
        /// <returns>
        /// The beat length in milliseconds and whether it's accurate,
        /// or <see langword="null"/> if no beat could be found (the song may be too short, or have drifting BPM or multiple timing sections).
        /// </returns>
        public static (double beatLength, bool accurate)? DetectBeatLength(float[] envelope)
        {
            int lagCount = envelope.Length / 2;
            int maxLag = Math.Min(lagCount, max_initial_lag);

            float[] ac = Autocorrelate(envelope, lagCount);

            var peaks = new List<int>();

            for (int i = 2; i < maxLag - 2; i++)
            {
                if (ac[i] > 0 && ac[i] > ac[i - 1] && ac[i] > ac[i + 1] && ac[i - 2] < ac[i - 1] && ac[i + 2] < ac[i + 1])
                    peaks.Add(i);
            }

            // Each peak is the base of a candidate beat length, which is followed through the peaks at its multiples.
            var candidates = new List<(double beatLength, float strength)>();
            int chosen = 0;
            float strongest = 0;
            int peakIndex = 0;

            for (int p = 0; p < peaks.Count; p++)
            {
                int basePeak = peaks[p];

                if (basePeak >= max_candidate_lag)
                    break;

                var fit = new MultipleFit();
                float strengthSum = 0;
                double beatLength = basePeak;
                int position = basePeak;
                int multiple = 1;
                peakIndex = p;

                while (position < maxLag - 10)
                {
                    while (peakIndex < peaks.Count && peaks[peakIndex] < position - 10)
                        peakIndex++;

                    if (peakIndex >= peaks.Count || peaks[peakIndex] > position + 10)
                        break;

                    int peak = peaks[peakIndex];
                    double location = InterpolatePeak(ac, peak);

                    strengthSum += ac[peak];
                    fit.Add(multiple, location);
                    beatLength = fit.BeatLength;

                    position = (int)Math.Floor(location + beatLength + 0.5);
                    multiple++;
                }

                if (fit.Count == 0)
                    continue;

                float strength = strengthSum / fit.Count;

                // the multiples have to be about as strong as the base peak.
                if (strength <= ac[basePeak] * 0.7f)
                    continue;

                // shorter beat lengths are preferred, unless a longer one is clearly stronger.
                if (strength > strongest * 1.2f)
                    chosen = candidates.Count;

                strongest = Math.Max(strongest, strength);
                candidates.Add((beatLength, strength));
            }

            if (candidates.Count == 0 || strongest <= 0)
                return null;

            double estimate = candidates[chosen].beatLength;

            if (estimate < min_beat_length)
            {
                for (int i = chosen + 1; i < candidates.Count; i++)
                {
                    // a multiple of the estimate, within 10ms.
                    if (mod(candidates[i].beatLength + 10, estimate) <= 20)
                    {
                        estimate = candidates[i].beatLength;
                        break;
                    }
                }
            }

            return refineBeatLength(ac, estimate);
        }

        /// <summary>
        /// Refines a beat length by fitting it through the peaks of the autocorrelation at its multiples, up to the first missing one.
        /// </summary>
        private static (double beatLength, bool accurate)? refineBeatLength(float[] ac, double estimate)
        {
            var fit = new MultipleFit();
            double beatLength = estimate;
            int position = (int)Math.Floor(estimate + 0.5);
            int multiple = 1;

            while (position < ac.Length - 10)
            {
                int best = position;
                float bestValue = 0;

                for (int i = Math.Max(1, position - 10); i <= position + 10 && i < ac.Length - 1; i++)
                {
                    if (ac[i] > bestValue)
                    {
                        best = i;
                        bestValue = ac[i];
                    }
                }

                // stop at the first missing multiple.
                if (bestValue <= 0 || Math.Abs(best - position) > 8)
                    break;

                double location = InterpolatePeak(ac, best);
                fit.Add(multiple, location);
                beatLength = fit.BeatLength;

                position = (int)Math.Floor(location + beatLength + 0.5);
                multiple++;
            }

            if (fit.Count < 4)
                return null;

            bool accurate = fit.Residual <= 2.4 && fit.RelativeDeviation <= 3e-5;

            return (beatLength, accurate);
        }

        /// <summary>
        /// A least squares fit of <c>location = multiple * beatLength</c>.
        /// </summary>
        private class MultipleFit
        {
            public int Count { get; private set; }

            private double sumMultipleSquared;
            private double sumMultipleLocation;
            private double sumLocationSquared;

            public void Add(int multiple, double location)
            {
                Count++;
                sumMultipleSquared += (double)multiple * multiple;
                sumMultipleLocation += multiple * location;
                sumLocationSquared += location * location;
            }

            public double BeatLength => sumMultipleLocation / sumMultipleSquared;

            /// <summary>
            /// The standard deviation of the locations from the fit, in milliseconds.
            /// </summary>
            public double Residual => Math.Sqrt(Math.Max(0, sumLocationSquared - sumMultipleLocation * sumMultipleLocation / sumMultipleSquared) / (Count - 1));

            /// <summary>
            /// The relative standard deviation of the beat length.
            /// </summary>
            public double RelativeDeviation => Math.Sqrt(Math.Max(0, sumLocationSquared * sumMultipleSquared / (sumMultipleLocation * sumMultipleLocation) - 1) / (Count - 1));
        }

        /// <summary>
        /// Calculates the autocorrelation of <paramref name="values"/> for lags from 0 to <paramref name="lagCount"/> - 1.
        /// </summary>
        public static float[] Autocorrelate(float[] values, int lagCount)
        {
            int size = 1;
            while (size < values.Length + lagCount)
                size <<= 1;

            var spectrum = new Complex[size];
            for (int i = 0; i < values.Length; i++)
                spectrum[i] = values[i];

            fft(spectrum, false);

            for (int i = 0; i < size; i++)
                spectrum[i] = spectrum[i].Real * spectrum[i].Real + spectrum[i].Imaginary * spectrum[i].Imaginary;

            fft(spectrum, true);

            float[] result = new float[lagCount];
            for (int i = 0; i < lagCount; i++)
                result[i] = (float)(spectrum[i].Real / size);

            return result;
        }

        private static void fft(Complex[] data, bool inverse)
        {
            int n = data.Length;

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;

                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;

                j ^= bit;

                if (i < j)
                    (data[i], data[j]) = (data[j], data[i]);
            }

            for (int length = 2; length <= n; length <<= 1)
            {
                double angle = 2 * Math.PI / length * (inverse ? 1 : -1);
                var step = new Complex(Math.Cos(angle), Math.Sin(angle));

                for (int start = 0; start < n; start += length)
                {
                    Complex w = Complex.One;

                    for (int k = 0; k < length / 2; k++)
                    {
                        Complex even = data[start + k];
                        Complex odd = data[start + k + length / 2] * w;

                        data[start + k] = even + odd;
                        data[start + k + length / 2] = even - odd;

                        w *= step;
                    }
                }
            }
        }

        #endregion

        #region Offset

        /// <summary>
        /// Detects the offset of an onset envelope with the given beat length.
        /// </summary>
        /// <returns>The time of the first beat in milliseconds, within one beat length after the start of the audio.</returns>
        public static double DetectOffset(float[] envelope, double beatLength)
        {
            int beatSamples = (int)Math.Ceiling(beatLength);
            float[] folded = new float[beatSamples + 40];

            int beats = (int)Math.Ceiling(envelope.Length / beatLength);

            for (int beat = 0; beat < beats; beat++)
            {
                int start = (int)Math.Floor(beat * beatLength + 0.5);

                for (int i = 0; i < folded.Length && start + i < envelope.Length; i++)
                    folded[i] += envelope[start + i];
            }

            int best = offset_search_start;

            for (int i = offset_search_start; i < offset_search_start + beatSamples; i++)
            {
                if (folded[i] > folded[best])
                    best = i;
            }

            return InterpolatePeak(folded, best) - ENVELOPE_DELAY;
        }

        /// <summary>
        /// The length of the sections the song is split into by <see cref="FindWarnings"/>, in milliseconds.
        /// </summary>
        public const double WARNING_SECTION_LENGTH = 10000;

        /// <summary>
        /// Checks how well a single timing fits each section of the song, by looking for the onsets near every beat.
        /// </summary>
        /// <returns>The warnings of each section of <see cref="WARNING_SECTION_LENGTH"/>.</returns>
        public static TimingWarning[] FindWarnings(float[] envelope, double beatLength, double offset)
        {
            int length = envelope.Length;
            int sectionCount = (int)Math.Ceiling(length / WARNING_SECTION_LENGTH);

            // per section: the sum of the strengths of onsets close to the beats, and of their (squared) weighted distances from the beats.
            double[] onsetStrength = new double[sectionCount];
            double[] onsetDistance = new double[sectionCount];
            double[] onsetDistanceSquared = new double[sectionCount];

            // per section: the sum of the strongest onsets around the beats, and of how much stronger they are than onsets close to the beats.
            double[] beatStrength = new double[sectionCount];
            double[] offBeatStrength = new double[sectionCount];

            int beatWindow = (int)Math.Floor(beatLength * 0.7 + 0.5);

            for (double beat = offset + ENVELOPE_DELAY; beat <= length - 1; beat += beatLength)
            {
                int index = (int)Math.Floor(beat + 0.5);
                int section = (int)Math.Floor(beat / WARNING_SECTION_LENGTH);

                if (index < 0 || section < 0)
                    continue;

                int peak = index;
                float closeMaximum = maximum(envelope, index - 10, index + 11, ref peak);
                float nearMaximum = Math.Max(closeMaximum, maximum(envelope, index - 30, index + 31, ref peak));
                float wideMaximum = Math.Max(nearMaximum, maximum(envelope, index - beatWindow, index + beatWindow, ref peak, false));

                if (closeMaximum > 0 && Math.Abs(peak - beat) <= 25)
                {
                    double distance = InterpolatePeak(envelope, peak) - beat;

                    onsetStrength[section] += closeMaximum;
                    onsetDistance[section] += closeMaximum * distance;
                    onsetDistanceSquared[section] += closeMaximum * distance * distance;
                    beatStrength[section] += nearMaximum;
                }
                else
                {
                    beatStrength[section] += wideMaximum;
                    offBeatStrength[section] += wideMaximum - closeMaximum;
                }
            }

            // scales sums over the beats of a section to averages over the whole section.
            double beatsPerSection = beatLength / WARNING_SECTION_LENGTH;

            var warnings = new TimingWarning[sectionCount];

            for (int s = 0; s < sectionCount; s++)
            {
                double meanDistance = onsetDistance[s] / onsetStrength[s];
                double deviation = Math.Sqrt(onsetDistanceSquared[s] / onsetStrength[s]);
                double allowedDistance = Math.Sqrt(0.2 * deviation * deviation + 6);

                double strength = beatStrength[s] * beatsPerSection;
                double offBeatRatio = offBeatStrength[s] / beatStrength[s];

                if ((strength > 3 && offBeatRatio > 0.7) || (strength > 4 && offBeatRatio > 0.5) || (strength > 6 && offBeatRatio > 0.4) || (strength > 10 && offBeatRatio > 0.3))
                    warnings[s] = TimingWarning.NeedsTimingSection;
                else if (strength > 3 && meanDistance > allowedDistance)
                    warnings[s] = TimingWarning.Late;
                else if (strength > 3 && meanDistance < -allowedDistance)
                    warnings[s] = TimingWarning.Early;
                else if (onsetStrength[s] * beatsPerSection > 3 && deviation > 10)
                    warnings[s] = TimingWarning.Jitter;
            }

            return warnings;
        }

        /// <summary>
        /// Finds the maximum of <paramref name="values"/> from <paramref name="start"/> (inclusive) to <paramref name="end"/> (exclusive), clamped to the array.
        /// </summary>
        /// <param name="values">The values.</param>
        /// <param name="start">The first index.</param>
        /// <param name="end">The index after the last one.</param>
        /// <param name="index">Set to the index of the maximum if it's greater than the value at the current index, if <paramref name="updateIndex"/> is set.</param>
        /// <param name="updateIndex">Whether to update <paramref name="index"/>.</param>
        private static float maximum(float[] values, int start, int end, ref int index, bool updateIndex = true)
        {
            start = Math.Max(0, start);
            end = Math.Min(values.Length, end);

            float max = index >= 0 && index < values.Length ? values[index] : 0;

            for (int i = start; i < end; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];

                    if (updateIndex)
                        index = i;
                }
            }

            return max;
        }

        /// <summary>
        /// Moves an offset by whole beats, such that it's after <paramref name="minimum"/>.
        /// </summary>
        public static double NormaliseOffset(double offset, double beatLength, double minimum = 0)
            => offset - Math.Floor((offset - minimum) / beatLength) * beatLength;

        #endregion

        /// <summary>
        /// Finds the location of a peak between samples, by fitting a parabola through it and its neighbours.
        /// </summary>
        public static double InterpolatePeak(float[] values, int index)
        {
            if (index <= 0 || index >= values.Length - 1)
                return index;

            double left = values[index - 1];
            double centre = values[index];
            double right = values[index + 1];

            double denominator = centre - right + (centre - left);

            if (denominator == 0)
                return index;

            return (right - left) * 0.5 / denominator + index;
        }

        private static double mod(double value, double divisor) => value - divisor * Math.Truncate(value / divisor);
    }

    public class TimingDetectionResult
    {
        /// <summary>
        /// The detected beat length in milliseconds, or <see langword="null"/> if none could be found.
        /// </summary>
        public double? BeatLength { get; set; }

        /// <summary>
        /// Whether <see cref="BeatLength"/> fits the whole song. If not, it's only approximate and no offset is detected.
        /// </summary>
        public bool Accurate { get; set; }

        /// <summary>
        /// The detected offset in milliseconds, or <see langword="null"/> if it wasn't detected.
        /// </summary>
        public double? Offset { get; set; }

        /// <summary>
        /// Whether the audio has the sample rate the filters are designed for (44.1 kHz). Results may be less accurate otherwise.
        /// </summary>
        public bool SampleRateSupported { get; set; } = true;

        /// <summary>
        /// The warnings of each section of <see cref="TimingDetection.WARNING_SECTION_LENGTH"/> of the song, if the offset was detected.
        /// </summary>
        public TimingWarning[] Warnings { get; set; } = Array.Empty<TimingWarning>();

        public double? BPM => 60000 / BeatLength;
    }

    public enum TimingWarning
    {
        None,

        /// <summary>
        /// The beats don't line up with the onsets, so the section probably needs an additional timing section. Shown as '#' by TimingAnlyz.
        /// </summary>
        NeedsTimingSection,

        /// <summary>
        /// The onsets are consistently after the beats, so the section probably has a slightly later offset. Shown as '+' by TimingAnlyz.
        /// </summary>
        Late,

        /// <summary>
        /// The onsets are consistently before the beats, so the section probably has a slightly earlier offset. Shown as '-' by TimingAnlyz.
        /// </summary>
        Early,

        /// <summary>
        /// The onsets are spread around the beats, so the BPM may drift or the offset jitter. Shown as '.' by TimingAnlyz.
        /// </summary>
        Jitter,
    }
}
