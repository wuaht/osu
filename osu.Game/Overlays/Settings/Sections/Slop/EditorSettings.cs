// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class EditorSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.EditorHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "skin", "backup", "autosave", "snap", "blanket", "triangle", "waveform" });

        /// <summary>
        /// Dropdown entry representing "use the regular gameplay skin".
        /// </summary>
        private static readonly Live<SkinInfo> same_as_gameplay_skin = new SkinInfo
        {
            ID = Guid.Empty,
            Name = "Same as gameplay skin",
        }.ToLiveUnmanaged();

        [Resolved]
        private SkinManager skins { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly Bindable<Live<SkinInfo>> selectedSkin = new Bindable<Live<SkinInfo>>(same_as_gameplay_skin);

        private Bindable<string> editorSkinSetting = null!;

        private EditorSkinDropdown skinDropdown = null!;

        private IDisposable? realmSubscription;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            editorSkinSetting = config.GetBindable<string>(OsuSetting.SlopEditorSkin);

            Children = new Drawable[]
            {
                new SettingsItemV2(skinDropdown = new EditorSkinDropdown
                {
                    AlwaysShowSearchBar = true,
                    AllowNonContiguousMatching = true,
                    Caption = SlopSettingsStrings.EditorSkin,
                    HintText = SlopSettingsStrings.EditorSkinDescription,
                    Current = selectedSkin,
                    Items = new[] { same_as_gameplay_skin },
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorSkinInTestMode,
                    HintText = SlopSettingsStrings.EditorSkinInTestModeDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorSkinInTestMode),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowSelectionBox,
                    HintText = SlopSettingsStrings.ShowSelectionBoxDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowSelectionBox),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowSelectionBoxButtons,
                    HintText = SlopSettingsStrings.ShowSelectionBoxButtonsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowSelectionBoxButtons),
                }),
                new SettingsItemV2(new FormEnumDropdown<EditorAnchorShape>
                {
                    Caption = SlopSettingsStrings.AnchorShape,
                    HintText = SlopSettingsStrings.AnchorShapeDescription,
                    Current = config.GetBindable<EditorAnchorShape>(OsuSetting.SlopEditorAnchorShape),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowSliderEndDragMarker,
                    HintText = SlopSettingsStrings.ShowSliderEndDragMarkerDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowSliderEndDragMarker),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ImmediateDrag,
                    HintText = SlopSettingsStrings.ImmediateDragDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorImmediateDrag),
                }),
                new SettingsItemV2(new FormEnumDropdown<EditorWaveformStyle>
                {
                    Caption = SlopSettingsStrings.WaveformStyle,
                    HintText = SlopSettingsStrings.WaveformStyleDescription,
                    Current = config.GetBindable<EditorWaveformStyle>(OsuSetting.SlopEditorWaveformStyle),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.VisualSpacingSnap,
                    HintText = SlopSettingsStrings.VisualSpacingSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorVisualSpacingSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.BlanketSnap,
                    HintText = SlopSettingsStrings.BlanketSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorBlanketSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.LineSnap,
                    HintText = SlopSettingsStrings.LineSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorLineSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowDifficultyStrains,
                    HintText = SlopSettingsStrings.ShowDifficultyStrainsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowDifficultyStrains),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.LoopMusic,
                    HintText = SlopSettingsStrings.LoopMusicDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorLoopMusic),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.BackupOnSave,
                    HintText = SlopSettingsStrings.BackupOnSaveDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorBackupOnSave),
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = SlopSettingsStrings.AutosaveInterval,
                    HintText = SlopSettingsStrings.AutosaveIntervalDescription,
                    Current = config.GetBindable<int>(OsuSetting.SlopEditorAutosaveInterval),
                    KeyboardStep = 1,
                    LabelFormat = minutes => minutes == 0 ? SlopSettingsStrings.AutosaveOff : SlopSettingsStrings.AutosaveMinutes(minutes),
                }),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            realmSubscription = realm.RegisterForNotifications(r => r.All<SkinInfo>()
                                                                     .Where(s => !s.DeletePending)
                                                                     .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase), skinsChanged);

            selectedSkin.BindValueChanged(skin =>
            {
                editorSkinSetting.Value = skin.NewValue.ID == Guid.Empty ? string.Empty : skin.NewValue.ID.ToString();
            });
        }

        private void skinsChanged(IRealmCollection<SkinInfo> sender, ChangeSet? changes)
        {
            // This can only mean that realm is recycling, else we would see the protected skins.
            if (!sender.Any())
                return;

            var items = new List<Live<SkinInfo>> { same_as_gameplay_skin };
            items.AddRange(skins.GetAllUsableSkins().Where(s => s.ID != SkinInfo.RANDOM_SKIN));

            Schedule(() =>
            {
                skinDropdown.Items = items;

                // A skin which no longer exists falls back to the gameplay skin (see EditorSkinSource), so reflect that here.
                selectedSkin.Value = Guid.TryParse(editorSkinSetting.Value, out var id)
                    ? items.FirstOrDefault(s => s.ID == id) ?? same_as_gameplay_skin
                    : same_as_gameplay_skin;
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            realmSubscription?.Dispose();
        }

        private partial class EditorSkinDropdown : FormDropdown<Live<SkinInfo>>
        {
            protected override LocalisableString GenerateItemText(Live<SkinInfo> item)
                => item.ID == Guid.Empty ? SlopSettingsStrings.SameAsGameplaySkin : item.ToString() ?? string.Empty;
        }
    }
}
