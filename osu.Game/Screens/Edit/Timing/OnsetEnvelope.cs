// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Finds onsets and fits beats in the onset envelope of a track (see <see cref="TimingDetection.CalculateEnvelope(float[], double)"/>).
    /// All times are times of the track, matching the offsets detected by <see cref="TimingDetection"/>.
    /// </summary>
    public class OnsetEnvelope
    {
        /// <summary>
        /// The time before and after an onset over which the average level is calculated, in milliseconds.
        /// </summary>
        private const int baseline_radius = 250;

        /// <summary>
        /// How much stronger than the average level around it a peak of the envelope has to be to count as an onset.
        /// </summary>
        private const double minimum_prominence = 1.3;

        /// <summary>
        /// How far from a beat an onset may be to be fitted to it by <see cref="FitBeats"/>, in milliseconds.
        /// </summary>
        private const double max_fit_distance = 12;

        /// <summary>
        /// The distance of the anchor from the fitted beats at which it's weighted half as much in <see cref="FitBeats"/>, in milliseconds.
        /// </summary>
        private const double anchor_tolerance = 4;

        /// <summary>
        /// The longest audio used by <see cref="EstimateBeatLength"/>, in milliseconds.
        /// </summary>
        private const double max_estimation_length = 30000;

        private readonly float[] values;

        /// <summary>
        /// The cumulative sums of <see cref="values"/>, for averaging ranges of them.
        /// </summary>
        private readonly double[] sums;

        private readonly Dictionary<(int, int), double?> estimationCache = new Dictionary<(int, int), double?>();

        /// <summary>
        /// The envelope at decreasing resolutions for display, normalised by <see cref="displayScale"/>.
        /// Level 0 has one value per millisecond, and every following level contains the maxima of two values of the previous one.
        /// </summary>
        public readonly IReadOnlyList<float[]> DisplayLevels;

        /// <summary>
        /// The value which is displayed at full height. Few onsets are stronger, so that a single loud hit doesn't make all other onsets tiny.
        /// </summary>
        private readonly float displayScale;

        /// <summary>
        /// The fraction of the values of the envelope which are displayed below full height.
        /// </summary>
        private const double display_scale_percentile = 0.998;

        public OnsetEnvelope(float[] values)
        {
            this.values = values;

            sums = new double[values.Length + 1];

            for (int i = 0; i < values.Length; i++)
                sums[i + 1] = sums[i] + values[i];

            if (values.Length > 0)
            {
                float[] sorted = values.OrderBy(v => v).ToArray();
                displayScale = sorted[(int)Math.Min(sorted.Length - 1, sorted.Length * display_scale_percentile)];
            }

            if (displayScale <= 0)
                displayScale = 1;

            var levels = new List<float[]> { values.Select(v => Math.Min(1, v / displayScale)).ToArray() };

            while (levels[^1].Length > 1)
            {
                float[] previous = levels[^1];
                float[] next = new float[(previous.Length + 1) / 2];

                for (int i = 0; i < next.Length; i++)
                    next[i] = Math.Max(previous[i * 2], i * 2 + 1 < previous.Length ? previous[i * 2 + 1] : 0);

                levels.Add(next);
            }

            DisplayLevels = levels;
        }

        /// <summary>
        /// The time of the track at which the value at <paramref name="index"/> of <see cref="DisplayLevels"/>[0] is displayed,
        /// such that the peaks of onsets are displayed where <see cref="FindOnset"/> places them.
        /// </summary>
        public static double DisplayTimeAt(double index) => toTime(index);

        private static double toIndex(double time) => time + TimingDetection.ENVELOPE_DELAY;

        private static double toTime(double index) => index - TimingDetection.ENVELOPE_DELAY;

        /// <summary>
        /// The envelopes of recently used audio files, so that the timeline and the timing screen (and all difficulties using the audio) share a single calculation.
        /// </summary>
        private static readonly AudioAnalysisCache<OnsetEnvelope> envelopes = new AudioAnalysisCache<OnsetEnvelope>(stream =>
        {
            float[]? values = TimingDetection.CalculateEnvelope(stream, out _);
            return values != null ? new OnsetEnvelope(values) : null;
        }, "onsets");

        /// <summary>
        /// Retrieves the onset envelope of the track of the given beatmap, calculating it in the background if it hasn't been recently.
        /// Must be called from the update thread, as the audio file is read from the beatmap.
        /// </summary>
        public static Task<OnsetEnvelope?> GetForTrack(WorkingBeatmap beatmap) => envelopes.GetForTrack(beatmap);

        /// <summary>
        /// Finds the onset which is the most likely meant at <paramref name="time"/>, preferring strong onsets close to it.
        /// </summary>
        /// <returns>The time of the onset, or <see langword="null"/> if there's no onset within <paramref name="radius"/> milliseconds.</returns>
        public double? FindOnset(double time, double radius) => findOnset(time, radius)?.time;

        private (double time, float strength)? findOnset(double time, double radius)
        {
            double centre = toIndex(time);
            int start = Math.Max(1, (int)Math.Ceiling(centre - radius));
            int end = Math.Min(values.Length - 2, (int)Math.Floor(centre + radius));

            int best = -1;
            double bestScore = 0;

            for (int i = start; i <= end; i++)
            {
                if (!isOnset(i))
                    continue;

                double distance = (i - centre) / radius;
                double score = values[i] * (1 - 0.5 * distance * distance);

                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            if (best < 0)
                return null;

            return (toTime(TimingDetection.InterpolatePeak(values, best)), values[best]);
        }

        private bool isOnset(int index)
        {
            float value = values[index];

            if (value <= 0 || value < values[index - 1] || value <= values[index + 1])
                return false;

            int from = Math.Max(0, index - baseline_radius);
            int to = Math.Min(values.Length, index + baseline_radius + 1);
            double average = (sums[to] - sums[from]) / (to - from);

            return value > minimum_prominence * average;
        }

        /// <summary>
        /// Fits evenly spaced beats from <paramref name="start"/> to the beat at <paramref name="anchorTime"/>, which is <paramref name="beats"/> beats later,
        /// to the onsets close to the beats in between. <paramref name="start"/> stays fixed.
        /// </summary>
        /// <remarks>
        /// The onset at <paramref name="anchorTime"/> was chosen by the user and is weighted as much as the strongest onset in between,
        /// unless it's far from the beats fitted to the other onsets (see <see cref="anchor_tolerance"/>).
        /// Fitting all beats of the section averages out the inaccuracy of single onsets, compared to only using <paramref name="anchorTime"/>.
        /// </remarks>
        public BeatFit FitBeats(double start, int beats, double anchorTime)
        {
            double beatLength = (anchorTime - start) / beats;
            var fit = new BeatFit(beatLength, null, 0);

            if (beats < 2 || beatLength <= 0)
                return fit;

            var onsets = new List<(int beat, double time, float strength)>();

            // the beats are searched again with the fitted beat length, in case the first fit moved them closer to other onsets.
            for (int iteration = 0; iteration < 3; iteration++)
            {
                double radius = Math.Min(max_fit_distance, beatLength * 0.15);

                onsets.Clear();
                float maxStrength = 0;

                for (int beat = 1; beat < beats; beat++)
                {
                    if (findOnset(start + beat * beatLength, radius) is not var (time, strength))
                        continue;

                    onsets.Add((beat, time, strength));
                    maxStrength = Math.Max(maxStrength, strength);
                }

                if (onsets.Count < 2)
                    return fit;

                // the anchor may have been snapped to a different onset than the beat (e.g. a note just before it).
                // it's trusted less the further it is from the beats fitted to the other onsets.
                double anchorDistance = iteration == 0 ? 0 : anchorTime - (start + beats * beatLength);
                double anchorWeight = maxStrength / (1 + Math.Pow(anchorDistance / anchor_tolerance, 2));

                // least squares fit of beat length through the fixed start: minimise sum of w * (t - start - k * L)^2.
                double numerator = anchorWeight * beats * (anchorTime - start);
                double denominator = anchorWeight * beats * beats;

                foreach (var (beat, time, strength) in onsets)
                {
                    numerator += strength * beat * (time - start);
                    denominator += strength * (double)beat * beat;
                }

                beatLength = numerator / denominator;
            }

            double weightedSquares = 0;
            double totalWeight = 0;

            foreach (var (beat, time, strength) in onsets)
            {
                double deviation = time - (start + beat * beatLength);
                weightedSquares += strength * deviation * deviation;
                totalWeight += strength;
            }

            return new BeatFit(beatLength, Math.Sqrt(weightedSquares / totalWeight), onsets.Count);
        }

        /// <summary>
        /// Estimates the beat length of the audio between <paramref name="start"/> and <paramref name="end"/> (see <see cref="TimingDetection.DetectBeatLength"/>).
        /// </summary>
        /// <returns>The beat length in milliseconds, or <see langword="null"/> if there's not enough audio or no clear beat.</returns>
        public double? EstimateBeatLength(double start, double end)
        {
            end = Math.Min(end, start + max_estimation_length);

            // round the range, such that repeated estimates for a moving cursor can be cached.
            int from = Math.Clamp((int)Math.Round(toIndex(start) / 500) * 500, 0, values.Length);
            int to = Math.Clamp((int)Math.Round(toIndex(end) / 500) * 500, 0, values.Length);

            if (estimationCache.TryGetValue((from, to), out double? cached))
                return cached;

            double? estimate = null;

            // the autocorrelation needs a few seconds to find beats up to 2 seconds long.
            if (to - from >= 6000)
                estimate = TimingDetection.DetectBeatLength(values[from..to])?.beatLength;

            return estimationCache[(from, to)] = estimate;
        }
    }

    /// <param name="BeatLength">The fitted beat length.</param>
    /// <param name="Deviation">The weighted root mean square distance of the onsets from the fitted beats in milliseconds, if enough onsets were found.</param>
    /// <param name="OnsetCount">The number of beats between the start and the anchor with an onset.</param>
    public readonly record struct BeatFit(double BeatLength, double? Deviation, int OnsetCount);
}
