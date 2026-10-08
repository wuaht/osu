// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    /// <summary>
    /// A <see cref="DrawableSliderPath"/> which blurs the content behind it while frosted slider bodies are enabled.
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

        private readonly Bindable<bool> frostedSliders = new Bindable<bool>();
        private readonly BindableFloat blur = new BindableFloat();

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(OsuConfigManager? config)
        {
            config?.BindWith(OsuSetting.SlopFrostedSliders, frostedSliders);
            config?.BindWith(OsuSetting.SlopFrostedSlidersBlur, blur);

            frostedSliders.BindValueChanged(_ => updateBlur());
            blur.BindValueChanged(_ => updateBlur());

            updateBlur();
        }

        private void updateBlur()
        {
            BlurSigma = frostedSliders.Value
                ? new Vector2(min_blur_sigma + (max_blur_sigma - min_blur_sigma) * blur.Value)
                : Vector2.Zero;
        }
    }
}
