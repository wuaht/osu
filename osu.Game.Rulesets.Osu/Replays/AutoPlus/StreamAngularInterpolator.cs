// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// Moves the cursor between stream notes along two straight lines meeting at the intersection of the note tangents.
    /// </summary>
    public class StreamAngularInterpolator : IAutoPlusInterpolator
    {
        private readonly List<PointWithTime> points;
        private readonly List<Vector2> derivatives = new List<Vector2>();
        private readonly double speedFactor;

        private int currentPointIndex;

        public int StartTime { get; }

        public int EndTime { get; }

        public bool Ignored => false;

        public StreamAngularInterpolator(List<PointWithTime> points, double speedFactor)
        {
            if (points.Count < 2)
                throw new ArgumentException("Cannot interpolate with less than 2 points.", nameof(points));

            this.points = new List<PointWithTime>(points);
            this.speedFactor = speedFactor;

            StartTime = this.points[0].Time;
            EndTime = this.points[^1].Time;

            computeDerivatives();
        }

        private void computeDerivatives()
        {
            Vector2 delta = points[1].Position - points[0].Position;
            Vector2 perpendicular = perpendicularOf(delta);

            if (points.Count > 2)
            {
                Vector2 next = (points[2].Position - points[1].Position).Normalized();

                if (Vector2.Dot(perpendicular.Normalized(), next) > 0.99f)
                    perpendicular = -perpendicular;
            }

            Vector2 derivative = (delta + perpendicular).Normalized();
            derivatives.Add(derivative);

            for (int i = 1; i < points.Count; i++)
            {
                delta = points[i].Position - points[i - 1].Position;
                Vector2 corner = cornerOf(points[i - 1].Position, delta, ref derivative, out _);

                derivative = (points[i].Position - corner).Normalized();
                derivatives.Add(derivative);
            }
        }

        /// <summary>
        /// Computes the point where the cursor changes its direction between two notes.
        /// </summary>
        private static Vector2 cornerOf(Vector2 start, Vector2 delta, ref Vector2 derivative, out float distanceToCorner)
        {
            Vector2 halfPerpendicular = perpendicularOf(delta) / 2;

            if (Vector2.Dot(derivative, delta) < 0)
                derivative = -derivative;

            if (Vector2.Dot(derivative, halfPerpendicular) < 0)
                halfPerpendicular = -halfPerpendicular;

            Vector2 normal = halfPerpendicular.Normalized();
            float cos = Vector2.Dot(derivative, normal);
            float ratio = (1 - cos * cos) / cos;
            float along = halfPerpendicular.Length * ratio;

            if (along > delta.Length)
                along = delta.Length / 2;

            float side = halfPerpendicular.Length;
            if (ratio != 0)
                side = along / ratio;

            Vector2 corner = start + delta.Normalized() * along + normal * side;
            distanceToCorner = (corner - start).Length;
            return corner;
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

            Vector2 derivative = derivatives[currentPointIndex];
            Vector2 corner = cornerOf(start.Position, end.Position - start.Position, ref derivative, out float firstLength);
            float secondLength = (end.Position - corner).Length;
            float distance = (float)(progress * (firstLength + secondLength));

            if (distance <= firstLength)
                return start.Position + derivative * distance;

            return corner + derivatives[currentPointIndex + 1] * (distance - firstLength);
        }

        public Vector2 DerivativeAtStartTime()
        {
            float length = (points[1].Position - points[0].Position).Length;
            return derivatives[0] * (length / 2) * (float)speedFactor / (points[1].Time - points[0].Time);
        }

        public Vector2 DerivativeAtEndTime()
        {
            int index = points.Count - 2;
            float length = (points[index + 1].Position - points[index].Position).Length;
            return derivatives[^1] * (length / 2) * (float)speedFactor / (points[index + 1].Time - points[index].Time);
        }

        private static Vector2 perpendicularOf(Vector2 v) => new Vector2(-v.Y, v.X);
    }
}
