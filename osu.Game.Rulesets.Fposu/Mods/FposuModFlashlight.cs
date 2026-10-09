// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Fposu.Mods
{
    /// <summary>
    /// Flashlight, whose light is always exactly at the cursor, which is in the centre of the view (unlike in osu!, it doesn't follow the cursor with a delay).
    /// </summary>
    public partial class FposuModFlashlight : OsuModFlashlight, IApplicableToDrawableHitObject
    {
        private FposuFlashlight flashlight = null!;

        protected override Flashlight CreateFlashlight() => flashlight = new FposuFlashlight(this);

        public new void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            if (drawable is DrawableSlider s)
                s.OnUpdate += _ => flashlight.OnSliderTrackingChange(s);
        }

        /// <summary>
        /// The flashlight of osu!, without the follow delay.
        /// </summary>
        private partial class FposuFlashlight : Flashlight
        {
            // see OsuModFlashlight.
            private const double scale_animation_speed = 1.875 / 1000;

            public FposuFlashlight(FposuModFlashlight modFlashlight)
                : base(modFlashlight)
            {
                FlashlightSize = new Vector2(0, GetSize());
                FlashlightSmoothness = 1.4f;
            }

            public void OnSliderTrackingChange(DrawableSlider e)
            {
                // If a slider is in a tracking state, a further dim should be applied to the (remaining) visible portion of the playfield.
                FlashlightDim = Time.Current >= e.HitObject.StartTime && e.Tracking.Value ? 0.8f : 0.0f;
            }

            protected override void Update()
            {
                base.Update();

                // every frame rather than on mouse movement, as the cursor is also moved by turning the camera (and in replays, which aren't processed as mouse movement here).
                if (GetContainingInputManager() is InputManager inputManager)
                    FlashlightPosition = ToLocalSpace(inputManager.CurrentState.Mouse.Position);
            }

            protected override void UpdateFlashlightSize(float size)
            {
                double relativeDelta = Math.Abs(FlashlightSize.Y - size) / DefaultFlashlightSize;
                double duration = relativeDelta / scale_animation_speed;
                this.TransformTo(nameof(FlashlightSize), new Vector2(0, size), duration);
            }

            protected override string FragmentShader => "CircularFlashlight";
        }
    }
}
