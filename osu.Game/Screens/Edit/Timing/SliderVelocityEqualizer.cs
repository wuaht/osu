// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Keeps the slider velocity the same across BPM changes (like osu!stable's "Equalize" button).
    /// </summary>
    public static class SliderVelocityEqualizer
    {
        /// <summary>
        /// Whether <paramref name="point"/> has a timing point before it whose velocity could be kept.
        /// </summary>
        public static bool CanEqualize(EditorBeatmap beatmap, TimingControlPoint point) => getPreviousTimingPoint(beatmap, point) != null;

        /// <summary>
        /// Sets the slider velocity of all objects in the section of <paramref name="point"/>
        /// such that they move at the same speed as the last object before <paramref name="point"/>.
        /// </summary>
        /// <returns>The number of objects which were changed.</returns>
        public static int Equalize(EditorBeatmap beatmap, TimingControlPoint point)
        {
            var previous = getPreviousTimingPoint(beatmap, point);

            if (previous == null)
                return 0;

            double sectionEnd = beatmap.ControlPointInfo.TimingPoints.FirstOrDefault(p => p.Time > point.Time)?.Time ?? double.PositiveInfinity;

            double referenceVelocity = beatmap.HitObjects.Where(h => h.StartTime < point.Time)
                                              .OfType<IHasSliderVelocity>()
                                              .LastOrDefault()?.SliderVelocityMultiplier ?? 1;

            // the speed of sliders is proportional to the slider velocity divided by the beat length.
            double equalizedVelocity = referenceVelocity * point.BeatLength / previous.BeatLength;

            var objectsInSection = beatmap.HitObjects.Where(h => h.StartTime >= point.Time && h.StartTime < sectionEnd && h is IHasSliderVelocity).ToArray();

            if (objectsInSection.Length == 0)
                return 0;

            beatmap.BeginChange();

            foreach (var h in objectsInSection)
            {
                var hasVelocity = (IHasSliderVelocity)h;
                double previousVelocity = hasVelocity.SliderVelocityMultiplier;

                hasVelocity.SliderVelocityMultiplier = equalizedVelocity;

                if (h is IHasPath hasPath && h is IHasDuration hasDuration && hasDuration.Duration > 0)
                    snapLength(beatmap, h, hasPath, hasDuration.Duration / ((h as IHasRepeats)?.SpanCount() ?? 1), previousVelocity / equalizedVelocity);

                beatmap.Update(h);
            }

            beatmap.EndChange();

            return objectsInSection.Length;
        }

        /// <summary>
        /// Changing the slider velocity changes the duration of a slider, so its length is adjusted slightly to keep its end on the beat snap divisor.
        /// </summary>
        /// <param name="beatmap">The beatmap containing <paramref name="hitObject"/>.</param>
        /// <param name="hitObject">The object whose slider velocity was changed.</param>
        /// <param name="hasPath">The path of <paramref name="hitObject"/>.</param>
        /// <param name="spanDuration">The duration of a span before the slider velocity was changed.</param>
        /// <param name="durationScale">The factor by which the slider velocity change scales the duration.</param>
        private static void snapLength(EditorBeatmap beatmap, HitObject hitObject, IHasPath hasPath, double spanDuration, double durationScale)
        {
            double tick = beatmap.GetBeatLengthAtTime(hitObject.StartTime);
            double newSpanDuration = spanDuration * durationScale;

            if (tick <= 0 || newSpanDuration <= 0)
                return;

            double snappedSpanDuration = Math.Max(1, Math.Round(newSpanDuration / tick)) * tick;

            hasPath.Path.ExpectedDistance.Value = hasPath.Path.Distance * snappedSpanDuration / newSpanDuration;
        }

        private static TimingControlPoint? getPreviousTimingPoint(EditorBeatmap beatmap, TimingControlPoint point)
            => beatmap.ControlPointInfo.TimingPoints.LastOrDefault(p => p.Time < point.Time);
    }
}
