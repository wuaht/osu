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
    public partial class EditorPlaybackSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.EditorPlaybackHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "music", "loop", "mute", "c999", "pitch", "speed", "stretch", "mods", "preview", "mania", "playfield", "waveform", "text editor", "notepad", "play button" });

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
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
                new SettingsItemV2(new FormTextBox
                {
                    Caption = SlopSettingsStrings.EditorTextEditor,
                    HintText = SlopSettingsStrings.EditorTextEditorDescription,
                    Current = config.GetBindable<string>(OsuSetting.SlopEditorTextEditor),
                }),
            };
        }
    }
}
