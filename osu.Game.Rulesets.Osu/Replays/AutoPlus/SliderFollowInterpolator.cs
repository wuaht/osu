// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// Follows the slider ball.
    /// </summary>
    public class SliderFollowInterpolator : IAutoPlusInterpolator
    {
        /// <summary>
        /// The time span used to compute the velocity of the slider ball.
        /// </summary>
        private const double derivative_step = 1;

        private readonly Slider slider;
        private readonly double speedFactor;

        public int StartTime { get; }

        public int EndTime { get; }

        public bool Ignored => false;

        public SliderFollowInterpolator(int startTime, int endTime, Slider slider, double speedFactor)
        {
            StartTime = startTime;
            EndTime = endTime;
            this.slider = slider;
            this.speedFactor = speedFactor;
        }

        public Vector2 PositionAtTime(int time) => positionAt(time);

        private Vector2 positionAt(double time)
        {
            if (slider.Duration <= 0)
                return slider.StackedPosition;

            double progress = Math.Clamp((time - slider.StartTime) / slider.Duration, 0, 1);
            return slider.StackedPositionAt(progress);
        }

        private Vector2 velocityAt(double time)
        {
            double start = Math.Max(time - derivative_step, slider.StartTime);
            double end = Math.Min(time + derivative_step, slider.EndTime);

            if (end <= start)
                return Vector2.Zero;

            return (positionAt(end) - positionAt(start)) / (float)(end - start);
        }

        public Vector2 DerivativeAtStartTime()
        {
            if (slider.Duration <= 0)
                return Vector2.Zero;

            Vector2 velocity = velocityAt(StartTime) * (float)speedFactor;

            if (velocity.LengthSquared > 25)
                velocity = velocity.Normalized() * 5;

            return velocity;
        }

        public Vector2 DerivativeAtEndTime()
        {
            if (slider.Duration <= 0)
                return Vector2.Zero;

            return velocityAt(EndTime) * (float)speedFactor;
        }
    }
}
