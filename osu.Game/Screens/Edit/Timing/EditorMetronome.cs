// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Plays the metronome ticks of the timing screen.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="MetronomeDisplay"/>, so that it keeps ticking while the display is collapsed (when no timing point is selected),
    /// as collapsed drawables aren't updated.
    /// </remarks>
    public partial class EditorMetronome : BeatSyncedContainer
    {
        /// <summary>
        /// Whether ticks should be played. Ticks are also muted while Alt is held.
        /// </summary>
        public bool EnableClicking = true;

        /// <summary>
        /// Invoked on every tick, also while muted by holding Alt.
        /// </summary>
        public Action? Ticked;

        private Sample? sampleTick;
        private Sample? sampleTickDownbeat;

        [Resolved]
        private BindableBeatDivisor? beatDivisor { get; set; }

        public EditorMetronome()
        {
            AllowMistimedEventFiring = false;
        }

        protected override void Update()
        {
            Divisor = computeDivisor();

            base.Update();
        }

        /// <summary>
        /// While Ctrl is held, the metronome also ticks on the halves (or thirds, for triplet beat snap divisors) of beats.
        /// </summary>
        private int computeDivisor()
        {
            bool spedUp = GetContainingInputManager()?.CurrentState.Keyboard.ControlPressed == true;

            if (!spedUp || beatDivisor == null)
                return 1;

            if (beatDivisor.Value % 3 == 0)
                return 3;
            if (beatDivisor.Value % 2 == 0)
                return 2;

            return 1;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            sampleTick = audio.Samples.Get(@"UI/metronome-tick");
            sampleTickDownbeat = audio.Samples.Get(@"UI/metronome-tick-downbeat");
        }

        protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
        {
            base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

            if (!IsBeatSyncedWithTrack || !EnableClicking)
                return;

            bool altPressed = GetContainingInputManager()?.CurrentState.Keyboard.AltPressed == true;

            if (!altPressed)
                SamplePlaybackHelper.PlayWithRandomPitch(beatIndex % timingPoint.TimeSignature.Numerator == 0 ? sampleTickDownbeat : sampleTick, 0.02f);

            Ticked?.Invoke();
        }
    }
}
