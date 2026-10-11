// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Tools for timing songs with a varying tempo (e.g. live recordings), where timing points are placed onto beats of the audio
    /// and the BPMs of the surrounding timing points are calculated from the number of beats between them.
    /// </summary>
    public static class VariableTiming
    {
        /// <summary>
        /// The number of whole beats between <paramref name="from"/> and <paramref name="to"/> with the given beat length, at least one.
        /// </summary>
        public static int CountBeats(double from, double to, double beatLength) => Math.Max(1, (int)Math.Round((to - from) / beatLength));

        /// <summary>
        /// The timing point preceding <paramref name="time"/>, if any.
        /// </summary>
        public static TimingControlPoint? TimingPointBefore(ControlPointInfo controlPointInfo, double time)
        {
            TimingControlPoint? result = null;

            foreach (var point in controlPointInfo.TimingPoints)
            {
                if (point.Time >= time)
                    break;

                result = point;
            }

            return result;
        }

        /// <summary>
        /// Calculates the outcome of placing a beat at <paramref name="anchorTime"/>, without changing the beatmap.
        /// </summary>
        /// <remarks>
        /// As every timing point starts a new bar, the new timing point is placed at the start of the bar which contains the beat,
        /// with its beat length chosen such that the beat lies exactly at <paramref name="anchorTime"/>. This keeps the bar lines intact.
        /// If the beat lies within the first bar of the preceding timing point, the start of the bar is already taken, so the timing point is placed at the beat itself.
        /// </remarks>
        /// <param name="controlPointInfo">The control points.</param>
        /// <param name="anchorTime">The time of the placed beat.</param>
        /// <param name="estimateBeatLength">Estimates the beat length of the audio in a time range (see <see cref="OnsetEnvelope.EstimateBeatLength"/>), if available.
        /// Only used to find the number of beats while the tempo isn't established yet, as the tempo of the timing is more reliable afterwards.</param>
        public static PlacementPreview CalculatePlacement(ControlPointInfo controlPointInfo, double anchorTime, Func<double, double, double?>? estimateBeatLength = null)
        {
            var previous = TimingPointBefore(controlPointInfo, anchorTime);
            var next = controlPointInfo.TimingPointAfter(anchorTime);

            // the new timing point continues the tempo of the timing point it is placed within.
            double referenceBeatLength = (previous ?? next)?.BeatLength ?? TimingControlPoint.DEFAULT_BEAT_LENGTH;

            if (IsEstablishingTempo(controlPointInfo))
            {
                double? estimate = previous != null
                    ? estimateBeatLength?.Invoke(Math.Min(previous.Time, anchorTime - 8000), anchorTime + 2000)
                    : estimateBeatLength?.Invoke(anchorTime, anchorTime + 10000);

                if (estimate != null)
                    referenceBeatLength = toCommonTempo(estimate.Value);
            }

            var preview = new PlacementPreview
            {
                Time = anchorTime,
                AnchorTime = anchorTime,
                Previous = previous,
                Next = next,
                BeatLength = referenceBeatLength,
            };

            if (previous == null)
            {
                if (next != null)
                {
                    preview.NextBeats = CountBeats(anchorTime, next.Time, referenceBeatLength);
                    preview.BeatLength = (next.Time - anchorTime) / preview.NextBeats;
                }

                return preview;
            }

            int beats = CountBeats(previous.Time, anchorTime, referenceBeatLength);
            int meter = previous.TimeSignature.Numerator;
            int beatInBar = beats % meter;
            int nextBeats = next != null ? CountBeats(anchorTime, next.Time, referenceBeatLength) : 0;

            if (beatInBar != 0 && beats >= meter)
            {
                // place the timing point at the start of the bar.
                preview.AnchorBeat = beatInBar;
                preview.PreviousBeats = beats - beatInBar;

                if (next != null)
                {
                    preview.BeatLength = (next.Time - anchorTime) / nextBeats;
                    preview.NextBeats = nextBeats + beatInBar;
                    preview.Time = anchorTime - beatInBar * preview.BeatLength;
                    preview.PreviousBeatLength = (preview.Time - previous.Time) / preview.PreviousBeats;
                }
                else
                {
                    preview.PreviousBeatLength = (anchorTime - previous.Time) / beats;
                    preview.BeatLength = preview.PreviousBeatLength;
                    preview.Time = previous.Time + preview.PreviousBeats * preview.PreviousBeatLength;
                }

                return preview;
            }

            // either on a bar line, or there's no bar line to place the timing point onto (which resets the bar lines).
            preview.PreviousBeats = beats;
            preview.PreviousBeatLength = (anchorTime - previous.Time) / beats;
            preview.BeatLength = preview.PreviousBeatLength;
            preview.IsOnBarLine = beatInBar == 0;

            if (next != null)
            {
                preview.NextBeats = nextBeats;
                preview.BeatLength = (next.Time - anchorTime) / nextBeats;
            }

            return preview;
        }

        /// <summary>
        /// Whether the tempo of the beatmap isn't established yet, as there's no timing point or only one which still has the default tempo (60 BPM).
        /// </summary>
        public static bool IsEstablishingTempo(ControlPointInfo controlPointInfo)
            => controlPointInfo.TimingPoints.Count == 0
               || (controlPointInfo.TimingPoints.Count == 1 && controlPointInfo.TimingPoints[0].BeatLength == TimingControlPoint.DEFAULT_BEAT_LENGTH);

        /// <summary>
        /// The relative change of a BPM caused by placing a timing point (see <see cref="FindLargeTempoChange"/>) above which the placement is probably a mistake,
        /// such as a beat counted too much or too little, or a click far away from a beat.
        /// </summary>
        public const double MAX_TEMPO_CHANGE = 0.1;

        /// <summary>
        /// Finds the largest change of a BPM caused by a placement, if it's larger than <see cref="MAX_TEMPO_CHANGE"/>.
        /// The BPM of the preceding timing point is compared to before, and the BPM of the new timing point to the tempo it's placed within.
        /// </summary>
        /// <returns>The BPM before and after the change, or <see langword="null"/> if all changes are small or the tempo isn't established yet.</returns>
        public static (double oldBeatLength, double newBeatLength)? FindLargeTempoChange(ControlPointInfo controlPointInfo, PlacementPreview preview)
        {
            if (IsEstablishingTempo(controlPointInfo))
                return null;

            (double oldBeatLength, double newBeatLength)? largest = null;
            double largestChange = MAX_TEMPO_CHANGE;

            if (preview.Previous != null && preview.PreviousBeats > 0)
                check(preview.Previous.BeatLength, preview.PreviousBeatLength);

            if ((preview.Previous ?? preview.Next) is TimingControlPoint reference)
                check(reference.BeatLength, preview.BeatLength);

            return largest;

            void check(double oldBeatLength, double newBeatLength)
            {
                double change = Math.Abs(oldBeatLength / newBeatLength - 1);

                if (change <= largestChange)
                    return;

                largest = (oldBeatLength, newBeatLength);
                largestChange = change;
            }
        }

        /// <summary>
        /// The lowest BPM a placed timing point continues with. Slower tempos are doubled.
        /// </summary>
        public const double MINIMUM_BPM = 55;

        /// <summary>
        /// The highest BPM a placed timing point continues with. Faster tempos are halved.
        /// </summary>
        public const double MAXIMUM_BPM = 200;

        /// <summary>
        /// Scales a beat length by powers of two into the range of common tempos (see <see cref="MINIMUM_BPM"/> and <see cref="MAXIMUM_BPM"/>).
        /// </summary>
        private static double toCommonTempo(double beatLength)
        {
            while (60000 / beatLength > MAXIMUM_BPM)
                beatLength *= 2;

            while (60000 / beatLength < MINIMUM_BPM)
                beatLength /= 2;

            return beatLength;
        }

        /// <summary>
        /// Removes a timing point and recalculates the BPM of the preceding timing point, such that the following timing point stays on a whole beat.
        /// </summary>
        public static void RemoveTimingPoint(EditorBeatmap beatmap, TimingControlPoint point, bool adjustHitObjects)
        {
            var controlPointInfo = beatmap.ControlPointInfo;

            var previous = TimingPointBefore(controlPointInfo, point.Time);
            var next = controlPointInfo.TimingPointAfter(point.Time);

            // the first timing point can't be removed if it's the only one.
            if (previous == null && next == null)
                return;

            beatmap.BeginChange();

            if (previous == null)
            {
                removeFromGroup(controlPointInfo, point);
                beatmap.EndChange();
                return;
            }

            int previousBeats = CountBeats(previous.Time, point.Time, previous.BeatLength);
            int nextBeats = next != null ? CountBeats(point.Time, next.Time, point.BeatLength) : 0;

            var oldLayout = new TimingLayout(new[]
            {
                new TimingAnchor(previous.Time, 0, previous.BeatLength),
                new TimingAnchor(point.Time, previousBeats, point.BeatLength),
            });

            var snapshot = new HitObjectSnapshot(beatmap, oldLayout, previous.Time, next?.Time ?? double.PositiveInfinity, adjustHitObjects);

            removeFromGroup(controlPointInfo, point);

            if (next != null)
                previous.BeatLength = (next.Time - previous.Time) / (previousBeats + nextBeats);

            snapshot.Apply(new TimingLayout(new[] { new TimingAnchor(previous.Time, 0, previous.BeatLength) }));

            beatmap.EndChange();
        }

        /// <summary>
        /// The time at which the last hit object of the beatmap ends.
        /// </summary>
        public static double GetEndTime(EditorBeatmap beatmap)
            => beatmap.HitObjects.Count > 0 ? beatmap.HitObjects.Max(h => h.GetEndTime()) : beatmap.BeatmapInfo.Length;

        private static void removeFromGroup(ControlPointInfo controlPointInfo, ControlPoint point)
        {
            var group = controlPointInfo.GroupAt(point.Time);

            if (group == null)
                return;

            group.Remove(point);

            if (group.ControlPoints.Count == 0)
                controlPointInfo.RemoveGroup(group);
        }

        /// <summary>
        /// The outcome of placing a timing point, as calculated by <see cref="CalculatePlacement"/>.
        /// </summary>
        public class PlacementPreview
        {
            /// <summary>
            /// The time of the new timing point.
            /// </summary>
            public double Time;

            /// <summary>
            /// The time of the placed beat.
            /// </summary>
            public double AnchorTime;

            /// <summary>
            /// The number of beats between the new timing point and the placed beat.
            /// </summary>
            public int AnchorBeat;

            /// <summary>
            /// The beat length of the new timing point.
            /// </summary>
            public double BeatLength;

            public TimingControlPoint? Previous;

            /// <summary>
            /// The number of beats between the preceding timing point and the new one.
            /// </summary>
            public int PreviousBeats;

            /// <summary>
            /// The recalculated beat length of the preceding timing point.
            /// </summary>
            public double PreviousBeatLength;

            public TimingControlPoint? Next;

            /// <summary>
            /// The number of beats between the new timing point and the following one.
            /// </summary>
            public int NextBeats;

            /// <summary>
            /// Whether the new timing point is placed onto a bar line of the preceding timing point. If not, the bar lines after it shift.
            /// </summary>
            public bool IsOnBarLine = true;
        }

        /// <summary>
        /// A timing point which places beat number <see cref="Beat"/> at <see cref="Time"/>.
        /// </summary>
        public readonly record struct TimingAnchor(double Time, double Beat, double BeatLength);

        /// <summary>
        /// Converts between times and beat numbers for a set of timing points, such that hit objects can keep their beat when the timing changes.
        /// </summary>
        public class TimingLayout
        {
            private readonly TimingAnchor[] anchors;

            public TimingLayout(IEnumerable<TimingAnchor> anchors)
            {
                this.anchors = anchors.OrderBy(a => a.Time).ToArray();

                if (this.anchors.Length == 0)
                    throw new ArgumentException("A timing layout requires at least one anchor.", nameof(anchors));
            }

            public double BeatAt(double time)
            {
                var anchor = anchors[0];

                foreach (var a in anchors)
                {
                    if (a.Time > time)
                        break;

                    anchor = a;
                }

                return anchor.Beat + (time - anchor.Time) / anchor.BeatLength;
            }

            public double TimeAt(double beat)
            {
                var anchor = anchors[0];

                foreach (var a in anchors)
                {
                    if (a.Beat > beat)
                        break;

                    anchor = a;
                }

                return anchor.Time + (beat - anchor.Beat) * anchor.BeatLength;
            }
        }

        /// <summary>
        /// Remembers the beats of the hit objects within a time range, to move them along with timing changes.
        /// </summary>
        public class HitObjectSnapshot
        {
            private readonly EditorBeatmap beatmap;
            private readonly bool adjust;
            private readonly List<(HitObject hitObject, double startBeat, double? endBeat)> entries = new List<(HitObject, double, double?)>();

            /// <param name="beatmap">The beatmap.</param>
            /// <param name="layout">The timing before the change.</param>
            /// <param name="startTime">The start of the time range of the affected hit objects (inclusive).</param>
            /// <param name="endTime">The end of the time range of the affected hit objects (exclusive).</param>
            /// <param name="adjust">Whether the hit objects should be moved. Otherwise they are only updated with the changed timing.</param>
            public HitObjectSnapshot(EditorBeatmap beatmap, TimingLayout layout, double startTime, double endTime, bool adjust)
            {
                this.beatmap = beatmap;
                this.adjust = adjust;

                foreach (var hitObject in beatmap.HitObjects)
                {
                    if (hitObject.StartTime < startTime || hitObject.StartTime >= endTime)
                        continue;

                    double? endBeat = hitObject is not IHasRepeats && hitObject is IHasDuration hasDuration ? layout.BeatAt(hasDuration.EndTime) : null;
                    entries.Add((hitObject, layout.BeatAt(hitObject.StartTime), endBeat));
                }
            }

            public void Apply(TimingLayout layout)
            {
                foreach (var (hitObject, startBeat, endBeat) in entries)
                {
                    if (adjust)
                    {
                        hitObject.StartTime = layout.TimeAt(startBeat);

                        if (endBeat != null && hitObject is IHasDuration hasDuration)
                            hasDuration.Duration = layout.TimeAt(endBeat.Value) - hitObject.StartTime;
                    }

                    beatmap.Update(hitObject);
                }
            }
        }

        /// <summary>
        /// Moves a beat of a timing point while recalculating the BPMs of it and the preceding timing point, such that the surrounding timing points stay on the same beats.
        /// </summary>
        public class TimingPointMove
        {
            public readonly TimingControlPoint Point;
            public readonly TimingControlPoint? Previous;
            public readonly TimingControlPoint? Next;

            /// <summary>
            /// The number of beats between <see cref="Previous"/> and <see cref="Point"/>, which is kept while moving.
            /// </summary>
            public readonly int PreviousBeats;

            /// <summary>
            /// The number of beats between <see cref="Point"/> and <see cref="Next"/>, which is kept while moving.
            /// </summary>
            public readonly int NextBeats;

            /// <summary>
            /// The beat of <see cref="Point"/> which is moved. Zero moves the timing point itself.
            /// </summary>
            public readonly int AnchorBeat;

            private readonly EditorBeatmap beatmap;
            private readonly ControlPoint[] movingPoints;
            private readonly HitObjectSnapshot snapshot;

            /// <summary>
            /// Whether the moved timing point continues the tempo of the preceding one (for newly placed timing points without a following timing point).
            /// </summary>
            private readonly bool followPreviousBeatLength;

            /// <summary>
            /// The time of the moved beat.
            /// </summary>
            public double AnchorTime => Point.Time + AnchorBeat * Point.BeatLength;

            /// <summary>
            /// Places a beat at <paramref name="time"/> (see <see cref="CalculatePlacement"/>), recalculating the BPMs of the new and the preceding timing point.
            /// The returned move can be used to further move the beat.
            /// </summary>
            public static TimingPointMove Place(EditorBeatmap beatmap, double time, bool adjustHitObjects, Func<double, double, double?>? estimateBeatLength = null)
            {
                var controlPointInfo = beatmap.ControlPointInfo;

                // there can only be one timing point at a time.
                if (controlPointInfo.GroupAt(time)?.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault() is TimingControlPoint existing)
                    return Begin(beatmap, existing, adjustHitObjects);

                var preview = CalculatePlacement(controlPointInfo, time, estimateBeatLength);

                // the beats of the hit objects are taken from the timing before placing the new timing point.
                var anchors = new List<TimingAnchor>();

                if (preview.Previous != null)
                    anchors.Add(new TimingAnchor(preview.Previous.Time, 0, preview.Previous.BeatLength));
                else if (preview.Next != null)
                    anchors.Add(new TimingAnchor(preview.Next.Time, preview.NextBeats, preview.Next.BeatLength));

                var point = new TimingControlPoint
                {
                    BeatLength = preview.BeatLength,
                    TimeSignature = (preview.Previous ?? preview.Next)?.TimeSignature ?? TimingControlPoint.DEFAULT.TimeSignature,
                };

                HitObjectSnapshot? snapshot = anchors.Count > 0
                    ? new HitObjectSnapshot(beatmap, new TimingLayout(anchors), preview.Previous?.Time ?? double.NegativeInfinity, preview.Next?.Time ?? double.PositiveInfinity, adjustHitObjects)
                    : null;

                double pointTime = Math.Round(preview.Time);

                // the start of the bar may coincide with another timing point after rounding.
                if (controlPointInfo.GroupAt(pointTime)?.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault() is TimingControlPoint coinciding)
                    return Begin(beatmap, coinciding, adjustHitObjects);

                controlPointInfo.GroupAt(pointTime, true).Add(point);

                return new TimingPointMove(beatmap, point, new ControlPoint[] { point }, preview.Previous, preview.Next, preview.PreviousBeats, preview.NextBeats,
                    preview.AnchorBeat, preview.Next == null, snapshot, adjustHitObjects, time);
            }

            /// <summary>
            /// Starts moving an existing timing point, along with the other control points of its group.
            /// </summary>
            public static TimingPointMove Begin(EditorBeatmap beatmap, TimingControlPoint point, bool adjustHitObjects)
            {
                var controlPointInfo = beatmap.ControlPointInfo;

                var previous = TimingPointBefore(controlPointInfo, point.Time);
                var next = controlPointInfo.TimingPointAfter(point.Time);

                int previousBeats = previous != null ? CountBeats(previous.Time, point.Time, previous.BeatLength) : 0;
                int nextBeats = next != null ? CountBeats(point.Time, next.Time, point.BeatLength) : 0;

                var movingPoints = controlPointInfo.GroupAt(point.Time)?.ControlPoints.ToArray() ?? new ControlPoint[] { point };

                return new TimingPointMove(beatmap, point, movingPoints, previous, next, previousBeats, nextBeats, 0, false, null, adjustHitObjects, point.Time);
            }

            private TimingPointMove(EditorBeatmap beatmap, TimingControlPoint point, ControlPoint[] movingPoints, TimingControlPoint? previous, TimingControlPoint? next,
                                    int previousBeats, int nextBeats, int anchorBeat, bool followPreviousBeatLength, HitObjectSnapshot? snapshot,
                                    bool adjustHitObjects, double anchorTime)
            {
                this.beatmap = beatmap;
                this.movingPoints = movingPoints;
                this.followPreviousBeatLength = followPreviousBeatLength;

                Point = point;
                Previous = previous;
                Next = next;
                PreviousBeats = previousBeats;
                NextBeats = nextBeats;
                AnchorBeat = anchorBeat;

                this.snapshot = snapshot ?? new HitObjectSnapshot(beatmap, createLayout(), previous?.Time ?? double.NegativeInfinity, next?.Time ?? double.PositiveInfinity, adjustHitObjects);

                MoveTo(anchorTime);
            }

            /// <summary>
            /// Moves the beat to <paramref name="anchorTime"/> and recalculates the BPMs. The timing point itself is placed at whole milliseconds.
            /// </summary>
            public void MoveTo(double anchorTime)
            {
                double pointTime;

                if (Next != null)
                    pointTime = anchorTime - AnchorBeat * (Next.Time - anchorTime) / (NextBeats - AnchorBeat);
                else if (followPreviousBeatLength && Previous != null)
                    pointTime = Previous.Time + PreviousBeats * (anchorTime - Previous.Time) / (PreviousBeats + AnchorBeat);
                else
                    pointTime = anchorTime - AnchorBeat * Point.BeatLength;

                pointTime = Math.Round(Math.Clamp(pointTime, (Previous?.Time ?? double.NegativeInfinity) + 1, (Next?.Time ?? double.PositiveInfinity) - 1));

                if (pointTime != Point.Time)
                    moveGroup(pointTime);

                if (Previous != null)
                    Previous.BeatLength = (Point.Time - Previous.Time) / PreviousBeats;

                if (Next != null)
                    Point.BeatLength = (Next.Time - Point.Time) / NextBeats;
                else if (followPreviousBeatLength && Previous != null)
                    Point.BeatLength = Previous.BeatLength;

                snapshot.Apply(createLayout());
            }

            private void moveGroup(double time)
            {
                var controlPointInfo = beatmap.ControlPointInfo;
                var target = controlPointInfo.GroupAt(time);

                // don't replace control points of another group which happens to be at the target time.
                if (target != null && target.ControlPoints.Any(existing => movingPoints.Any(p => p.GetType() == existing.GetType())))
                    return;

                foreach (var point in movingPoints)
                    removeFromGroup(controlPointInfo, point);

                target = controlPointInfo.GroupAt(time, true);

                foreach (var point in movingPoints)
                    target.Add(point);
            }

            private TimingLayout createLayout()
            {
                var anchors = new List<TimingAnchor>();

                if (Previous != null)
                    anchors.Add(new TimingAnchor(Previous.Time, 0, Previous.BeatLength));

                anchors.Add(new TimingAnchor(Point.Time, PreviousBeats, Point.BeatLength));

                return new TimingLayout(anchors);
            }
        }
    }
}
