// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// Three-dimensional gradient noise, following Ken Perlin's improved noise.
    /// </summary>
    public static class PerlinNoise
    {
        private static readonly int[] permutation = createPermutation();

        /// <summary>
        /// Samples the noise at the given position.
        /// </summary>
        /// <returns>A smoothly varying value in the range [-1, 1].</returns>
        public static float Sample(float x, float y, float z)
        {
            int xi = (int)MathF.Floor(x) & 255;
            int yi = (int)MathF.Floor(y) & 255;
            int zi = (int)MathF.Floor(z) & 255;

            x -= MathF.Floor(x);
            y -= MathF.Floor(y);
            z -= MathF.Floor(z);

            float u = fade(x);
            float v = fade(y);
            float w = fade(z);

            int[] p = permutation;

            int a = p[xi] + yi;
            int aa = p[a] + zi;
            int ab = p[a + 1] + zi;
            int b = p[xi + 1] + yi;
            int ba = p[b] + zi;
            int bb = p[b + 1] + zi;

            float result = lerp(w,
                lerp(v,
                    lerp(u, gradient(p[aa], x, y, z), gradient(p[ba], x - 1, y, z)),
                    lerp(u, gradient(p[ab], x, y - 1, z), gradient(p[bb], x - 1, y - 1, z))),
                lerp(v,
                    lerp(u, gradient(p[aa + 1], x, y, z - 1), gradient(p[ba + 1], x - 1, y, z - 1)),
                    lerp(u, gradient(p[ab + 1], x, y - 1, z - 1), gradient(p[bb + 1], x - 1, y - 1, z - 1))));

            return Math.Clamp(result, -1, 1);
        }

        private static float fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

        private static float lerp(float t, float a, float b) => a + t * (b - a);

        private static float gradient(int hash, float x, float y, float z)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        private static int[] createPermutation()
        {
            // fixed seed, so the noise looks the same every time.
            var random = new Random(1337);

            int[] values = new int[256];
            for (int i = 0; i < values.Length; i++)
                values[i] = i;

            random.Shuffle(values);

            int[] result = new int[512];
            for (int i = 0; i < result.Length; i++)
                result[i] = values[i & 255];

            return result;
        }
    }
}
