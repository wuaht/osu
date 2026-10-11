// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Provides the onset envelope of the track (see <see cref="OnsetEnvelope.GetForTrack"/>), for placing timing points onto the onsets of the audio.
    /// </summary>
    public partial class TimingAnalyser : Component
    {
        /// <summary>
        /// The onset envelope of the track, once it has been calculated.
        /// </summary>
        public OnsetEnvelope? Envelope { get; private set; }

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        private Waveform? analysedWaveform;

        /// <summary>
        /// Incremented whenever the track changes, so that envelopes of previous tracks are ignored.
        /// </summary>
        private int version;

        protected override void Update()
        {
            base.Update();

            // the waveform is replaced when the track changes.
            if (analysedWaveform == workingBeatmap.Value.Waveform)
                return;

            analysedWaveform = workingBeatmap.Value.Waveform;
            Envelope = null;

            int currentVersion = ++version;

            OnsetEnvelope.GetForTrack(workingBeatmap.Value).ContinueWith(task => Schedule(() =>
            {
                if (currentVersion == version && task.IsCompletedSuccessfully)
                    Envelope = task.GetResultSafely();
            }));
        }
    }
}
