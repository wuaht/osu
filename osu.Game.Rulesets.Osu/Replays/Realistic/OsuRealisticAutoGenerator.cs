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

        /// <summary>
        /// How far the cursor wanders around its target while following a slider, relative to the slider's radius.
        /// </summary>
        private const float slider_wander_amount = 0.45f;

        /// <summary>
        /// How long it takes the wandering offset to change direction, in milliseconds.
        /// </summary>
        private const double slider_wander_interval = 220;

        /// <summary>
        /// How far from the centre of the slider body the cursor may move, relative to the slider's radius.
        /// Slightly smaller than 1, so that the cursor stays visibly inside the body including its border.
        /// </summary>
        private const float slider_body_limit = 0.8f;

        /// <summary>
        /// How far the cursor may run ahead of or fall behind the slider ball while following a slider, in milliseconds.
        /// </summary>
        private const double slider_timing_variation = 60;

        /// <summary>
        /// How long it takes the cursor to change between running ahead and falling behind the slider ball, in milliseconds.
        /// </summary>
        private const double slider_timing_interval = 400;

        /// <summary>
        /// How long the cursor takes to settle onto a slider's target after hitting its head, in milliseconds.
        /// </summary>
        private const double slider_entry_duration = 150;

        /// <summary>
        /// How far the cursor may run ahead of or fall behind the slider ball at most, relative to the slider's radius.
        /// Keeps the cursor well within the follow circle on fast sliders.
        /// </summary>
        private const float slider_timing_max_distance = 0.75f;

        /// <summary>
        /// How far from its centre a slider may extend for the cursor to stay still on it, relative to the slider's radius.
        /// This applies to short buzz and kick sliders, as the follow circle covers the whole slider.
        /// </summary>
        private const float still_slider_extent = 1.2f;

        /// <summary>
        /// The longest time between two objects for the cursor to flow through them as part of a stream, in milliseconds.
        /// </summary>
        private const double stream_max_interval = 125;

        private static readonly Vector2 playfield_min = new Vector2(-512, -384);
        private static readonly Vector2 playfield_max = new Vector2(1024, 768);

        private readonly IReadOnlyList<OsuHitObject> hitObjects;
        private readonly Dictionary<Slider, LenientSliderPath> sliderPaths = new Dictionary<Slider, LenientSliderPath>();
        private readonly Dictionary<Slider, List<Vector2>> sliderBodies = new Dictionary<Slider, List<Vector2>>();
        private readonly Dictionary<Slider, Vector2> stillSliderPositions = new Dictionary<Slider, Vector2>();
        private readonly Dictionary<OsuHitObject, double> leaveTimes = new Dictionary<OsuHitObject, double>();

        private SecondOrderDynamics cursor = null!;

        private OsuHitObject? targetSource;
        private OsuHitObject? leftObject;
        private Vector2 leavePosition;
        private Vector2 lastTarget;
        private Slider? enteredSlider;
        private Vector2 sliderEntryOffset;

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

                var body = new List<Vector2>();
                slider.Path.GetPathToProgress(body, 0, 1);
                for (int j = 0; j < body.Count; j++)
                    body[j] += slider.StackedPosition;
                sliderBodies[slider] = body;

                if (body.Count > 0)
                {
                    Vector2 min = body.Aggregate(Vector2.ComponentMin);
                    Vector2 max = body.Aggregate(Vector2.ComponentMax);
                    Vector2 centre = closestPointOnPath(body, (min + max) / 2);

                    if (body.All(p => Vector2.Distance(p, centre) <= slider.Radius * still_slider_extent))
                        stillSliderPositions[slider] = centre;
                }
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
            lastTarget = SPINNER_CENTRE;
            targetSource = null;
            leftObject = null;
            enteredSlider = null;

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
                        setTargetSource(spinner);
                        spin(time, dt, spinner);
                        return;

                    case Slider slider:
                        setTargetSource(slider);
                        followSlider(time, dt, slider);
                        return;
                }
            }

            if (current != null && next != null)
            {
                // the target moves continuously through streams, so it keeps the source it had when the stream started.
                if (!flowsInto(currentIndex))
                    setTargetSource(next);

                approach(time, dt, currentIndex);
                return;
            }

            // before the first object, move towards it. after the last object, rest.
            setTargetSource(next);
            moveTo(dt, next?.StackedPosition ?? cursor.Position, resting);
        }

        /// <summary>
        /// Sets the object which the cursor's target currently follows.
        /// The target jumps when this changes, which must not be mistaken for a fast moving target.
        /// </summary>
        private void setTargetSource(OsuHitObject? source)
        {
            if (source == targetSource)
                return;

            targetSource = source;
            cursor.ResetTarget();
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
            // short buzz and kick sliders are held by keeping the cursor still within the follow circle.
            if (stillSliderPositions.TryGetValue(slider, out Vector2 stillPosition))
            {
                moveTo(dt, enterSlider(time, slider, stillPosition), tracking);
                cursor.ConstrainToCircle(ballPositionAt(slider, time), (float)slider.Radius * 2);
                return;
            }

            // players don't follow the slider ball at exactly its speed, but run slightly ahead of it or fall slightly behind.
            // the offset fades in after the head is hit, so that the target doesn't jump along the slider.
            double maxTimingOffset = Math.Min(ApplyModsToRate(time, slider_timing_variation), slider.Radius * slider_timing_max_distance / Math.Max(slider.Velocity, 0.001));
            double timingFadeIn = Math.Clamp((time - slider.StartTime) / slider_timing_interval, 0, 1);
            double timingOffset = maxTimingOffset * timingFadeIn * (smoothNoise(slider.StackedPosition + new Vector2(13, (float)(slider.StartTime % 10000)), (time - slider.StartTime) / slider_timing_interval) * 2 - 1);
            double aheadTime = Math.Clamp(time + ApplyModsToRate(time, tracking_lag) + timingOffset, slider.StartTime, slider.EndTime);

            // slow sliders are followed closely, fast and short sliders cut corners.
            float followAmount = (float)(Math.Clamp(1 - slider.Velocity / 2, 0, 1) * Math.Clamp((slider.SpanDuration - 50) / 100, 0, 1));
            Vector2 target = Vector2.Lerp(sliderPaths[slider].PositionAt(aheadTime), ballPositionAt(slider, aheadTime), followAmount);

            // players don't follow the exact centre of the slider, but wander around within the body.
            target += wanderOffset(slider, aheadTime) * (float)slider.Radius * slider_wander_amount;

            moveTo(dt, enterSlider(time, slider, target), tracking);

            // stay inside the slider body.
            var body = sliderBodies[slider];
            if (body.Count > 0)
                cursor.ConstrainToCircle(closestPointOnPath(body, cursor.Position), (float)slider.Radius * slider_body_limit);

            // staying inside the follow circle takes priority, so that no ticks are missed even where the body overlaps itself.
            cursor.ConstrainToCircle(ballPositionAt(slider, time), (float)slider.Radius * 2);
        }

        /// <summary>
        /// Smoothly moves the target from where the cursor hit the slider onto <paramref name="target"/>.
        /// Without this, the cursor would snap from the hit position onto the slider's target in a single frame.
        /// </summary>
        private Vector2 enterSlider(double time, Slider slider, Vector2 target)
        {
            if (enteredSlider != slider)
            {
                enteredSlider = slider;
                sliderEntryOffset = cursor.Position - target;
            }

            // the offset moves along with the slider, so that the cursor doesn't fall behind on fast sliders.
            double progress = Math.Clamp(ApplyModsToTimeDelta(slider.StartTime, time) / slider_entry_duration, 0, 1);
            return target + sliderEntryOffset * (float)(1 - progress * progress * (3 - 2 * progress));
        }

        /// <summary>
        /// A smoothly changing offset with a length of at most 1, which is always the same for the same slider and time.
        /// </summary>
        private static Vector2 wanderOffset(Slider slider, double time)
        {
            double t = (time - slider.StartTime) / slider_wander_interval;
            Vector2 seed = slider.StackedPosition + new Vector2((float)(slider.StartTime % 10000), 0);

            Vector2 offset = new Vector2(smoothNoise(seed, t) * 2 - 1, smoothNoise(seed + new Vector2(0, 7), t) * 2 - 1);
            return offset.Length > 1 ? offset.Normalized() : offset;
        }

        /// <summary>
        /// Smoothly interpolates between pseudo-random values at whole numbers of <paramref name="t"/>.
        /// </summary>
        private static float smoothNoise(Vector2 seed, double t)
        {
            double knot = Math.Floor(t);
            float progress = (float)(t - knot);
            float a = RealisticMath.Random(seed + new Vector2((float)knot, 0));
            float b = RealisticMath.Random(seed + new Vector2((float)knot + 1, 0));

            return a + (b - a) * progress * progress * (3 - 2 * progress);
        }

        private static Vector2 closestPointOnPath(List<Vector2> path, Vector2 position)
        {
            Vector2 closest = path[0];
            float closestDistance = Vector2.DistanceSquared(position, closest);

            for (int i = 1; i < path.Count; i++)
            {
                Vector2 start = path[i - 1];
                Vector2 segment = path[i] - start;
                float lengthSquared = segment.LengthSquared;
                float progress = lengthSquared > 0 ? Math.Clamp(Vector2.Dot(position - start, segment) / lengthSquared, 0, 1) : 0;

                Vector2 point = start + segment * progress;
                float distance = Vector2.DistanceSquared(position, point);

                if (distance < closestDistance)
                {
                    closest = point;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private void approach(double time, float dt, int currentIndex)
        {
            var current = hitObjects[currentIndex];
            var next = hitObjects[currentIndex + 1];

            // the target runs ahead of the cursor, so it may already be flowing through the stream which starts at the next object.
            if (flowsInto(currentIndex) || (flowsInto(currentIndex + 1) && time + ApplyModsToRate(time, tracking_lag) >= next.StartTime))
            {
                flow(time, dt, flowsInto(currentIndex) ? currentIndex : currentIndex + 1);
                return;
            }

            double leaveTime = getLeaveTime(current);

            // movements start where the cursor's target was when it left the previous object, so that the target doesn't jump.
            if (leftObject != current)
            {
                leftObject = current;
                leavePosition = lastTarget;
            }

            Vector2 from = leavePosition;

            // aim for the near side of the next object, never at exactly the same spot.
            // streams are aimed at their centres instead, as they are flowed through.
            Vector2 to = flowsInto(currentIndex + 1)
                ? next.StackedPosition
                : RealisticMath.MoveIntoCircle((from + next.StackedPosition) / 2, next.StackedPosition, (float)next.Radius * 0.5f)
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
        /// Moves through a stream in one continuous motion, instead of stopping at every object.
        /// </summary>
        /// <remarks>
        /// The target follows a Catmull-Rom spline through the centres of the objects, parametrised by time.
        /// This keeps the speed constant through evenly spaced streams, and only eases in and out at their start and end.
        /// </remarks>
        private void flow(double time, float dt, int currentIndex)
        {
            double aheadTime = time + ApplyModsToRate(time, tracking_lag);

            // the target runs ahead of the cursor, so it may already be between the following objects.
            while (flowsInto(currentIndex + 1) && hitObjects[currentIndex + 1].StartTime <= aheadTime)
                currentIndex++;

            var current = hitObjects[currentIndex];
            var next = hitObjects[currentIndex + 1];

            double duration = next.StartTime - current.StartTime;
            float t = (float)Math.Clamp((aheadTime - current.StartTime) / duration, 0, 1);

            Vector2 startTangent = flowTangent(currentIndex) * (float)duration;
            Vector2 endTangent = flowTangent(currentIndex + 1) * (float)duration;

            float t2 = t * t;
            float t3 = t2 * t;

            Vector2 target = (2 * t3 - 3 * t2 + 1) * current.StackedPosition
                             + (t3 - 2 * t2 + t) * startTangent
                             + (-2 * t3 + 3 * t2) * next.StackedPosition
                             + (t3 - t2) * endTangent;

            moveTo(dt, target, tracking);
        }

        /// <summary>
        /// The velocity at which the cursor passes through the object at <paramref name="index"/> in a stream, in osu!pixels per millisecond.
        /// The cursor comes to rest on the first and last object of a stream.
        /// </summary>
        private Vector2 flowTangent(int index)
        {
            if (!flowsInto(index - 1) || !flowsInto(index))
                return Vector2.Zero;

            var previous = hitObjects[index - 1];
            var following = hitObjects[index + 1];

            return (following.StackedPosition - previous.StackedPosition) / (float)(following.StartTime - previous.StartTime);
        }

        /// <summary>
        /// Whether the cursor flows from the object at <paramref name="index"/> to the next one as part of a stream.
        /// </summary>
        private bool flowsInto(int index)
        {
            if (index < 0 || index + 1 >= hitObjects.Count)
                return false;

            var current = hitObjects[index];
            var next = hitObjects[index + 1];

            return isHitLikeCircle(current) && isHitLikeCircle(next)
                                            && ApplyModsToTimeDelta(current.StartTime, next.StartTime) <= stream_max_interval;
        }

        private bool isHitLikeCircle(OsuHitObject hitObject) => hitObject is HitCircle || (hitObject is Slider && getLeaveTime(hitObject) == hitObject.StartTime);

        /// <summary>
        /// The progress of a natural point-to-point hand movement, which starts and stops smoothly.
        /// </summary>
        private static double minimumJerk(double t) => t * t * t * (10 - 15 * t + 6 * t * t);

        private void moveTo(float dt, Vector2 target, DynamicsParameters parameters)
        {
            lastTarget = target;
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
