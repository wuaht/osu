// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Mutes and unmutes the music and effects volumes in the editor, leaving the master volume as it is.
    /// </summary>
    public partial class EditorVolumeMuting : Component, IKeyBindingHandler<GlobalAction>
    {
        // static, so that the volumes are restored even after leaving and re-entering the editor while muted.
        private static double? musicVolumeBeforeMute;
        private static double? effectsVolumeBeforeMute;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.EditorMuteMusic:
                    toggle(audio.VolumeTrack, ref musicVolumeBeforeMute, SlopEditorStrings.MusicVolume, e.Action);
                    return true;

                case GlobalAction.EditorMuteEffects:
                    toggle(audio.VolumeSample, ref effectsVolumeBeforeMute, SlopEditorStrings.EffectsVolume, e.Action);
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        private void toggle(BindableNumber<double> volume, ref double? volumeBeforeMute, LocalisableString description, GlobalAction action)
        {
            if (volume.Value > 0)
            {
                volumeBeforeMute = volume.Value;
                volume.Value = 0;
            }
            else
            {
                // the default volume if it was muted otherwise (e.g. in the volume settings).
                volume.Value = volumeBeforeMute ?? volume.Default;
                volumeBeforeMute = null;
            }

            onScreenDisplay?.Display(new VolumeToast(description, volume.Value, action));
        }

        private partial class VolumeToast : Toast
        {
            private readonly GlobalAction action;

            public VolumeToast(LocalisableString description, double volume, GlobalAction action)
                : base(description, volume > 0 ? volume.ToString("0%") : SlopEditorStrings.Muted)
            {
                this.action = action;
            }

            [BackgroundDependencyLoader]
            private void load(RealmKeyBindingStore keyBindingStore)
            {
                ExtraText = keyBindingStore.GetBindingsStringFor(action);
            }
        }
    }
}
