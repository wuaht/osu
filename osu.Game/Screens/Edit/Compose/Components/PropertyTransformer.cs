// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Compose.Components
{
    /// <summary>
    /// A transform of a numerical property in the form of <c>value * multiplier + additive</c>.
    /// </summary>
    public readonly record struct PropertyTransform(double Multiplier, double Additive)
    {
        public static readonly PropertyTransform IDENTITY = new PropertyTransform(1, 0);

        public bool IsIdentity => Multiplier == 1 && Additive == 0;

        public double Apply(double value) => value * Multiplier + Additive;
    }

    /// <summary>
    /// Transforms the BPM, slider velocity and volume of a selection at once (like osu!stable's property transformer).
    /// </summary>
    public static class PropertyTransformer
    {
        /// <summary>
        /// The timing points which lie within the time span of <paramref name="objects"/>.
        /// </summary>
        public static IReadOnlyList<TimingControlPoint> GetAffectedTimingPoints(EditorBeatmap beatmap, IReadOnlyCollection<HitObject> objects)
        {
            if (objects.Count == 0)
                return Array.Empty<TimingControlPoint>();

            double start = objects.Min(h => h.StartTime);
            double end = objects.Max(h => h.GetEndTime());

            return beatmap.ControlPointInfo.TimingPoints.Where(p => p.Time >= start && p.Time <= end).ToArray();
        }

        /// <summary>
        /// Returns the beat length of <paramref name="beatLength"/> after transforming its BPM.
        /// Transforms resulting in a non-positive BPM leave the beat length unchanged.
        /// </summary>
        public static double TransformBeatLength(double beatLength, PropertyTransform transform)
        {
            double bpm = transform.Apply(60000 / beatLength);
            return bpm > 0 ? 60000 / bpm : beatLength;
        }

        public static double TransformSliderVelocity(double velocity, PropertyTransform transform) => Math.Clamp(transform.Apply(velocity), 0.1, 10);

        public static int TransformVolume(int volume, PropertyTransform transform)
            => Math.Clamp((int)Math.Round(transform.Apply(volume)), DrawableHitObject.MINIMUM_SAMPLE_VOLUME, 100);

        /// <summary>
        /// All sample lists of <paramref name="hitObject"/>, including the ones of its nodes.
        /// </summary>
        public static IEnumerable<IList<HitSampleInfo>> GetSampleLists(HitObject hitObject)
        {
            yield return hitObject.Samples;

            if (hitObject is IHasRepeats hasRepeats)
            {
                foreach (var node in hasRepeats.NodeSamples)
                    yield return node;
            }
        }

        public static void Apply(EditorBeatmap beatmap, IReadOnlyCollection<HitObject> objects, PropertyTransform bpm, PropertyTransform sliderVelocity, PropertyTransform volume)
        {
            beatmap.BeginChange();

            if (!bpm.IsIdentity)
            {
                foreach (var point in GetAffectedTimingPoints(beatmap, objects))
                    point.BeatLength = TransformBeatLength(point.BeatLength, bpm);
            }

            foreach (var h in objects)
            {
                bool changed = false;

                if (!sliderVelocity.IsIdentity && h is IHasSliderVelocity hasSliderVelocity)
                {
                    hasSliderVelocity.SliderVelocityMultiplier = TransformSliderVelocity(hasSliderVelocity.SliderVelocityMultiplier, sliderVelocity);
                    changed = true;
                }

                if (!volume.IsIdentity)
                {
                    foreach (var samples in GetSampleLists(h))
                    {
                        for (int i = 0; i < samples.Count; i++)
                            samples[i] = samples[i].With(newVolume: TransformVolume(samples[i].Volume, volume));
                    }

                    changed = true;
                }

                if (changed)
                    beatmap.Update(h);
            }

            beatmap.EndChange();
        }
    }
}
