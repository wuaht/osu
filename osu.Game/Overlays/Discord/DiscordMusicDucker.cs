// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Fades out the music of the game while sound of Discord media is playing, and fades it back in afterwards.
    /// </summary>
    /// <remarks>
    /// <see cref="MusicController.Duck"/> can't be used, as it applies to all tracks, which includes the sound of the media itself.
    /// </remarks>
    public partial class DiscordMusicDucker : Component
    {
        private const double fade_out_duration = 300;
        private const double fade_in_duration = 800;

        private readonly BindableDouble volume = new BindableDouble(1);

        private readonly HashSet<object> playing = new HashSet<object>();

        [Resolved]
        private MusicController? musicController { get; set; }

        private DrawableTrack? adjustedTrack;

        /// <summary>
        /// Sets whether the sound of <paramref name="source"/> is currently audible.
        /// </summary>
        public void SetPlaying(object source, bool isPlaying)
        {
            // Media is disposed in the background once its channel is closed.
            if (!ThreadSafety.IsUpdateThread)
            {
                Schedule(() => SetPlaying(source, isPlaying));
                return;
            }

            bool changed = isPlaying ? playing.Add(source) : playing.Remove(source);

            if (!changed || musicController == null)
                return;

            // Media is paused when the overlay is hidden, which also stops updating this component, so the fade runs on the music controller.
            if (playing.Count > 0)
                musicController.TransformBindableTo(volume, 0, fade_out_duration, Easing.OutQuint);
            else
                musicController.TransformBindableTo(volume, 1, fade_in_duration, Easing.InQuad);
        }

        protected override void Update()
        {
            base.Update();

            // The music controller replaces its track whenever the song changes, so the adjustment follows the current one.
            var track = musicController?.CurrentTrack;

            if (track == adjustedTrack)
                return;

            adjustedTrack?.RemoveAdjustment(AdjustableProperty.Volume, volume);
            adjustedTrack = track;
            adjustedTrack?.AddAdjustment(AdjustableProperty.Volume, volume);
        }

        protected override void Dispose(bool isDisposing)
        {
            adjustedTrack?.RemoveAdjustment(AdjustableProperty.Volume, volume);
            adjustedTrack = null;

            base.Dispose(isDisposing);
        }
    }
}
