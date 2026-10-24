// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Shared sizes, colours and fonts of the Discord overlay.
    /// </summary>
    public static class DiscordStyle
    {
        /// <summary>
        /// The hue of the <see cref="OverlayColourProvider"/>, close to the Discord blurple.
        /// </summary>
        public const int HUE = 235;

        public const float GUILD_RAIL_WIDTH = 72;
        public const float SIDEBAR_WIDTH = 240;
        public const float HEADER_HEIGHT = 48;

        public const float MESSAGE_TEXT_SIZE = 15;
        public const float AVATAR_SIZE = 40;
        public const float MESSAGE_GUTTER = 72;

        public static readonly Color4 Blurple = Color4Extensions.FromHex(@"5865f2");
        public static readonly Color4 MentionBackground = new Color4(88, 101, 242, 77);
        public static readonly Color4 MentionText = Color4Extensions.FromHex(@"c9cdfb");
        public static readonly Color4 MentionedMessage = new Color4(240, 177, 50, 25);
        public static readonly Color4 MentionedMessageBar = Color4Extensions.FromHex(@"f0b232");
        public static readonly Color4 Link = Color4Extensions.FromHex(@"00a8fc");
        public static readonly Color4 Error = Color4Extensions.FromHex(@"f23f43");
        public static readonly Color4 CodeBackground = new Color4(0, 0, 0, 115);
        public static readonly Color4 Spoiler = Color4Extensions.FromHex(@"4e5058");

        /// <summary>
        /// The font of message content. Inter is used because it has italic variants.
        /// </summary>
        public static FontUsage MessageFont(float size = MESSAGE_TEXT_SIZE, bool bold = false, bool italic = false)
            => new FontUsage(@"Inter", size, bold ? @"SemiBold" : @"Regular", italic);

        /// <summary>
        /// The font of code. There is no monospace font in the game, and Inter's fixed width variant spaces letters too far apart to be readable.
        /// </summary>
        public static FontUsage CodeFont(float size = MESSAGE_TEXT_SIZE - 1) => new FontUsage(@"Inter", size, @"Regular");

        public static FontUsage UiFont(float size = 14, FontWeight weight = FontWeight.Regular) => OsuFont.Torus.With(size: size, weight: weight);

        /// <summary>
        /// Converts a Discord colour integer (such as a role or embed colour) to a colour, or returns null if no colour is set.
        /// </summary>
        public static Color4? FromInteger(int? colour)
        {
            if (colour == null || colour.Value == 0)
                return null;

            int value = colour.Value;
            return new Color4((byte)((value >> 16) & 0xff), (byte)((value >> 8) & 0xff), (byte)(value & 0xff), 255);
        }
    }
}
