// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Plays the hitsounds of the beatmap while the track is playing, and previews hitsounds while editing.
    /// Needs to be inside an <see cref="EditorSkinProvidingContainer"/>, such that the samples of the beatmap are used.
    /// </summary>
    public partial class HitsoundPlayback : CompositeDrawable
    {
        /// <summary>
        /// Hitsounds are only played while the track runs over them, not when seeking further than this.
        /// </summary>
        private const double max_playback_gap = 500;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        private HitsoundEditor hitsoundEditor { get; set; } = null!;

        /// <summary>
        /// One sound per sample (regardless of volume), created when first played.
        /// </summary>
        private readonly Dictionary<HitSampleInfo, SkinnableSound> sounds = new Dictionary<HitSampleInfo, SkinnableSound>();

        private double lastTime;

        protected override void Update()
        {
            base.Update();

            double time = clock.CurrentTime;

            if (clock.IsRunning && !clock.IsSeeking && time > lastTime && time - lastTime < max_playback_gap)
            {
                foreach (var column in hitsoundEditor.Map.GetColumnsInRange(lastTime, time))
                {
                    if (column.Time > lastTime)
                        PlayColumn(column);
                }
            }

            lastTime = time;
        }

        /// <summary>
        /// Plays all hitsounds of a column, except for those on muted lanes.
        /// </summary>
        public void PlayColumn(HitsoundColumn column)
        {
            foreach (var target in column.Targets)
                Play(target.Samples);
        }

        /// <summary>
        /// Plays the samples, except for those on muted lanes.
        /// </summary>
        public void Play(IEnumerable<HitSampleInfo> samples)
        {
            foreach (var sample in samples)
            {
                if (isMuted(sample))
                    continue;

                playSample(sample);
            }
        }

        /// <summary>
        /// Plays the sample of a lane, regardless of whether it is muted.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="volume">The volume.</param>
        /// <param name="customIndex">The custom sample index, which is ignored for sub-lanes, as they play the sample of their own custom sample index.</param>
        public void PlayLane(HitsoundLane lane, int volume = 100, int customIndex = 0)
        {
            var samples = HitsoundSamples.WithCustomIndex(new[] { new HitSampleInfo(lane.Sample, lane.Bank, volume: volume) }, lane.CustomIndex ?? customIndex);
            playSample(samples[0]);
        }

        private bool isMuted(HitSampleInfo sample)
        {
            if (HitsoundSamples.IsFileSample(sample))
                return false;

            int customIndex = HitsoundSamples.GetCustomIndex(new[] { sample });

            // muting a lane mutes all of its sub-lanes, and muting a sub-lane only mutes its custom sample index.
            return hitsoundEditor.MutedLanes.Any(lane => lane.Bank == sample.Bank && lane.Sample == sample.Name && (lane.CustomIndex == null || lane.CustomIndex == customIndex));
        }

        private void playSample(HitSampleInfo sample)
        {
            // the volume isn't part of the equality of samples, so one sound is used for all volumes.
            if (!sounds.TryGetValue(sample, out var sound))
            {
                AddInternal(sound = new SkinnableSound(sample.With(newVolume: 100)));
                sounds[sample] = sound;
            }

            sound.Volume.Value = Math.Max(sample.Volume, DrawableHitObject.MINIMUM_SAMPLE_VOLUME) / 100.0;
            sound.Play();
        }
    }
}
