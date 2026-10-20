// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// Moves the cursor on flattened half circles between stream notes, alternating the side.
    /// </summary>
    public class StreamInterpolator : IAutoPlusInterpolator
    {
        private readonly List<PointWithTime> points;
        private readonly double borderAngle;
        private readonly double speedFactor;

        private int currentPointIndex;

        public int StartTime { get; }

        public int EndTime { get; }

        public bool Ignored => false;

        public StreamInterpolator(List<PointWithTime> points, double borderAngle, double speedFactor)
        {
            if (points.Count < 2)
                throw new ArgumentException("Cannot interpolate with less than 2 points.", nameof(points));

            this.points = new List<PointWithTime>(points);
            this.borderAngle = Math.Clamp(borderAngle, 0, 80) * Math.PI / 180;
            this.speedFactor = speedFactor;

            StartTime = this.points[0].Time;
            EndTime = this.points[^1].Time;
        }

        public Vector2 PositionAtTime(int time)
        {
            while (currentPointIndex < points.Count - 2 && points[currentPointIndex + 1].Time <= time)
                currentPointIndex++;

            while (currentPointIndex > 0 && points[currentPointIndex].Time > time)
                currentPointIndex--;

            PointWithTime start = points[currentPointIndex];
            PointWithTime end = points[currentPointIndex + 1];

            if (start.Time == time)
                return start.Position;

            double progress = (double)(time - start.Time) / (end.Time - start.Time);
            bool even = currentPointIndex % 2 == 0;
            double angle = progress * (Math.PI - 2 * borderAngle) + borderAngle;

            Vector2 delta = end.Position - start.Position;
            Vector2 perpendicular = perpendicularOf(delta).Normalized();

            double radius = delta.Length / 2 / Math.Cos(borderAngle);
            double along = (Math.Cos(angle) - Math.Cos(borderAngle)) / (Math.Cos(Math.PI - borderAngle) - Math.Cos(borderAngle));
            double side = (Math.Sin(angle) - Math.Sin(borderAngle)) * radius * (even ? 1 : -1);

            return start.Position + delta * (float)along + perpendicular * (float)side;
        }

        public Vector2 DerivativeAtStartTime()
        {
            Vector2 delta = points[1].Position - points[0].Position;
            float length = delta.Length;
            Vector2 direction = delta.Normalized();
            Vector2 perpendicular = perpendicularOf(direction);

            Vector2 derivative = (direction * (float)Math.Sin(borderAngle) + perpendicular * (float)Math.Cos(borderAngle)).Normalized();

            return derivative * (length / 2) * (float)speedFactor / (points[1].Time - points[0].Time);
        }

        public Vector2 DerivativeAtEndTime()
        {
            int index = points.Count - 2;
            float sign = index % 2 == 0 ? 1 : -1;

            Vector2 delta = points[index + 1].Position - points[index].Position;
            float length = delta.Length;
            Vector2 direction = delta.Normalized();
            Vector2 perpendicular = perpendicularOf(direction);

            Vector2 derivative = (-direction * sign * (float)Math.Sin(Math.PI - borderAngle) + perpendicular * sign * (float)Math.Cos(Math.PI - borderAngle)).Normalized();

            return derivative * (length / 2) * (float)speedFactor / (points[index + 1].Time - points[index].Time);
        }

        private static Vector2 perpendicularOf(Vector2 v) => new Vector2(-v.Y, v.X);
    }
}
