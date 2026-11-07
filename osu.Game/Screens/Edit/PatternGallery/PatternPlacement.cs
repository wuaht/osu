// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.PatternGallery
{
    /// <summary>
    /// Turns the beatmap of a <see cref="Pattern"/> into hitobjects to be inserted into another beatmap.
    /// </summary>
    public static class PatternPlacement
    {
        /// <summary>
        /// The number of beats between two times, following every timing point in between.
        /// </summary>
        public static double BeatsBetween(ControlPointInfo controlPoints, double from, double to)
        {
            if (to < from)
                return -BeatsBetween(controlPoints, to, from);

            double beats = 0;

            while (from < to)
            {
                var timingPoint = controlPoints.TimingPointAt(from);
                double segmentEnd = Math.Min(to, nextTimingPointTime(controlPoints, from));

                beats += (segmentEnd - from) / timingPoint.BeatLength;
                from = segmentEnd;
            }

            return beats;
        }

        /// <summary>
        /// The time which lies the given number of beats after another time, following every timing point in between.
        /// </summary>
        public static double TimeAfterBeats(ControlPointInfo controlPoints, double from, double beats)
        {
            while (true)
            {
                var timingPoint = controlPoints.TimingPointAt(from);
                double next = nextTimingPointTime(controlPoints, from);
                double segmentBeats = (next - from) / timingPoint.BeatLength;

                if (beats <= segmentBeats)
                    return from + beats * timingPoint.BeatLength;

                beats -= segmentBeats;
                from = next;
            }
        }

        private static double nextTimingPointTime(ControlPointInfo controlPoints, double time)
            => controlPoints.TimingPoints.FirstOrDefault(t => t.Time > time)?.Time ?? double.PositiveInfinity;

        /// <summary>
        /// Converts the pattern to the target ruleset and moves its first hitobject to the given time.
        /// </summary>
        /// <param name="pattern">The beatmap of the pattern, as read from its file. Changed by this method.</param>
        /// <param name="target">The beatmap the hitobjects are inserted into.</param>
        /// <param name="ruleset">The ruleset of the target beatmap.</param>
        /// <param name="time">The time of the first hitobject.</param>
        /// <param name="scaleToTiming">Whether to keep the lengths in beats rather than milliseconds, such that the pattern follows the timing of the target beatmap.</param>
        /// <param name="snapTime">Snaps the times of the hitobjects, or null to keep them unsnapped.</param>
        /// <param name="preserveSliderVelocity">Whether to change the slider velocities such that the sliders keep their lengths relative to the rest of the pattern.</param>
        /// <exception cref="osu.Game.Rulesets.UI.BeatmapInvalidForRulesetException">If the pattern cannot be converted to the ruleset.</exception>
        public static List<HitObject> Place(Beatmap pattern, IBeatmap target, RulesetInfo ruleset, double time, bool scaleToTiming, Func<double, double>? snapTime, bool preserveSliderVelocity)
        {
            double sourceSliderMultiplier = pattern.Difficulty.SliderMultiplier;
            var sourceControlPoints = pattern.ControlPointInfo;

            // spread the columns of mania patterns over the key count of the target.
            if (ruleset.OnlineID == 3)
                pattern.Difficulty.CircleSize = target.Difficulty.CircleSize;

            var objects = new FlatWorkingBeatmap(pattern).GetPlayableBeatmap(ruleset).HitObjects.ToList();

            if (objects.Count == 0)
                return objects;

            double origin = objects.Min(h => h.StartTime);

            foreach (var h in objects)
            {
                double newStart = map(h.StartTime);
                double newEnd = map(h.GetEndTime());
                double oldDuration = h.GetEndTime() - h.StartTime;
                double newDuration = Math.Max(0, newEnd - newStart);

                if (h is IHasSliderVelocity slider)
                {
                    if (preserveSliderVelocity && oldDuration > 0 && newDuration > 0)
                    {
                        double sourceBase = sourceSliderMultiplier / sourceControlPoints.TimingPointAt(h.StartTime).BeatLength;
                        double targetBase = target.Difficulty.SliderMultiplier / target.ControlPointInfo.TimingPointAt(newStart).BeatLength;

                        slider.SliderVelocityMultiplier *= oldDuration / newDuration * sourceBase / targetBase;
                    }
                }
                else if (h is IHasDuration hasDuration)
                    hasDuration.Duration = newDuration;

                h.StartTime = newStart;
            }

            return objects;

            double map(double t)
            {
                double mapped = scaleToTiming
                    ? TimeAfterBeats(target.ControlPointInfo, time, BeatsBetween(sourceControlPoints, origin, t))
                    : time + t - origin;

                return snapTime?.Invoke(mapped) ?? mapped;
            }
        }
    }
}
