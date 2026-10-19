// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class CustomColoursSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.CustomColoursHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "colour", "color", "strain", "graph", "mania", "snap", "input", "key", "overlay", "counter" });

        private OsuConfigManager config = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            this.config = config;

            Children = new Drawable[]
            {
                colour(SlopSettingsStrings.StrainGraphLowColour, OsuSetting.SlopStrainGraphLowColour),
                colour(SlopSettingsStrings.StrainGraphHighColour, OsuSetting.SlopStrainGraphHighColour),
                alpha(SlopSettingsStrings.StrainGraphActiveAlpha, OsuSetting.SlopStrainGraphActiveAlpha),
                alpha(SlopSettingsStrings.StrainGraphInactiveAlpha, OsuSetting.SlopStrainGraphInactiveAlpha),
                colour(SlopSettingsStrings.ManiaSnapColour2, OsuSetting.SlopManiaSnapColour2, SlopSettingsStrings.ManiaSnapColourDescription),
                colour(SlopSettingsStrings.ManiaSnapColour3, OsuSetting.SlopManiaSnapColour3, SlopSettingsStrings.ManiaSnapColourDescription),
                colour(SlopSettingsStrings.ManiaSnapColour4, OsuSetting.SlopManiaSnapColour4, SlopSettingsStrings.ManiaSnapColourDescription),
                colour(SlopSettingsStrings.ManiaSnapColour6, OsuSetting.SlopManiaSnapColour6, SlopSettingsStrings.ManiaSnapColourDescription),
                colour(SlopSettingsStrings.ManiaSnapColour8, OsuSetting.SlopManiaSnapColour8, SlopSettingsStrings.ManiaSnapColourDescription),
                colour(SlopSettingsStrings.ManiaSnapColour12, OsuSetting.SlopManiaSnapColour12, SlopSettingsStrings.ManiaSnapColourDescription),
                colour(SlopSettingsStrings.ManiaSnapColour16, OsuSetting.SlopManiaSnapColour16, SlopSettingsStrings.ManiaSnapColourDescription),
                colour(SlopSettingsStrings.ManiaSnapColourOther, OsuSetting.SlopManiaSnapColourOther, SlopSettingsStrings.ManiaSnapColourDescription),
                colour(SlopSettingsStrings.KeyOverlayColourK1, OsuSetting.SlopKeyOverlayColourK1),
                colour(SlopSettingsStrings.KeyOverlayColourK2, OsuSetting.SlopKeyOverlayColourK2),
                colour(SlopSettingsStrings.KeyOverlayColourM1, OsuSetting.SlopKeyOverlayColourM1),
                colour(SlopSettingsStrings.KeyOverlayColourM2, OsuSetting.SlopKeyOverlayColourM2),
            };
        }

        private SettingsItemV2 colour(LocalisableString caption, OsuSetting setting, LocalisableString hintText = default) => new SettingsItemV2(new FormColourPicker
        {
            Caption = caption,
            HintText = hintText,
            Current = config.GetBindable<string>(setting),
        });

        private SettingsItemV2 alpha(LocalisableString caption, OsuSetting setting) => new SettingsItemV2(new FormSliderBar<int>
        {
            Caption = caption,
            Current = config.GetBindable<int>(setting),
            KeyboardStep = 1,
            LabelFormat = v => $@"{v}%",
        });
    }
}
