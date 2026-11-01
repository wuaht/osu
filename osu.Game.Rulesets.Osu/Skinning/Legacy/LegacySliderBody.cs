// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacySliderBody : FrostedPlaySliderBody
    {
        // kept here as well, as the path is recreated when the body is recycled.
        private bool darkenBody = true;

        protected override DrawableSliderPath CreateSliderPath() => new LegacyDrawableSliderPath { DarkenBody = darkenBody };

        protected override Color4 GetBorderColour(ISkinSource skin)
            => skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderBorder)?.Value ?? Color4.White;

        protected override Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour)
        {
            // glass slider bodies may always use the combo colour, regardless of the skin's slider track colour.
            Color4? trackOverride = IsGlass && AlwaysColoured.Value ? null : skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderTrackOverride)?.Value;

            // like argon, a custom track colour is shown as is under liquid glass, it would be barely distinguishable from black if it was darkened like the combo colour.
            darkenBody = trackOverride == null;
            ((LegacyDrawableSliderPath)Path).DarkenBody = darkenBody;

            // legacy skins use a constant value for slider track alpha, regardless of the source colour.
            return (trackOverride ?? hitObjectAccentColour).Opacity(getBodyAlpha());
        }

        private const float default_body_alpha = 0.7f;

        private float getBodyAlpha()
        {
            if (!IsGlass)
                return default_body_alpha;

            // glass slider bodies are more transparent to let the backdrop show through.
            return default_body_alpha - 0.6f * Transparency;
        }

        private partial class LegacyDrawableSliderPath : FrostedDrawableSliderPath
        {
            private const float default_shadow_alpha = 0.25f;

            /// <summary>
            /// The alpha of the shadow at the edge of the body.
            /// Scaled along with the alpha of the body, such that the shadow can always be told apart from the body (see <see cref="Update"/>).
            /// </summary>
            private float shadowAlpha => default_shadow_alpha * Math.Min(1, AccentColour.A / default_body_alpha);

            // https://github.com/peppy/osu-stable-reference/blob/3ea48705eb67172c430371dcfc8a16a002ed0d3d/osu!/Graphics/Renderers/MmSliderRendererGL.cs#L59-L70
            private const float shadow_portion = 1 - (OsuLegacySkinTransformer.LEGACY_CIRCLE_RADIUS / OsuHitObject.OBJECT_RADIUS);
            private const float default_border_portion = 0.1875f;

            // the border starts right after the shadow.
            protected override float BorderStart => shadow_portion;

            // under liquid glass, the border is as thick as the border of argon slider bodies.
            protected override float BorderEnd => shadow_portion + (IsLiquidGlass ? ARGON_BORDER_PORTION : default_border_portion - shadow_portion) * BorderScale;

            public LegacyDrawableSliderPath()
            {
                FrostedTintStrength = 0.75f;
            }

            protected override void Update()
            {
                // Prevents the shadow at the edge of the body from blurring the backdrop.
                // The margin accounts for the precision of the path texture.
                MaskCutoff = shadowAlpha + 0.02f;

                base.Update();
            }

            protected override Color4 ColourAt(float position)
            {
                Color4 shadow = new Color4(0, 0, 0, shadowAlpha);
                Color4 outerColour = AccentColour.Darken(0.1f);
                Color4 innerColour = lighten(AccentColour, 0.5f);

                if (position <= shadow_portion)
                    return LegacyUtils.InterpolateNonLinear(position, Color4.Black.Opacity(0f), shadow, 0, shadow_portion);

                if (position <= BorderEnd)
                    return GlassBorderColour;

                // liquid glass bodies are a flat dark fill like argon, rather than the legacy gradient.
                if (IsLiquidGlass)
                    return ArgonBodyColour;

                return LegacyUtils.InterpolateNonLinear(position, outerColour, innerColour, BorderEnd, 1);
            }

            /// <summary>
            /// Lightens a colour in a way more friendly to dark or strong colours.
            /// </summary>
            private static Color4 lighten(Color4 color, float amount)
            {
                amount *= 0.5f;
                return new Color4(
                    Math.Min(1, color.R * (1 + 0.5f * amount) + 1 * amount),
                    Math.Min(1, color.G * (1 + 0.5f * amount) + 1 * amount),
                    Math.Min(1, color.B * (1 + 0.5f * amount) + 1 * amount),
                    color.A);
            }
        }
    }
}
