// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class UserInterfaceSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.UserInterfaceHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "intro", "startup", "menu", "navigation", "transition", "fade", "instant", "logo", "miku" });

        private Bindable<bool> mikuMedalUnlocked = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            SettingsItemV2 mikuTakeoverItem;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.SkipIntro,
                    HintText = SlopSettingsStrings.SkipIntroDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopSkipIntro),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.InstantScreenTransitions,
                    HintText = SlopSettingsStrings.InstantScreenTransitionsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopInstantScreenTransitions),
                }),
                mikuTakeoverItem = new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MikuTakeover,
                    HintText = SlopSettingsStrings.MikuTakeoverDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMikuTakeover),
                }),
            };

            // the toggle is an easter egg, which only shows up once its medal has been unlocked.
            mikuMedalUnlocked = config.GetBindable<bool>(OsuSetting.SlopMikuMedalUnlocked);
            mikuMedalUnlocked.BindValueChanged(e => mikuTakeoverItem.CanBeShown.Value = e.NewValue, true);
        }
    }
}
