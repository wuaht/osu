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
using osu.Game.Overlays.Visualizer;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class VisualizerSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.VisualizerHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "music", "player", "spectrum", "trap nation", "ncs", "logo" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<VisualizerStyle>
                {
                    Caption = SlopSettingsStrings.VisualizerStyle,
                    HintText = SlopSettingsStrings.VisualizerStyleDescription,
                    Current = config.GetBindable<VisualizerStyle>(OsuSetting.SlopVisualizerStyle),
                }),
                new SettingsItemV2(new FormEnumDropdown<TrapNationLogo>
                {
                    Caption = SlopSettingsStrings.TrapNationLogo,
                    HintText = SlopSettingsStrings.TrapNationLogoDescription,
                    Current = config.GetBindable<TrapNationLogo>(OsuSetting.SlopVisualizerTrapNationLogo),
                }),
                new SettingsItemV2(new FormEnumDropdown<NcsTitle>
                {
                    Caption = SlopSettingsStrings.NcsTitle,
                    HintText = SlopSettingsStrings.NcsTitleDescription,
                    Current = config.GetBindable<NcsTitle>(OsuSetting.SlopVisualizerNcsTitle),
                }),
            };
        }
    }
}
