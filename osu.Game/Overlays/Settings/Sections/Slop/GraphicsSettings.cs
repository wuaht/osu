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
    public partial class GraphicsSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.GraphicsHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "frosted", "frostiness", "blur", "slider" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FrostedSliders,
                    HintText = SlopSettingsStrings.FrostedSlidersDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFrostedSliders),
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = SlopSettingsStrings.Frostiness,
                    HintText = SlopSettingsStrings.FrostinessDescription,
                    Current = config.GetBindable<float>(OsuSetting.SlopFrostedSlidersFrostiness),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = SlopSettingsStrings.FrostBlur,
                    HintText = SlopSettingsStrings.FrostBlurDescription,
                    Current = config.GetBindable<float>(OsuSetting.SlopFrostedSlidersBlur),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                }),
            };
        }
    }
}
