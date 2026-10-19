// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;

namespace osu.Game.Rulesets.Osu.Skinning
{
    public abstract partial class FollowCircle : CompositeDrawable
    {
        protected DrawableSlider? DrawableObject { get; private set; }

        private readonly IBindable<bool> tracking = new Bindable<bool>();

        private readonly Bindable<bool> disableAnimations = new Bindable<bool>();

        /// <summary>
        /// The scale of this follow circle while the slider is being tracked.
        /// </summary>
        protected virtual float TrackingScale => DrawableSliderBall.FOLLOW_AREA;

        protected FollowCircle()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(DrawableHitObject? hitObject, OsuConfigManager? config)
        {
            config?.BindWith(OsuSetting.SlopDisableFollowCircleAnimations, disableAnimations);

            DrawableObject = hitObject as DrawableSlider;

            if (DrawableObject != null)
            {
                tracking.BindTo(DrawableObject.Tracking);
                tracking.BindValueChanged(tracking =>
                {
                    if (DrawableObject.Judged)
                        return;

                    using (BeginAbsoluteSequence(Math.Max(Time.Current, DrawableObject.HitObject?.StartTime ?? 0)))
                    {
                        if (tracking.NewValue)
                            onSliderPress();
                        else
                            onSliderRelease();
                    }
                }, true);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (DrawableObject != null)
            {
                DrawableObject.HitObjectApplied += onHitObjectApplied;
                onHitObjectApplied(DrawableObject);

                DrawableObject.ApplyCustomUpdateState += updateStateTransforms;
                updateStateTransforms(DrawableObject, DrawableObject.State.Value);
            }
        }

        private void onHitObjectApplied(DrawableHitObject drawableObject)
        {
            // Sane defaults when a new hitobject is applied to the drawable slider.
            this.ScaleTo(1f)
                .FadeOut();

            // Immediately play out any pending transforms from press/release
            FinishTransforms(true);
        }

        private void updateStateTransforms(DrawableHitObject d, ArmedState state)
        {
            Debug.Assert(DrawableObject != null);

            switch (state)
            {
                case ArmedState.Hit:
                    switch (d)
                    {
                        case DrawableSliderTail:
                            // Use DrawableObject instead of local object because slider tail's
                            // HitStateUpdateTime is ~36ms before the actual slider end (aka slider
                            // tail leniency)
                            using (BeginAbsoluteSequence(DrawableObject.HitStateUpdateTime))
                                onSliderEnd();
                            break;

                        case DrawableSliderTick:
                        case DrawableSliderRepeat:
                            using (BeginAbsoluteSequence(d.HitStateUpdateTime))
                                onSliderTick();
                            break;
                    }

                    break;

                case ArmedState.Miss:
                    switch (d)
                    {
                        case DrawableSliderTail:
                        case DrawableSliderTick:
                        case DrawableSliderRepeat:
                            // Despite above comment, ok to use d.HitStateUpdateTime
                            // here, since on stable, the break anim plays right when the tail is
                            // missed, not when the slider ends
                            using (BeginAbsoluteSequence(d.HitStateUpdateTime))
                                onSliderBreak();
                            break;
                    }

                    break;
            }
        }

        // Without animations, the follow circle appears and disappears instantly and doesn't pulse on ticks.

        private void onSliderPress()
        {
            if (disableAnimations.Value)
                this.ScaleTo(TrackingScale).FadeIn();
            else
                OnSliderPress();
        }

        private void onSliderRelease()
        {
            if (disableAnimations.Value)
                this.FadeOut();
            else
                OnSliderRelease();
        }

        private void onSliderEnd()
        {
            if (disableAnimations.Value)
                this.FadeOut();
            else
                OnSliderEnd();
        }

        private void onSliderTick()
        {
            if (!disableAnimations.Value)
                OnSliderTick();
        }

        private void onSliderBreak()
        {
            if (disableAnimations.Value)
                this.FadeOut();
            else
                OnSliderBreak();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (DrawableObject != null)
            {
                DrawableObject.HitObjectApplied -= onHitObjectApplied;
                DrawableObject.ApplyCustomUpdateState -= updateStateTransforms;
            }
        }

        protected abstract void OnSliderPress();

        protected abstract void OnSliderRelease();

        protected abstract void OnSliderEnd();

        protected abstract void OnSliderTick();

        protected abstract void OnSliderBreak();
    }
}
