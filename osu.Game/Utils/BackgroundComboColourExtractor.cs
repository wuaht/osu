// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace osu.Game.Utils
{
    /// <summary>
    /// Extracts prominent combo colours from a beatmap background.
    /// </summary>
    public static class BackgroundComboColourExtractor
    {
        public const int MAX_COLOUR_COUNT = 8;

        // Resolution to downscale to before quantizing.
        private const int max_dimension = 128;

        // Ranking criteria: avoid colours with lightness <50 or >220.
        private const float min_lightness = 50 / 255f;
        private const float max_lightness = 220 / 255f;

        /// <summary>
        /// The number of colours extracted for <see cref="PickRandomColours"/> (like osu!stable).
        /// </summary>
        public const int RANDOM_CANDIDATE_COUNT = 32;

        /// <summary>
        /// Colours closer than this (euclidean RGB distance, 0-1 per channel) are considered duplicates by <see cref="PickRandomColours"/>.
        /// </summary>
        private const float similarity_threshold = 30 / 255f;

        private const int max_random_attempts = 100;

        /// <summary>
        /// Replaces all unlocked colours with random candidates, avoiding colours which are similar to any other colour (like osu!stable).
        /// </summary>
        /// <param name="colours">The current colours.</param>
        /// <param name="isLocked">Whether the colour at an index is locked and should be kept.</param>
        /// <param name="candidates">The colours to pick from.</param>
        /// <param name="random">The random number generator.</param>
        /// <returns>The new colours, or the current colours if there are no candidates.</returns>
        public static Colour4[] PickRandomColours(IReadOnlyList<Colour4> colours, Func<int, bool> isLocked, IReadOnlyList<Colour4> candidates, Random random)
        {
            var result = colours.ToArray();

            if (candidates.Count == 0)
                return result;

            for (int i = 0; i < result.Length; i++)
            {
                if (isLocked(i))
                    continue;

                Colour4 colour = candidates[random.Next(candidates.Count)];

                for (int attempt = 0; attempt < max_random_attempts && isDuplicate(colour, i); attempt++)
                    colour = candidates[random.Next(candidates.Count)];

                result[i] = colour;
            }

            return result;

            bool isDuplicate(Colour4 colour, int index)
            {
                for (int i = 0; i < result.Length; i++)
                {
                    if (i == index)
                        continue;

                    // unlocked colours after the current index will be replaced anyway.
                    if (i > index && !isLocked(i))
                        continue;

                    float r = colour.R - result[i].R;
                    float g = colour.G - result[i].G;
                    float b = colour.B - result[i].B;

                    if (MathF.Sqrt(r * r + g * g + b * b) < similarity_threshold)
                        return true;
                }

                return false;
            }
        }

        public static Colour4[] Extract(Stream imageStream, int maxColours = MAX_COLOUR_COUNT)
        {
            using var image = Image.Load<Rgba32>(imageStream);

            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(max_dimension, max_dimension),
                Mode = ResizeMode.Max,
            }));

            var quantizer = new WuQuantizer(new QuantizerOptions
            {
                MaxColors = maxColours,
            });

            using var frameQuantizer = quantizer.CreatePixelSpecificQuantizer<Rgba32>(SixLabors.ImageSharp.Configuration.Default);
            using var result = frameQuantizer.BuildPaletteAndQuantizeFrame(image.Frames.RootFrame, image.Bounds);

            return result.Palette.ToArray()
                         .Where(p => p.A >= 128)
                         .Select(p => clampLightness(new Colour4(p.R, p.G, p.B, 255)))
                         .ToArray();
        }

        private static Colour4 clampLightness(Colour4 colour)
        {
            var hsl = colour.ToHSL();
            return Colour4.FromHSL(hsl.X, hsl.Y, Math.Clamp(hsl.Z, min_lightness, max_lightness));
        }
    }
}
