// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public abstract partial class PlaySliderBody : SnakingSliderBody
    {
        protected IBindable<float> ScaleBindable { get; private set; } = null!;

        protected IBindable<Color4> AccentColourBindable { get; private set; } = null!;

        private IBindable<int> pathVersion = null!;

        [Resolved(CanBeNull = true)]
        private OsuRulesetConfigManager? config { get; set; }

        private readonly Bindable<bool> configSnakingOut = new Bindable<bool>();

        [Resolved(CanBeNull = true)]
        private OsuConfigManager? gameConfig { get; set; }

        private readonly Bindable<bool> borderComboColour = new Bindable<bool>();

        private readonly Bindable<bool> hideSliderBodies = new Bindable<bool>();

        private ISkinSource skin = null!;

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, DrawableHitObject drawableObject)
        {
            this.skin = skin;

            var drawableSlider = (DrawableSlider)drawableObject;

            ScaleBindable = drawableSlider.ScaleBindable.GetBoundCopy();
            ScaleBindable.BindValueChanged(scale => PathRadius = OsuHitObject.OBJECT_RADIUS * scale.NewValue, true);

            pathVersion = drawableSlider.PathVersion.GetBoundCopy();
            pathVersion.BindValueChanged(_ => Scheduler.AddOnce(Refresh));

            AccentColourBindable = drawableObject.AccentColour.GetBoundCopy();
            AccentColourBindable.BindValueChanged(accent => AccentColour = GetBodyAccentColour(skin, accent.NewValue), true);

            config?.BindWith(OsuRulesetSetting.SnakingInSliders, SnakingIn);
            config?.BindWith(OsuRulesetSetting.SnakingOutSliders, configSnakingOut);

            SnakingOut.BindTo(configSnakingOut);

            gameConfig?.BindWith(OsuSetting.SlopSliderBorderComboColour, borderComboColour);

            AccentColourBindable.BindValueChanged(_ => updateBorderColour());
            borderComboColour.BindValueChanged(_ => updateBorderColour(), true);

            gameConfig?.BindWith(OsuSetting.SlopHideSliderBodies, hideSliderBodies);
            hideSliderBodies.BindValueChanged(_ => updatePathVisibility(), true);
        }

        public override void RecyclePath()
        {
            base.RecyclePath();
            updatePathVisibility();
        }

        private void updatePathVisibility() => Path.Alpha = hideSliderBodies.Value ? 0 : 1;

        private void updateBorderColour()
        {
            // same as argon: the border takes the combo colour as is.
            BorderColour = borderComboColour.Value
                ? AccentColourBindable.Value
                : GetBorderColour(skin);
        }

        // the slider colours of a beatmap override the skin (like osu!stable), non-legacy skins don't provide any themselves.
        protected virtual Color4 GetBorderColour(ISkinSource skin) => GetSkinBorderColour() ?? Color4.White;

        /// <summary>
        /// The slider border colour of the beatmap or skin, if any.
        /// </summary>
        protected Color4? GetSkinBorderColour() => skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderBorder)?.Value;

        protected virtual Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour) => GetSkinTrackColour() ?? hitObjectAccentColour;

        /// <summary>
        /// The slider track colour of the beatmap or skin, if any.
        /// </summary>
        protected Color4? GetSkinTrackColour() => skin.GetConfig<OsuSkinColour, Color4>(OsuSkinColour.SliderTrackOverride)?.Value;
    }
}
