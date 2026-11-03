﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
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
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class EditorSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.EditorHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "skin", "waveform", "frosted", "bn", "nominator", "request", "border", "fps", "star", "difficulty", "curve", "colour", "color", "accent", "hue", "zoom", "grid", "strain", "autoplay", "cursor", "offscreen" });

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

        private Bindable<bool> customAccentColour = null!;

        private Bindable<bool> showAutoplayCursor = null!;

        private SettingsItemV2 cursorSkinItem = null!;

        private SettingsItemV2 cursorEngineItem = null!;

        private SettingsItemV2 accentHueItem = null!;

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
                    Caption = SlopSettingsStrings.CustomEditorAccentColour,
                    HintText = SlopSettingsStrings.CustomEditorAccentColourDescription,
                    Current = customAccentColour = config.GetBindable<bool>(OsuSetting.SlopEditorCustomAccentColour),
                }),
                accentHueItem = new SettingsItemV2(new FormHueSlider
                {
                    Caption = SlopSettingsStrings.EditorAccentHue,
                    Current = config.GetBindable<int>(OsuSetting.SlopEditorAccentHue),
                }),
                new SettingsItemV2(new FormEnumDropdown<EditorWaveformStyle>
                {
                    Caption = SlopSettingsStrings.WaveformStyle,
                    HintText = SlopSettingsStrings.WaveformStyleDescription,
                    Current = config.GetBindable<EditorWaveformStyle>(OsuSetting.SlopEditorWaveformStyle),
                }),
                new SettingsItemV2(new FormEnumDropdown<GlassStyle>
                {
                    Caption = SlopSettingsStrings.BnTrackerPanelsGlassStyle,
                    HintText = SlopSettingsStrings.BnTrackerPanelsGlassStyleDescription,
                    Current = config.GetBindable<GlassStyle>(OsuSetting.SlopBnTrackerPanelsGlassStyle),
                }),
                new SettingsItemV2(new FormEnumDropdown<SliderCurveColourStyle>
                {
                    Caption = SlopSettingsStrings.SliderCurveColour,
                    Current = config.GetBindable<SliderCurveColourStyle>(OsuSetting.SlopEditorSliderCurveColour),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowGrid,
                    HintText = SlopSettingsStrings.ShowGridDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowGrid),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DrawScreenBorders,
                    HintText = SlopSettingsStrings.DrawScreenBordersDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorDrawScreenBorders),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowOffscreenObjects,
                    HintText = SlopSettingsStrings.ShowOffscreenObjectsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowOffscreenObjects),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ZoomOutPlayfield,
                    HintText = SlopSettingsStrings.ZoomOutPlayfieldDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorZoomOut),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorFlashAdditions,
                    HintText = SlopSettingsStrings.EditorFlashAdditionsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorFlashAdditions),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowDifficultyStrains,
                    HintText = SlopSettingsStrings.ShowDifficultyStrainsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowDifficultyStrains),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DifficultyStars,
                    HintText = SlopSettingsStrings.DifficultyStarsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorDifficultyStars),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.AlwaysHideFps,
                    HintText = SlopSettingsStrings.AlwaysHideFpsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAlwaysHideFps),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorShowAutoplayCursor,
                    HintText = SlopSettingsStrings.EditorShowAutoplayCursorDescription,
                    Current = showAutoplayCursor = config.GetBindable<bool>(OsuSetting.SlopEditorShowAutoplayCursor),
                }),
                cursorSkinItem = new SettingsItemV2(cursorSkinDropdown = new EditorSkinDropdown
                {
                    AlwaysShowSearchBar = true,
                    AllowNonContiguousMatching = true,
                    Caption = SlopSettingsStrings.EditorAutoplayCursorSkin,
                    HintText = SlopSettingsStrings.EditorAutoplayCursorSkinDescription,
                    DefaultItemText = SlopSettingsStrings.SameAsEditorSkin,
                    Current = selectedCursorSkin,
                    Items = new[] { same_as_gameplay_skin },
                }),
                cursorEngineItem = new SettingsItemV2(new FormEnumDropdown<SlopAutoplayEngine>
                {
                    Caption = SlopSettingsStrings.EditorAutoplayEngine,
                    HintText = SlopSettingsStrings.EditorAutoplayEngineDescription,
                    Current = config.GetBindable<SlopAutoplayEngine>(OsuSetting.SlopEditorAutoplayEngine),
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

            customAccentColour.BindValueChanged(custom => accentHueItem.Alpha = custom.NewValue ? 1 : 0, true);

            showAutoplayCursor.BindValueChanged(show => cursorSkinItem.Alpha = cursorEngineItem.Alpha = show.NewValue ? 1 : 0.5f, true);
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
