// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Generates random slider shapes between a fixed head and tail.
    /// </summary>
    public static class RandomSliderShapes
    {
        private delegate List<Vector2> ShapeProvider(Vector2 head, Vector2 tail, int nodeBudget, Random rng);

        private static readonly ShapeProvider[] providers =
        {
            chaoticBlob,
            loopBack,
            multiArcSnake,
            tightCorner,
        };

        /// <summary>
        /// Generates the inner points of a random bezier path from <paramref name="head"/> to <paramref name="tail"/>, in playfield space.
        /// Points that are added twice in a row form a red anchor.
        /// </summary>
        /// <param name="head">The position of the slider head.</param>
        /// <param name="tail">The position of the last control point.</param>
        /// <param name="rng">The random number generator.</param>
        /// <returns>The points between <paramref name="head"/> and <paramref name="tail"/>, excluding both.</returns>
        public static List<Vector2> Generate(Vector2 head, Vector2 tail, Random rng)
        {
            var provider = providers[rng.Next(providers.Length)];
            int nodeBudget = 6 + rng.Next(15);

            return provider(head, tail, nodeBudget, rng);
        }

        /// <summary>
        /// Gives every segment of <paramref name="points"/> a random type, so that the path mixes bezier, perfect curve, linear, catmull and B-spline anchors.
        /// </summary>
        /// <param name="points">The control points of the path, where every point with a type starts a new segment.</param>
        /// <param name="rng">The random number generator.</param>
        public static void RandomiseSegmentTypes(IReadOnlyList<PathControlPoint> points, Random rng)
        {
            for (int start = 0; start < points.Count - 1; start++)
            {
                if (points[start].Type == null)
                    continue;

                int end = start + 1;

                while (end < points.Count - 1 && points[end].Type == null)
                    end++;

                points[start].Type = randomSegmentType(points, start, end, rng);
            }
        }

        private static PathType randomSegmentType(IReadOnlyList<PathControlPoint> points, int start, int end, Random rng)
        {
            int pointCount = end - start + 1;

            // a straight segment looks the same with every type.
            if (pointCount == 2)
                return rng.Next(2) == 0 ? PathType.LINEAR : PathType.BEZIER;

            var candidates = new List<PathType> { PathType.BEZIER, PathType.BEZIER, PathType.CATMULL, PathType.LINEAR };

            if (pointCount == 3 && isShortArc(points[start].Position, points[start + 1].Position, points[end].Position))
            {
                candidates.Add(PathType.PERFECT_CURVE);
                candidates.Add(PathType.PERFECT_CURVE);
            }

            if (pointCount > 3)
            {
                candidates.Add(PathType.BSpline(2));
                candidates.Add(PathType.BSpline(3));
            }

            return candidates[rng.Next(candidates.Count)];
        }

        /// <summary>
        /// Whether the arc through the three points stays below half a circle, so that a perfect curve doesn't swing far outside of its points.
        /// </summary>
        private static bool isShortArc(Vector2 start, Vector2 middle, Vector2 end)
        {
            Vector2 chord = end - start;
            float lengthSquared = chord.LengthSquared;

            if (lengthSquared < 0.0001f)
                return false;

            float t = Vector2.Dot(middle - start, chord) / lengthSquared;
            return t > 0 && t < 1;
        }

        private static List<Vector2> chaoticBlob(Vector2 head, Vector2 tail, int nodeBudget, Random rng)
        {
            var points = new List<Vector2>();

            int count = Math.Clamp(nodeBudget, 5, 12);
            float length = Vector2.Distance(head, tail);
            Vector2 perp = perpendicular(head, tail);

            Vector2 centre = (head + tail) / 2 + perp * (50 + next(rng) * 110) * randomSign(rng);

            float a = Math.Max(80, length * 0.55f) + next(rng) * 90;
            float b = a * (0.35f + next(rng) * 0.4f);
            float rotation = next(rng) * MathF.PI;

            float frequency1 = 1.5f + next(rng) * 2.5f;
            float frequency2 = 3.5f + next(rng) * 3.5f;
            float phase1 = next(rng) * MathF.PI * 2;
            float phase2 = next(rng) * MathF.PI * 2;
            float amplitude1 = 0.15f + next(rng) * 0.15f;
            float amplitude2 = 0.05f + next(rng) * 0.1f;

            float startAngle = next(rng) * MathF.PI * 2;
            float sweep = MathF.PI * (1.2f + next(rng) * 0.6f);
            float direction = randomSign(rng);

            for (int i = 1; i <= count; i++)
            {
                float t = (float)i / (count + 1);
                float angle = startAngle + direction * t * sweep;
                float radius = 1 + amplitude1 * MathF.Sin(angle * frequency1 + phase1) + amplitude2 * MathF.Sin(angle * frequency2 + phase2);

                var point = new Vector2(a * MathF.Cos(angle) * radius, b * MathF.Sin(angle) * radius);
                points.Add(clamp(centre + rotate(point, rotation)));
            }

            return points;
        }

        private static List<Vector2> loopBack(Vector2 head, Vector2 tail, int nodeBudget, Random rng)
        {
            var points = new List<Vector2>();

            int pivotCount = Math.Clamp((nodeBudget - 2) / 3, 1, 3);
            Vector2 perp = perpendicular(head, tail);
            float side = randomSign(rng);

            var chain = new List<Vector2> { head };

            for (int i = 0; i < pivotCount; i++)
            {
                float t = (float)(i + 1) / (pivotCount + 1);
                float magnitude = (160 + next(rng) * 260) * MathF.Sin(t * MathF.PI) * side * (i % 2 == 0 ? 1 : 0.4f + next(rng) * 0.4f);

                chain.Add(clamp(Vector2.Lerp(head, tail, t + (next(rng) - 0.5f) * 0.15f) + perp * magnitude));
            }

            chain.Add(tail);

            int segmentCount = chain.Count - 1;
            int pointsPerSegment = Math.Max(1, Math.Min(3, (nodeBudget - 2 * pivotCount) / segmentCount));

            for (int s = 0; s < segmentCount; s++)
            {
                Vector2 start = chain[s];
                Vector2 end = chain[s + 1];
                Vector2 segmentPerp = perpendicular(start, end);

                for (int j = 1; j <= pointsPerSegment; j++)
                {
                    float offset = (next(rng) - 0.5f) * 2 * (50 + next(rng) * 130);
                    points.Add(clamp(Vector2.Lerp(start, end, (float)j / (pointsPerSegment + 1)) + segmentPerp * offset));
                }

                if (s < segmentCount - 1)
                {
                    points.Add(end);
                    points.Add(end);
                }
            }

            return points;
        }

        private static List<Vector2> multiArcSnake(Vector2 head, Vector2 tail, int nodeBudget, Random rng)
        {
            var points = new List<Vector2>();

            int arcCount = Math.Clamp(nodeBudget / 4, 2, 6);
            Vector2 perp = perpendicular(head, tail);
            float amplitude = 60 + next(rng) * 120;
            float phase = next(rng) * MathF.PI * 2;

            var anchors = new Vector2[arcCount + 1];
            anchors[0] = head;
            anchors[arcCount] = tail;

            for (int i = 1; i < arcCount; i++)
                anchors[i] = clamp(Vector2.Lerp(head, tail, (float)i / arcCount) + perp * amplitude * MathF.Sin(phase + i * MathF.PI));

            for (int s = 0; s < arcCount; s++)
            {
                Vector2 start = anchors[s];
                Vector2 end = anchors[s + 1];
                float bulge = (20 + next(rng) * 60) * randomSign(rng);

                points.Add(clamp((start + end) / 2 + perpendicular(start, end) * bulge));

                if (s < arcCount - 1)
                {
                    points.Add(end);
                    points.Add(end);
                }
            }

            return points;
        }

        private static List<Vector2> tightCorner(Vector2 head, Vector2 tail, int nodeBudget, Random rng)
        {
            var points = new List<Vector2>();

            int cornerCount = Math.Clamp(nodeBudget / 3, 2, 6);
            Vector2 perp = perpendicular(head, tail);
            float amplitude = 50 + next(rng) * 110;
            float phase = next(rng) * MathF.PI;

            for (int i = 0; i < cornerCount; i++)
            {
                float offset = amplitude * MathF.Sin(phase + i * MathF.PI * 0.75f) * (1 + 0.4f * MathF.Cos(i * 1.3f));
                var point = clamp(Vector2.Lerp(head, tail, (float)(i + 1) / (cornerCount + 1)) + perp * offset);

                points.Add(point);

                if (i < cornerCount - 1)
                    points.Add(point);
            }

            return points;
        }

        private static float next(Random rng) => (float)rng.NextDouble();

        private static float randomSign(Random rng) => rng.Next(2) == 0 ? 1 : -1;

        private static Vector2 perpendicular(Vector2 start, Vector2 end)
        {
            Vector2 direction = end - start;

            if (direction.LengthSquared < 0.0001f)
                return new Vector2(0, 1);

            direction.Normalize();
            return new Vector2(-direction.Y, direction.X);
        }

        private static Vector2 rotate(Vector2 point, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            return new Vector2(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);
        }

        private static Vector2 clamp(Vector2 point) => Vector2.Clamp(point, Vector2.Zero, OsuPlayfield.BASE_SIZE);
    }
}
