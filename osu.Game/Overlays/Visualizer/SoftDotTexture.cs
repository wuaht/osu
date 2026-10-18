// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// Creates a white dot with a soft edge, for drawing particles.
    /// </summary>
    public static class SoftDotTexture
    {
        private const int size = 32;

        private const int glow_size = 64;

        public static Texture Create(IRenderer renderer) => create(renderer, size, distance =>
        {
            // a solid core with a smooth falloff towards the edge.
            float t = Math.Clamp((1 - distance) / 0.6f, 0, 1);
            return t * t * (3 - 2 * t);
        });

        /// <summary>
        /// Creates a white glow without a solid core, which fades out smoothly towards the edge.
        /// </summary>
        public static Texture CreateGlow(IRenderer renderer) => create(renderer, glow_size, distance =>
        {
            float t = Math.Clamp(1 - distance, 0, 1);
            return t * t * t;
        });

        private static Texture create(IRenderer renderer, int textureSize, Func<float, float> alphaAt)
        {
            var image = new Image<Rgba32>(textureSize, textureSize);

            float centre = (textureSize - 1) / 2f;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = MathF.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / (textureSize / 2f);
                    image[x, y] = new Rgba32(1f, 1f, 1f, alphaAt(distance));
                }
            }

            var texture = renderer.CreateTexture(textureSize, textureSize);
            texture.SetData(new TextureUpload(image));
            return texture;
        }
    }
}
