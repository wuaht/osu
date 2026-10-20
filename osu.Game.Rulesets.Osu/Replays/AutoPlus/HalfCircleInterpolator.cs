// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// Moves the cursor on half circles between the points, alternating the side.
    /// </summary>
    public class HalfCircleInterpolator : IAutoPlusInterpolator
    {
        private readonly List<PointWithTime> points;

        private int currentPointIndex;

        public int StartTime { get; }

        public int EndTime { get; }

        public bool Ignored => false;

        public HalfCircleInterpolator(List<PointWithTime> points)
        {
            this.points = points;
            StartTime = points[0].Time;
            EndTime = points[^1].Time;
        }

        public Vector2 PositionAtTime(int time)
        {
            while (currentPointIndex < points.Count - 2 && points[currentPointIndex + 1].Time <= time)
                currentPointIndex++;

            while (currentPointIndex > 0 && points[currentPointIndex].Time > time)
                currentPointIndex--;

            PointWithTime start = points[currentPointIndex];
            PointWithTime end = points[currentPointIndex + 1];

            if (start.Position == end.Position)
                return start.Position;

            Vector2 centre = (end.Position + start.Position) / 2;
            double startAngle = Math.Atan2(start.Position.Y - end.Position.Y, start.Position.X - end.Position.X);
            double radius = Vector2.Distance(end.Position, start.Position) / 2;
            double angle = (double)(time - start.Time) / (end.Time - start.Time) * Math.PI;
            angle = currentPointIndex % 2 != 0 ? Math.Abs(angle) : -Math.Abs(angle);

            double finalAngle = startAngle + angle;

            return centre + new Vector2((float)(Math.Cos(finalAngle) * radius), (float)(Math.Sin(finalAngle) * radius));
        }

        public Vector2 DerivativeAtStartTime() => Vector2.Zero;

        public Vector2 DerivativeAtEndTime() => Vector2.Zero;
    }
}
