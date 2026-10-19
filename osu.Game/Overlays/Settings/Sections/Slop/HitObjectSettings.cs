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
    public partial class HitObjectSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.HitObjectsHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "300", "judgement", "slider", "combo", "number", "follow point", "approach" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowGreatJudgements,
                    Current = config.GetBindable<bool>(OsuSetting.SlopShowGreatJudgements),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowSliderEnds,
                    HintText = SlopSettingsStrings.ShowSliderEndsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopShowSliderEnds),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowComboNumbers,
                    Current = config.GetBindable<bool>(OsuSetting.SlopShowComboNumbers),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.HideFollowPoints,
                    HintText = SlopSettingsStrings.HideFollowPointsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopHideFollowPoints),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.HideSliderBodies,
                    HintText = SlopSettingsStrings.HideSliderBodiesDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopHideSliderBodies),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.WhiteApproachCircles,
                    HintText = SlopSettingsStrings.WhiteApproachCirclesDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopWhiteApproachCircles),
                }),
            };
        }
    }
}
