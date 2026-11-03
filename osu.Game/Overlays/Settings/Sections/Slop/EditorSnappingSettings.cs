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
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class EditorSnappingSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.EditorSnappingHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "snap", "blanket", "triangle", "line", "stack", "anchor", "alt", "distance", "spacing", "unsnap", "divisor" });

        private Bindable<EditorUnsnapMode> unsnapMode = null!;

        private SettingsItemV2 unsnapDivisorsItem = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
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
                    Caption = SlopSettingsStrings.TriangleCentreSnap,
                    HintText = SlopSettingsStrings.TriangleCentreSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorTriangleCentreSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.SliderBlanketSnap,
                    HintText = SlopSettingsStrings.SliderBlanketSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorSliderBlanketSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorAnchorSnap,
                    HintText = SlopSettingsStrings.EditorAnchorSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorAnchorSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorStackSnap,
                    HintText = SlopSettingsStrings.EditorStackSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorStackSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DisableAltDistanceSnap,
                    HintText = SlopSettingsStrings.DisableAltDistanceSnapDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorDisableAltDistanceSnap),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.SnapSliderEndsToLaterTimingPoints,
                    HintText = SlopSettingsStrings.SnapSliderEndsToLaterTimingPointsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorSnapSliderEndsToLaterTimingPoints),
                }),
                new SettingsItemV2(new FormEnumDropdown<EditorUnsnapMode>
                {
                    Caption = SlopSettingsStrings.UnsnapMode,
                    HintText = SlopSettingsStrings.UnsnapModeDescription,
                    Current = unsnapMode = config.GetBindable<EditorUnsnapMode>(OsuSetting.SlopEditorUnsnapMode),
                }),
                unsnapDivisorsItem = new SettingsItemV2(new FormTextBox
                {
                    Caption = SlopSettingsStrings.UnsnapDivisors,
                    HintText = SlopSettingsStrings.UnsnapDivisorsDescription,
                    Current = config.GetBindable<string>(OsuSetting.SlopEditorUnsnapDivisors),
                }),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            unsnapMode.BindValueChanged(mode => unsnapDivisorsItem.Alpha = mode.NewValue == EditorUnsnapMode.Enabled ? 1 : 0, true);
        }
    }
}
