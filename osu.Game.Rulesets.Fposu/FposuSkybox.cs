// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Fposu
{
    /// <summary>
    /// A skybox around the camera, whose texture is a cubemap in the horizontal cross layout (like the skybox of McOsu's FPoSu):
    /// the left, front, right and back faces in the middle row, with the top face above and the bottom face below the front face.
    /// </summary>
    public static class FposuSkybox
    {
        /// <summary>
        /// The number of faces in each row of the texture.
        /// </summary>
        public const int TEXTURE_COLUMNS = 4;

        /// <summary>
        /// The number of faces in each column of the texture.
        /// </summary>
        public const int TEXTURE_ROWS = 3;

        public enum Face
        {
            Left,
            Front,
            Right,
            Back,
            Up,
            Down,
        }

        /// <summary>
        /// Returns the column and row of a face in the texture.
        /// </summary>
        public static (int column, int row) GetTexturePosition(Face face)
        {
            switch (face)
            {
                case Face.Left:
                    return (0, 1);

                case Face.Front:
                    return (1, 1);

                case Face.Right:
                    return (2, 1);

                case Face.Back:
                    return (3, 1);

                case Face.Up:
                    return (1, 0);

                default:
                    return (1, 2);
            }
        }

        /// <summary>
        /// Returns the direction (in the coordinate system of <see cref="FposuCamera"/>) of a position on a face of the cube.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="position">The position on the face as displayed in the texture, from (0, 0) at the top left to (1, 1) at the bottom right.</param>
        /// <returns>The direction, which is a point on the cube from (-1, -1, -1) to (1, 1, 1).</returns>
        public static Vector3 GetDirection(Face face, Vector2 position)
        {
            float x = 2 * position.X - 1;
            float y = 1 - 2 * position.Y;

            // each face is seen as displayed in the texture when looking at it with the camera upright, or when looking up or down from the front face.
            switch (face)
            {
                case Face.Left:
                    return new Vector3(-1, y, -x);

                case Face.Front:
                    return new Vector3(x, y, -1);

                case Face.Right:
                    return new Vector3(1, y, x);

                case Face.Back:
                    return new Vector3(-x, y, 1);

                case Face.Up:
                    return new Vector3(x, 1, y);

                default:
                    return new Vector3(x, -1, -y);
            }
        }

        /// <summary>
        /// Returns the face and the position on it which a direction points at. The inverse of <see cref="GetDirection"/>.
        /// </summary>
        public static (Face face, Vector2 position) GetFacePosition(Vector3 direction)
        {
            float ax = MathF.Abs(direction.X);
            float ay = MathF.Abs(direction.Y);
            float az = MathF.Abs(direction.Z);

            Face face;
            float x;
            float y;

            if (ax >= ay && ax >= az)
            {
                var d = direction / ax;

                if (d.X > 0)
                    (face, x, y) = (Face.Right, d.Z, d.Y);
                else
                    (face, x, y) = (Face.Left, -d.Z, d.Y);
            }
            else if (ay >= az)
            {
                var d = direction / ay;

                if (d.Y > 0)
                    (face, x, y) = (Face.Up, d.X, d.Z);
                else
                    (face, x, y) = (Face.Down, d.X, -d.Z);
            }
            else
            {
                var d = direction / az;

                if (d.Z < 0)
                    (face, x, y) = (Face.Front, d.X, d.Y);
                else
                    (face, x, y) = (Face.Back, -d.X, d.Y);
            }

            return (face, new Vector2((x + 1) / 2, (1 - y) / 2));
        }

        private static readonly Vector3 zenith_colour = new Vector3(5, 6, 14);
        private static readonly Vector3 horizon_colour = new Vector3(26, 30, 54);
        private static readonly Vector3 ground_colour = new Vector3(8, 8, 12);

        private const int star_count = 2500;

        /// <summary>
        /// Creates the default skybox texture: a night sky with stars.
        /// </summary>
        /// <param name="faceSize">The size of each face in pixels.</param>
        public static Image<Rgba32> CreateDefaultTexture(int faceSize)
        {
            var image = new Image<Rgba32>(faceSize * TEXTURE_COLUMNS, faceSize * TEXTURE_ROWS, new Rgba32(0, 0, 0, 255));

            var colours = new Vector3[image.Width, image.Height];

            foreach (var face in Enum.GetValues<Face>())
            {
                var (column, row) = GetTexturePosition(face);

                for (int py = 0; py < faceSize; py++)
                {
                    for (int px = 0; px < faceSize; px++)
                    {
                        var direction = GetDirection(face, new Vector2((px + 0.5f) / faceSize, (py + 0.5f) / faceSize)).Normalized();
                        colours[column * faceSize + px, row * faceSize + py] = getSkyColour(direction.Y);
                    }
                }
            }

            // deterministic, so that the sky is the same every time.
            var random = new Random(727);

            for (int i = 0; i < star_count; i++)
            {
                var direction = new Vector3(nextGaussian(random), nextGaussian(random), nextGaussian(random));

                if (direction.LengthSquared < 0.0001f)
                    continue;

                direction.Normalize();

                // stars fade out towards the horizon.
                float visibility = Math.Clamp(direction.Y * 5 + 0.3f, 0, 1);

                if (visibility <= 0)
                    continue;

                float brightness = (0.25f + 0.75f * MathF.Pow((float)random.NextDouble(), 3)) * visibility;
                float radius = 0.6f + 1.1f * (float)random.NextDouble() * brightness;

                // slightly coloured, like real stars.
                var tint = Vector3.Lerp(new Vector3(0.75f, 0.85f, 1f), new Vector3(1f, 0.9f, 0.75f), (float)random.NextDouble());

                var (face, position) = GetFacePosition(direction);
                var (column, row) = GetTexturePosition(face);

                drawStar(colours, column * faceSize, row * faceSize, faceSize, position * faceSize, radius, tint * 255 * brightness);
            }

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var pixels = accessor.GetRowSpan(y);

                    for (int x = 0; x < pixels.Length; x++)
                    {
                        var c = colours[x, y];
                        pixels[x] = new Rgba32(toByte(c.X), toByte(c.Y), toByte(c.Z), 255);
                    }
                }
            });

            return image;
        }

        private static Vector3 getSkyColour(float elevation)
        {
            if (elevation >= 0)
                return Vector3.Lerp(horizon_colour, zenith_colour, MathF.Sqrt(elevation));

            // continuous at the horizon, but darkening quicker below it.
            return Vector3.Lerp(horizon_colour, ground_colour, MathF.Pow(-elevation, 0.3f));
        }

        /// <summary>
        /// Adds a star with a soft edge to a face, clipped to the face.
        /// </summary>
        private static void drawStar(Vector3[,] colours, int faceX, int faceY, int faceSize, Vector2 centre, float radius, Vector3 colour)
        {
            int minX = Math.Max(0, (int)MathF.Floor(centre.X - radius - 1));
            int maxX = Math.Min(faceSize - 1, (int)MathF.Ceiling(centre.X + radius + 1));
            int minY = Math.Max(0, (int)MathF.Floor(centre.Y - radius - 1));
            int maxY = Math.Min(faceSize - 1, (int)MathF.Ceiling(centre.Y + radius + 1));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = (new Vector2(x + 0.5f, y + 0.5f) - centre).Length;
                    float intensity = Math.Clamp(1 - distance / (radius + 0.5f), 0, 1);

                    if (intensity > 0)
                        colours[faceX + x, faceY + y] += colour * intensity * intensity;
                }
            }
        }

        private static float nextGaussian(Random random)
        {
            // Box-Muller transform, for uniformly distributed directions.
            double u1 = 1 - random.NextDouble();
            double u2 = random.NextDouble();
            return (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2));
        }

        private static byte toByte(float value) => (byte)Math.Clamp(MathF.Round(value), 0, 255);
    }
}
