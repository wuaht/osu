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
    public partial class EditorHitsoundSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.EditorHitsoundsHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "hitsound", "frosted", "samples", "lanes", "toolbox" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoApplyHitsoundDifficulty,
                    HintText = SlopSettingsStrings.AutoApplyHitsoundDifficultyDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AlwaysIgnoreBeatmapSamples,
                    HintText = SlopSettingsStrings.AlwaysIgnoreBeatmapSamplesDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAlwaysIgnoreBeatmapSamples),
                }),
                new SettingsItemV2(new FormEnumDropdown<GlassStyle>
                {
                    Caption = SlopSettingsStrings.HitsoundLanesGlassStyle,
                    HintText = SlopSettingsStrings.HitsoundLanesGlassStyleDescription,
                    Current = config.GetBindable<GlassStyle>(OsuSetting.SlopHitsoundEditorLanesGlassStyle),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.HitsoundToolboxExpandOnHover,
                    HintText = SlopSettingsStrings.HitsoundToolboxExpandOnHoverDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopHitsoundEditorToolboxExpandOnHover),
                }),
            };
        }
    }
}
