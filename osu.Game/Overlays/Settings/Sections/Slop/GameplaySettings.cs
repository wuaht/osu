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
    public partial class GameplaySettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.GameplayHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "skip", "break", "fail", "multiplayer", "hitsound", "volume", "mute", "flashlight", "mania", "judgement", "counter" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.SkipBreaks,
                    HintText = SlopSettingsStrings.SkipBreaksDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopSkipBreaks),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.KillOnFail,
                    HintText = SlopSettingsStrings.KillOnFailDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopKillOnFail),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DisableSoftFailEffects,
                    HintText = SlopSettingsStrings.DisableSoftFailEffectsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopDisableSoftFailEffects),
                }),
                new SettingsItemV2(new FormEnumDropdown<IgnoreBeatmapVolumeMode>
                {
                    Caption = SlopSettingsStrings.IgnoreBeatmapVolume,
                    HintText = SlopSettingsStrings.IgnoreBeatmapVolumeDescription,
                    Current = config.GetBindable<IgnoreBeatmapVolumeMode>(OsuSetting.SlopIgnoreBeatmapVolume),
                }),
                new SettingsItemV2(new FormEnumDropdown<MuteHitsoundsMode>
                {
                    Caption = SlopSettingsStrings.MuteHitsounds,
                    Current = config.GetBindable<MuteHitsoundsMode>(OsuSetting.SlopMuteHitsounds),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FlashlightMinSize,
                    HintText = SlopSettingsStrings.FlashlightMinSizeDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFlashlightMinSize),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FlashlightIgnoreComboBreaks,
                    HintText = SlopSettingsStrings.FlashlightIgnoreComboBreaksDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFlashlightIgnoreComboBreaks),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.InstantComboCounter,
                    HintText = SlopSettingsStrings.InstantComboCounterDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopInstantComboCounter),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.InstantScoreDisplay,
                    HintText = SlopSettingsStrings.InstantScoreDisplayDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopInstantScoreDisplay),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FastBackgroundDim,
                    HintText = SlopSettingsStrings.FastBackgroundDimDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopFastBackgroundDim),
                }),
                new SettingsItemV2(new FormEnumDropdown<ManiaJudgementCounterPosition>
                {
                    Caption = SlopSettingsStrings.ManiaJudgementCounter,
                    Current = config.GetBindable<ManiaJudgementCounterPosition>(OsuSetting.SlopManiaJudgementCounter),
                }),
            };
        }
    }
}
