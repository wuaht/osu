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
using osu.Game.Screens.Edit.Components;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class EditorEditingSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.EditorEditingHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "selection", "anchor", "drag", "insert", "rotation", "origin", "slider", "break", "alt", "velocity" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
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
                    Caption = SlopSettingsStrings.ImmediateDrag,
                    HintText = SlopSettingsStrings.ImmediateDragDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorImmediateDrag),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.PointInsertPreview,
                    HintText = SlopSettingsStrings.PointInsertPreviewDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorPointInsertPreview),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.RotateAroundObjectStarts,
                    HintText = SlopSettingsStrings.RotateAroundObjectStartsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorRotateAroundObjectStarts),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.RotateAroundSelection,
                    HintText = SlopSettingsStrings.RotateAroundSelectionDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorRotateAroundSelection),
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = SlopSettingsStrings.EditorCustomRotation,
                    HintText = SlopSettingsStrings.EditorCustomRotationDescription,
                    Current = config.GetBindable<int>(OsuSetting.SlopEditorCustomRotation),
                    KeyboardStep = 1,
                    LabelFormat = v => $@"{v}°",
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorTrueSliderEnds,
                    HintText = SlopSettingsStrings.EditorTrueSliderEndsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorTrueSliderEnds),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.EditorRandomSliderAnchors,
                    HintText = SlopSettingsStrings.EditorRandomSliderAnchorsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorRandomSliderAnchors),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.SliderPartOnFirstClick,
                    HintText = SlopSettingsStrings.SliderPartOnFirstClickDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorSliderPartOnFirstClick),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.ShowSliderEndDragMarker,
                    HintText = SlopSettingsStrings.ShowSliderEndDragMarkerDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorShowSliderEndDragMarker),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.TimelineSliderVelocityWithAlt,
                    HintText = SlopSettingsStrings.TimelineSliderVelocityWithAltDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorTimelineSliderVelocityWithAlt),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.NoAutomaticBreaks,
                    HintText = SlopSettingsStrings.NoAutomaticBreaksDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopEditorNoAutomaticBreaks),
                }),
            };
        }
    }
}
