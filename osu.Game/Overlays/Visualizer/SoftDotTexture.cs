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

        public static Texture Create(IRenderer renderer)
        {
            var image = new Image<Rgba32>(size, size);

            const float centre = (size - 1) / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = MathF.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre)) / (size / 2f);

                    // a solid core with a smooth falloff towards the edge.
                    float t = Math.Clamp((1 - distance) / 0.6f, 0, 1);
                    float alpha = t * t * (3 - 2 * t);

                    image[x, y] = new Rgba32(1f, 1f, 1f, alpha);
                }
            }

            var texture = renderer.CreateTexture(size, size);
            texture.SetData(new TextureUpload(image));
            return texture;
        }
    }
}
