// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Configuration
{
    /// <summary>
    /// How a translucent element shows the content behind it.
    /// </summary>
    public enum GlassStyle
    {
        /// <summary>
        /// The content behind the element isn't changed.
        /// </summary>
        Standard,

        /// <summary>
        /// The content behind the element is blurred (see <see cref="OsuSetting.SlopFrostedSlidersBlur"/>).
        /// </summary>
        Frosted,

        /// <summary>
        /// The content behind the element is refracted and lit at its edges, like through a slab of glass (see <see cref="LiquidGlassConfig"/>).
        /// </summary>
        [Description("Liquid glass")]
        LiquidGlass,
    }
}
