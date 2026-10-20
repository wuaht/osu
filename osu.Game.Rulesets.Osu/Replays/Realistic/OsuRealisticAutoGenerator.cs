// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.Realistic
{
    /// <summary>
    /// Generates a replay whose cursor moves like a player would: it eases into jumps, overshoots slightly and only follows sliders as closely as needed.
    /// </summary>
    /// <remarks>
    /// The cursor is simulated with <see cref="SecondOrderDynamics"/> which follows a target position depending on the current object.
    /// The simulation always starts from the beginning of the beatmap, so the replay is the same regardless of where it is seeked to.
    /// </remarks>
    public class OsuRealisticAutoGenerator : OsuAutoGeneratorBase
    {
        public new OsuBeatmap Beatmap => (OsuBeatmap)base.Beatmap;

        /// <summary>
        /// How long the replay continues after the last object.
        /// </summary>
        private const double end_padding = 500;

        /// <summary>
        /// The largest simulation step, in seconds. Larger steps (e.g. during breaks) are shortened to keep the simulation stable.
        /// </summary>
        private const float max_step = 0.1f;

        /// <summary>
        /// Follows smooth targets closely without overshooting.
        /// </summary>
        private static readonly DynamicsParameters tracking = new DynamicsParameters(8, 1, 1);

        /// <summary>
        /// How far the cursor lags behind a moving target with <see cref="tracking"/>, in milliseconds.
        /// Targets are sampled this far ahead to compensate, like a player anticipating the movement.
        /// </summary>
        private const double tracking_lag = 1000 / (Math.PI * 8) * 0.5;

        private static readonly DynamicsParameters resting = new DynamicsParameters(2, 1, 1);

        private static readonly Vector2 playfield_min = new Vector2(-512, -384);
        private static readonly Vector2 playfield_max = new Vector2(1024, 768);

        private readonly IReadOnlyList<OsuHitObject> hitObjects;
        private readonly Dictionary<Slider, LenientSliderPath> sliderPaths = new Dictionary<Slider, LenientSliderPath>();
        private readonly Dictionary<OsuHitObject, double> leaveTimes = new Dictionary<OsuHitObject, double>();

        private SecondOrderDynamics cursor = null!;

        public OsuRealisticAutoGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            : base(beatmap, mods)
        {
            hitObjects = Beatmap.HitObjects;

            for (int i = 0; i < hitObjects.Count; i++)
            {
                leaveTimes[hitObjects[i]] = computeLeaveTime(hitObjects[i]);

                if (hitObjects[i] is not Slider slider)
                    continue;

                var next = i + 1 < hitObjects.Count ? hitObjects[i + 1] : null;
                sliderPaths[slider] = new LenientSliderPath(slider, next is Spinner ? null : next);
            }
        }

        public override Replay Generate()
        {
            if (hitObjects.Count == 0)
                return Replay;

            var buttonEvents = generateButtonEvents();
            var heldButtons = new Dictionary<OsuAction, int>();

            double startTime = hitObjects[0].StartTime - hitObjects[0].TimePreempt;
            double endTime = Math.Max(hitObjects.Max(h => h.GetEndTime()) + end_padding, buttonEvents[^1].time);

            cursor = new SecondOrderDynamics(SPINNER_CENTRE);

            int currentIndex = -1;
            int nextHitIndex = 0;
            int nextEventIndex = 0;
            double lastTime = startTime;

            for (double time = startTime;; time = nextFrameTime(time, endTime, buttonEvents, nextEventIndex))
            {
                while (currentIndex + 1 < hitObjects.Count && hitObjects[currentIndex + 1].StartTime <= time)
                    currentIndex++;

                for (; nextEventIndex < buttonEvents.Count && buttonEvents[nextEventIndex].time <= time; nextEventIndex++)
                {
                    var (_, action, pressed) = buttonEvents[nextEventIndex];
                    heldButtons[action] = heldButtons.GetValueOrDefault(action) + (pressed ? 1 : -1);
                }

                float dt = (float)Math.Clamp(ApplyModsToTimeDelta(lastTime, time) / 1000, 0, max_step);
                updateCursor(time, dt, currentIndex);

                for (; nextHitIndex < hitObjects.Count && hitObjects[nextHitIndex].StartTime <= time; nextHitIndex++)
                {
                    // make sure the press lands on the object.
                    if (hitObjects[nextHitIndex] is not Spinner)
                        cursor.ConstrainToCircle(hitObjects[nextHitIndex].StackedPosition, (float)hitObjects[nextHitIndex].Radius * 0.8f);
                }

                var actions = heldButtons.Where(b => b.Value > 0).Select(b => b.Key).ToArray();
                Frames.Add(new OsuReplayFrame(time, cursor.Position, actions));

                if (time >= endTime)
                    break;

                lastTime = time;
            }

            return Replay;
        }

        private double nextFrameTime(double time, double endTime, List<(double time, OsuAction action, bool pressed)> buttonEvents, int nextEventIndex)
        {
            double next = time + GetFrameDelay(time);

            // button events need frames at their exact time, so that hits are judged at the right time.
            if (nextEventIndex < buttonEvents.Count)
                next = Math.Min(next, buttonEvents[nextEventIndex].time);

            return Math.Min(next, endTime);
        }

        #region Buttons

        /// <summary>
        /// Generates the button presses and releases for all objects, sorted by time.
        /// Close objects alternate between both buttons like <see cref="OsuAutoGenerator"/>.
        /// </summary>
        private List<(double time, OsuAction action, bool pressed)> generateButtonEvents()
        {
            var presses = new List<(OsuHitObject hitObject, OsuAction action)>();
            OsuAction lastAction = OsuAction.RightButton;

            for (int i = 0; i < hitObjects.Count; i++)
            {
                var hitObject = hitObjects[i];
                bool alternate = i > 0 && ApplyModsToTimeDelta(hitObjects[i - 1].GetEndTime(), hitObject.StartTime) < OsuAutoGenerator.MIN_FRAME_SEPARATION_FOR_ALTERNATING;

                lastAction = alternate && lastAction == OsuAction.LeftButton ? OsuAction.RightButton : OsuAction.LeftButton;
                presses.Add((hitObject, lastAction));
            }

            var events = new List<(double time, OsuAction action, bool pressed)>();

            for (int i = 0; i < presses.Count; i++)
            {
                var (hitObject, action) = presses[i];
                double release = hitObject.GetEndTime() + KEY_UP_DELAY;

                // release before the next press of the same button.
                for (int j = i + 1; j < presses.Count && presses[j].hitObject.StartTime <= release; j++)
                {
                    if (presses[j].action == action)
                    {
                        release = presses[j].hitObject.StartTime - 1;
                        break;
                    }
                }

                events.Add((hitObject.StartTime, action, true));
                events.Add((Math.Max(release, hitObject.StartTime + 1), action, false));
            }

            // releases go first, as the release of an earlier press may coincide with the next press of the same button.
            return events.OrderBy(e => e.time).ThenBy(e => e.pressed).ToList();
        }

        #endregion

        #region Cursor movement

        private void updateCursor(double time, float dt, int currentIndex)
        {
            var current = currentIndex >= 0 ? hitObjects[currentIndex] : null;
            var next = currentIndex + 1 < hitObjects.Count ? hitObjects[currentIndex + 1] : null;

            if (current != null && time < getLeaveTime(current))
            {
                switch (current)
                {
                    case Spinner spinner:
                        spin(time, dt, spinner);
                        return;

                    case Slider slider:
                        followSlider(time, dt, slider);
                        return;
                }
            }

            if (current != null && next != null)
            {
                approach(time, dt, current, next);
                return;
            }

            // before the first object, move towards it. after the last object, rest.
            moveTo(dt, next?.StackedPosition ?? cursor.Position, resting);
        }

        private void spin(double time, float dt, Spinner spinner)
        {
            float angle = (float)(time - spinner.StartTime) * 0.05f;
            float rotation = MathF.Floor(angle / (2 * MathF.PI));

            // vary the shape of the circle with every rotation.
            var scale = new Vector2(0.5f + RealisticMath.Random(new Vector2(rotation, 1)) * 0.4f, 0.8f + RealisticMath.Random(new Vector2(rotation, 2)) * 0.6f);
            float tilt = MathF.PI * (0.2f + RealisticMath.Random(new Vector2(rotation, 3)) * 0.1f);

            Vector2 target = SPINNER_CENTRE + rotate(rotate(new Vector2(0, 120), angle) * scale, tilt);
            float random = RealisticMath.Random(new Vector2(rotation, 4));

            moveTo(dt, target, new DynamicsParameters(4 + random, 0.8f + random, 0.5f));
        }

        private void followSlider(double time, float dt, Slider slider)
        {
            double aheadTime = Math.Min(time + ApplyModsToRate(time, tracking_lag), slider.EndTime);

            // slow sliders are followed closely, fast and short sliders cut corners.
            float followAmount = (float)(Math.Clamp(1 - slider.Velocity / 2, 0, 1) * Math.Clamp((slider.SpanDuration - 50) / 100, 0, 1));
            Vector2 target = Vector2.Lerp(sliderPaths[slider].PositionAt(aheadTime), ballPositionAt(slider, aheadTime), followAmount);

            moveTo(dt, target, tracking);

            // stay inside the follow circle, so that no ticks are missed.
            cursor.ConstrainToCircle(ballPositionAt(slider, time), (float)slider.Radius * 2);
        }

        private void approach(double time, float dt, OsuHitObject current, OsuHitObject next)
        {
            double leaveTime = getLeaveTime(current);
            Vector2 from = current is Slider slider ? sliderPaths[slider].PositionAt(leaveTime) : current.StackedEndPosition;

            // aim for the near side of the next object, never at exactly the same spot.
            Vector2 to = RealisticMath.MoveIntoCircle((from + next.StackedPosition) / 2, next.StackedPosition, (float)next.Radius * 0.5f)
                         + new Vector2(RealisticMath.Random(next.StackedPosition) - 0.5f, RealisticMath.Random(next.StackedPosition + new Vector2(1, 0)) - 0.5f) * (float)next.Radius * 0.25f;

            float distance = Vector2.Distance(from, to);

            // during pauses, players wait and only start moving when needed (Fitts's law), arriving at the next object just in time.
            double gap = next.StartTime - leaveTime;
            double realGap = ApplyModsToTimeDelta(leaveTime, next.StartTime);
            double movementTime = 80 + 90 * Math.Log2(distance / (2 * next.Radius) + 1);
            double moveStartTime = realGap > movementTime ? next.StartTime - gap * movementTime / realGap : leaveTime;

            double aheadTime = time + ApplyModsToRate(time, tracking_lag);
            double progress = next.StartTime > moveStartTime ? Math.Clamp((aheadTime - moveStartTime) / (next.StartTime - moveStartTime), 0, 1) : 1;

            Vector2 target = Vector2.Lerp(from, to, (float)minimumJerk(progress));

            // movements curve slightly to one side.
            if (distance > 0)
            {
                Vector2 direction = (to - from) / distance;
                float side = RealisticMath.Random(next.StackedPosition + new Vector2(0, 1)) < 0.5f ? -1 : 1;

                target += new Vector2(-direction.Y, direction.X) * side * MathF.Sin(MathF.PI * (float)progress) * distance * 0.05f;
            }

            moveTo(dt, target, tracking);
        }

        /// <summary>
        /// The progress of a natural point-to-point hand movement, which starts and stops smoothly.
        /// </summary>
        private static double minimumJerk(double t) => t * t * t * (10 - 15 * t + 6 * t * t);

        private void moveTo(float dt, Vector2 target, DynamicsParameters parameters)
        {
            cursor.Parameters = parameters;
            cursor.Update(dt, Vector2.Clamp(target, playfield_min, playfield_max));
        }

        /// <summary>
        /// Returns the time at which the cursor can start moving away from <paramref name="hitObject"/>.
        /// </summary>
        private double getLeaveTime(OsuHitObject hitObject) => leaveTimes[hitObject];

        private static double computeLeaveTime(OsuHitObject hitObject)
        {
            switch (hitObject)
            {
                case Spinner spinner:
                    return Math.Max(spinner.StartTime, spinner.EndTime - 150);

                case Slider slider:
                    // tiny sliders are hit like circles.
                    if (slider.Duration < 100 && Vector2.Distance(slider.StackedPosition, slider.StackedEndPosition) < 50)
                        return slider.StartTime;

                    double lastNestedTime = slider.NestedHitObjects.OfType<OsuHitObject>().Where(h => h is SliderTick or SliderRepeat).Select(h => h.StartTime).DefaultIfEmpty(slider.StartTime).Max();
                    return Math.Max(slider.EndTime + SliderEventGenerator.TAIL_LENIENCY, Math.Max(slider.EndTime - slider.SpanDuration, lastNestedTime));

                default:
                    return hitObject.StartTime;
            }
        }

        private static Vector2 ballPositionAt(Slider slider, double time) => slider.StackedPositionAt(Math.Clamp((time - slider.StartTime) / slider.Duration, 0, 1));

        private static Vector2 rotate(Vector2 vector, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
        }

        #endregion
    }
}
