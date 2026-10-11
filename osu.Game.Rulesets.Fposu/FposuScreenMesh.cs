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
    /// Positions on the screen are given as UV coordinates, from (0, 0) at the top left to (1, 1) at the bottom right of the whole mesh.
    /// Without <see cref="HorizontalScale"/> and <see cref="VerticalScale"/>, this is exactly the 2D game.
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
        /// The width of the whole mesh relative to the width of the 2D game, which is displayed in the middle of it.
        /// The rest of the mesh shows what the 2D game draws beyond its edges.
        /// </summary>
        public readonly float HorizontalScale;

        /// <summary>
        /// The height of the whole mesh relative to the height of the 2D game (see <see cref="HorizontalScale"/>).
        /// </summary>
        public readonly float VerticalScale;

        /// <summary>
        /// The width of the 2D game along the surface of the screen. The height is scaled by this, so that the 2D game isn't stretched.
        /// </summary>
        public readonly float CircumLength;

        /// <summary>
        /// Half of the height of the whole mesh.
        /// </summary>
        public readonly float HalfHeight;

        /// <summary>
        /// The X and Z positions of the vertical edges of the faces, from left to right.
        /// </summary>
        private readonly Vector2[] columns;

        /// <param name="distance">The distance of the centre of the screen from the camera.</param>
        /// <param name="curved">Whether the screen is curved around the camera.</param>
        /// <param name="aspectRatio">The ratio of height to width of the 2D game.</param>
        /// <param name="horizontalScale">The width of the whole mesh relative to the 2D game (see <see cref="HorizontalScale"/>).</param>
        /// <param name="verticalScale">The height of the whole mesh relative to the 2D game (see <see cref="VerticalScale"/>).</param>
        public FposuScreenMesh(float distance, bool curved, float aspectRatio, float horizontalScale = 1, float verticalScale = 1)
        {
            Distance = distance;
            Curved = curved;
            AspectRatio = aspectRatio;
            HorizontalScale = horizontalScale;
            VerticalScale = verticalScale;

            // the edges of the 2D game are at x = ±0.5, and curved screens go through these edges.
            float edgeDistance = new Vector2(0.5f, distance).Length;

            CircumLength = getLength(createColumns(1, edgeDistance));
            columns = createColumns(horizontalScale, edgeDistance);

            HalfHeight = 0.5f * aspectRatio * CircumLength * verticalScale;
        }

        /// <summary>
        /// Creates the vertical edges of the faces of a mesh which is <paramref name="scale"/> times as wide as the 2D game.
        /// </summary>
        private Vector2[] createColumns(float scale, float edgeDistance)
        {
            Vector2 left, right;

            if (Curved)
            {
                // curved meshes continue around the circle, with the angle scaled like the width.
                float angle = MathF.Atan2(0.5f, Distance) * scale;

                left = new Vector2(-MathF.Sin(angle), -MathF.Cos(angle)) * edgeDistance;
                right = new Vector2(MathF.Sin(angle), -MathF.Cos(angle)) * edgeDistance;
            }
            else
            {
                left = new Vector2(-0.5f * scale, -Distance);
                right = new Vector2(0.5f * scale, -Distance);
            }

            var list = new List<Vector2>();
            subdivide(list, left, right, SUBDIVISIONS, edgeDistance);
            list.Add(right);
            return list.ToArray();
        }

        private static float getLength(Vector2[] points)
        {
            float length = 0;

            for (int i = 0; i < points.Length - 1; i++)
                length += Vector2.Distance(points[i], points[i + 1]);

            return length;
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
