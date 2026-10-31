// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    /// <summary>
    /// A <see cref="DrawableSliderPath"/> which shows the content behind it according to the glass style of slider bodies.
    /// </summary>
    public abstract partial class FrostedDrawableSliderPath : DrawableSliderPath
    {
        /// <summary>
        /// The blur sigma at a blur setting of 0.
        /// </summary>
        private const float min_blur_sigma = 2;

        /// <summary>
        /// The blur sigma at a blur setting of 1.
        /// </summary>
        private const float max_blur_sigma = 24;

        /// <summary>
        /// The width of the refracting edge of liquid glass at the highest thickness setting, relative to the path radius.
        /// </summary>
        private const float max_liquid_glass_thickness = 0.8f;

        private readonly Bindable<GlassStyle> glassStyle = new Bindable<GlassStyle>();
        private readonly BindableFloat blur = new BindableFloat();

        private LiquidGlassConfig? liquidGlass;

        /// <summary>
        /// How strongly frosted bodies tint the backdrop with their colour. Liquid glass uses its tint setting instead.
        /// </summary>
        protected float FrostedTintStrength { get; init; }

        /// <summary>
        /// Whether the border of the body is left out, as it would cover the refracting edge of liquid glass.
        /// </summary>
        protected bool HideBorder => glassStyle.Value == GlassStyle.LiquidGlass;

        public override float PathRadius
        {
            get => base.PathRadius;
            set
            {
                base.PathRadius = value;

                // the thickness of liquid glass is relative to the path radius.
                if (liquidGlass != null && glassStyle.Value == GlassStyle.LiquidGlass)
                    updateGlass();
            }
        }

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(OsuConfigManager? config)
        {
            config?.BindWith(OsuSetting.SlopSliderGlassStyle, glassStyle);
            config?.BindWith(OsuSetting.SlopFrostedSlidersBlur, blur);

            liquidGlass = new LiquidGlassConfig(config, updateGlass);

            glassStyle.BindValueChanged(_ =>
            {
                updateGlass();
                InvalidateTexture();
            });
            blur.BindValueChanged(_ => updateGlass());

            updateGlass();
        }

        private void updateGlass()
        {
            BlurSigma = glassStyle.Value switch
            {
                GlassStyle.Frosted => new Vector2(GetBlurSigma(blur.Value)),
                GlassStyle.LiquidGlass => liquidGlass!.BlurSigma,
                _ => Vector2.Zero,
            };

            BackdropTintStrength = glassStyle.Value == GlassStyle.LiquidGlass ? liquidGlass!.TintStrength : FrostedTintStrength;
            LiquidGlass = glassStyle.Value == GlassStyle.LiquidGlass ? liquidGlass!.CreateParameters(PathRadius * max_liquid_glass_thickness) : default;
        }

        /// <summary>
        /// Returns the blur sigma for a blur setting from 0 to 1.
        /// </summary>
        public static float GetBlurSigma(float blur) => min_blur_sigma + (max_blur_sigma - min_blur_sigma) * blur;
    }
}
