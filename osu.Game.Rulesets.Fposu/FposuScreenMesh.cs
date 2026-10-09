// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Fposu
{
    /// <summary>
    /// The (optionally curved) screen in front of the camera which the 2D game is displayed on, built like in McOsu's FPoSu.
    /// </summary>
    /// <remarks>
    /// Positions on the screen are given as UV coordinates, from (0, 0) at the top left to (1, 1) at the bottom right of the 2D game.
    /// </remarks>
    public class FposuScreenMesh
    {
        /// <summary>
        /// The number of times the screen is halved horizontally. Curved screens are made of 2^n flat faces.
        /// </summary>
        public const int SUBDIVISIONS = 4;

        /// <summary>
        /// The number of flat faces the screen is made of.
        /// </summary>
        public const int FACE_COUNT = 1 << SUBDIVISIONS;

        /// <summary>
        /// The offset of the screen along Z. McOsu moves the screen back slightly to avoid aliasing with the background cube.
        /// </summary>
        private const float z_offset = -0.0015f;

        /// <summary>
        /// The distance of the centre of the screen from the camera.
        /// </summary>
        public readonly float Distance;

        /// <summary>
        /// Whether the screen is curved around the camera.
        /// </summary>
        public readonly bool Curved;

        /// <summary>
        /// The ratio of height to width of the 2D game.
        /// </summary>
        public readonly float AspectRatio;

        /// <summary>
        /// The width of the screen along its surface. The height is scaled by this, so that the 2D game isn't stretched.
        /// </summary>
        public readonly float CircumLength;

        /// <summary>
        /// Half of the height of the screen.
        /// </summary>
        public readonly float HalfHeight;

        /// <summary>
        /// The X and Z positions of the vertical edges of the faces, from left to right.
        /// </summary>
        private readonly Vector2[] columns;

        public FposuScreenMesh(float distance, bool curved, float aspectRatio)
        {
            Distance = distance;
            Curved = curved;
            AspectRatio = aspectRatio;

            // the edges of the screen are at x = ±0.5, and curved screens go through these edges.
            float edgeDistance = new Vector2(0.5f, distance).Length;

            var left = new Vector2(-0.5f, -distance);
            var right = new Vector2(0.5f, -distance);

            var list = new List<Vector2>();
            subdivide(list, left, right, SUBDIVISIONS, edgeDistance);
            list.Add(right);
            columns = list.ToArray();

            for (int i = 0; i < columns.Length - 1; i++)
                CircumLength += Vector2.Distance(columns[i], columns[i + 1]);

            HalfHeight = 0.5f * aspectRatio * CircumLength;
        }

        /// <summary>
        /// Adds the columns from <paramref name="a"/> (inclusive) to <paramref name="b"/> (exclusive), halving the segment between them <paramref name="n"/> times.
        /// For curved screens, the midpoints are moved onto the circle through the edges of the screen.
        /// </summary>
        private void subdivide(List<Vector2> list, Vector2 a, Vector2 b, int n, float edgeDistance)
        {
            Vector2 middle = (a + b) / 2;

            if (Curved)
                middle = middle.Normalized() * edgeDistance;

            if (n > 1)
            {
                subdivide(list, a, middle, n - 1, edgeDistance);
                subdivide(list, middle, b, n - 1, edgeDistance);
            }
            else
            {
                list.Add(a);
                list.Add(middle);
            }
        }

        /// <summary>
        /// Returns the position of a point on the screen in 3D space.
        /// </summary>
        /// <param name="uv">The position on the screen.</param>
        public Vector3 GetPosition(Vector2 uv)
        {
            float faceProgress = Math.Clamp(uv.X, 0, 1) * FACE_COUNT;
            int face = Math.Min((int)faceProgress, FACE_COUNT - 1);

            Vector2 xz = Vector2.Lerp(columns[face], columns[face + 1], faceProgress - face);
            float y = HalfHeight * (1 - 2 * Math.Clamp(uv.Y, 0, 1));

            return new Vector3(xz.X, y, xz.Y + z_offset);
        }

        /// <summary>
        /// Intersects a ray from the camera with the screen.
        /// </summary>
        /// <param name="direction">The direction of the ray.</param>
        /// <returns>The position on the screen which the ray hits, or <c>null</c> if it misses the screen.</returns>
        public Vector2? Intersect(Vector3 direction)
        {
            for (int face = 0; face < FACE_COUNT; face++)
            {
                Vector3 topLeft = new Vector3(columns[face].X, HalfHeight, columns[face].Y + z_offset);
                Vector3 topRight = new Vector3(columns[face + 1].X, HalfHeight, columns[face + 1].Y + z_offset);
                Vector3 bottomLeft = new Vector3(columns[face].X, -HalfHeight, columns[face].Y + z_offset);

                Vector3 right = topRight - topLeft;
                Vector3 down = bottomLeft - topLeft;
                Vector3 normal = Vector3.Cross(right, down);

                float denominator = Vector3.Dot(normal, direction);

                if (MathF.Abs(denominator) < 1e-9f)
                    continue;

                // the ray starts at the camera, which is at the origin.
                float t = Vector3.Dot(normal, topLeft) / denominator;

                // only in front of the camera.
                if (t <= 0)
                    continue;

                Vector3 offset = direction * t - topLeft;

                float x = Vector3.Dot(offset, right) / right.LengthSquared;
                float y = Vector3.Dot(offset, down) / down.LengthSquared;

                if (x >= 0 && x <= 1 && y >= 0 && y <= 1)
                    return new Vector2((face + x) / FACE_COUNT, y);
            }

            return null;
        }
    }
}
