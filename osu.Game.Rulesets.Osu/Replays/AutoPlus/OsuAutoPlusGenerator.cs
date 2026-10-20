// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// An autoplay which moves the cursor on smooth curves between the objects and samples it at a high frequency.
    /// </summary>
    public class OsuAutoPlusGenerator : OsuAutoGeneratorBase
    {
        private const float stream_min_distance_squared = 25;
        private const float stream_max_distance_squared = 4000;
        private const float angular_stream_max_distance_squared = 8000;

        private static readonly Vector2 off_screen = new Vector2(256, 500);

        private readonly IReadOnlyList<OsuHitObject> objects;
        private readonly bool useInterpolation;
        private readonly bool invisibleDummy;

        private readonly bool followSliders;
        private readonly bool followSliderEnds;
        private readonly bool rotation;
        private readonly bool bounceBorder;
        private readonly bool alternateButtons;
        private readonly bool cubicSpline;
        private readonly bool halfCircle;
        private readonly bool halfCircleStreams;
        private readonly bool angularStreams;
        private readonly bool zeroInterpolation;
        private readonly double frequency;
        private readonly double sliderSpeedFactor;
        private readonly double streamSpeedFactor;
        private readonly double streamBorderAngle;

        /// <param name="beatmap">The beatmap.</param>
        /// <param name="mods">The active mods.</param>
        /// <param name="objects">The objects to play, or null to play all objects of the beatmap.</param>
        /// <param name="useInterpolation">Whether the cursor should be interpolated between the objects.</param>
        /// <param name="invisibleDummy">Whether this replay drives the hidden main cursor while other cursors are shown instead.</param>
        public OsuAutoPlusGenerator(IBeatmap beatmap, IReadOnlyList<Mod> mods, IReadOnlyList<OsuHitObject>? objects = null, bool useInterpolation = true, bool invisibleDummy = false)
            : base(beatmap, mods)
        {
            this.objects = objects ?? beatmap.HitObjects.OfType<OsuHitObject>().ToList();
            this.useInterpolation = useInterpolation;
            this.invisibleDummy = invisibleDummy;

            followSliders = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusFollowSliders, true);
            followSliderEnds = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusFollowSliderEnds, true);
            rotation = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusRotation, false);
            bounceBorder = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusBounceBorder, false);
            alternateButtons = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusAlternateButtons, true);
            cubicSpline = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusCubicSplineInterpolation, true);
            halfCircle = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusHalfCircleInterpolation, false);
            halfCircleStreams = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusHalfCircleStreamInterpolation, true);
            angularStreams = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusAngularStreamInterpolation, true);
            zeroInterpolation = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusZeroInterpolation, false);
            frequency = Math.Clamp(SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusInterpolationFrequency, 1000.0), 0, 1000);
            sliderSpeedFactor = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusSliderSpeedFactor, 4.0);
            streamSpeedFactor = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusStreamSpeedFactor, 4.0);
            streamBorderAngle = SlopAutoplaySettings.Get(OsuSetting.SlopAutoPlusStreamHalfCircleBorderAngle, 30.0);
        }

        #region Key frames

        private enum FrameType
        {
            Click = 0,
            Drag = 1,
            Spin = 2,
        }

        private class KeyFrame
        {
            public int Time;
            public Vector2 Position;
            public bool Pressed;
            public readonly FrameType Type;
            public readonly Slider? Slider;

            public KeyFrame(int time, Vector2 position, bool pressed, FrameType type, Slider? slider = null)
            {
                Time = time;
                Position = position;
                Pressed = pressed;
                Type = type;
                Slider = slider;
            }

            public PointWithTime Point => new PointWithTime(Time, Position);
        }

        private static void addKeyFrame(List<KeyFrame> frames, KeyFrame frame)
        {
            int index = frames.Count;

            while (index > 0 && frames[index - 1].Time > frame.Time)
                index--;

            frames.Insert(index, frame);
        }

        private static int toTime(double time) => (int)Math.Round(time);

        #endregion

        public override Replay Generate()
        {
            Frames.Clear();

            if (objects.Count == 0)
                return Replay;

            List<KeyFrame> keyFrames = createKeyFrames(out int lastEndTime);
            List<IAutoPlusInterpolator> interpolators = createInterpolators(keyFrames);

            List<(int time, Vector2 position, bool pressed)> frames = sample(keyFrames, interpolators, lastEndTime);

            int counter = 2;

            foreach (var (time, position, pressed) in frames)
            {
                if (!pressed)
                {
                    counter++;
                    Frames.Add(new OsuReplayFrame(time, position));
                    continue;
                }

                OsuAction action = !alternateButtons || counter % 4 == 0 || counter % 4 == 2 ? OsuAction.LeftButton : OsuAction.RightButton;
                Frames.Add(new OsuReplayFrame(time, position, action));
            }

            return Replay;
        }

        private List<KeyFrame> createKeyFrames(out int lastEndTime)
        {
            int firstTime = toTime(objects[0].StartTime);

            var frames = new List<KeyFrame>
            {
                new KeyFrame(-100000, off_screen, false, FrameType.Click),
                new KeyFrame(firstTime - 1500, off_screen, false, FrameType.Click),
                new KeyFrame(firstTime - 1000, SPINNER_CENTRE, false, FrameType.Click),
            };

            var clickTimes = new SortedSet<int>();
            var spinRanges = new List<(int start, int end)>();
            lastEndTime = int.MinValue;

            foreach (var obj in objects)
            {
                int startTime = toTime(obj.StartTime);
                int endTime = toTime(obj.GetEndTime());
                lastEndTime = Math.Max(lastEndTime, endTime);

                switch (obj)
                {
                    case Spinner:
                        if (endTime <= startTime)
                            break;

                        if (spinRanges.Count > 0 && spinRanges[^1].end >= startTime - 1)
                            spinRanges[^1] = (spinRanges[^1].start, Math.Max(spinRanges[^1].end, endTime));
                        else
                            spinRanges.Add((startTime, endTime));

                        break;

                    case Slider slider:
                        addKeyFrame(frames, new KeyFrame(startTime, slider.StackedPosition, true, FrameType.Click, slider));
                        clickTimes.Add(startTime);

                        // Ticks are rounded up, so that the cursor is on the slider ball when they are judged.
                        var tickTimes = new SortedSet<int>();

                        foreach (var nested in slider.NestedHitObjects)
                        {
                            if (nested is SliderTick || nested is SliderRepeat)
                                tickTimes.Add((int)Math.Ceiling(nested.StartTime));
                        }

                        double finalSpanStartTime = slider.StartTime + (slider.SpanCount() - 1) * slider.SpanDuration;
                        tickTimes.Add((int)Math.Ceiling(Math.Max(slider.StartTime + slider.Duration / 2, finalSpanStartTime + slider.SpanDuration + SliderEventGenerator.TAIL_LENIENCY)));

                        if (followSliderEnds && useInterpolation)
                            tickTimes.Add(endTime);

                        foreach (int tickTime in tickTimes)
                        {
                            if (tickTime <= startTime)
                                continue;

                            addKeyFrame(frames, new KeyFrame(tickTime, sliderPositionAt(slider, tickTime), true, FrameType.Drag, slider));
                            clickTimes.Add(tickTime);
                        }

                        break;

                    default:
                        addKeyFrame(frames, new KeyFrame(startTime, obj.StackedPosition, true, FrameType.Click));
                        clickTimes.Add(startTime);
                        break;
                }
            }

            addSpinnerFrames(frames, spinRanges, clickTimes);

            frames.Add(new KeyFrame(toTime(objects[^1].StartTime) + 10000, SPINNER_CENTRE, false, FrameType.Click));

            return frames;
        }

        private static Vector2 sliderPositionAt(Slider slider, double time)
        {
            if (slider.Duration <= 0)
                return slider.StackedPosition;

            return slider.StackedPositionAt(Math.Clamp((time - slider.StartTime) / slider.Duration, 0, 1));
        }

        private void addSpinnerFrames(List<KeyFrame> frames, List<(int start, int end)> spinRanges, SortedSet<int> clickTimes)
        {
            float step = (float)ApplyModsToRate(0, 1000.0 / 60);

            foreach (var (start, end) in spinRanges)
            {
                var clicksInRange = clickTimes.GetViewBetween(start, end + (int)step + 1);
                bool spinning = false;
                Vector2 position = SPINNER_CENTRE;
                float time = start;

                for (; time <= end; time += step)
                {
                    position = SPINNER_CENTRE + CirclePosition(time / 10.0, 1);

                    if (clicksInRange.Contains((int)time))
                        continue;

                    if (clicksInRange.GetViewBetween((int)time + 1, (int)(time + step)).Count == 0)
                    {
                        if (!spinning && wouldHitObject((int)time, position))
                            position = new Vector2(-1000);

                        addKeyFrame(frames, new KeyFrame((int)time, position, true, FrameType.Spin));
                        spinning = true;
                    }
                    else if (spinning)
                    {
                        addKeyFrame(frames, new KeyFrame((int)time, position, true, FrameType.Spin));
                        addKeyFrame(frames, new KeyFrame((int)time, position, false, FrameType.Spin));
                        spinning = false;
                    }
                }

                if (spinning)
                {
                    addKeyFrame(frames, new KeyFrame((int)time, position, true, FrameType.Spin));
                    addKeyFrame(frames, new KeyFrame((int)time, position, false, FrameType.Spin));
                }
            }
        }

        /// <summary>
        /// Whether pressing at <paramref name="position"/> at <paramref name="time"/> would hit a visible object which is not hit yet.
        /// </summary>
        private bool wouldHitObject(int time, Vector2 position)
        {
            foreach (var obj in objects)
            {
                if (obj is Spinner)
                    continue;

                if (obj.StartTime - obj.TimePreempt > time || obj.StartTime < time)
                    continue;

                if (Vector2.DistanceSquared(obj.StackedPosition, position) <= obj.Radius * obj.Radius)
                    return true;
            }

            return false;
        }

        #region Interpolation passes

        private List<IAutoPlusInterpolator> createInterpolators(List<KeyFrame> frames)
        {
            var interpolators = new List<IAutoPlusInterpolator>();

            if (!useInterpolation)
            {
                interpolators.Add(new LinearInterpolator(new List<PointWithTime> { frames[0].Point, frames[^1].Point }));
                return interpolators;
            }

            interpSpinners(frames, interpolators);

            if (!zeroInterpolation)
            {
                if (invisibleDummy || followSliders)
                    interpFollowSliders(frames, interpolators);

                if (rotation)
                    addJumps(frames, interpolators);

                interpCloseFrames(frames, interpolators);

                if (halfCircleStreams)
                    interpStreams(frames, interpolators);

                if (angularStreams)
                    interpStreamsAngular(frames, interpolators);
            }

            interpolators.Sort(AutoPlusInterpolatorComparer.DEFAULT);
            interpRest(frames, interpolators);

            return interpolators;
        }

        private static bool isCovered(List<IAutoPlusInterpolator> interpolators, List<KeyFrame> frames, int index)
        {
            int start = frames[index].Time;
            int end = frames[index + 1].Time;
            return interpolators.Exists(i => i.StartTime <= start && i.EndTime >= end);
        }

        private static float distanceSquared(KeyFrame a, KeyFrame b) => Vector2.DistanceSquared(a.Position, b.Position);

        private static float angleCosine(KeyFrame a, KeyFrame b, KeyFrame c)
            => Vector2.Dot((a.Position - b.Position).Normalized(), (c.Position - b.Position).Normalized());

        private static bool hasEvenSpacing(KeyFrame a, KeyFrame b, KeyFrame c) => Math.Abs(c.Time - b.Time - (b.Time - a.Time)) < 2;

        private void interpFollowSliders(List<KeyFrame> frames, List<IAutoPlusInterpolator> interpolators)
        {
            for (int i = 0; i < frames.Count - 1; i++)
            {
                if (isCovered(interpolators, frames, i) || frames[i].Slider == null || frames[i + 1].Slider != frames[i].Slider)
                    continue;

                int j = i + 1;

                while (j < frames.Count - 1 && !isCovered(interpolators, frames, j) && frames[j + 1].Slider == frames[j].Slider)
                    j++;

                Slider slider = frames[i].Slider!;

                if (frames[i].Time >= toTime(slider.StartTime) && frames[j].Time <= toTime(slider.EndTime))
                    interpolators.Add(new SliderFollowInterpolator(frames[i].Time, frames[j].Time, slider, sliderSpeedFactor));

                i = j - 1;
            }
        }

        private static void interpSpinners(List<KeyFrame> frames, List<IAutoPlusInterpolator> interpolators)
        {
            for (int i = 0; i < frames.Count - 1; i++)
            {
                if (isCovered(interpolators, frames, i) || frames[i].Type != FrameType.Spin || frames[i + 1].Type != FrameType.Spin)
                    continue;

                var points = new List<PointWithTime> { frames[i].Point, frames[i + 1].Point };

                int j = i + 2;

                for (; j < frames.Count && !isCovered(interpolators, frames, j - 1) && frames[j].Type == FrameType.Spin; j++)
                    points.Add(frames[j].Point);

                interpolators.Add(new LinearInterpolator(points, true));
                i = j - 2;
            }
        }

        private static void interpCloseFrames(List<KeyFrame> frames, List<IAutoPlusInterpolator> interpolators)
        {
            static bool isClose(KeyFrame a, KeyFrame b)
            {
                if (a.Time == b.Time)
                    return true;

                int duration = Math.Max(b.Time - a.Time, 1);
                return Vector2.Distance(a.Position, b.Position) / duration > 5;
            }

            for (int i = 0; i < frames.Count - 1; i++)
            {
                if (isCovered(interpolators, frames, i) || !isClose(frames[i], frames[i + 1]))
                    continue;

                var points = new List<PointWithTime> { frames[i].Point, frames[i + 1].Point };

                int j = i + 2;

                for (; j < frames.Count && !isCovered(interpolators, frames, j - 1) && isClose(frames[j - 1], frames[j]); j++)
                    points.Add(frames[j].Point);

                interpolators.Add(new LinearInterpolator(points));
                i = j - 2;
            }
        }

        private void interpStreamsAngular(List<KeyFrame> frames, List<IAutoPlusInterpolator> interpolators)
        {
            static bool isValidDistance(float d) => d >= stream_min_distance_squared && d <= angular_stream_max_distance_squared;

            // Either almost straight backwards or almost perpendicular.
            static bool isValidAngle(float cos) => cos <= -0.99f || (cos <= 0.01f && cos >= -0.01f);

            for (int i = 0; i < frames.Count - 2; i++)
            {
                if (isCovered(interpolators, frames, i) || isCovered(interpolators, frames, i + 1)
                                                         || !hasEvenSpacing(frames[i], frames[i + 1], frames[i + 2])
                                                         || !isValidDistance(distanceSquared(frames[i + 2], frames[i + 1]))
                                                         || !isValidDistance(distanceSquared(frames[i + 1], frames[i])))
                    continue;

                if (!isValidAngle(angleCosine(frames[i], frames[i + 1], frames[i + 2])))
                    continue;

                int spacing = frames[i + 1].Time - frames[i].Time;
                var points = new List<PointWithTime> { frames[i].Point, frames[i + 1].Point, frames[i + 2].Point };

                int j = i + 3;

                for (; j < frames.Count && !isCovered(interpolators, frames, j - 1)
                                        && Math.Abs(frames[j].Time - frames[j - 1].Time - spacing) < 2
                                        && isValidDistance(distanceSquared(frames[j], frames[j - 1])); j++)
                {
                    if (!isValidAngle(angleCosine(frames[j - 2], frames[j - 1], frames[j])))
                        break;

                    points.Add(frames[j].Point);
                }

                interpolators.Add(new StreamAngularInterpolator(points, streamSpeedFactor));
                i = j - 2;
            }
        }

        private void interpStreams(List<KeyFrame> frames, List<IAutoPlusInterpolator> interpolators)
        {
            static bool isValidDistance(float d) => d >= stream_min_distance_squared && d <= stream_max_distance_squared;

            for (int i = 0; i < frames.Count - 2; i++)
            {
                if (isCovered(interpolators, frames, i) || isCovered(interpolators, frames, i + 1)
                                                         || !hasEvenSpacing(frames[i], frames[i + 1], frames[i + 2])
                                                         || !isValidDistance(distanceSquared(frames[i + 2], frames[i + 1]))
                                                         || !isValidDistance(distanceSquared(frames[i + 1], frames[i]))
                                                         || angleCosine(frames[i], frames[i + 1], frames[i + 2]) > -0.5f)
                    continue;

                // Straight back-and-forth streams are left to the angular interpolation if it is enabled.
                bool leaveToAngular = angularStreams;

                if (angleCosine(frames[i], frames[i + 1], frames[i + 2]) > -0.99f)
                    leaveToAngular = false;

                int spacing = frames[i + 1].Time - frames[i].Time;
                var points = new List<PointWithTime> { frames[i].Point, frames[i + 1].Point, frames[i + 2].Point };

                int j = i + 3;

                for (; j < frames.Count && !isCovered(interpolators, frames, j - 1)
                                        && Math.Abs(frames[j].Time - frames[j - 1].Time - spacing) < 2
                                        && isValidDistance(distanceSquared(frames[j], frames[j - 1]))
                                        && angleCosine(frames[j - 2], frames[j - 1], frames[j]) <= -0.5f; j++)
                {
                    if (angleCosine(frames[j - 2], frames[j - 1], frames[j]) > -0.99f)
                        leaveToAngular = false;

                    points.Add(frames[j].Point);
                }

                if (!leaveToAngular)
                    interpolators.Add(new StreamInterpolator(points, streamBorderAngle, streamSpeedFactor));

                i = j - 2;
            }
        }

        private void addJumps(List<KeyFrame> frames, List<IAutoPlusInterpolator> interpolators)
        {
            int typeThreshold = followSliders ? 0 : 1;
            float maxStreamDistance = angularStreams ? angular_stream_max_distance_squared : stream_max_distance_squared;
            int angle = 0;

            for (int i = 0; i < frames.Count - 2; i++)
            {
                if (isCovered(interpolators, frames, i) || (int)frames[i].Type > typeThreshold || (int)frames[i + 1].Type > typeThreshold)
                    continue;

                bool skip = false;

                if ((angularStreams || halfCircleStreams)
                    && hasEvenSpacing(frames[i], frames[i + 1], frames[i + 2])
                    && distanceSquared(frames[i + 2], frames[i + 1]) >= stream_min_distance_squared && distanceSquared(frames[i + 2], frames[i + 1]) <= maxStreamDistance
                    && distanceSquared(frames[i + 1], frames[i]) >= stream_min_distance_squared && distanceSquared(frames[i + 1], frames[i]) <= maxStreamDistance)
                {
                    skip = true;
                }

                KeyFrame from = frames[i];
                int targetTime = frames[i + 1].Time;
                Vector2 toPosition = frames[i + 1].Position;

                if ((int)frames[i + 2].Type <= typeThreshold)
                {
                    if (from.Slider != null)
                    {
                        targetTime = toTime(from.Slider.EndTime);
                        toPosition = from.Slider.StackedEndPosition;
                    }

                    Vector2 next = frames[i + 2].Position;

                    double angleToTarget = Math.Atan2(from.Position.X - toPosition.X, from.Position.Y - toPosition.Y);
                    double angleToNext = Math.Atan2(from.Position.X - next.X, from.Position.Y - next.Y);
                    int newAngle = (int)Math.Abs(angleToTarget - angleToNext) * 90;

                    angle = newAngle != 0 ? newAngle : -angle;
                }
                else
                    skip = true;

                if (from.Time + 1 < targetTime && Vector2.Distance(from.Position, toPosition) > 25 && !skip)
                {
                    Vector2 middle = (from.Position + toPosition) / 2;
                    Vector2 position = middle + rotate(middle - from.Position, angle);
                    bool pressed = from.Slider != null && frames[i + 1].Slider == from.Slider;

                    addKeyFrame(frames, new KeyFrame((int)((from.Time + targetTime) / 2.0), position, pressed, FrameType.Drag, from.Slider));
                    i++;
                }
            }
        }

        /// <summary>
        /// Rotates <paramref name="vector"/> by <paramref name="degrees"/> clockwise.
        /// </summary>
        private static Vector2 rotate(Vector2 vector, float degrees)
        {
            double radians = -degrees * Math.PI / 180;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);

            return new Vector2(vector.X * cos + vector.Y * sin, -vector.X * sin + vector.Y * cos);
        }

        private void interpRest(List<KeyFrame> frames, List<IAutoPlusInterpolator> interpolators)
        {
            int i = 0;
            Vector2 derivativeAtStart = Vector2.Zero;

            for (int j = 0; j < interpolators.Count; j++)
            {
                IAutoPlusInterpolator interpolator = interpolators[j];

                if (interpolator.StartTime > frames[i].Time)
                {
                    int k = i;

                    while (frames[k].Time < interpolator.StartTime)
                        k++;

                    var points = new List<PointWithTime>();

                    for (int l = i; l <= k; l++)
                        points.Add(frames[l].Point);

                    if (j == 0)
                        derivativeAtStart = (frames[i].Position - SPINNER_CENTRE) / 1000;
                    else
                        derivativeAtStart = interpolators[j - 1].DerivativeAtEndTime();

                    interpolators.Insert(j, createRestInterpolator(points, derivativeAtStart, interpolator.DerivativeAtStartTime(), false));
                    j++;
                }

                while (i != frames.Count - 1 && frames[i + 1].Time <= interpolator.EndTime)
                    i++;
            }

            if (frames[^1].Time > frames[i].Time)
            {
                var points = new List<PointWithTime>();

                for (int m = i; m < frames.Count; m++)
                    points.Add(frames[m].Point);

                Vector2 derivative = interpolators.Count > 0 ? interpolators[^1].DerivativeAtEndTime() : Vector2.Zero;
                interpolators.Add(createRestInterpolator(points, derivative, Vector2.Zero, true));
            }
        }

        private IAutoPlusInterpolator createRestInterpolator(List<PointWithTime> points, Vector2 derivativeAtStart, Vector2 derivativeAtEnd, bool isTail)
        {
            // The tail of the replay never uses the dummy or zero interpolation.
            if (!isTail)
            {
                if (invisibleDummy)
                    return new LinearInterpolator(points);

                if (zeroInterpolation)
                    return new ZeroInterpolator(points);
            }

            if (cubicSpline && points[0].Time != points[^1].Time)
                return new CubicSplineInterpolator(points, derivativeAtStart, derivativeAtEnd);

            if (halfCircle)
                return new HalfCircleInterpolator(points);

            return new LinearInterpolator(points);
        }

        #endregion

        private List<(int time, Vector2 position, bool pressed)> sample(List<KeyFrame> keyFrames, List<IAutoPlusInterpolator> interpolators, int lastEndTime)
        {
            var frames = new List<(int time, Vector2 position, bool pressed)>();

            double step = frequency > 0 ? 1000 / frequency : 0;
            double next = keyFrames[0].Time + step;

            // The cursor rests off-screen before the first object, so the samples until then would be identical.
            if (step > 0 && keyFrames.Count > 1 && next < keyFrames[1].Time)
                next += Math.Floor((keyFrames[1].Time - next) / step) * step;

            int interpolatorIndex = 0;

            for (int i = 0; i < keyFrames.Count; i++)
            {
                KeyFrame keyFrame = keyFrames[i];

                if (step > 0)
                {
                    for (; next < keyFrame.Time; next += step)
                    {
                        while (interpolatorIndex < interpolators.Count - 1 && next > interpolators[interpolatorIndex].EndTime)
                            interpolatorIndex++;

                        IAutoPlusInterpolator interpolator = interpolators[interpolatorIndex];

                        if (!useInterpolation || interpolator.Ignored || frames.Count == 0)
                            continue;

                        Vector2 position = interpolator.PositionAtTime((int)next);

                        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                            continue;

                        if (bounceBorder && interpolator is not SliderFollowInterpolator)
                            position = new Vector2(rangeBounce(position.X, -170, 683), rangeBounce(position.Y, -55, 423));

                        frames.Add(((int)next, position, frames[^1].pressed));
                    }

                    if ((int)next == keyFrame.Time)
                        next += step;
                }

                frames.Add((keyFrame.Time, keyFrame.Position, keyFrame.Pressed));

                if (keyFrame.Type != FrameType.Spin && (i == keyFrames.Count - 1 || keyFrames[i + 1].Type != FrameType.Drag))
                    frames.Add((keyFrame.Time, keyFrame.Position, false));
            }

            if (frames[^1].time <= lastEndTime)
                frames.Add((lastEndTime + 1, frames[^1].position, false));

            return frames;
        }

        private static float rangeBounce(float value, float min, float max)
        {
            float range = max - min;
            float offset = (value - min) % (2 * range);

            if (offset < 0)
                offset += 2 * range;

            if (offset > range)
                offset = 2 * range - offset;

            return min + offset;
        }
    }
}
