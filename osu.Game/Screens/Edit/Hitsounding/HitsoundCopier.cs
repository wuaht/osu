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
            => Copy(source, sourceBodies, target.Columns, target.Bodies, options);

        /// <summary>
        /// Copies the given hitsounds onto some of the hitsounds of a beatmap (e.g. the ones in a time range).
        /// </summary>
        /// <param name="source">The hitsounds to copy, ordered by time.</param>
        /// <param name="sourceBodies">The slider body hitsounds to copy, ordered by start time.</param>
        /// <param name="targetColumns">The hitsounds to copy onto. The hit objects are modified directly.</param>
        /// <param name="targetBodies">The slider bodies to copy onto.</param>
        /// <param name="options">The options.</param>
        public static HitsoundCopyResult Copy(IReadOnlyList<HitsoundColumnState> source, IReadOnlyList<HitsoundBodyState> sourceBodies,
                                              IEnumerable<HitsoundColumn> targetColumns, IEnumerable<HitsoundBody> targetBodies, HitsoundCopyOptions options)
        {
            var result = new HitsoundCopyResult();

            var columns = targetColumns.Select(c => (column: c, state: findClosest(source, s => s.Time, c.Time, options.Leniency))).ToList();

            // hit objects of which some hitsound has a counterpart in the source.
            var matchedObjects = columns.Where(c => c.state != null).SelectMany(c => c.column.Targets).Select(t => t.HitObject).ToHashSet();

            foreach (var (column, state) in columns)
            {
                if (state != null)
                    apply(column, state, options, result);
                else
                    applyUnmatched(column, source, matchedObjects, options, result);
            }

            if (options.CopySliderBodies)
            {
                foreach (var body in targetBodies)
                {
                    // bodies without a counterpart only get the bank of the source, as most hitsound difficulties don't have sliders.
                    var state = findClosest(sourceBodies, s => s.StartTime, body.StartTime, options.Leniency);

                    if (state != null)
                        apply(body, state, options, result);
                    else if (shouldResetBank(body.HitObject, matchedObjects, options))
                        applyUnmatched(body, source, options, result);
                }
            }

            return result;
        }

        /// <summary>
        /// Whether the hitnormal bank of an unmatched part of an object with repeats (a slider node or body) should be replaced with the bank of the source at its time.
        /// </summary>
        /// <remarks>
        /// Slider nodes and bodies get the bank of the slider head when placed, which is inherited from the previous hit object,
        /// so keeping it for parts without a counterpart would make them play a bank which the source doesn't use there.
        /// Without overwriting, this is only done for objects of which another part has a counterpart, as fully unmatched objects keep what they were placed with.
        /// </remarks>
        private static bool shouldResetBank(HitObject hitObject, HashSet<HitObject> matchedObjects, HitsoundCopyOptions options)
            => options.CopySampleSets && (options.OverwriteUnmatched || matchedObjects.Contains(hitObject));

        /// <summary>
        /// The hitnormal bank which is active in the source at the given time, which is the bank of the last hitsound at or before it
        /// (like a sample set which stays active until the next one).
        /// </summary>
        private static string? getActiveBank(IReadOnlyList<HitsoundColumnState> source, double time, double leniency)
        {
            int min = 0;
            int max = source.Count;

            while (min < max)
            {
                int mid = (min + max) / 2;

                if (source[mid].Time <= time + leniency)
                    min = mid + 1;
                else
                    max = mid;
            }

            for (int i = min - 1; i >= 0; i--)
            {
                if (source[i].NormalBanks.Count > 0)
                    return source[i].NormalBanks[0];
            }

            return null;
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

        private static void applyUnmatched(HitsoundColumn column, IReadOnlyList<HitsoundColumnState> source, HashSet<HitObject> matchedObjects, HitsoundCopyOptions options,
                                           HitsoundCopyResult result)
        {
            bool muteSliderEnd = options.MuteUnmatchedSliderEnds && column.Targets.Count == 1 && column.Targets[0].Kind == HitsoundTargetKind.Tail;

            string? activeBank = getActiveBank(source, column.Time, options.Leniency);

            foreach (var target in column.Targets)
            {
                var samples = target.Samples.ToList();

                if (activeBank != null && isNode(target) && shouldResetBank(target.HitObject, matchedObjects, options))
                    samples = HitsoundSamples.WithNormalBank(samples, activeBank, true);

                if ((options.OverwriteUnmatched && options.CopyAdditions) || muteSliderEnd)
                    samples = HitsoundSamples.WithoutAdditions(samples);

                if (muteSliderEnd)
                    samples = HitsoundSamples.WithVolume(samples, DrawableHitObject.MINIMUM_SAMPLE_VOLUME);

                setSamples(target, samples, result);
            }
        }

        private static bool isNode(HitsoundTarget target)
            => target.Kind == HitsoundTargetKind.Head || target.Kind == HitsoundTargetKind.Repeat || target.Kind == HitsoundTargetKind.Tail;

        private static void applyUnmatched(HitsoundBody body, IReadOnlyList<HitsoundColumnState> source, HitsoundCopyOptions options, HitsoundCopyResult result)
        {
            string? activeBank = getActiveBank(source, body.StartTime, options.Leniency);

            if (activeBank == null)
                return;

            var samples = HitsoundSamples.WithNormalBank(body.Samples, activeBank, true);

            if (HitsoundSamples.AreEquivalent(samples, body.Samples))
                return;

            body.SetSamples(samples);
            result.ChangedHitObjects.Add(body.HitObject);
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

            if (!options.PreserveMutedVolumes || current.Count == 0)
                return true;

            // volume 0 means inherit from the timing point, not muted.
            // muted hitsounds (e.g. slider ends) are explicitly set to at most MINIMUM_SAMPLE_VOLUME.
            int maxVolume = current.Max(s => s.Volume);
            return maxVolume == 0 || maxVolume > DrawableHitObject.MINIMUM_SAMPLE_VOLUME;
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
        /// This also gives unmatched slider nodes and bodies the hitnormal bank which is active in the source at their time,
        /// which is otherwise only done for sliders of which another part has a counterpart.
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
