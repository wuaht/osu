// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// A colour summary of a background image, used to colour visualizer elements to fit the background.
    /// </summary>
    /// <param name="DominantHue">The most prominent hue in the range [0, 1).</param>
    /// <param name="IsChromatic">Whether the image is colourful enough for <see cref="DominantHue"/> to be meaningful.</param>
    /// <param name="LeftLuminance">The average relative luminance of the left half of the image, in the range [0, 1].</param>
    /// <param name="AverageLuminance">The average relative luminance of the whole image, in the range [0, 1].</param>
    public readonly record struct BackgroundPalette(float DominantHue, bool IsChromatic, float LeftLuminance, float AverageLuminance)
    {
        /// <summary>
        /// The palette used when no background is available (matching the default osu! backgrounds).
        /// </summary>
        public static readonly BackgroundPalette DEFAULT = new BackgroundPalette(330 / 360f, true, 0.3f, 0.3f);

        private const int hue_buckets = 24;

        /// <summary>
        /// The average chroma weight (saturation × value) above which an image is considered colourful.
        /// </summary>
        private const float chromatic_threshold = 0.12f;

        /// <summary>
        /// The width backgrounds are downscaled to before analysis.
        /// </summary>
        private const int analysis_width = 64;

        /// <summary>
        /// The dimmed luminance above which dark text is used for legibility.
        /// </summary>
        private const float dark_text_threshold = 0.55f;

        /// <summary>
        /// Analyses raw pixel data.
        /// </summary>
        /// <param name="pixels">Row-major pixels.</param>
        /// <param name="width">The width of the image.</param>
        public static BackgroundPalette FromPixels(ReadOnlySpan<Rgba32> pixels, int width)
        {
            if (width <= 0 || pixels.Length < width)
                return DEFAULT;

            Span<float> hueWeights = stackalloc float[hue_buckets];
            Span<float> hueSines = stackalloc float[hue_buckets];
            Span<float> hueCosines = stackalloc float[hue_buckets];

            int height = pixels.Length / width;
            int count = width * height;
            int leftWidth = Math.Max(1, width / 2);

            float totalChroma = 0;
            float totalLuminance = 0;
            float leftLuminance = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = pixels[y * width + x];

                    float r = pixel.R / 255f;
                    float g = pixel.G / 255f;
                    float b = pixel.B / 255f;

                    float luminance = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                    totalLuminance += luminance;
                    if (x < leftWidth)
                        leftLuminance += luminance;

                    float max = MathF.Max(r, MathF.Max(g, b));
                    float min = MathF.Min(r, MathF.Min(g, b));
                    float delta = max - min;

                    if (delta <= 0)
                        continue;

                    // weight by saturation × value, which reduces to the chroma. dull and dark pixels barely contribute.
                    float weight = delta;
                    float hue = getHue(r, g, b, max, delta);

                    int bucket = Math.Min((int)(hue * hue_buckets), hue_buckets - 1);
                    float angle = hue * MathF.Tau;

                    hueWeights[bucket] += weight;
                    hueSines[bucket] += MathF.Sin(angle) * weight;
                    hueCosines[bucket] += MathF.Cos(angle) * weight;
                    totalChroma += weight;
                }
            }

            // find the strongest hue region (a bucket together with its neighbours, so hues straddling a bucket edge are not split).
            int best = 0;
            float bestWeight = float.MinValue;

            for (int i = 0; i < hue_buckets; i++)
            {
                float regionWeight = hueWeights[(i + hue_buckets - 1) % hue_buckets] + hueWeights[i] + hueWeights[(i + 1) % hue_buckets];

                if (regionWeight > bestWeight)
                {
                    bestWeight = regionWeight;
                    best = i;
                }
            }

            // circular mean of the region for a precise hue.
            float sine = 0, cosine = 0;

            for (int offset = -1; offset <= 1; offset++)
            {
                int i = (best + offset + hue_buckets) % hue_buckets;
                sine += hueSines[i];
                cosine += hueCosines[i];
            }

            float dominantHue = sine == 0 && cosine == 0
                ? 0
                : (MathF.Atan2(sine, cosine) / MathF.Tau + 1) % 1;

            return new BackgroundPalette(
                dominantHue,
                totalChroma / count > chromatic_threshold,
                leftLuminance / (leftWidth * height),
                totalLuminance / count);
        }

        /// <summary>
        /// Downscales and analyses an image.
        /// </summary>
        public static BackgroundPalette FromImage(Image<Rgba32> image)
        {
            if (image.Width > analysis_width)
                image.Mutate(i => i.Resize(analysis_width, 0));

            var pixels = new Rgba32[image.Width * image.Height];
            image.CopyPixelDataTo(pixels);

            return FromPixels(pixels, image.Width);
        }

        /// <summary>
        /// Analyses the background of a beatmap on a background thread.
        /// Must be called from the update thread.
        /// </summary>
        /// <returns>The palette, or <see cref="DEFAULT"/> if the beatmap has no usable background.</returns>
        public static Task<BackgroundPalette> LoadAsync(WorkingBeatmap working, CancellationToken cancellationToken = default)
        {
            string? backgroundFile = working.Metadata.BackgroundFile;
            string? storagePath = string.IsNullOrEmpty(backgroundFile) ? null : working.BeatmapSetInfo.GetPathForFile(backgroundFile);

            if (storagePath == null)
                return Task.FromResult(DEFAULT);

            return Task.Run(() =>
            {
                try
                {
                    using var stream = working.GetStream(storagePath);

                    if (stream == null)
                        return DEFAULT;

                    cancellationToken.ThrowIfCancellationRequested();

                    using var image = Image.Load<Rgba32>(stream);
                    return FromImage(image);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    Logger.Log($"Failed to analyse beatmap background for the visualizer: {e.Message}");
                    return DEFAULT;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Whether text drawn over the left half of the background should be dark for legibility.
        /// </summary>
        /// <param name="dim">The amount the background is dimmed by, in the range [0, 1].</param>
        public bool PrefersDarkText(float dim) => LeftLuminance * (1 - dim) > dark_text_threshold;

        /// <summary>
        /// Chooses a bright accent colour which goes well with the background.
        /// </summary>
        /// <param name="seed">A value stable per background, so the same background always receives the same colour.</param>
        public Colour4 ChooseAccentColour(int seed)
        {
            var random = new Random(seed);

            double choice = random.NextDouble();
            float saturation = 0.75f + (float)random.NextDouble() * 0.2f;

            AccentKind kind = IsChromatic
                ? choice < 0.5 ? AccentKind.Fitting : choice < 0.85 ? AccentKind.Complementary : AccentKind.Monochrome
                : choice < 0.5 ? AccentKind.Complementary : AccentKind.Monochrome;

            switch (kind)
            {
                case AccentKind.Fitting:
                    return Colour4.FromHSV(DominantHue, saturation, 1);

                case AccentKind.Complementary:
                    return Colour4.FromHSV((DominantHue + 0.5f) % 1, saturation, 1);

                default:
                    return Colour4.White;
            }
        }

        private static float getHue(float r, float g, float b, float max, float delta)
        {
            float hue;

            if (max == r)
                hue = (g - b) / delta;
            else if (max == g)
                hue = (b - r) / delta + 2;
            else
                hue = (r - g) / delta + 4;

            hue /= 6;
            return hue < 0 ? hue + 1 : hue;
        }

        private enum AccentKind
        {
            Fitting,
            Complementary,
            Monochrome,
        }
    }
}
