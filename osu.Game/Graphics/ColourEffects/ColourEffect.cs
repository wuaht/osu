// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Graphics.ColourEffects
{
    /// <summary>
    /// Helpers for rainbow / gradient colour effects.
    /// </summary>
    public static class ColourEffect
    {
        public const string DEFAULT_GRADIENT = "#FF66AA,#66CCFF";

        public const string DEFAULT_COMBO_GRADIENT = "#FF66AA,#FFCC22,#66CCFF";

        /// <summary>
        /// The duration of one full cycle at a speed of 1, in milliseconds.
        /// </summary>
        public const double CYCLE_DURATION = 5000;

        /// <summary>
        /// Parses a comma-separated list of hex colours. Invalid entries are skipped.
        /// </summary>
        public static List<Colour4> ParseGradient(string? value)
        {
            var colours = new List<Colour4>();

            if (string.IsNullOrWhiteSpace(value))
                return colours;

            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Colour4.TryParseHex(part, out var colour))
                    colours.Add(colour);
            }

            return colours;
        }

        public static string SerialiseGradient(IEnumerable<Colour4> colours)
            => string.Join(',', colours.Select(c => c.ToHex()));

        /// <summary>
        /// Computes the colour of an effect at the given phase.
        /// </summary>
        /// <param name="mode">The effect mode.</param>
        /// <param name="stops">The gradient stops, used in <see cref="ColourEffectMode.Gradient"/> mode.</param>
        /// <param name="phase">The cycle phase, wrapped into the range [0, 1).</param>
        /// <param name="rainbowSaturation">The saturation used in <see cref="ColourEffectMode.Rainbow"/> mode.</param>
        /// <remarks>
        /// <see cref="ColourEffectMode.ComboColours"/> depends on the beatmap rather than the phase (see <see cref="GetComboColourTimeline"/>), so it falls back to <paramref name="stops"/> here.
        /// </remarks>
        public static Colour4 GetColour(ColourEffectMode mode, IReadOnlyList<Colour4> stops, float phase, float rainbowSaturation = 1)
        {
            phase -= MathF.Floor(phase);

            switch (mode)
            {
                case ColourEffectMode.Rainbow:
                    return getRainbowColour(phase, rainbowSaturation);

                case ColourEffectMode.Gradient:
                case ColourEffectMode.ComboColours:
                    return getGradientColour(stops, phase);

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        /// <summary>
        /// Retrieves the combo colours of the given skin (including beatmap combo colours, if the skin provides them).
        /// </summary>
        public static IReadOnlyList<Colour4> GetComboColours(ISkin? skin)
        {
            var colours = skin?.GetConfig<GlobalSkinColours, IReadOnlyList<Color4>>(GlobalSkinColours.ComboColours)?.Value;
            return colours?.Select(c => (Colour4)c).ToArray() ?? Array.Empty<Colour4>();
        }

        private static readonly ConditionalWeakTable<IBeatmap, CachedComboColourTimeline> combo_colour_timelines = new ConditionalWeakTable<IBeatmap, CachedComboColourTimeline>();

        /// <summary>
        /// Retrieves the combo colours of the given beatmap over time, see <see cref="ComboColourTimeline"/>.
        /// </summary>
        public static ComboColourTimeline GetComboColourTimeline(ISkin? skin, IBeatmap? beatmap)
        {
            if (skin == null || beatmap == null)
                return ComboColourTimeline.EMPTY;

            var skinColours = GetComboColours(skin);

            lock (combo_colour_timelines)
            {
                // the colours only change along with the skin colours (e.g. when toggling beatmap colours).
                if (combo_colour_timelines.TryGetValue(beatmap, out var cached) && cached.SkinColours.SequenceEqual(skinColours))
                    return cached.Timeline;

                var times = new List<double>();
                var colours = new List<Colour4>();
                int lastComboIndex = -1;

                foreach (var hitObject in beatmap.HitObjects)
                {
                    if (!(hitObject is IHasComboInformation combo) || combo.ComboIndex == lastComboIndex)
                        continue;

                    lastComboIndex = combo.ComboIndex;
                    times.Add(hitObject.StartTime);
                    colours.Add(combo.GetComboColour(skin));
                }

                var timeline = new ComboColourTimeline(times.ToArray(), colours.ToArray());
                combo_colour_timelines.AddOrUpdate(beatmap, new CachedComboColourTimeline(skinColours, timeline));
                return timeline;
            }
        }

        private record CachedComboColourTimeline(IReadOnlyList<Colour4> SkinColours, ComboColourTimeline Timeline);

        /// <summary>
        /// The combo colours of a beatmap over time.
        /// Every combo starts with its own combo colour and smoothly blends into the colour of the next combo until that one starts,
        /// so that the colour pattern of the beatmap (e.g. colour hax) is kept.
        /// </summary>
        public class ComboColourTimeline
        {
            public static readonly ComboColourTimeline EMPTY = new ComboColourTimeline(Array.Empty<double>(), Array.Empty<Colour4>());

            private readonly double[] startTimes;
            private readonly Colour4[] colours;

            public bool IsEmpty => colours.Length == 0;

            public ComboColourTimeline(double[] startTimes, Colour4[] colours)
            {
                this.startTimes = startTimes;
                this.colours = colours;
            }

            public Colour4 GetColourAt(double time)
            {
                if (IsEmpty)
                    return Colour4.White;

                int index = Array.BinarySearch(startTimes, time);

                // the combo which started last at the given time.
                if (index < 0)
                    index = ~index - 1;

                if (index < 0)
                    return colours[0];

                if (index >= colours.Length - 1)
                    return colours[^1];

                double duration = startTimes[index + 1] - startTimes[index];
                float t = duration > 0 ? (float)Math.Clamp((time - startTimes[index]) / duration, 0, 1) : 1;

                return lerp(colours[index], colours[index + 1], t);
            }
        }

        private static Colour4 getGradientColour(IReadOnlyList<Colour4> stops, float phase)
        {
            if (stops.Count == 0)
                return Colour4.White;

            if (stops.Count == 1)
                return stops[0];

            // cyclic interpolation, the last stop blends back into the first one.
            float position = phase * stops.Count;
            int index = (int)position % stops.Count;
            float t = position - MathF.Floor(position);

            return lerp(stops[index], stops[(index + 1) % stops.Count], t);
        }

        /// <summary>
        /// The combo colours of the argon skin, in rainbow order.
        /// </summary>
        private static readonly Colour4[] rainbow_colours =
        {
            new Colour4(241, 0, 0, 255), // Red
            new Colour4(241, 116, 0, 255), // Orange
            new Colour4(232, 235, 0, 255), // Yellow
            new Colour4(0, 241, 53, 255), // Green
            new Colour4(0, 82, 241, 255), // Blue
            new Colour4(92, 0, 241, 255), // Purple
        };

        private static Colour4 getRainbowColour(float phase, float saturation)
        {
            // lower saturations blend towards white, which brightens dark colours like blue and purple.
            return lerp(Colour4.White, getGradientColour(rainbow_colours, phase), Math.Clamp(saturation, 0, 1));
        }

        private static Colour4 lerp(Colour4 a, Colour4 b, float t)
            => new Colour4(
                a.R + (b.R - a.R) * t,
                a.G + (b.G - a.G) * t,
                a.B + (b.B - a.B) * t,
                a.A + (b.A - a.A) * t);
    }
}
