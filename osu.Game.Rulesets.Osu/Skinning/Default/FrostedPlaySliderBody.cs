// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    /// <summary>
    /// A <see cref="PlaySliderBody"/> which supports glass slider bodies.
    /// The body accent colour is re-evaluated whenever the glass settings change,
    /// such that derived classes can lower the opacity of the body to let the backdrop show through.
    /// </summary>
    /// <remarks>
    /// The path should derive from <see cref="FrostedDrawableSliderPath"/>, which handles the backdrop itself.
    /// </remarks>
    public abstract partial class FrostedPlaySliderBody : PlaySliderBody
    {
        /// <summary>
        /// How transparent liquid glass slider bodies are, from 0 to 1 (see <see cref="Transparency"/>).
        /// </summary>
        private const float liquid_glass_transparency = 0.8f;

        /// <summary>
        /// The glass style of slider bodies.
        /// </summary>
        protected readonly Bindable<GlassStyle> GlassStyle = new Bindable<GlassStyle>();

        /// <summary>
        /// How frosted slider bodies are, from 0 to 1.
        /// </summary>
        private readonly BindableFloat frostiness = new BindableFloat();

        /// <summary>
        /// Whether glass slider bodies are always coloured with the combo colour, ignoring the slider track colour of the skin.
        /// </summary>
        protected readonly Bindable<bool> AlwaysColoured = new Bindable<bool>();

        /// <summary>
        /// Whether slider bodies show the content behind them in any way.
        /// </summary>
        protected bool IsGlass => GlassStyle.Value != osu.Game.Configuration.GlassStyle.Standard;

        /// <summary>
        /// How much more transparent than usual slider bodies are to let the backdrop show through, from 0 to 1.
        /// </summary>
        protected float Transparency => GlassStyle.Value switch
        {
            osu.Game.Configuration.GlassStyle.Frosted => frostiness.Value,
            osu.Game.Configuration.GlassStyle.LiquidGlass => liquid_glass_transparency,
            _ => 0,
        };

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(ISkinSource skin, OsuConfigManager? config)
        {
            config?.BindWith(OsuSetting.SlopSliderGlassStyle, GlassStyle);
            config?.BindWith(OsuSetting.SlopFrostedSlidersFrostiness, frostiness);
            config?.BindWith(OsuSetting.SlopFrostedSlidersAlwaysColoured, AlwaysColoured);

            GlassStyle.BindValueChanged(_ => updateAccentColour(skin));
            frostiness.BindValueChanged(_ => updateAccentColour(skin));
            AlwaysColoured.BindValueChanged(_ => updateAccentColour(skin));

            updateAccentColour(skin);
        }

        private void updateAccentColour(ISkinSource skin) => AccentColour = GetBodyAccentColour(skin, AccentColourBindable.Value);
    }
}
