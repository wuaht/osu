// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Timing
{
    public static class TimingSectionAdjustments
    {
        /// <summary>
        /// Returns all objects from <paramref name="beatmap"/> which are affected by the supplied <paramref name="timingControlPoint"/>.
        /// </summary>
        public static List<HitObject> HitObjectsInTimingRange(IBeatmap beatmap, TimingControlPoint timingControlPoint)
        {
            var (startTime, endTime) = getTimingRange(beatmap, timingControlPoint);

            return beatmap.HitObjects.Where(x => isInRange(x.StartTime, startTime, endTime)).ToList();
        }

        private static (double startTime, double endTime) getTimingRange(IBeatmap beatmap, TimingControlPoint timingControlPoint)
        {
            // If the first group, we grab all hitobjects prior to the next, if the last group, we grab all remaining hitobjects
            double startTime = beatmap.ControlPointInfo.TimingPoints.Any(x => x.Time < timingControlPoint.Time) ? timingControlPoint.Time : double.MinValue;
            double endTime = beatmap.ControlPointInfo.TimingPoints.FirstOrDefault(x => x.Time > timingControlPoint.Time)?.Time ?? double.MaxValue;

            return (startTime, endTime);
        }

        private static bool isInRange(double time, double startTime, double endTime) => Precision.AlmostBigger(time, startTime) && Precision.DefinitelyBigger(endTime, time);

        /// <summary>
        /// Moves all relevant objects after <paramref name="timingControlPoint"/>'s offset has been changed by <paramref name="adjustment"/>.
        /// </summary>
        public static void AdjustHitObjectOffset(IBeatmap beatmap, TimingControlPoint timingControlPoint, double adjustment)
        {
            foreach (HitObject hitObject in HitObjectsInTimingRange(beatmap, timingControlPoint))
            {
                hitObject.StartTime += adjustment;
            }

            if (beatmap is EditorBeatmap editorBeatmap)
                adjustTimelineMarkers(editorBeatmap, timingControlPoint, adjustment);
        }

        /// <summary>
        /// Moves the bookmarks and the preview point affected by <paramref name="timingControlPoint"/> along with its objects.
        /// </summary>
        private static void adjustTimelineMarkers(EditorBeatmap beatmap, TimingControlPoint timingControlPoint, double adjustment)
        {
            int intAdjustment = (int)Math.Round(adjustment);

            if (intAdjustment == 0)
                return;

            var (startTime, endTime) = getTimingRange(beatmap, timingControlPoint);

            if (beatmap.PreviewTime.Value >= 0 && isInRange(beatmap.PreviewTime.Value, startTime, endTime))
                beatmap.PreviewTime.Value += intAdjustment;

            int[] movedBookmarks = beatmap.Bookmarks.Where(b => isInRange(b, startTime, endTime)).ToArray();

            if (movedBookmarks.Length == 0)
                return;

            beatmap.Bookmarks.RemoveAll(b => movedBookmarks.Contains(b));
            beatmap.Bookmarks.AddRange(movedBookmarks.Select(b => b + intAdjustment));
        }

        /// <summary>
        /// Ensures all relevant objects are still snapped to the same beats after <paramref name="timingControlPoint"/>'s beat length / BPM has been changed.
        /// </summary>
        public static void SetHitObjectBPM(IBeatmap beatmap, TimingControlPoint timingControlPoint, double oldBeatLength)
        {
            foreach (HitObject hitObject in HitObjectsInTimingRange(beatmap, timingControlPoint))
            {
                double beat = (hitObject.StartTime - timingControlPoint.Time) / oldBeatLength;

                hitObject.StartTime = (beat * timingControlPoint.BeatLength) + timingControlPoint.Time;

                if (hitObject is not IHasRepeats && hitObject is IHasDuration hitObjectWithDuration)
                    hitObjectWithDuration.Duration *= timingControlPoint.BeatLength / oldBeatLength;
            }
        }
    }
}
