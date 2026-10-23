// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Resnapping
{
    /// <summary>
    /// Snapping logic ported from Mapping Tools.
    /// Unlike <see cref="ControlPointInfo.GetClosestSnappedTime(double,int,double?)"/>, every time is snapped to the closest tick of <i>any</i> of the given divisors,
    /// so a map mixing 1/4 and 1/3 rhythms keeps both intact.
    /// </summary>
    public static class Resnapper
    {
        /// <summary>
        /// Times within this many milliseconds before a timing point are snapped onto it.
        /// </summary>
        private const double timing_point_leniency = 10;

        /// <summary>
        /// Snaps <paramref name="time"/> to the closest tick of any of <paramref name="divisors"/>.
        /// </summary>
        /// <param name="controlPoints">The timing to snap against.</param>
        /// <param name="time">The time to snap.</param>
        /// <param name="divisors">Allowed fractions of a beat.</param>
        /// <param name="floor">Whether to floor the result to whole milliseconds, like the legacy file format expects.</param>
        public static double Resnap(ControlPointInfo controlPoints, double time, IReadOnlyList<double> divisors, bool floor = true)
        {
            if (controlPoints.TimingPoints.Count == 0)
                return time;

            var before = controlPoints.TimingPointAt(time);
            var after = controlPoints.TimingPointAfter(time);

            double newTime = time;
            double lowestDistance = double.PositiveInfinity;

            foreach (double divisor in divisors)
            {
                double tick = nearestMultiple(time - before.Time, before.BeatLength * divisor) + before.Time;
                double distance = Math.Abs(time - tick);

                if (distance < lowestDistance)
                {
                    lowestDistance = distance;
                    newTime = tick;
                }
            }

            if (after != null && newTime > before.Time + timing_point_leniency && newTime >= after.Time - timing_point_leniency)
                newTime = after.Time;

            return floor ? floorMilliseconds(newTime) : newTime;
        }

        /// <summary>
        /// Snaps a duration starting at <paramref name="time"/> to the closest multiple of any of <paramref name="divisors"/>.
        /// </summary>
        public static double ResnapDuration(ControlPointInfo controlPoints, double time, double duration, IReadOnlyList<double> divisors)
        {
            if (controlPoints.TimingPoints.Count == 0)
                return duration;

            double beatLength = controlPoints.TimingPointAt(time).BeatLength;

            double newDuration = duration;
            double lowestDistance = double.PositiveInfinity;

            foreach (double divisor in divisors)
            {
                double candidate = nearestMultiple(duration, beatLength * divisor);
                double distance = Math.Abs(duration - candidate);

                if (distance < lowestDistance)
                {
                    lowestDistance = distance;
                    newDuration = candidate;
                }
            }

            return newDuration;
        }

        /// <summary>
        /// Moves the start of <paramref name="hitObject"/> onto the closest tick.
        /// </summary>
        /// <returns>Whether the object moved.</returns>
        public static bool ResnapStart(IBeatmap beatmap, HitObject hitObject, IReadOnlyList<double> divisors)
        {
            double newTime = Resnap(beatmap.ControlPointInfo, hitObject.StartTime, divisors);

            if (Math.Abs(newTime - hitObject.StartTime) < 1e-6)
                return false;

            hitObject.StartTime = newTime;
            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            return true;
        }

        /// <summary>
        /// Snaps the end of <paramref name="hitObject"/>.
        /// Sliders keep their slider velocity and change their length instead, like in osu!stable.
        /// If no timing point lies inside the slider body, the span duration is snapped (so repeats stay on the grid),
        /// otherwise the absolute end time is snapped to the timing at the end.
        /// </summary>
        /// <returns>Whether the end moved.</returns>
        public static bool ResnapEnd(IBeatmap beatmap, HitObject hitObject, IReadOnlyList<double> divisors)
        {
            var controlPoints = beatmap.ControlPointInfo;

            switch (hitObject)
            {
                case IHasPathWithRepeats pathObject:
                {
                    int spans = pathObject.SpanCount();
                    double spanDuration = pathObject.Duration / spans;

                    if (spanDuration <= 0 || pathObject.Path.Distance <= 0)
                        return false;

                    double endTime = hitObject.GetEndTime();
                    bool timingChangeInBody = controlPoints.TimingPoints.Any(t => t.Time > hitObject.StartTime && t.Time <= endTime + 20);

                    double newSpanDuration = timingChangeInBody
                        ? spanDuration + (Resnap(controlPoints, endTime, divisors) - endTime) / spans
                        : ResnapDuration(controlPoints, hitObject.StartTime, spanDuration, divisors);

                    if (newSpanDuration <= 0 || Math.Abs(newSpanDuration - spanDuration) < 1e-6)
                        return false;

                    pathObject.Path.ExpectedDistance.Value = pathObject.Path.Distance * newSpanDuration / spanDuration;
                    hitObject.ApplyDefaults(controlPoints, beatmap.Difficulty);
                    return true;
                }

                case IHasDuration hasDuration:
                {
                    if (hasDuration.Duration <= 0)
                        return false;

                    double endTime = hitObject.GetEndTime();
                    double newEndTime = Resnap(controlPoints, endTime, divisors);

                    if (newEndTime <= hitObject.StartTime || Math.Abs(newEndTime - endTime) < 1e-6)
                        return false;

                    hasDuration.Duration = newEndTime - hitObject.StartTime;
                    hitObject.ApplyDefaults(controlPoints, beatmap.Difficulty);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes all non-timing control points from <paramref name="controlPoints"/> and returns copies of them.
        /// Timing points are kept.
        /// </summary>
        public static List<ControlPoint> ExtractNonTimingControlPoints(ControlPointInfo controlPoints)
        {
            var timingPoints = controlPoints.TimingPoints.ToArray();
            var others = controlPoints.AllControlPoints.Where(p => p is not TimingControlPoint).Select(p => p.DeepClone()).ToList();

            RemoveAll(controlPoints);

            foreach (var timingPoint in timingPoints)
                controlPoints.Add(timingPoint.Time, timingPoint);

            return others;
        }

        /// <summary>
        /// Re-adds non-timing control points in chronological order.
        /// Points which don't change anything are dropped, which cleans up redundant kiai and scroll speed changes.
        /// </summary>
        public static void AddNonTimingControlPoints(ControlPointInfo controlPoints, IEnumerable<ControlPoint> points)
        {
            foreach (var point in points.OrderBy(p => p.Time))
                controlPoints.Add(point.Time, point);
        }

        /// <summary>
        /// Removes every control point group while raising change events, so editor change tracking notices.
        /// </summary>
        public static void RemoveAll(ControlPointInfo controlPoints)
        {
            foreach (var group in controlPoints.Groups.ToArray())
                controlPoints.RemoveGroup(group);
        }

        private static double nearestMultiple(double value, double step)
        {
            if (step <= 0)
                return value;

            return Math.Floor(value / step + 0.5) * step;
        }

        // the epsilon protects against ticks like 1099.9999999 being floored a full millisecond early.
        private static double floorMilliseconds(double time) => Math.Floor(time + 1e-6);
    }
}
