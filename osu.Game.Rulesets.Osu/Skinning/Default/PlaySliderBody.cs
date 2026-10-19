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
            BorderColour = borderComboColour.Value
                ? getComboBorderColour(AccentColourBindable.Value)
                : GetBorderColour(skin);
        }

        /// <summary>
        /// The amount the combo colour is blended towards white for the border.
        /// The edge of the body uses the combo colour as well, so the border would otherwise barely stand out.
        /// </summary>
        private const float combo_border_whiteness = 0.5f;

        private static Color4 getComboBorderColour(Color4 comboColour) => new Color4(
            comboColour.R + (1 - comboColour.R) * combo_border_whiteness,
            comboColour.G + (1 - comboColour.G) * combo_border_whiteness,
            comboColour.B + (1 - comboColour.B) * combo_border_whiteness,
            1);

        protected virtual Color4 GetBorderColour(ISkinSource skin) => Color4.White;

        protected virtual Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour) => hitObjectAccentColour;
    }
}
