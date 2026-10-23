// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Resnapping
{
    /// <summary>
    /// Copies timing points from one beatmap to another, ported from the Timing Copier in Mapping Tools.
    /// </summary>
    public static class TimingCopier
    {
        /// <summary>
        /// Replaces the timing points of <paramref name="target"/> with <paramref name="sourceTimingPoints"/>.
        /// </summary>
        /// <param name="target">The beatmap to change. Its control points must not contain legacy difficulty or sample points.</param>
        /// <param name="sourceTimingPoints">The timing points to copy.</param>
        /// <param name="mode">How hit objects, bookmarks and other control points react to the new timing.</param>
        /// <param name="divisors">The beat divisors used for snapping.</param>
        public static void Apply(IBeatmap target, IReadOnlyList<TimingControlPoint> sourceTimingPoints, TimingCopierResnapMode mode, IReadOnlyList<double> divisors)
        {
            if (sourceTimingPoints.Count == 0)
                return;

            var controlPoints = target.ControlPointInfo;
            var oldTiming = controlPoints.TimingPoints.ToArray();
            var otherPoints = Resnapper.ExtractNonTimingControlPoints(controlPoints);

            var hitObjects = target.HitObjects;

            // positions in beats under the old timing, used to preserve beat spacing.
            double[] startBeats = hitObjects.Select(h => toBeats(oldTiming, h.StartTime)).ToArray();
            double[] endBeats = hitObjects.Select(h => toBeats(oldTiming, h.GetEndTime())).ToArray();
            double[] otherBeats = otherPoints.Select(p => toBeats(oldTiming, p.Time)).ToArray();
            double[] bookmarkBeats = target.Bookmarks.Select(b => toBeats(oldTiming, b)).ToArray();

            Resnapper.RemoveAll(controlPoints);

            foreach (var timingPoint in sourceTimingPoints)
                controlPoints.Add(timingPoint.Time, timingPoint.DeepClone());

            var newTiming = controlPoints.TimingPoints.ToArray();

            switch (mode)
            {
                case TimingCopierResnapMode.PreserveBeatSpacing:
                    for (int i = 0; i < otherPoints.Count; i++)
                        otherPoints[i].Time = Resnapper.Resnap(controlPoints, fromBeats(newTiming, otherBeats[i]), divisors);

                    target.Bookmarks = bookmarkBeats.Select(b => (int)Resnapper.Resnap(controlPoints, fromBeats(newTiming, b), divisors)).Distinct().ToArray();
                    break;

                case TimingCopierResnapMode.Resnap:
                    foreach (var point in otherPoints)
                        point.Time = Resnapper.Resnap(controlPoints, point.Time, divisors);
                    break;
            }

            Resnapper.AddNonTimingControlPoints(controlPoints, otherPoints);

            for (int i = 0; i < hitObjects.Count; i++)
            {
                var hitObject = hitObjects[i];

                switch (mode)
                {
                    case TimingCopierResnapMode.PreserveBeatSpacing:
                    {
                        double newStart = Resnapper.Resnap(controlPoints, fromBeats(newTiming, startBeats[i]), divisors);
                        hitObject.StartTime = newStart;

                        // sliders follow the new beat length through their slider velocity, everything else needs its duration moved explicitly.
                        if (hitObject is IHasDuration hasDuration and not IHasPath && hasDuration.Duration > 0)
                        {
                            double newEnd = fromBeats(newTiming, endBeats[i]);

                            if (newEnd > newStart)
                                hasDuration.Duration = newEnd - newStart;
                        }

                        break;
                    }

                    case TimingCopierResnapMode.Resnap:
                        hitObject.StartTime = Resnapper.Resnap(controlPoints, hitObject.StartTime, divisors);
                        break;
                }

                hitObject.ApplyDefaults(controlPoints, target.Difficulty);
            }

            if (mode == TimingCopierResnapMode.KeepObjectsFixed)
                return;

            foreach (var hitObject in hitObjects)
                Resnapper.ResnapEnd(target, hitObject, divisors);
        }

        /// <summary>
        /// Converts a time into a number of beats counted from the first timing point.
        /// </summary>
        private static double toBeats(IReadOnlyList<TimingControlPoint> timing, double time)
        {
            if (timing.Count == 0)
                return time;

            double beats = 0;
            int i = 0;

            for (; i < timing.Count - 1 && timing[i + 1].Time <= time; i++)
                beats += (timing[i + 1].Time - timing[i].Time) / timing[i].BeatLength;

            return beats + (time - timing[i].Time) / timing[i].BeatLength;
        }

        /// <summary>
        /// The inverse of <see cref="toBeats"/>.
        /// </summary>
        private static double fromBeats(IReadOnlyList<TimingControlPoint> timing, double beats)
        {
            if (timing.Count == 0)
                return beats;

            double accumulated = 0;

            for (int i = 0; i < timing.Count - 1; i++)
            {
                double sectionBeats = (timing[i + 1].Time - timing[i].Time) / timing[i].BeatLength;

                if (beats < accumulated + sectionBeats)
                    return timing[i].Time + (beats - accumulated) * timing[i].BeatLength;

                accumulated += sectionBeats;
            }

            return timing[^1].Time + (beats - accumulated) * timing[^1].BeatLength;
        }
    }
}
