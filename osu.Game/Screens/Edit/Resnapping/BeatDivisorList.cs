// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;

namespace osu.Game.Screens.Edit.Resnapping
{
    /// <summary>
    /// Parses comma-separated lists of beat divisors in the style of Mapping Tools (e.g. "1/16, 1/12, 0.25").
    /// Each divisor is a fraction of one beat.
    /// </summary>
    public static class BeatDivisorList
    {
        public const string DEFAULT = @"1/16, 1/12";

        /// <summary>
        /// Attempts to parse <paramref name="text"/> into a list of positive beat fractions.
        /// A bare integer like "16" is read as "1/16".
        /// </summary>
        public static bool TryParse(string? text, out double[] divisors)
        {
            var result = new List<double>();
            divisors = result.ToArray();

            if (string.IsNullOrWhiteSpace(text))
                return false;

            foreach (string part in text.Split(',', ';'))
            {
                string trimmed = part.Trim();

                if (trimmed.Length == 0)
                    continue;

                double value;
                int slash = trimmed.IndexOf('/');

                if (slash >= 0)
                {
                    if (!tryParseNumber(trimmed[..slash], out double numerator) || !tryParseNumber(trimmed[(slash + 1)..], out double denominator) || denominator == 0)
                        return false;

                    value = numerator / denominator;
                }
                else
                {
                    if (!tryParseNumber(trimmed, out value))
                        return false;

                    // "16" means a sixteenth of a beat, not sixteen beats.
                    if (value > 1 && value == System.Math.Floor(value))
                        value = 1 / value;
                }

                if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                    return false;

                result.Add(value);
            }

            divisors = result.ToArray();
            return divisors.Length > 0;
        }

        private static bool tryParseNumber(string text, out double value)
            => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
