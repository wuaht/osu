// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacySliderBody : PlaySliderBody
    {
        private readonly Bindable<bool> frostedSliders = new Bindable<bool>();

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(ISkinSource skin, OsuConfigManager? config)
        {
            config?.BindWith(OsuSetting.SlopFrostedSliders, frostedSliders);

            // The body accent colour depends on whether sliders are frosted.
            frostedSliders.BindValueChanged(_ => AccentColour = GetBodyAccentColour(skin, AccentColourBindable.Value), true);
        }

        protected override DrawableSliderPath CreateSliderPath() => new LegacyDrawableSliderPath();

        protected override Color4 GetBorderColour(ISkinSource skin)
            => skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderBorder)?.Value ?? Color4.White;

        protected override Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour)
            // legacy skins use a constant value for slider track alpha, regardless of the source colour.
            // frosted slider bodies are slightly more transparent, as the blurred backdrop already makes them stand out.
            => (skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderTrackOverride)?.Value ?? hitObjectAccentColour).Opacity(frostedSliders.Value ? 0.6f : 0.7f);

        private partial class LegacyDrawableSliderPath : DrawableSliderPath
        {
            private readonly Bindable<bool> frostedSliders = new Bindable<bool>();

            public LegacyDrawableSliderPath()
            {
                BackdropTintStrength = 0.5f;

                // Prevents the shadow at the edge of the body from blurring the backdrop.
                MaskCutoff = 0.25f;
            }

            [BackgroundDependencyLoader(permitNulls: true)]
            private void load(OsuConfigManager? config)
            {
                config?.BindWith(OsuSetting.SlopFrostedSliders, frostedSliders);
                frostedSliders.BindValueChanged(frosted => BlurSigma = frosted.NewValue ? new Vector2(16) : Vector2.Zero, true);
            }

            protected override Color4 ColourAt(float position)
            {
                Color4 shadow = new Color4(0, 0, 0, 0.25f);
                Color4 outerColour = AccentColour.Darken(0.1f);
                Color4 innerColour = lighten(AccentColour, 0.5f);

                // https://github.com/peppy/osu-stable-reference/blob/3ea48705eb67172c430371dcfc8a16a002ed0d3d/osu!/Graphics/Renderers/MmSliderRendererGL.cs#L59-L70
                const float shadow_portion = 1 - (OsuLegacySkinTransformer.LEGACY_CIRCLE_RADIUS / OsuHitObject.OBJECT_RADIUS);
                const float border_portion = 0.1875f;

                if (position <= shadow_portion)
                    return LegacyUtils.InterpolateNonLinear(position, Color4.Black.Opacity(0f), shadow, 0, shadow_portion);

                if (position <= border_portion)
                    return BorderColour;

                return LegacyUtils.InterpolateNonLinear(position, outerColour, innerColour, border_portion, 1);
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
