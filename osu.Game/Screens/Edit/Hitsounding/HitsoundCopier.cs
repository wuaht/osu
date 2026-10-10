// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Copies hitsounds onto the hit objects of a beatmap, matching them by time (like the hitsound copier of Mapping Tools).
    /// </summary>
    public static class HitsoundCopier
    {
        /// <summary>
        /// Copies the given hitsounds onto the hit objects of <paramref name="target"/>.
        /// </summary>
        /// <param name="source">The hitsounds to copy, ordered by time.</param>
        /// <param name="sourceBodies">The slider body hitsounds to copy, ordered by start time.</param>
        /// <param name="target">The hitsounds of the beatmap to copy onto. The hit objects are modified directly.</param>
        /// <param name="options">The options.</param>
        public static HitsoundCopyResult Copy(IReadOnlyList<HitsoundColumnState> source, IReadOnlyList<HitsoundBodyState> sourceBodies, HitsoundMap target, HitsoundCopyOptions options)
        {
            var result = new HitsoundCopyResult();

            foreach (var column in target.Columns)
            {
                var state = findClosest(source, s => s.Time, column.Time, options.Leniency);

                if (state != null)
                    apply(column, state, options, result);
                else
                    applyUnmatched(column, options, result);
            }

            if (options.CopySliderBodies)
            {
                foreach (var body in target.Bodies)
                {
                    // bodies without a counterpart are left alone even when overwriting, as most hitsound difficulties don't have sliders.
                    var state = findClosest(sourceBodies, s => s.StartTime, body.StartTime, options.Leniency);

                    if (state != null)
                        apply(body, state, options, result);
                }
            }

            return result;
        }

        private static void apply(HitsoundColumn column, HitsoundColumnState state, HitsoundCopyOptions options, HitsoundCopyResult result)
        {
            for (int i = 0; i < column.Targets.Count; i++)
            {
                var target = column.Targets[i];
                var samples = target.Samples.ToList();

                // targets beyond the number of distinct banks (e.g. the other notes of an osu!mania chord) use the first bank.
                if (options.CopySampleSets && state.NormalBanks.Count > 0)
                    samples = HitsoundSamples.WithNormalBank(samples, state.NormalBanks[i < state.NormalBanks.Count ? i : 0], !options.CopyAdditions);

                if (options.CopyAdditions)
                {
                    samples = i < state.AdditionGroups.Count
                        ? HitsoundSamples.WithAdditions(samples, state.AdditionGroups[i].Bank, state.AdditionGroups[i].Names)
                        : HitsoundSamples.WithoutAdditions(samples);
                }

                if (shouldCopyVolume(target.Samples, options))
                    samples = HitsoundSamples.WithVolume(samples, state.Volume);

                if (options.CopyCustomIndices)
                    samples = HitsoundSamples.WithCustomIndex(samples, state.CustomIndex);

                setSamples(target, samples, result);
            }

            // each target can only play one hitnormal and one bank of additions.
            if (options.CopySampleSets)
                result.DroppedHitsounds += Math.Max(0, state.NormalBanks.Count - column.Targets.Count);

            if (options.CopyAdditions)
                result.DroppedHitsounds += state.AdditionGroups.Skip(column.Targets.Count).Sum(g => g.Names.Count);
        }

        private static void applyUnmatched(HitsoundColumn column, HitsoundCopyOptions options, HitsoundCopyResult result)
        {
            bool muteSliderEnd = options.MuteUnmatchedSliderEnds && column.Targets.Count == 1 && column.Targets[0].Kind == HitsoundTargetKind.Tail;

            foreach (var target in column.Targets)
            {
                var samples = target.Samples.ToList();

                if ((options.OverwriteUnmatched && options.CopyAdditions) || muteSliderEnd)
                    samples = HitsoundSamples.WithoutAdditions(samples);

                if (muteSliderEnd)
                    samples = HitsoundSamples.WithVolume(samples, DrawableHitObject.MINIMUM_SAMPLE_VOLUME);

                setSamples(target, samples, result);
            }
        }

        private static void apply(HitsoundBody body, HitsoundBodyState state, HitsoundCopyOptions options, HitsoundCopyResult result)
        {
            var samples = body.Samples.ToList();

            if (options.CopySampleSets && state.NormalBank != null)
                samples = HitsoundSamples.WithNormalBank(samples, state.NormalBank, !options.CopyAdditions);

            if (options.CopyAdditions)
            {
                // only the whistle is audible on a slider body.
                samples = state.WhistleBank != null
                    ? HitsoundSamples.WithAddition(samples, HitSampleInfo.HIT_WHISTLE, state.WhistleBank)
                    : HitsoundSamples.WithoutAddition(samples, HitSampleInfo.HIT_WHISTLE);
            }

            if (shouldCopyVolume(body.Samples, options))
                samples = HitsoundSamples.WithVolume(samples, state.Volume);

            if (options.CopyCustomIndices)
                samples = HitsoundSamples.WithCustomIndex(samples, state.CustomIndex);

            if (HitsoundSamples.AreEquivalent(samples, body.Samples))
                return;

            body.SetSamples(samples);
            result.ChangedHitObjects.Add(body.HitObject);
        }

        private static bool shouldCopyVolume(IList<HitSampleInfo> current, HitsoundCopyOptions options)
        {
            if (!options.CopyVolumes)
                return false;

            // muted hitsounds (e.g. slider ends) are usually muted on purpose in the target difficulty.
            return !options.PreserveMutedVolumes || current.Count == 0 || current.Max(s => s.Volume) > DrawableHitObject.MINIMUM_SAMPLE_VOLUME;
        }

        private static void setSamples(HitsoundTarget target, List<HitSampleInfo> samples, HitsoundCopyResult result)
        {
            if (HitsoundSamples.AreEquivalent(samples, target.Samples))
                return;

            target.SetSamples(samples);
            result.ChangedHitObjects.Add(target.HitObject);
            result.ChangedHitsounds++;
        }

        private static T? findClosest<T>(IReadOnlyList<T> items, Func<T, double> getTime, double time, double leniency)
            where T : class
        {
            int min = 0;
            int max = items.Count;

            while (min < max)
            {
                int mid = (min + max) / 2;

                if (getTime(items[mid]) < time)
                    min = mid + 1;
                else
                    max = mid;
            }

            T? closest = null;

            for (int i = Math.Max(0, min - 1); i <= Math.Min(items.Count - 1, min); i++)
            {
                double distance = Math.Abs(getTime(items[i]) - time);

                if (distance <= leniency && (closest == null || distance < Math.Abs(getTime(closest) - time)))
                    closest = items[i];
            }

            return closest;
        }
    }

    public sealed class HitsoundCopyOptions
    {
        /// <summary>
        /// How many milliseconds apart hitsounds may be to still be considered the same.
        /// </summary>
        public double Leniency { get; init; } = 5;

        /// <summary>
        /// Whether to copy the banks of the hitnormals.
        /// </summary>
        public bool CopySampleSets { get; init; } = true;

        /// <summary>
        /// Whether to copy the additions (whistle, finish and clap) and their banks.
        /// </summary>
        public bool CopyAdditions { get; init; } = true;

        public bool CopyVolumes { get; init; } = true;

        /// <summary>
        /// Whether hitsounds which are muted in the target (at the minimum volume) should keep their volume when copying volumes.
        /// </summary>
        public bool PreserveMutedVolumes { get; init; } = true;

        public bool CopyCustomIndices { get; init; } = true;

        /// <summary>
        /// Whether to copy the hitsounds of slider bodies, from sliders starting at the same time.
        /// </summary>
        public bool CopySliderBodies { get; init; } = true;

        /// <summary>
        /// Whether to remove the additions of hitsounds which have no counterpart in the source.
        /// </summary>
        public bool OverwriteUnmatched { get; init; } = true;

        /// <summary>
        /// Whether to mute slider ends which have no counterpart in the source.
        /// </summary>
        public bool MuteUnmatchedSliderEnds { get; init; }
    }

    public sealed class HitsoundCopyResult
    {
        /// <summary>
        /// The hit objects whose samples changed. These need to be updated.
        /// </summary>
        public readonly HashSet<HitObject> ChangedHitObjects = new HashSet<HitObject>();

        /// <summary>
        /// The number of changed hitsounds, excluding slider bodies.
        /// </summary>
        public int ChangedHitsounds;

        /// <summary>
        /// The number of samples which couldn't be copied, as there were fewer hit objects at their time than they need.
        /// </summary>
        public int DroppedHitsounds;
    }
}
