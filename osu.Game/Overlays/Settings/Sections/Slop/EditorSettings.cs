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

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "skin", "backup", "autosave", "snap", "blanket", "triangle", "waveform", "hitsound", "frosted", "bn", "nominator", "request", "alias", "break", "own", "border", "fps", "star", "difficulty", "bezier", "curve", "colour", "color", "alt", "pitch", "speed", "stretch", "mods", "preview", "mania", "playfield", "text editor", "notepad", "mute", "c999", "zoom", "grid", "rotation", "origin" });

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

        private readonly Bindable<Live<SkinInfo>> selectedCursorSkin = new Bindable<Live<SkinInfo>>(same_as_gameplay_skin);

        private Bindable<string> cursorSkinSetting = null!;

        private EditorSkinDropdown cursorSkinDropdown = null!;

        private IDisposable? realmSubscription;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            editorSkinSetting = config.GetBindable<string>(OsuSetting.SlopEditorSkin);
            cursorSkinSetting = config.GetBindable<string>(OsuSetting.SlopEditorAutoplayCursorSkin);

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
                    Caption = SlopSettingsStrings.FrostedHitsoundLanes,
                    HintText = SlopSettingsStrings.FrostedHitsoundLanesDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopHitsoundEditorFrostedLanes),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.HitsoundToolboxExpandOnHover,
                    HintText = SlopSettingsStrings.HitsoundToolboxExpandOnHoverDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopHitsoundEditorToolboxExpandOnHover),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutoApplyHitsoundDifficulty,
                    HintText = SlopSettingsStrings.AutoApplyHitsoundDifficultyDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.FrostedBnTrackerPanels,
                    HintText = SlopSettingsStrings.FrostedBnTrackerPanelsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopBnTrackerFrostedPanels),
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
                    Caption = SlopSettingsStrings.SliderBlanketSnap,
                    HintText = SlopSettingsStrings.SliderBlanketSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorSliderBlanketSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.RotateAroundObjectStarts,
                    HintText = SlopSettingsStrings.RotateAroundObjectStartsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorRotateAroundObjectStarts),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.TimelineSliderVelocityWithAlt,
                    HintText = SlopSettingsStrings.TimelineSliderVelocityWithAltDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorTimelineSliderVelocityWithAlt),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowOffscreenObjects,
                    HintText = SlopSettingsStrings.ShowOffscreenObjectsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowOffscreenObjects),
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
                    Caption = SlopSettingsStrings.MutedEndsSoftC999,
                    HintText = SlopSettingsStrings.MutedEndsSoftC999Description,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorMutedEndsSoftC999),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ZoomOutPlayfield,
                    HintText = SlopSettingsStrings.ZoomOutPlayfieldDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorZoomOut),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowGrid,
                    HintText = SlopSettingsStrings.ShowGridDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowGrid),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.RotateAroundSelection,
                    HintText = SlopSettingsStrings.RotateAroundSelectionDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorRotateAroundSelection),
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
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.OnlyAutosaveOwnMaps,
                    HintText = SlopSettingsStrings.OnlyAutosaveOwnMapsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorOnlyAutosaveOwnMaps),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AutosaveToBackups,
                    HintText = SlopSettingsStrings.AutosaveToBackupsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAutosaveToBackups),
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = SlopSettingsStrings.MyMapsAliases,
                    Current = config.GetBindable<string>(OsuSetting.SlopEditorMyMapsAliases),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.NoAutomaticBreaks,
                    HintText = SlopSettingsStrings.NoAutomaticBreaksDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorNoAutomaticBreaks),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DrawScreenBorders,
                    HintText = SlopSettingsStrings.DrawScreenBordersDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorDrawScreenBorders),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AlwaysHideFps,
                    HintText = SlopSettingsStrings.AlwaysHideFpsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAlwaysHideFps),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DifficultyStars,
                    HintText = SlopSettingsStrings.DifficultyStarsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorDifficultyStars),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AlwaysIgnoreBeatmapSamples,
                    HintText = SlopSettingsStrings.AlwaysIgnoreBeatmapSamplesDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAlwaysIgnoreBeatmapSamples),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.PointInsertPreview,
                    HintText = SlopSettingsStrings.PointInsertPreviewDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorPointInsertPreview),
                }),
                new SettingsItemV2(new FormEnumDropdown<SliderCurveColourStyle>
                {
                    Caption = SlopSettingsStrings.SliderCurveColour,
                    Current = config.GetBindable<SliderCurveColourStyle>(OsuSetting.SlopEditorSliderCurveColour),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DisableAltDistanceSnap,
                    HintText = SlopSettingsStrings.DisableAltDistanceSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorDisableAltDistanceSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.SliderPartOnFirstClick,
                    HintText = SlopSettingsStrings.SliderPartOnFirstClickDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorSliderPartOnFirstClick),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorNoAudioStretch,
                    HintText = SlopSettingsStrings.EditorNoAudioStretchDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorNoAudioStretch),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorPreviewMods,
                    HintText = SlopSettingsStrings.EditorPreviewModsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorPreviewMods),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorSongSelectShowPlayButtons,
                    HintText = SlopSettingsStrings.EditorSongSelectShowPlayButtonsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorSongSelectShowPlayButtons),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorManiaPlayfieldWaveform,
                    HintText = SlopSettingsStrings.EditorManiaPlayfieldWaveformDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorManiaPlayfieldWaveform),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorShowAutoplayCursor,
                    HintText = SlopSettingsStrings.EditorShowAutoplayCursorDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowAutoplayCursor),
                }),
                new SettingsItemV2(cursorSkinDropdown = new EditorSkinDropdown
                {
                    AlwaysShowSearchBar = true,
                    AllowNonContiguousMatching = true,
                    Caption = SlopSettingsStrings.EditorAutoplayCursorSkin,
                    HintText = SlopSettingsStrings.EditorAutoplayCursorSkinDescription,
                    DefaultItemText = SlopSettingsStrings.SameAsEditorSkin,
                    Current = selectedCursorSkin,
                    Items = new[] { same_as_gameplay_skin },
                }),
                new SettingsItemV2(new FormEnumDropdown<SlopAutoplayEngine>
                {
                    Caption = SlopSettingsStrings.EditorAutoplayEngine,
                    HintText = SlopSettingsStrings.EditorAutoplayEngineDescription,
                    Current = config.GetBindable<SlopAutoplayEngine>(OsuSetting.SlopEditorAutoplayEngine),
                }),
                new SettingsItemV2(new FormTextBox
                {
                    Caption = SlopSettingsStrings.EditorTextEditor,
                    HintText = SlopSettingsStrings.EditorTextEditorDescription,
                    Current = config.GetBindable<string>(OsuSetting.SlopEditorTextEditor),
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

            selectedCursorSkin.BindValueChanged(skin =>
            {
                cursorSkinSetting.Value = skin.NewValue.ID == Guid.Empty ? string.Empty : skin.NewValue.ID.ToString();
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
                cursorSkinDropdown.Items = items;

                // A skin which no longer exists falls back to the default entry (see EditorSkinSource), so reflect that here.
                selectedSkin.Value = findSkin(items, editorSkinSetting.Value);
                selectedCursorSkin.Value = findSkin(items, cursorSkinSetting.Value);
            });
        }

        private static Live<SkinInfo> findSkin(List<Live<SkinInfo>> items, string setting)
            => Guid.TryParse(setting, out var id) ? items.FirstOrDefault(s => s.ID == id) ?? same_as_gameplay_skin : same_as_gameplay_skin;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            realmSubscription?.Dispose();
        }

        private partial class EditorSkinDropdown : FormDropdown<Live<SkinInfo>>
        {
            /// <summary>
            /// The text of the entry which doesn't select a specific skin.
            /// </summary>
            public LocalisableString DefaultItemText { get; init; } = SlopSettingsStrings.SameAsGameplaySkin;

            protected override LocalisableString GenerateItemText(Live<SkinInfo> item)
                => item.ID == Guid.Empty ? DefaultItemText : item.ToString() ?? string.Empty;
        }
    }
}
