// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace osu.Game.Screens.Edit.Components
{
    /// <summary>
    /// A frosted glass background for editor panels. The content behind it stays visible, but is blurred and slightly darkened,
    /// such that text and controls on top of it stay readable even on bright or busy backgrounds.
    /// </summary>
    public partial class FrostedPanelBackground : BackdropBlurContainer
    {
        public FrostedPanelBackground()
        {
            RelativeSizeAxes = Axes.Both;

            BlurSigma = new Vector2(EditorPanelStyle.FROSTED_PANEL_BLUR_SIGMA);

            // The backdrop is blurred at a quarter of the resolution, which is visually indistinguishable but much cheaper.
            EffectBufferScale = new Vector2(0.25f);

            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = EditorPanelStyle.FrostedPanelTint,
            };
        }
    }
}
