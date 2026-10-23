// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Resnapping
{
    /// <summary>
    /// Tidies up a beatmap, ported from the Map Cleaner in Mapping Tools.
    /// </summary>
    public static class MapCleaner
    {
        /// <returns>The number of hit objects which were changed.</returns>
        public static int Apply(EditorBeatmap beatmap, MapCleanerOptions options)
        {
            string ruleset = beatmap.BeatmapInfo.Ruleset.ShortName;
            var changed = new HashSet<HitObject>();

            beatmap.BeginChange();

            // always rebuild the control points, which drops kiai and scroll speed changes that don't change anything.
            var otherPoints = Resnapper.ExtractNonTimingControlPoints(beatmap.ControlPointInfo);

            if (options.ResnapObjects)
            {
                foreach (var point in otherPoints)
                    point.Time = Resnapper.Resnap(beatmap.ControlPointInfo, point.Time, options.Divisors);
            }

            Resnapper.AddNonTimingControlPoints(beatmap.ControlPointInfo, otherPoints);

            if (options.ResnapObjects)
            {
                foreach (var hitObject in beatmap.HitObjects.ToArray())
                {
                    if (Resnapper.ResnapStart(beatmap, hitObject, options.Divisors))
                        changed.Add(hitObject);
                }

                foreach (var hitObject in beatmap.HitObjects.ToArray())
                {
                    if (Resnapper.ResnapEnd(beatmap, hitObject, options.Divisors))
                        changed.Add(hitObject);
                }
            }

            if (options.ResnapBookmarks && beatmap.Bookmarks.Count > 0)
            {
                int[] bookmarks = beatmap.Bookmarks.Select(b => (int)Resnapper.Resnap(beatmap.ControlPointInfo, b, options.Divisors)).Distinct().Order().ToArray();

                beatmap.Bookmarks.Clear();
                beatmap.Bookmarks.AddRange(bookmarks);
            }

            foreach (var hitObject in beatmap.HitObjects.ToArray())
            {
                bool hitObjectChanged = false;

                // additions decide the note colour in osu!taiko, so they can't be removed there.
                if (options.RemoveHitsounds && ruleset != @"taiko")
                    hitObjectChanged |= transformSamples(hitObject, ruleset, (_, samples) => removeAdditions(samples));

                if (options.RemoveMuting)
                    hitObjectChanged |= transformSamples(hitObject, ruleset, (isEnd, samples) => isEnd && samples.Count > 0 && samples.All(s => s.Volume <= EditorObjectEndMuting.MUTED_VOLUME)
                        ? EditorObjectEndMuting.Unmute(samples, getHeadSamples(beatmap, hitObject))
                        : samples);
                else if (options.MuteUnclickableHitsounds && ruleset != @"mania" && ruleset != @"taiko")
                    hitObjectChanged |= transformSamples(hitObject, ruleset, (isEnd, samples) => isEnd && samples.Any(s => s.Volume > EditorObjectEndMuting.MUTED_VOLUME)
                        ? EditorObjectEndMuting.Mute(samples, options.MuteWithSoftC999)
                        : samples);

                if (hitObjectChanged)
                    changed.Add(hitObject);
            }

            beatmap.UpdateAllHitObjects();

            if (ruleset == @"osu")
                fixDoubleTaps(beatmap);

            beatmap.EndChange();

            return changed.Count;
        }

        /// <summary>
        /// Makes sure circles come before sliders and spinners which start at the same time,
        /// so osu!stable doesn't play the circle after the slider head (a "2B double tap").
        /// </summary>
        private static void fixDoubleTaps(EditorBeatmap beatmap)
        {
            var hitObjects = beatmap.HitObjects;

            for (int i = 0; i < hitObjects.Count - 1; i++)
            {
                var current = hitObjects[i];
                var next = hitObjects[i + 1];

                if (current is not IHasDuration || next is IHasDuration || current.StartTime != next.StartTime)
                    continue;

                // re-adding inserts the object after all objects at the same time.
                beatmap.Remove(current);
                beatmap.Add(current);
                i--;
            }
        }

        /// <summary>
        /// Applies <paramref name="transform"/> to every sample list of <paramref name="hitObject"/>.
        /// The first argument of the transform is whether the list is played at a point which can't be clicked:
        /// a slider repeat or tail, or the end of an object without nodes such as a spinner.
        /// </summary>
        /// <returns>Whether any sample changed.</returns>
        private static bool transformSamples(HitObject hitObject, string ruleset, TransformSamples transform)
        {
            bool anyChanged = false;

            if (hitObject is IHasRepeats repeats)
            {
                for (int i = 0; i < repeats.NodeSamples.Count; i++)
                {
                    var transformed = transform(i > 0, repeats.NodeSamples[i]);

                    if (!transformed.SequenceEqual(repeats.NodeSamples[i], SampleComparer.INSTANCE))
                    {
                        repeats.NodeSamples[i] = transformed.ToList();
                        anyChanged = true;
                    }
                }
            }

            // hold notes play their samples at the head.
            bool samplesAtEnd = hitObject is IHasDuration and not IHasRepeats && ruleset != @"mania";
            var transformedSamples = transform(samplesAtEnd, hitObject.Samples);

            if (!transformedSamples.SequenceEqual(hitObject.Samples, SampleComparer.INSTANCE))
            {
                hitObject.Samples = transformedSamples.ToList();
                anyChanged = true;
            }

            return anyChanged;
        }

        private static IList<HitSampleInfo> removeAdditions(IList<HitSampleInfo> samples)
        {
            if (samples.Count == 0)
                return samples;

            var normal = samples.Where(s => s.Name == HitSampleInfo.HIT_NORMAL).ToList();

            if (normal.Count == 0)
                normal.Add(samples[0].With(newName: HitSampleInfo.HIT_NORMAL));

            // custom sample sets of muted samples are part of the muting, everything else goes back to the default sample set.
            return normal.Select(s => s.Volume <= EditorObjectEndMuting.MUTED_VOLUME ? s : s.With(newSuffix: null)).ToList();
        }

        private static IList<HitSampleInfo>? getHeadSamples(EditorBeatmap beatmap, HitObject hitObject)
        {
            if (hitObject is IHasRepeats repeats && repeats.NodeSamples.Count > 1)
                return repeats.NodeSamples[0];

            return beatmap.HitObjects.LastOrDefault(h => h.StartTime < hitObject.StartTime)?.Samples;
        }

        private delegate IList<HitSampleInfo> TransformSamples(bool isUnclickable, IList<HitSampleInfo> samples);

        /// <summary>
        /// <see cref="HitSampleInfo"/> equality ignores volume, which matters here.
        /// </summary>
        private class SampleComparer : IEqualityComparer<HitSampleInfo>
        {
            public static readonly SampleComparer INSTANCE = new SampleComparer();

            public bool Equals(HitSampleInfo? x, HitSampleInfo? y)
                => x == y || (x != null && y != null && x.Equals(y) && x.Volume == y.Volume && x.EditorAutoBank == y.EditorAutoBank);

            public int GetHashCode(HitSampleInfo obj) => obj.GetHashCode();
        }
    }

    public record MapCleanerOptions(
        double[] Divisors,
        bool ResnapObjects,
        bool ResnapBookmarks,
        bool RemoveHitsounds,
        bool RemoveMuting,
        bool MuteUnclickableHitsounds,
        bool MuteWithSoftC999);
}
