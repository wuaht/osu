// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Utils;
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
        /// Jumps longer than this arc slightly upwards, like a flicking wrist.
        /// </summary>
        private const float arc_jump_distance = 125;

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
                        cursor.Position = RealisticMath.MoveIntoCircle(cursor.Position, hitObjects[nextHitIndex].StackedPosition, (float)hitObjects[nextHitIndex].Radius * 0.8f);
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
            moveTo(dt, next?.StackedPosition ?? cursor.Position, new DynamicsParameters(2, 0.5f, 0.5f));
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
            Vector2 ballPosition = slider.StackedPositionAt(Math.Clamp((time - slider.StartTime) / slider.Duration, 0, 1));

            // fast and short sliders are followed loosely, slow and long sliders closely.
            float followAmount = Interpolation.ValueAt(slider.Velocity, 1f, 0f, 0, 2) * Interpolation.ValueAt(slider.SpanDuration, 0f, 1f, 50, 150);
            Vector2 target = Vector2.Lerp(sliderPaths[slider].PositionAt(time), ballPosition, followAmount);

            float distance = Math.Clamp(Vector2.Distance(cursor.Position, target) / (float)slider.Radius, 0.5f, 6);

            moveTo(dt, target, new DynamicsParameters(MathF.Max(distance * 5, 5), MathF.Max(1, 2 - distance), 0.5f));
            moveTo(dt, ballPosition, new DynamicsParameters(1, 1, 3));

            // stay inside the follow circle, so that no ticks are missed.
            cursor.Position = RealisticMath.MoveIntoCircle(cursor.Position, ballPosition, (float)slider.Radius * 2);
        }

        private void approach(double time, float dt, OsuHitObject current, OsuHitObject next)
        {
            Vector2 from = current is Slider slider ? sliderPaths[slider].PositionAt(time) : current.StackedEndPosition;

            // aim for the near side of the next object.
            Vector2 to = RealisticMath.MoveIntoCircle((from + next.StackedPosition) / 2, next.StackedPosition, (float)next.Radius * 0.5f);

            double moveStartTime = Math.Max(getLeaveTime(current), next.StartTime - next.TimePreempt);

            if (time < moveStartTime)
            {
                moveTo(dt, from, new DynamicsParameters(2, 0.5f, 0.5f));
                return;
            }

            double moveDuration = next.StartTime - moveStartTime;
            Vector2 target = Interpolation.ValueAt(time, from, to, moveStartTime, moveStartTime + Math.Min(200, moveDuration * 0.75), Easing.Out);

            float distance = Vector2.Distance(from, to);
            float frequencyMultiplier = 1;

            if (distance > arc_jump_distance)
            {
                double gap = next.StartTime - current.GetEndTime();
                float progress = gap > 0 ? (float)Math.Clamp((time - current.GetEndTime()) / gap, 0, 1) : 1;

                target.Y -= (1 - progress) * (1 - progress) * distance * 0.1f;
                frequencyMultiplier = 1.5f;
            }

            // players never aim at exactly the same spot.
            target += new Vector2(
                RealisticMath.Random(current.StackedPosition) - 0.5f,
                RealisticMath.Random(current.StackedPosition + new Vector2(1, 0)) - 0.5f) * (float)current.Radius * 0.25f;

            double realDuration = ApplyModsToTimeDelta(moveStartTime, next.StartTime);
            float frequency = realDuration > 0 ? (float)(1000 / realDuration * 0.75 * (1 + Math.Log(distance + 1) * 0.05)) * frequencyMultiplier : 10;

            // start the movement gently.
            float fadeIn = Interpolation.ValueAt(time, 0.15f, 1f, moveStartTime, moveStartTime + Math.Min(250, moveDuration * 0.75));

            moveTo(dt, target, new DynamicsParameters(MathF.Min(MathF.Max(frequency, 0.5f) * fadeIn, 2) * 3, 1, 0.75f * fadeIn));
        }

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

        private static Vector2 rotate(Vector2 vector, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
        }

        #endregion
    }
}
