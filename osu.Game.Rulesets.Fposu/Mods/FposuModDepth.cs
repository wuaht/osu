// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Fposu.Mods
{
    /// <summary>
    /// Depth, whose objects actually come towards the camera in 3D (see <see cref="UI.FposuDepthLayer"/>), instead of being scaled in 2D.
    /// </summary>
    /// <remarks>
    /// The depths are the same as in osu!, in osu!pixels behind the playfield. Objects reach the playfield at their start time.
    /// </remarks>
    public class FposuModDepth : OsuModDepth, IUpdatableByPlayfield
    {
        // see OsuModDepth, which places its camera 200 osu!pixels in front of the playfield.
        private const float camera_distance = 200;

        /// <summary>
        /// The depth at which sliders are 1.5 times as large as on the playfield in osu!, which they approach after their start time.
        /// </summary>
        private const float slider_min_depth = camera_distance / 1.5f - camera_distance;

        private readonly ConditionalWeakTable<DrawableHitObject, StrongBox<float>> depths = new ConditionalWeakTable<DrawableHitObject, StrongBox<float>>();

        /// <summary>
        /// Returns the current depth of an object in osu!pixels behind the playfield (negative in front of it), or <c>null</c> if it isn't moved in depth (e.g. spinners).
        /// </summary>
        public float? GetDepth(DrawableHitObject drawable) => drawable is DrawableHitCircle or DrawableSlider && depths.TryGetValue(drawable, out var depth) ? depth.Value : null;

        // replaces the 2D transformations of osu!.
        public new void Update(Playfield playfield)
        {
            double time = playfield.Time.Current;

            foreach (var entry in playfield.HitObjectContainer.AliveEntries)
            {
                var drawable = entry.Value;

                switch (drawable)
                {
                    case DrawableHitCircle circle:
                        setDepth(circle, getHitObjectDepth(time, circle.HitObject));
                        break;

                    case DrawableSlider slider:
                        setDepth(slider, getSliderDepth(time, slider.HitObject));
                        break;
                }
            }
        }

        private void setDepth(DrawableHitObject drawable, float depth) => depths.GetValue(drawable, _ => new StrongBox<float>()).Value = depth;

        // the same as OsuModDepth.
        private float getHitObjectDepth(double time, OsuHitObject hitObject)
        {
            // Circles are always moving at the constant speed. They'll fade out before reaching the camera even at extreme conditions (AR 11, max depth).
            double speed = MaxDepth.Value / hitObject.TimePreempt;
            double appearTime = hitObject.StartTime - hitObject.TimePreempt;
            return MaxDepth.Value - (float)((Math.Max(time, appearTime) - appearTime) * speed);
        }

        // the same as OsuModDepth.
        private float getSliderDepth(double time, Slider hitObject)
        {
            double baseSpeed = MaxDepth.Value / hitObject.TimePreempt;
            double appearTime = hitObject.StartTime - hitObject.TimePreempt;

            // Allow slider to move at a constant speed if its scale at the end time will be lower than 1.5f
            float zEnd = MaxDepth.Value - (float)((Math.Max(hitObject.StartTime + hitObject.Duration, appearTime) - appearTime) * baseSpeed);

            if (zEnd > slider_min_depth)
                return getHitObjectDepth(time, hitObject);

            double offsetAfterStartTime = hitObject.Duration + 500;
            double slowSpeed = Math.Min(-slider_min_depth / offsetAfterStartTime, baseSpeed);

            double decelerationTime = hitObject.TimePreempt * 0.2;
            float decelerationDistance = (float)(decelerationTime * (baseSpeed + slowSpeed) * 0.5);

            if (time < hitObject.StartTime - decelerationTime)
            {
                float fullDistance = decelerationDistance + (float)(baseSpeed * (hitObject.TimePreempt - decelerationTime));
                return fullDistance - (float)((Math.Max(time, appearTime) - appearTime) * baseSpeed);
            }

            if (time < hitObject.StartTime)
            {
                double timeOffset = time - (hitObject.StartTime - decelerationTime);
                double deceleration = (slowSpeed - baseSpeed) / decelerationTime;
                return decelerationDistance - (float)(baseSpeed * timeOffset + deceleration * timeOffset * timeOffset * 0.5);
            }

            double endTime = hitObject.StartTime + offsetAfterStartTime;
            return -(float)((Math.Min(time, endTime) - hitObject.StartTime) * slowSpeed);
        }
    }
}
