// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Previews the audio at a time while the right mouse button is held, on a separate instance of the track, without moving the editor's time.
    /// The metronome ticks along, with the same keys for ticking on halves of beats and muting it as during playback.
    /// </summary>
    public partial class CursorAudioPreview : Component
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private EditorMetronome? metronome { get; set; }

        private Track? track;

        /// <summary>
        /// The tick of the metronome the preview was last at, to tick when it reaches the next one.
        /// </summary>
        private (TimingControlPoint point, int divisor, int index)? lastTick;

        /// <summary>
        /// The difference between the editor's time and the time of the track (e.g. offsets applied by the editor's clock).
        /// </summary>
        private double offset;

        /// <summary>
        /// Whether the editor's playback was paused for the preview, and should be resumed afterwards.
        /// </summary>
        private bool resumeAfterPreview;

        /// <summary>
        /// The editor time of the preview (a "shadow" of the editor's time), or null if it isn't playing.
        /// </summary>
        public double? CurrentTime => track?.IsRunning == true ? track.CurrentTime + offset : null;

        public void Start(double time)
        {
            track ??= createTrack();

            if (track == null)
                return;

            if (editorClock.IsRunning)
            {
                resumeAfterPreview = true;
                editorClock.Stop();
            }

            offset = editorClock.CurrentTime - workingBeatmap.Value.Track.CurrentTime;
            track.Seek(Math.Max(0, time - offset));
            track.Start();
            lastTick = null;
        }

        protected override void Update()
        {
            base.Update();

            if (metronome == null || CurrentTime is not double time)
                return;

            var point = beatmap.ControlPointInfo.TimingPointAt(time);
            int divisor = metronome.ComputeDivisor();

            if (time < point.Time)
                return;

            int index = (int)Math.Floor((time - point.Time) / (point.BeatLength / divisor));
            var tick = (point, divisor, index);

            // a changed divisor only realigns the ticks, without ticking.
            if (lastTick is var (lastPoint, lastDivisor, lastIndex) && lastDivisor == divisor && (lastPoint != point || lastIndex != index))
                metronome.PlayTick(index % (point.TimeSignature.Numerator * divisor) == 0);

            lastTick = tick;
        }

        public void Stop()
        {
            if (track?.IsRunning != true)
                return;

            track.Stop();

            if (resumeAfterPreview)
                editorClock.Start();

            resumeAfterPreview = false;
        }

        private Track? createTrack()
        {
            var working = workingBeatmap.Value;
            string? audioFile = working.Metadata?.AudioFile;

            if (string.IsNullOrEmpty(audioFile))
                return null;

            string? path = working.BeatmapSetInfo?.GetPathForFile(audioFile);

            if (path == null)
                return null;

            var created = beatmapManager.BeatmapTrackStore.Get(path);

            // play at the same speed as the editor.
            created?.BindAdjustments(editorClock.AudioAdjustments);
            return created;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            track?.Dispose();
        }
    }
}
