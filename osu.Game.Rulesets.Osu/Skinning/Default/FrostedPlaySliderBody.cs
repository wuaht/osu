// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    /// <summary>
    /// A <see cref="PlaySliderBody"/> which supports frosted slider bodies.
    /// The body accent colour is re-evaluated whenever the frosted slider settings change,
    /// such that derived classes can lower the opacity of the body to let the blurred backdrop show through.
    /// </summary>
    /// <remarks>
    /// The path should derive from <see cref="FrostedDrawableSliderPath"/>, which handles the blurring itself.
    /// </remarks>
    public abstract partial class FrostedPlaySliderBody : PlaySliderBody
    {
        /// <summary>
        /// Whether slider bodies are frosted.
        /// </summary>
        protected readonly Bindable<bool> FrostedSliders = new Bindable<bool>();

        /// <summary>
        /// How frosted slider bodies are, from 0 to 1.
        /// </summary>
        protected readonly BindableFloat Frostiness = new BindableFloat();

        /// <summary>
        /// Whether frosted slider bodies are always coloured with the combo colour, ignoring the slider track colour of the skin.
        /// </summary>
        protected readonly Bindable<bool> AlwaysColoured = new Bindable<bool>();

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(ISkinSource skin, OsuConfigManager? config)
        {
            config?.BindWith(OsuSetting.SlopFrostedSliders, FrostedSliders);
            config?.BindWith(OsuSetting.SlopFrostedSlidersFrostiness, Frostiness);
            config?.BindWith(OsuSetting.SlopFrostedSlidersAlwaysColoured, AlwaysColoured);

            FrostedSliders.BindValueChanged(_ => updateAccentColour(skin));
            Frostiness.BindValueChanged(_ => updateAccentColour(skin));
            AlwaysColoured.BindValueChanged(_ => updateAccentColour(skin));

            updateAccentColour(skin);
        }

        private void updateAccentColour(ISkinSource skin) => AccentColour = GetBodyAccentColour(skin, AccentColourBindable.Value);
    }
}
