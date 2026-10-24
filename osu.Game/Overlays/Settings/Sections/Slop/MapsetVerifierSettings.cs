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
    /// <summary>
    /// Settings of the mapset verifier, which replaces the verify screen of the beatmap editor.
    /// </summary>
    public partial class MapsetVerifierSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.MapsetVerifierHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[]
        {
            "mv", "verify", "verifier", "check", "minor", "negligible", "snapshot", "strain", "audio", "frosted", "editor", "modding"
        });

        private readonly Bindable<string> hiddenMinorChecks = new Bindable<string>();

        private SettingsItemV2 deltaUnchangedItem = null!;
        private SettingsButtonV2 resetMinorFilterButton = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopMapsetVerifierHiddenMinorChecks, hiddenMinorChecks);

            var showCheckRunDelta = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierShowCheckRunDelta);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierShowMinor,
                    HintText = SlopSettingsStrings.MapsetVerifierShowMinorDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierShowMinor),
                }),
                resetMinorFilterButton = new SettingsButtonV2
                {
                    Text = SlopSettingsStrings.MapsetVerifierResetMinorFilter,
                    Action = () => hiddenMinorChecks.Value = string.Empty,
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierShowIssueDetails,
                    HintText = SlopSettingsStrings.MapsetVerifierShowIssueDetailsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierShowIssueDetails),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierGamemodeDifficultyNames,
                    HintText = SlopSettingsStrings.MapsetVerifierGamemodeDifficultyNamesDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierShowGamemodeDifficultyNames),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierAutoSnapshot,
                    HintText = SlopSettingsStrings.MapsetVerifierAutoSnapshotDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierAutoSnapshot),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierCheckRunDelta,
                    HintText = SlopSettingsStrings.MapsetVerifierCheckRunDeltaDescription,
                    Current = showCheckRunDelta,
                }),
                deltaUnchangedItem = new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierDeltaUnchanged,
                    HintText = SlopSettingsStrings.MapsetVerifierDeltaUnchangedDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierDeltaShowUnchanged),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierRecheckOnSave,
                    HintText = SlopSettingsStrings.MapsetVerifierRecheckOnSaveDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierRecheckOnSave),
                }),
                new SettingsItemV2(new FormEnumDropdown<MapsetVerifierStrainDisplayMode>
                {
                    Caption = SlopSettingsStrings.MapsetVerifierStrainDisplay,
                    HintText = SlopSettingsStrings.MapsetVerifierStrainDisplayDescription,
                    Current = config.GetBindable<MapsetVerifierStrainDisplayMode>(OsuSetting.SlopMapsetVerifierStrainDisplayMode),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierAdvancedAudio,
                    HintText = SlopSettingsStrings.MapsetVerifierAdvancedAudioDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierShowAdvancedAudioAnalysis),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierCheckSpeed,
                    HintText = SlopSettingsStrings.MapsetVerifierCheckSpeedDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierShowCheckSpeedStats),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.MapsetVerifierFrostedPanels,
                    HintText = SlopSettingsStrings.MapsetVerifierFrostedPanelsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopMapsetVerifierFrostedPanels),
                }),
            };

            // like in Mapset Verifier, the unchanged issues are only an option of the changes since the last run.
            showCheckRunDelta.BindValueChanged(e => deltaUnchangedItem.CanBeShown.Value = e.NewValue, true);

            // only offered while there is something to reset.
            hiddenMinorChecks.BindValueChanged(e => resetMinorFilterButton.Alpha = string.IsNullOrEmpty(e.NewValue) ? 0 : 1, true);
        }
    }
}
