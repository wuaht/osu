// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    /// <summary>
    /// The slider body of the triangles skin, which looks like argon slider bodies under liquid glass.
    /// </summary>
    public partial class DefaultGlassSliderBody : FrostedPlaySliderBody
    {
        // kept here as well, as the path is recreated when the body is recycled.
        private bool darkenBody = true;

        private bool isLiquidGlass => GlassStyle.Value == osu.Game.Configuration.GlassStyle.LiquidGlass;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            GlassStyle.BindValueChanged(_ =>
            {
                // the border is as thick as the border of argon slider bodies.
                BorderSize = isLiquidGlass ? FrostedDrawableSliderPath.ARGON_BORDER_PORTION / DrawableSliderPath.BORDER_PORTION : 1;
                UpdateBorderColour();
            }, true);
        }

        protected override DrawableSliderPath CreateSliderPath() => new DefaultGlassDrawableSliderPath { DarkenBody = darkenBody };

        // same as argon: the border takes the combo colour unless the beatmap or skin provides one.
        protected override Color4 GetBorderColour(ISkinSource skin) => isLiquidGlass ? GetSkinBorderColour() ?? AccentColourBindable.Value : base.GetBorderColour(skin);

        protected override Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour)
        {
            Color4 colour = base.GetBodyAccentColour(skin, hitObjectAccentColour);

            if (!isLiquidGlass)
                return colour;

            // a custom track colour is shown as is, it would be barely distinguishable from black if it was darkened like the combo colour.
            darkenBody = GetSkinTrackColour() == null;
            ((DefaultGlassDrawableSliderPath)Path).DarkenBody = darkenBody;

            // same transparency as argon glass slider bodies.
            return colour.Opacity(0.85f - 0.75f * Transparency);
        }

        private partial class DefaultGlassDrawableSliderPath : FrostedDrawableSliderPath
        {
            private const float opacity_at_centre = 0.3f;
            private const float opacity_at_edge = 0.8f;

            public DefaultGlassDrawableSliderPath()
            {
                FrostedTintStrength = 0.6f;
            }

            protected override float BorderEnd => CalculatedBorderPortion * BorderScale;

            protected override Color4 ColourAt(float position)
            {
                if (BorderEnd != 0f && position <= BorderEnd)
                    return GlassBorderColour;

                if (IsLiquidGlass)
                    return ArgonBodyColour;

                position -= BorderEnd;
                return new Color4(AccentColour.R, AccentColour.G, AccentColour.B, (opacity_at_edge - (opacity_at_edge - opacity_at_centre) * position / GRADIENT_PORTION) * AccentColour.A);
            }
        }
    }
}
