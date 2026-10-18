// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.ColourEffects;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class ColourEffectSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.ColourEffectsHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "rainbow", "gradient", "chroma", "saturation", "colour", "color", "cursor", "trail", "combo" });

        private readonly List<Bindable<bool>> enabledBindables = new List<Bindable<bool>>();
        private readonly List<Bindable<ColourEffectMode>> modeBindables = new List<Bindable<ColourEffectMode>>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var children = new List<Drawable>();

            children.AddRange(createTargetItems(config, SlopSettingsStrings.CursorColourEffect, SlopSettingsStrings.CursorColourEffectDescription,
                OsuSetting.SlopCursorColourEffect, OsuSetting.SlopCursorColourEffectMode, OsuSetting.SlopCursorColourEffectGradient));
            children.AddRange(createTargetItems(config, SlopSettingsStrings.CursorTrailColourEffect, SlopSettingsStrings.CursorTrailColourEffectDescription,
                OsuSetting.SlopCursorTrailColourEffect, OsuSetting.SlopCursorTrailColourEffectMode, OsuSetting.SlopCursorTrailColourEffectGradient));
            children.AddRange(createTargetItems(config, SlopSettingsStrings.ComboColourEffect, SlopSettingsStrings.ComboColourEffectDescription,
                OsuSetting.SlopComboColourEffect, OsuSetting.SlopComboColourEffectMode, OsuSetting.SlopComboColourEffectGradient));

            children.Add(new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = SlopSettingsStrings.ColourEffectSpeed,
                HintText = SlopSettingsStrings.ColourEffectSpeedDescription,
                Current = config.GetBindable<float>(OsuSetting.SlopColourEffectSpeed),
                KeyboardStep = 0.05f,
                DisplayAsPercentage = true,
            }));

            children.Add(new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = SlopSettingsStrings.ColourEffectRainbowSaturation,
                HintText = SlopSettingsStrings.ColourEffectRainbowSaturationDescription,
                Current = config.GetBindable<float>(OsuSetting.SlopColourEffectRainbowSaturation),
                KeyboardStep = 0.01f,
                DisplayAsPercentage = true,
            }));

            Children = children;
        }

        private IEnumerable<Drawable> createTargetItems(OsuConfigManager config, LocalisableString caption, LocalisableString hint, OsuSetting enabledSetting, OsuSetting modeSetting, OsuSetting gradientSetting)
        {
            var enabled = config.GetBindable<bool>(enabledSetting);
            var mode = config.GetBindable<ColourEffectMode>(modeSetting);

            // keep references, so that the visibility bindings below stay alive.
            enabledBindables.Add(enabled);
            modeBindables.Add(mode);

            var gradientPicker = new FormGradientPicker
            {
                Caption = SlopSettingsStrings.ColourEffectGradient,
                HintText = SlopSettingsStrings.ColourEffectGradientDescription,
                Current = config.GetBindable<string>(gradientSetting),
            };
            gradientPicker.Mode.BindTo(mode);

            var modeItem = new SettingsItemV2(new FormEnumDropdown<ColourEffectMode>
            {
                Caption = SlopSettingsStrings.ColourEffectMode,
                Current = mode,
            });

            var gradientItem = new SettingsItemV2(gradientPicker);

            enabled.BindValueChanged(e => modeItem.CanBeShown.Value = e.NewValue, true);

            void updateGradientVisibility() => gradientItem.CanBeShown.Value = enabled.Value && mode.Value == ColourEffectMode.Gradient;

            enabled.BindValueChanged(_ => updateGradientVisibility());
            mode.BindValueChanged(_ => updateGradientVisibility(), true);

            return new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = caption,
                    HintText = hint,
                    Current = enabled,
                }),
                modeItem,
                gradientItem,
            };
        }
    }
}
