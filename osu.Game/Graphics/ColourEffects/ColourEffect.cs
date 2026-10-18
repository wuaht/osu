// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;

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
        public static Colour4 GetColour(ColourEffectMode mode, IReadOnlyList<Colour4> stops, float phase, float rainbowSaturation = 1)
        {
            phase -= MathF.Floor(phase);

            switch (mode)
            {
                case ColourEffectMode.Rainbow:
                    return Colour4.FromHSV(phase, rainbowSaturation, 1);

                case ColourEffectMode.Gradient:
                    if (stops.Count == 0)
                        return Colour4.White;

                    if (stops.Count == 1)
                        return stops[0];

                    // cyclic interpolation, the last stop blends back into the first one.
                    float position = phase * stops.Count;
                    int index = (int)position % stops.Count;
                    float t = position - MathF.Floor(position);

                    return lerp(stops[index], stops[(index + 1) % stops.Count], t);

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static Colour4 lerp(Colour4 a, Colour4 b, float t)
            => new Colour4(
                a.R + (b.R - a.R) * t,
                a.G + (b.G - a.G) * t,
                a.B + (b.B - a.B) * t,
                a.A + (b.A - a.A) * t);
    }
}
