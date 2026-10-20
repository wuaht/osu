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
    public partial class AutoplaySettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.AutoplayHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "auto", "autoplay", "auto+", "pippi", "cursor", "dance", "interpolation", "tag", "replay" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<SlopAutoplayEngine>
                {
                    Caption = SlopSettingsStrings.AutoplayEngine,
                    HintText = SlopSettingsStrings.AutoplayEngineDescription,
                    Current = config.GetBindable<SlopAutoplayEngine>(OsuSetting.SlopAutoplayEngine),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.SaveAutoplayReplays,
                    Current = config.GetBindable<bool>(OsuSetting.SlopSaveAutoplayReplays),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusFollowSliders,
                    HintText = SlopSettingsStrings.AutoPlusFollowSlidersDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusFollowSliders),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusFollowSliderEnds,
                    HintText = SlopSettingsStrings.AutoPlusFollowSliderEndsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusFollowSliderEnds),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusRotation,
                    HintText = SlopSettingsStrings.AutoPlusRotationDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusRotation),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusBounceBorder,
                    HintText = SlopSettingsStrings.AutoPlusBounceBorderDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusBounceBorder),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusAlternateButtons,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusAlternateButtons),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusCubicSplineInterpolation,
                    HintText = SlopSettingsStrings.AutoPlusCubicSplineInterpolationDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusCubicSplineInterpolation),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusHalfCircleInterpolation,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusHalfCircleInterpolation),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusHalfCircleStreamInterpolation,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusHalfCircleStreamInterpolation),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusAngularStreamInterpolation,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusAngularStreamInterpolation),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = SlopSettingsStrings.AutoPlusInterpolationFrequency,
                    Current = config.GetBindable<double>(OsuSetting.SlopAutoPlusInterpolationFrequency),
                    KeyboardStep = 10,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = SlopSettingsStrings.AutoPlusSliderSpeedFactor,
                    Current = config.GetBindable<double>(OsuSetting.SlopAutoPlusSliderSpeedFactor),
                    KeyboardStep = 0.1f,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = SlopSettingsStrings.AutoPlusStreamSpeedFactor,
                    Current = config.GetBindable<double>(OsuSetting.SlopAutoPlusStreamSpeedFactor),
                    KeyboardStep = 0.1f,
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = SlopSettingsStrings.AutoPlusStreamHalfCircleBorderAngle,
                    Current = config.GetBindable<double>(OsuSetting.SlopAutoPlusStreamHalfCircleBorderAngle),
                    KeyboardStep = 1,
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusZeroInterpolation,
                    HintText = SlopSettingsStrings.AutoPlusZeroInterpolationDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusZeroInterpolation),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoPlusTag,
                    HintText = SlopSettingsStrings.AutoPlusTagDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAutoPlusTag),
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = SlopSettingsStrings.AutoPlusTagCount,
                    HintText = SlopSettingsStrings.AutoPlusTagCountDescription,
                    Current = config.GetBindable<int>(OsuSetting.SlopAutoPlusTagCount),
                    KeyboardStep = 1,
                }),
                new SettingsItemV2(new FormEnumDropdown<AutoPlusTagSplitMode>
                {
                    Caption = SlopSettingsStrings.AutoPlusTagSplit,
                    Current = config.GetBindable<AutoPlusTagSplitMode>(OsuSetting.SlopAutoPlusTagSplit),
                }),
                new SettingsItemV2(new FormEnumDropdown<AutoPlusTagColourSource>
                {
                    Caption = SlopSettingsStrings.AutoPlusTagColours,
                    Current = config.GetBindable<AutoPlusTagColourSource>(OsuSetting.SlopAutoPlusTagColours),
                }),
            };
        }
    }
}
