// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Shared colours for the editor's bars and panels, which are kept translucent so that the background stays visible (similar to osu!stable).
    /// </summary>
    public static class EditorPanelStyle
    {
        /// <summary>
        /// Background for panels containing text or controls, dark enough to keep them readable.
        /// </summary>
        public static Color4 PanelBackground => Color4.Black.Opacity(0.5f);

        /// <summary>
        /// The darkening applied on top of the blurred backdrop of frosted panels (see <see cref="Components.FrostedPanelBackground"/>).
        /// Lighter than <see cref="PanelBackground"/>, as the blur already helps to keep text readable.
        /// </summary>
        public static Color4 FrostedPanelTint => Color4.Black.Opacity(0.3f);

        /// <summary>
        /// The strength of the blur of frosted panels.
        /// </summary>
        public const float FROSTED_PANEL_BLUR_SIGMA = 15;

        /// <summary>
        /// A subtle additional layer to visually separate areas within a panel.
        /// </summary>
        public static Color4 PanelAccent => Color4.Black.Opacity(0.2f);

        /// <summary>
        /// A translucent highlight for hovered items placed on top of a panel.
        /// </summary>
        public static Color4 HoverHighlight => Color4.White.Opacity(0.1f);
    }
}
