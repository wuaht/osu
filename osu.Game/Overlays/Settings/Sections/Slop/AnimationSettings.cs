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
    public partial class AnimationSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.AnimationsHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "kiai", "instafade", "fade", "overlay", "follow circle", "reverse arrow", "cursor", "expand" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DisableKiaiEffects,
                    HintText = SlopSettingsStrings.DisableKiaiEffectsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopDisableKiaiEffects),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AnimateHitCircleOverlay,
                    HintText = SlopSettingsStrings.AnimateHitCircleOverlayDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopAnimateHitCircleOverlay),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.InstafadeSliders,
                    Current = config.GetBindable<bool>(OsuSetting.SlopInstafadeSliders),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DisableFollowCircleAnimations,
                    HintText = SlopSettingsStrings.DisableFollowCircleAnimationsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopDisableFollowCircleAnimations),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DisableReverseArrowAnimations,
                    HintText = SlopSettingsStrings.DisableReverseArrowAnimationsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopDisableReverseArrowAnimations),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.InstafadeNumbers,
                    Current = config.GetBindable<bool>(OsuSetting.SlopInstafadeNumbers),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ForceDisableCursorExpand,
                    HintText = SlopSettingsStrings.ForceDisableCursorExpandDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopForceDisableCursorExpand),
                }),
            };
        }
    }
}
