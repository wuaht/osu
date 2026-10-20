// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.Realistic
{
    /// <summary>
    /// The path a player takes through a slider: it only follows the slider ball as closely as needed to hit ticks, repeats and the tail,
    /// and leaves towards the next object as early as the slider end leniency allows.
    /// </summary>
    public class LenientSliderPath
    {
        /// <summary>
        /// The interval between the points which follow the slider ball, in milliseconds.
        /// </summary>
        private const double step_interval = 25;

        /// <summary>
        /// How much each point is pulled from the lazy path towards the slider ball.
        /// </summary>
        private const float ball_attraction = 0.1f;

        private readonly List<(Vector2 position, double time)> points = new List<(Vector2, double)>();

        /// <summary>
        /// The time at which the cursor can leave the slider.
        /// </summary>
        public double EndTime => points[^1].time;

        public LenientSliderPath(Slider slider, OsuHitObject? next)
        {
            points.Add((slider.StackedPosition, slider.StartTime));

            foreach (var nested in slider.NestedHitObjects)
            {
                if (nested is not (SliderTick or SliderRepeat or SliderTailCircle))
                    continue;

                var target = (OsuHitObject)nested;
                bool leavesTowardsNext = target is SliderTailCircle && next != null;

                double maxStepTime = leavesTowardsNext ? Math.Min(target.StartTime, slider.EndTime - 50) : target.StartTime;

                for (double time = points[^1].time + step_interval; time < maxStepTime; time += step_interval)
                {
                    var (lastPosition, lastTime) = points[^1];
                    Vector2 lazyPosition = Interpolation.ValueAt(time, lastPosition, target.StackedPosition, lastTime, target.StartTime);
                    Vector2 ballPosition = ballPositionAt(slider, time);

                    points.Add((Vector2.Lerp(RealisticMath.MoveIntoCircle(lazyPosition, ballPosition, (float)slider.Radius), ballPosition, ball_attraction), time));
                }

                if (leavesTowardsNext)
                {
                    // move towards the next object as far as the tail leniency allows.
                    var (lastPosition, lastTime) = points[^1];
                    double leaveTime = Math.Max(slider.EndTime + SliderEventGenerator.TAIL_LENIENCY, lastTime);
                    Vector2 towardsNext = Interpolation.ValueAt(leaveTime, lastPosition, next!.StackedPosition, lastTime, next.StartTime);

                    points.Add((RealisticMath.MoveIntoCircle(towardsNext, ballPositionAt(slider, leaveTime), (float)slider.Radius), leaveTime));
                    break;
                }

                points.Add((target.StackedPosition, target.StartTime));
            }
        }

        public Vector2 PositionAt(double time)
        {
            if (time <= points[0].time)
                return points[0].position;

            for (int i = 1; i < points.Count; i++)
            {
                if (time <= points[i].time)
                    return Interpolation.ValueAt(time, points[i - 1].position, points[i].position, points[i - 1].time, points[i].time);
            }

            return points[^1].position;
        }

        private static Vector2 ballPositionAt(Slider slider, double time) => slider.StackedPositionAt(Math.Clamp((time - slider.StartTime) / slider.Duration, 0, 1));
    }
}
