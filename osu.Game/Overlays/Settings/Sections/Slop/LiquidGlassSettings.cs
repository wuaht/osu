// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    /// <summary>
    /// The settings of <see cref="GlassStyle.LiquidGlass"/>, which are only shown while anything uses liquid glass.
    /// </summary>
    public partial class LiquidGlassSettings : SettingsSubsection, IConditionalFilterable
    {
        protected override LocalisableString Header => SlopSettingsStrings.LiquidGlassHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "liquid", "glass", "refraction", "chromatic", "aberration", "tint", "border", "blur" });

        public BindableBool CanBeShown { get; } = new BindableBool();

        IBindable<bool> IConditionalFilterable.CanBeShown => CanBeShown;

        private readonly List<Bindable<GlassStyle>> styles = new List<Bindable<GlassStyle>>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                createSlider(config, OsuSetting.SlopLiquidGlassThickness, SlopSettingsStrings.LiquidGlassThickness, SlopSettingsStrings.LiquidGlassThicknessDescription),
                createSlider(config, OsuSetting.SlopLiquidGlassRefraction, SlopSettingsStrings.LiquidGlassRefraction, SlopSettingsStrings.LiquidGlassRefractionDescription),
                createSlider(config, OsuSetting.SlopLiquidGlassChromaticAberration, SlopSettingsStrings.LiquidGlassChromaticAberration,
                    SlopSettingsStrings.LiquidGlassChromaticAberrationDescription),
                createSlider(config, OsuSetting.SlopLiquidGlassTint, SlopSettingsStrings.LiquidGlassTint, SlopSettingsStrings.LiquidGlassTintDescription),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.LiquidGlassSliderBorder,
                    HintText = SlopSettingsStrings.LiquidGlassSliderBorderDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopLiquidGlassSliderBorder),
                }),
                createSlider(config, OsuSetting.SlopLiquidGlassSliderBorderThickness, SlopSettingsStrings.LiquidGlassSliderBorderThickness,
                    SlopSettingsStrings.LiquidGlassSliderBorderThicknessDescription),
                createSlider(config, OsuSetting.SlopLiquidGlassSliderBodyBrightness, SlopSettingsStrings.LiquidGlassSliderBodyBrightness,
                    SlopSettingsStrings.LiquidGlassSliderBodyBrightnessDescription),
                createSlider(config, OsuSetting.SlopLiquidGlassBlur, SlopSettingsStrings.LiquidGlassBlur, SlopSettingsStrings.LiquidGlassBlurDescription),
                createSlider(config, OsuSetting.SlopLiquidGlassDarkness, SlopSettingsStrings.LiquidGlassDarkness, SlopSettingsStrings.LiquidGlassDarknessDescription),
            };

            foreach (var setting in new[]
                     {
                         OsuSetting.SlopSliderGlassStyle,
                         OsuSetting.SlopSettingsGlassStyle,
                         OsuSetting.SlopHitsoundEditorLanesGlassStyle,
                         OsuSetting.SlopBnTrackerPanelsGlassStyle,
                         OsuSetting.SlopMapsetVerifierPanelsGlassStyle,
                         OsuSetting.SlopDiscordGlassStyle,
                     })
            {
                var style = config.GetBindable<GlassStyle>(setting);
                style.BindValueChanged(_ => updateCanBeShown());
                styles.Add(style);
            }

            updateCanBeShown();
        }

        private void updateCanBeShown() => CanBeShown.Value = styles.Any(s => s.Value == GlassStyle.LiquidGlass);

        private static SettingsItemV2 createSlider(OsuConfigManager config, OsuSetting setting, LocalisableString caption, LocalisableString hintText) => new SettingsItemV2(new FormSliderBar<float>
        {
            Caption = caption,
            HintText = hintText,
            Current = config.GetBindable<float>(setting),
            KeyboardStep = 0.01f,
            DisplayAsPercentage = true,
        });
    }
}
