// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Helpers for colours which are stored as hex strings in <see cref="OsuConfigManager"/>.
    /// </summary>
    public static class SlopColours
    {
        /// <summary>
        /// Parses a colour stored as hex string, falling back to <paramref name="fallback"/> if it's invalid.
        /// </summary>
        public static Colour4 Parse(string? hex, Colour4 fallback) => !string.IsNullOrWhiteSpace(hex) && Colour4.TryParseHex(hex.Trim(), out var colour) ? colour : fallback;

        /// <summary>
        /// Retrieves the setting containing the colour of mania notes snapped to <paramref name="beatDivisor"/>, used by timing-based note colouring.
        /// </summary>
        /// <remarks>
        /// Notes on whole beats use the 1/2 colour, as they are also snapped to 1/2.
        /// </remarks>
        public static OsuSetting GetManiaSnapColourSetting(int beatDivisor)
        {
            switch (beatDivisor)
            {
                case 1:
                case 2:
                    return OsuSetting.SlopManiaSnapColour2;

                case 3:
                    return OsuSetting.SlopManiaSnapColour3;

                case 4:
                    return OsuSetting.SlopManiaSnapColour4;

                case 6:
                    return OsuSetting.SlopManiaSnapColour6;

                case 8:
                    return OsuSetting.SlopManiaSnapColour8;

                case 12:
                    return OsuSetting.SlopManiaSnapColour12;

                case 16:
                    return OsuSetting.SlopManiaSnapColour16;

                default:
                    return OsuSetting.SlopManiaSnapColourOther;
            }
        }
    }
}
