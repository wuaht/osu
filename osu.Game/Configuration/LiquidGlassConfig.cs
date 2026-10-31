// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osuTK;

namespace osu.Game.Configuration
{
    /// <summary>
    /// The liquid glass settings shared by everything using <see cref="GlassStyle.LiquidGlass"/>.
    /// </summary>
    public class LiquidGlassConfig
    {
        /// <summary>
        /// The blur sigma at a blur setting of 1.
        /// </summary>
        private const float max_blur_sigma = 20;

        private readonly BindableFloat thickness = new BindableFloat(1);
        private readonly BindableFloat refraction = new BindableFloat(1);
        private readonly BindableFloat chromaticAberration = new BindableFloat(1);
        private readonly BindableFloat tint = new BindableFloat(0.6f);
        private readonly BindableFloat sliderBorderThickness = new BindableFloat(0.5f);
        private readonly BindableFloat blur = new BindableFloat(0.15f);
        private readonly BindableFloat darkness = new BindableFloat(0.1f);

        /// <param name="config">The config to read the settings from, or null to use the default settings.</param>
        /// <param name="changed">Invoked whenever a setting changes.</param>
        public LiquidGlassConfig(OsuConfigManager? config, Action changed)
        {
            bind(OsuSetting.SlopLiquidGlassThickness, thickness);
            bind(OsuSetting.SlopLiquidGlassRefraction, refraction);
            bind(OsuSetting.SlopLiquidGlassChromaticAberration, chromaticAberration);
            bind(OsuSetting.SlopLiquidGlassTint, tint);
            bind(OsuSetting.SlopLiquidGlassSliderBorderThickness, sliderBorderThickness);
            bind(OsuSetting.SlopLiquidGlassBlur, blur);
            bind(OsuSetting.SlopLiquidGlassDarkness, darkness);

            void bind(OsuSetting setting, BindableFloat bindable)
            {
                config?.BindWith(setting, bindable);
                bindable.BindValueChanged(_ => changed());
            }
        }

        /// <summary>
        /// The blur sigma of the content behind liquid glass.
        /// </summary>
        public Vector2 BlurSigma => new Vector2(blur.Value * max_blur_sigma);

        /// <summary>
        /// How strongly coloured liquid glass tints the content behind it with its colour (see <see cref="IBackdropBlurDrawable.BackdropTintStrength"/>).
        /// </summary>
        public float TintStrength => tint.Value;

        /// <summary>
        /// The thickness of the border of slider bodies, relative to the border of the other glass styles.
        /// </summary>
        public float SliderBorderThickness => sliderBorderThickness.Value;

        /// <summary>
        /// Creates the parameters of liquid glass.
        /// </summary>
        /// <param name="maxThickness">The width of the refracting edge at the highest thickness setting, in the local space of the drawable.</param>
        /// <param name="cornerRadius">The corner radius of the glass, in the local space of the drawable.</param>
        /// <param name="shape">The area covered by the glass, or null to cover the whole drawable.</param>
        public LiquidGlassParameters CreateParameters(float maxThickness, float cornerRadius = 0, RectangleF? shape = null) => new LiquidGlassParameters
        {
            Thickness = maxThickness * thickness.Value,
            CornerRadius = cornerRadius,
            Shape = shape,
            Refraction = refraction.Value,
            ChromaticAberration = chromaticAberration.Value,
            Darkness = darkness.Value,
        };

        /// <summary>
        /// Applies a glass style to the backdrop of a container.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="style">The glass style.</param>
        /// <param name="frostedBlurSigma">The blur sigma while <paramref name="style"/> is <see cref="GlassStyle.Frosted"/>.</param>
        /// <param name="maxThickness">See <see cref="CreateParameters"/>.</param>
        /// <param name="cornerRadius">See <see cref="CreateParameters"/>.</param>
        /// <param name="shape">See <see cref="CreateParameters"/>.</param>
        public void ApplyTo(BackdropBlurContainer container, GlassStyle style, float frostedBlurSigma, float maxThickness, float cornerRadius = 0, RectangleF? shape = null)
        {
            // a zero blur without liquid glass skips the backdrop entirely, so it costs nothing in the standard style.
            container.BlurSigma = style switch
            {
                GlassStyle.Frosted => new Vector2(frostedBlurSigma),
                GlassStyle.LiquidGlass => BlurSigma,
                _ => Vector2.Zero,
            };

            container.LiquidGlass = style == GlassStyle.LiquidGlass ? CreateParameters(maxThickness, cornerRadius, shape) : default;
        }
    }
}
