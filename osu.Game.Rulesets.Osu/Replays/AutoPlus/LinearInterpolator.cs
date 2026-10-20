// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    public class LinearInterpolator : IAutoPlusInterpolator
    {
        private readonly List<PointWithTime> points;

        public int StartTime { get; }

        public int EndTime { get; }

        public bool Ignored { get; }

        public LinearInterpolator(List<PointWithTime> points, bool ignored = false)
        {
            if (points.Count < 2)
                throw new ArgumentException("Cannot interpolate with less than 2 points.", nameof(points));

            this.points = new List<PointWithTime>(points);
            StartTime = this.points[0].Time;
            EndTime = this.points[^1].Time;
            Ignored = ignored;
        }

        public Vector2 PositionAtTime(int time)
        {
            if (StartTime == EndTime)
                return points[^1].Position;

            int index = points.BinarySearch(new PointWithTime(time, Vector2.Zero));
            if (index < 0)
                index = Math.Min(~index, points.Count - 1);

            int previous = Math.Max(index - 1, 0);

            if (points[previous].Time == points[index].Time)
                return points[index].Position;

            return Vector2.Lerp(points[previous].Position, points[index].Position, (float)(time - points[previous].Time) / (points[index].Time - points[previous].Time));
        }

        public Vector2 DerivativeAtStartTime()
        {
            int duration = Math.Max(points[1].Time - points[0].Time, 1);
            return (points[1].Position - points[0].Position) / duration;
        }

        public Vector2 DerivativeAtEndTime()
        {
            int duration = Math.Max(points[^1].Time - points[^2].Time, 1);
            return (points[^1].Position - points[^2].Position) / duration;
        }
    }
}
