// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Configuration;
using osuTK;
using osuTK.Graphics;

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
        /// The thickness of the border relative to its usual thickness, as a thick border covers the refracting edge of liquid glass.
        /// </summary>
        protected float BorderScale { get; private set; } = 1;

        /// <summary>
        /// The position at which the border starts, from 0 (the edge of the path) to 1 (the centre).
        /// </summary>
        protected virtual float BorderStart => 0;

        /// <summary>
        /// The position at which the border ends and the body starts, from 0 (the edge of the path) to 1 (the centre).
        /// </summary>
        protected abstract float BorderEnd { get; }

        private bool borderIsGlass;

        /// <summary>
        /// The colour of the border. Under liquid glass, it is as transparent as the body, such that its glass shows through and is tinted like the body.
        /// </summary>
        protected Color4 GlassBorderColour => borderIsGlass ? BorderColour.Opacity(BorderColour.A * AccentColour.A) : BorderColour;

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

            glassStyle.BindValueChanged(_ => updateGlass());
            blur.BindValueChanged(_ => updateGlass());

            updateGlass();
        }

        protected override void Update()
        {
            base.Update();

            // the border is a separate ring of glass, such that it stands out from the body.
            LiquidGlassStart = BorderStart;
            LiquidGlassSeparation = BorderEnd;
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

            bool isLiquidGlass = glassStyle.Value == GlassStyle.LiquidGlass;
            float borderScale = isLiquidGlass ? liquidGlass!.SliderBorderThickness : 1;

            if (borderScale != BorderScale || isLiquidGlass != borderIsGlass)
            {
                BorderScale = borderScale;
                borderIsGlass = isLiquidGlass;
                InvalidateTexture();
            }
        }

        /// <summary>
        /// Returns the blur sigma for a blur setting from 0 to 1.
        /// </summary>
        public static float GetBlurSigma(float blur) => min_blur_sigma + (max_blur_sigma - min_blur_sigma) * blur;
    }
}
