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
    public partial class EditorSavingSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.EditorSavingHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "backup", "autosave", "own", "alias" });

        private Bindable<int> autosaveInterval = null!;

        private SettingsItemV2 onlyOwnMapsItem = null!;

        private SettingsItemV2 toBackupsItem = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = SlopSettingsStrings.AutosaveInterval,
                    HintText = SlopSettingsStrings.AutosaveIntervalDescription,
                    Current = autosaveInterval = config.GetBindable<int>(OsuSetting.SlopEditorAutosaveInterval),
                    KeyboardStep = 1,
                    LabelFormat = minutes => minutes == 0 ? SlopSettingsStrings.AutosaveOff : SlopSettingsStrings.AutosaveMinutes(minutes),
                }),
                onlyOwnMapsItem = new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.OnlyAutosaveOwnMaps,
                    HintText = SlopSettingsStrings.OnlyAutosaveOwnMapsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorOnlyAutosaveOwnMaps),
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = SlopSettingsStrings.MyMapsAliases,
                    Current = config.GetBindable<string>(OsuSetting.SlopEditorMyMapsAliases),
                }),
                toBackupsItem = new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutosaveToBackups,
                    HintText = SlopSettingsStrings.AutosaveToBackupsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAutosaveToBackups),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.BackupOnSave,
                    HintText = SlopSettingsStrings.BackupOnSaveDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorBackupOnSave),
                }),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Both options only matter while autosave is on; keep them visible but dimmed so they stay discoverable.
            autosaveInterval.BindValueChanged(interval =>
            {
                float alpha = interval.NewValue > 0 ? 1 : 0.5f;
                onlyOwnMapsItem.Alpha = toBackupsItem.Alpha = alpha;
            }, true);
        }
    }
}
