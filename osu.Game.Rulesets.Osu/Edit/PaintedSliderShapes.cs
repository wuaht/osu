// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Generates slider shapes the way Slider Painter does, as a chain of quadratic bezier arcs
    /// where each arc is a rotated or mirrored copy of the previous one.
    /// </summary>
    public static class PaintedSliderShapes
    {
        /// <summary>
        /// The arc length of a single segment relative to its radius.
        /// </summary>
        public const float SEGMENT_LENGTH_RATIO = 1.0035644f;

        // Slider Painter passes these angles in degrees, but the arc continuations use them as radians.
        private const float arc_angle = 57.5f;
        private const float turn_angle = 30 * MathF.PI / 180;
        private const float mirror_angle = 115 * MathF.PI / 180;

        /// <summary>
        /// Generates a shape made of quadratic bezier segments, centred on the origin.
        /// </summary>
        /// <param name="segmentCount">The number of segments.</param>
        /// <param name="arcChance">The chance of a segment continuing as an arc instead of turning or mirroring.</param>
        /// <param name="radius">The radius of each segment's arc.</param>
        /// <param name="rng">The random number generator to use.</param>
        /// <returns>The segments, as (start, control, end) triples where each segment starts at the previous one's end.</returns>
        public static List<(Vector2 P0, Vector2 P1, Vector2 P2)> Generate(int segmentCount, double arcChance, float radius, Random rng)
        {
            var segments = new List<(Vector2 P0, Vector2 P1, Vector2 P2)>(segmentCount);

            // a 57.5° arc of the circle around the origin.
            var start = new Vector2(radius, 0);
            var end = new Vector2(radius * 0.5372996f, radius * 0.8433915f);
            var middle = (start + end) / 2;
            segments.Add((start, middle + middle.Normalized() * radius * 0.25629133f, end));

            int previousType = -1;

            for (int i = 1; i < segmentCount; i++)
            {
                int type;

                do
                    type = pickType(arcChance, rng);
                while (!isValid(type, previousType));

                segments.Add(next(segments[^1], type));
                previousType = type;
            }

            var min = new Vector2(segments.Min(s => min3(s.P0.X, s.P1.X, s.P2.X)), segments.Min(s => min3(s.P0.Y, s.P1.Y, s.P2.Y)));
            var max = new Vector2(segments.Max(s => max3(s.P0.X, s.P1.X, s.P2.X)), segments.Max(s => max3(s.P0.Y, s.P1.Y, s.P2.Y)));
            var centre = (min + max) / 2;

            return segments.Select(s => (s.P0 - centre, s.P1 - centre, s.P2 - centre)).ToList();
        }

        private static int pickType(double arcChance, Random rng)
        {
            double u = rng.NextDouble();
            double accumulated = 0;

            for (int type = 0; type < 6; type++)
            {
                accumulated += type < 2 ? arcChance / 2 : (1 - arcChance) / 4;

                if (u < accumulated)
                    return type;
            }

            return 5;
        }

        private static bool isValid(int type, int previousType)
        {
            if (type < 2)
                return true;

            if (type == previousType)
                return false;

            switch (type)
            {
                case 2:
                    return previousType != 3 && previousType != 4 && previousType != 5;

                case 3:
                    return previousType != 2 && previousType != 5;

                case 5:
                    return previousType != 2;

                default:
                    return true;
            }
        }

        private static (Vector2 P0, Vector2 P1, Vector2 P2) next((Vector2 P0, Vector2 P1, Vector2 P2) previous, int type)
        {
            var (p0, p1, p2) = previous;

            switch (type)
            {
                case 0:
                {
                    // continue the arc in the same direction.
                    Vector2 d = p1 - p0;
                    Vector2 v = p2 - p0;
                    float angle = sign(cross(d, v)) * arc_angle;
                    return (p2, p2 + rotate(d, angle), p2 + rotate(v, angle));
                }

                case 1:
                {
                    // an S-bend.
                    Vector2 d = p1 - p0;
                    Vector2 control = p2 + rotate(d, arc_angle);
                    return (p2, control, control + d);
                }

                case 2:
                case 3:
                    return turn(previous, (type == 2 ? 1 : -1) * turn_angle);

                default:
                {
                    // mirror the previous segment along its chord and reverse it, so that it ends where it started.
                    Vector2 chord = (p2 - p0).Normalized();
                    Vector2 tangent = (p1 - p0).Normalized();
                    float tangentAngle = MathF.Acos(Math.Clamp(Vector2.Dot(chord, tangent), -1, 1));
                    Vector2 mirroredStart = p2;
                    Vector2 mirroredControl = p0 + (p1 - p0).Length * rotate(tangent, -2 * tangentAngle * sign(cross(chord, tangent)));
                    Vector2 mirroredEnd = p0;

                    // then rotate it around its start, by an angle measured at its end like for a turn.
                    Vector2 a = mirroredControl - mirroredEnd;
                    Vector2 b = mirroredStart - mirroredEnd;
                    float s = sign(cross(a, b));
                    float spread = MathF.Acos(Math.Clamp(Vector2.Dot(a.Normalized(), b.Normalized()), -1, 1));
                    float angle = (type == 4 ? s * mirror_angle : 0) - 2 * spread * s;

                    return (mirroredStart, mirroredStart + rotate(mirroredControl - mirroredStart, angle), mirroredStart + rotate(mirroredEnd - mirroredStart, angle));
                }
            }
        }

        private static (Vector2 P0, Vector2 P1, Vector2 P2) turn((Vector2 P0, Vector2 P1, Vector2 P2) previous, float angle)
        {
            var (p0, p1, p2) = previous;

            // rotate the previous segment around its end, so that the new one doubles back next to it.
            Vector2 a = p1 - p2;
            Vector2 b = p0 - p2;
            angle *= sign(cross(a, b));

            return (p2, p2 + rotate(a, angle), p2 + rotate(b, angle));
        }

        private static Vector2 rotate(Vector2 v, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

        private static float cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;

        private static float sign(float value) => value < 0 ? -1 : 1;

        private static float min3(float a, float b, float c) => MathF.Min(a, MathF.Min(b, c));

        private static float max3(float a, float b, float c) => MathF.Max(a, MathF.Max(b, c));
    }
}
