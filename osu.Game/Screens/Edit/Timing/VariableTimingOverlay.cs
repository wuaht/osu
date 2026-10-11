// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Allows placing, moving and removing timing points on the timeline of the timing screen while holding Ctrl,
    /// recalculating the BPMs of the surrounding timing points such that they stay on whole beats.
    /// Holding the right mouse button previews the audio at the cursor.
    /// </summary>
    public partial class VariableTimingOverlay : TimelinePart
    {
        /// <summary>
        /// The maximum number of beat lines displayed in the preview.
        /// </summary>
        private const int max_preview_beats = 300;

        /// <summary>
        /// How far to the right of a timing point it can be grabbed, in pixels.
        /// Timing points are displayed to the left of their time (see <see cref="TimelineTimingChangeDisplay.TimingPointPiece"/>).
        /// </summary>
        private const float grab_leniency = 4;

        /// <summary>
        /// How far from the cursor onsets are snapped to, in pixels.
        /// The distance is limited in milliseconds (see <see cref="min_onset_snap_radius"/> and <see cref="max_onset_snap_radius"/>), such that other notes aren't snapped to when zoomed out.
        /// </summary>
        private const float onset_snap_distance = 6;

        private const double min_onset_snap_radius = 8;
        private const double max_onset_snap_radius = 30;

        /// <summary>
        /// The deviation of the onsets from the fitted beats above which the placement is shown as doubtful, in milliseconds.
        /// </summary>
        private const double max_good_deviation = 3;

        [Resolved]
        private Timeline timeline { get; set; } = null!;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private Bindable<ControlPointGroup?> selectedGroup { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private TimingAnalyser? analyser { get; set; }

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        /// <summary>
        /// A separate instance of the track, which previews the audio at the cursor while holding the right mouse button, without moving the editor's time.
        /// </summary>
        private Track? previewTrack;

        /// <summary>
        /// The difference between the editor's time and the time of the track (e.g. offsets applied by the editor's clock).
        /// </summary>
        private double previewOffset;

        /// <summary>
        /// Whether the editor's playback was paused for the preview, and should be resumed afterwards.
        /// </summary>
        private bool resumeAfterPreview;

        /// <summary>
        /// Shows the position of <see cref="previewTrack"/> (a "shadow" of the editor's time).
        /// </summary>
        private Box previewLine = null!;

        private Bindable<bool> adjustHitObjects = null!;

        private InputManager inputManager = null!;

        private Box cursorLine = null!;

        /// <summary>
        /// Marks where the timing point is placed, if it isn't placed at the cursor (but at the start of the bar).
        /// </summary>
        private Box pointLine = null!;
        private Container beatLines = null!;
        private readonly List<Box> beatLinePool = new List<Box>();
        private Container label = null!;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText detailText = null!;
        private OsuSpriteText warningText = null!;

        private VariableTiming.TimingPointMove? activeMove;

        /// <summary>
        /// The offset between the grabbed timing point and the cursor at the start of a move.
        /// </summary>
        private double moveOffset;

        /// <summary>
        /// The timing point under the cursor, if any.
        /// </summary>
        private TimingControlPoint? hoveredPoint;

        /// <summary>
        /// The fit of the beats of the placement or move that is currently shown, if any.
        /// </summary>
        private BeatFit? currentFit;

        public VariableTimingOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            adjustHitObjects = config.GetBindable<bool>(OsuSetting.EditorAdjustExistingObjectsOnTimingChanges);

            AddRangeInternal(new Drawable[]
            {
                label = new Container
                {
                    AutoSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 4,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.75f),
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Horizontal = 5, Vertical = 2 },
                            Children = new Drawable[]
                            {
                                titleText = new OsuSpriteText { Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold) },
                                detailText = new OsuSpriteText { Font = OsuFont.Default.With(size: 11) },
                                warningText = new OsuSpriteText
                                {
                                    Font = OsuFont.Default.With(size: 11, weight: FontWeight.SemiBold),
                                    Colour = colours.Orange1,
                                },
                            }
                        },
                    }
                },
            });

            AddRange(new Drawable[]
            {
                previewLine = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    RelativePositionAxes = Axes.X,
                    Width = 2,
                    Origin = Anchor.TopCentre,
                    Colour = Color4.White,
                    Alpha = 0,
                },
                beatLines = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    RelativeChildSize = Vector2.One,
                },
                pointLine = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    RelativePositionAxes = Axes.X,
                    Width = 4,
                    Origin = Anchor.TopCentre,
                    Alpha = 0,
                },
                cursorLine = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    RelativePositionAxes = Axes.X,
                    Width = 2,
                    Origin = Anchor.TopCentre,
                    Alpha = 0,
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;
        }

        private bool shiftPressed => inputManager.CurrentState.Keyboard.ShiftPressed;

        private double millisecondsPerPixel => Content.DrawWidth > 0 ? Content.RelativeChildSize.X / Content.DrawWidth : 0;

        /// <summary>
        /// Snaps a time to the closest onset in the audio, unless Shift is held.
        /// </summary>
        private (double time, bool snapped) snapToOnset(double time)
        {
            var envelope = analyser?.Envelope;

            if (envelope == null || shiftPressed)
                return (Math.Round(time), false);

            double radius = Math.Clamp(onset_snap_distance * millisecondsPerPixel, min_onset_snap_radius, max_onset_snap_radius);
            double? onset = envelope.FindOnset(time, radius);

            return onset == null ? (Math.Round(time), false) : (onset.Value, true);
        }

        /// <summary>
        /// Fits a beat which was snapped to an onset to the onsets of all beats since <paramref name="start"/> (see <see cref="OnsetEnvelope.FitBeats"/>),
        /// such that the BPM doesn't only depend on the single onset.
        /// </summary>
        /// <param name="anchorTime">The time of the beat.</param>
        /// <param name="start">The time of the timing point the beats are counted from, which stays fixed.</param>
        /// <param name="beats">The number of beats from <paramref name="start"/> to the beat.</param>
        /// <param name="snapped">Whether the beat was snapped to an onset. Beats placed freely (with Shift) are kept where they are.</param>
        /// <returns>The fitted time of the beat.</returns>
        private double fitToBeats(double anchorTime, double? start, int beats, bool snapped)
        {
            currentFit = null;

            var envelope = analyser?.Envelope;

            if (envelope == null || !snapped || start == null || beats < 2)
                return anchorTime;

            var fit = envelope.FitBeats(start.Value, beats, anchorTime);

            if (fit.Deviation == null)
                return anchorTime;

            currentFit = fit;
            return start.Value + beats * fit.BeatLength;
        }

        /// <summary>
        /// Calculates the placement of a beat at the cursor, snapped to an onset and fitted to the beats since the preceding timing point.
        /// </summary>
        private (VariableTiming.PlacementPreview preview, bool snapped) calculatePlacement(double cursorTime)
        {
            var (time, snapped) = snapToOnset(cursorTime);
            var preview = VariableTiming.CalculatePlacement(beatmap.ControlPointInfo, time, estimateBeatLength);

            double fitted = fitToBeats(time, preview.Previous?.Time, preview.PreviousBeats + preview.AnchorBeat, snapped);

            if (fitted != time)
                preview = VariableTiming.CalculatePlacement(beatmap.ControlPointInfo, fitted, estimateBeatLength);

            return (preview, snapped);
        }

        /// <summary>
        /// Calculates where the beat of <paramref name="move"/> is moved to for the cursor at <paramref name="cursorTime"/>.
        /// </summary>
        private double calculateMove(VariableTiming.TimingPointMove move, double cursorTime)
        {
            var (time, snapped) = snapToOnset(cursorTime);

            return fitToBeats(time, move.Previous?.Time, move.PreviousBeats + move.AnchorBeat, snapped);
        }

        private bool controlPressed => inputManager.CurrentState.Keyboard.ControlPressed;

        private bool altPressed => inputManager.CurrentState.Keyboard.AltPressed;

        /// <summary>
        /// Whether the cursor is above the visible part of the timeline.
        /// </summary>
        private bool cursorInTimeline => timeline.ScreenSpaceDrawQuad.Contains(inputManager.CurrentState.Mouse.Position);

        protected override void Update()
        {
            base.Update();

            beatLines.RelativeChildSize = Content.RelativeChildSize;

            updatePreviewLine();

            if (activeMove == null && !(controlPressed && cursorInTimeline))
            {
                hoveredPoint = null;
                hidePreview();
                return;
            }

            double cursorTime = timeline.TimeAtScreenSpacePosition(inputManager.CurrentState.Mouse.Position);

            if (activeMove != null)
            {
                showMove(activeMove);
                return;
            }

            hoveredPoint = findTimingPointAt(inputManager.CurrentState.Mouse.Position);

            if (hoveredPoint != null)
                showHover(hoveredPoint);
            else
            {
                var (preview, snapped) = calculatePlacement(cursorTime);
                showPlacement(preview, snapped);
            }
        }

        private Func<double, double, double?>? estimateBeatLength => analyser?.Envelope != null ? analyser.Envelope.EstimateBeatLength : null;

        private void showPlacement(VariableTiming.PlacementPreview preview, bool snapped)
        {
            showCursorLine(preview.AnchorTime, colours.Yellow);
            string snapText = snapped ? " (snapped to onset, hold Shift to disable)" : string.Empty;
            var sections = new List<(double start, double end, double beatLength, int meter)>();

            // the timing point may be placed away from the cursor (at the start of the bar), so its position is always marked.
            showPointLine(preview.Time);

            titleText.Text = preview.AnchorBeat > 0
                ? $"New timing point at the start of this bar (red line), you clicked beat {preview.AnchorBeat + 1}: {bpm(preview.BeatLength)}{snapText}"
                : $"New timing point here: {bpm(preview.BeatLength)}{snapText}";

            detailText.Text = preview.Previous != null
                ? $"Previous: {bpm(preview.Previous.BeatLength)} → {bpm(preview.PreviousBeatLength)} ({preview.PreviousBeats} beats)"
                : string.Empty;

            warningText.Colour = colours.Orange1;
            warningText.Text = preview.IsOnBarLine
                ? string.Empty
                : "Not on a bar line of the previous timing point, so the following bar lines shift";

            if (preview.Previous != null)
                sections.Add((preview.Previous.Time, preview.Time, preview.PreviousBeatLength, preview.Previous.TimeSignature.Numerator));

            sections.Add((preview.Time, preview.Next?.Time ?? double.PositiveInfinity, preview.BeatLength, (preview.Previous ?? preview.Next)?.TimeSignature.Numerator ?? 4));

            showFit();

            if (VariableTiming.FindLargeTempoChange(beatmap.ControlPointInfo, preview) is var (oldBeatLength, newBeatLength))
            {
                double percentage = (oldBeatLength / newBeatLength - 1) * 100;

                warningText.Colour = colours.Red1;
                warningText.Text = $"Changes {bpm(oldBeatLength)} to {bpm(newBeatLength)} ({percentage:+0;-0}%), probably not on the right beat. "
                                   + (altPressed ? "Placing anyway as Alt is held" : "Hold Alt to place anyway");
            }

            showBeatLines(sections);
            showLabel(preview.AnchorTime);
        }

        /// <summary>
        /// Adds how well the beats fit the onsets of the audio to the label.
        /// </summary>
        private void showFit()
        {
            if (currentFit?.Deviation is not double deviation)
                return;

            string separator = detailText.Text.ToString().Length > 0 ? ", " : string.Empty;
            detailText.Text = $"{detailText.Text}{separator}fitted to {currentFit.Value.OnsetCount} onsets (±{deviation:0.0} ms)";

            if (deviation > max_good_deviation)
                warningText.Text = $"The onsets deviate from the beats by ±{deviation:0.0} ms, the number of beats may be wrong";
        }

        private void showHover(TimingControlPoint point)
        {
            showCursorLine(point.Time, colours.YellowLighter);
            showPointLine(null);

            titleText.Text = bpm(point.BeatLength);
            detailText.Text = "Drag to move, right-click to remove";
            warningText.Text = string.Empty;

            hideBeatLines();
            showLabel(point.Time);
        }

        private void showMove(VariableTiming.TimingPointMove move)
        {
            var point = move.Point;

            showCursorLine(move.AnchorTime, colours.Orange1);
            showPointLine(point.Time);

            titleText.Text = bpm(point.BeatLength);
            detailText.Text = move.Previous != null ? $"Previous: {bpm(move.Previous.BeatLength)} ({move.PreviousBeats} beats)" : string.Empty;
            warningText.Text = string.Empty;
            showFit();

            var sections = new List<(double start, double end, double beatLength, int meter)>();

            if (move.Previous != null)
                sections.Add((move.Previous.Time, point.Time, move.Previous.BeatLength, move.Previous.TimeSignature.Numerator));

            sections.Add((point.Time, move.Next?.Time ?? double.PositiveInfinity, point.BeatLength, point.TimeSignature.Numerator));

            showBeatLines(sections);
            showLabel(move.AnchorTime);
        }

        private void showPointLine(double? time)
        {
            if (time == null)
            {
                pointLine.Alpha = 0;
                return;
            }

            pointLine.X = (float)time.Value;
            pointLine.Colour = colours.Red1;
            pointLine.Alpha = 0.8f;
        }

        private void showCursorLine(double time, Color4 colour)
        {
            cursorLine.X = (float)time;
            cursorLine.Colour = colour;
            cursorLine.Alpha = 1;
        }

        private void showLabel(double time)
        {
            label.Alpha = 1;

            // the label isn't sized relative to the track length, so it has to be positioned manually.
            label.Position = new Vector2((float)(time / Content.RelativeChildSize.X * DrawWidth) + 4, 2);
        }

        private void hidePreview()
        {
            cursorLine.Alpha = 0;
            pointLine.Alpha = 0;
            label.Alpha = 0;
            hideBeatLines();
        }

        private void hideBeatLines()
        {
            foreach (var line in beatLinePool)
                line.Alpha = 0;
        }

        /// <summary>
        /// Displays the beats of the given sections within the visible range of the timeline.
        /// </summary>
        private void showBeatLines(IEnumerable<(double start, double end, double beatLength, int meter)> sections)
        {
            double visibleStart = timeline.TimeAtScreenSpacePosition(timeline.ScreenSpaceDrawQuad.TopLeft);
            double visibleEnd = timeline.TimeAtScreenSpacePosition(timeline.ScreenSpaceDrawQuad.TopRight);

            int used = 0;

            foreach (var (start, end, beatLength, meter) in sections)
            {
                if (beatLength <= 0)
                    continue;

                double sectionEnd = Math.Min(end, visibleEnd);
                int firstBeat = Math.Max(0, (int)Math.Floor((visibleStart - start) / beatLength));

                for (int beat = firstBeat; used < max_preview_beats; beat++)
                {
                    double time = start + beat * beatLength;

                    // the end of a section is the start of the next one, which displays its own beat.
                    if (time >= sectionEnd - 0.5)
                        break;

                    var line = getBeatLine(used++);
                    bool isBarLine = beat % Math.Max(1, meter) == 0;

                    line.X = (float)time;
                    line.Width = isBarLine ? 2 : 1;
                    line.Alpha = isBarLine ? 0.9f : 0.5f;
                }
            }

            for (int i = used; i < beatLinePool.Count; i++)
                beatLinePool[i].Alpha = 0;
        }

        private Box getBeatLine(int index)
        {
            while (beatLinePool.Count <= index)
            {
                var line = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    RelativePositionAxes = Axes.X,
                    Origin = Anchor.TopCentre,
                    Colour = colours.Yellow,
                    Alpha = 0,
                };

                beatLinePool.Add(line);
                beatLines.Add(line);
            }

            return beatLinePool[index];
        }

        private TimingControlPoint? findTimingPointAt(Vector2 screenSpacePosition)
        {
            double cursorTime = timeline.TimeAtScreenSpacePosition(screenSpacePosition);

            TimingControlPoint? closest = null;

            foreach (var point in beatmap.ControlPointInfo.TimingPoints)
            {
                double distance = (cursorTime - point.Time) / millisecondsPerPixel;

                if (distance > grab_leniency || distance < -TimelineTimingChangeDisplay.TimingPointPiece.WIDTH)
                    continue;

                if (closest == null || Math.Abs(point.Time - cursorTime) < Math.Abs(closest.Time - cursorTime))
                    closest = point;
            }

            return closest;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (!cursorInTimeline || activeMove != null)
                return false;

            // listen to the audio at the cursor while holding the right mouse button.
            if (e.Button == MouseButton.Right && !(controlPressed && hoveredPoint != null))
            {
                startPreview(timeline.TimeAtScreenSpacePosition(e.ScreenSpaceMousePosition));
                return true;
            }

            if (!controlPressed)
                return false;

            double cursorTime = timeline.TimeAtScreenSpacePosition(e.ScreenSpaceMousePosition);

            // act on exactly what the preview shows.
            var point = hoveredPoint;

            switch (e.Button)
            {
                case MouseButton.Left:
                    VariableTiming.PlacementPreview? placement = null;

                    if (point == null)
                    {
                        placement = calculatePlacement(cursorTime).preview;

                        // the preview explains why nothing is placed.
                        if (!altPressed && VariableTiming.FindLargeTempoChange(beatmap.ControlPointInfo, placement) != null)
                            return true;
                    }

                    beatmap.BeginChange();

                    if (placement == null)
                    {
                        activeMove = VariableTiming.TimingPointMove.Begin(beatmap, point!, adjustHitObjects.Value);
                        moveOffset = activeMove.AnchorTime - cursorTime;
                        currentFit = null;
                    }
                    else
                    {
                        activeMove = VariableTiming.TimingPointMove.Place(beatmap, placement.AnchorTime, adjustHitObjects.Value, estimateBeatLength);
                        moveOffset = 0;
                    }

                    selectMovedGroup();
                    return true;

                case MouseButton.Right:
                    if (point == null)
                        return false;

                    VariableTiming.RemoveTimingPoint(beatmap, point, adjustHitObjects.Value);

                    if (selectedGroup.Value != null && !beatmap.ControlPointInfo.Groups.Contains(selectedGroup.Value))
                        selectedGroup.Value = null;

                    return true;
            }

            return false;
        }

        protected override bool OnDragStart(DragStartEvent e) => activeMove != null && e.Button == MouseButton.Left;

        protected override void OnDrag(DragEvent e)
        {
            if (activeMove == null)
                return;

            activeMove.MoveTo(calculateMove(activeMove, timeline.TimeAtScreenSpacePosition(e.ScreenSpaceMousePosition) + moveOffset));
            selectMovedGroup();
        }

        protected override void OnDragEnd(DragEndEvent e) => endMove();

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button == MouseButton.Left)
                endMove();

            if (e.Button == MouseButton.Right)
                endPreview();

            base.OnMouseUp(e);
        }

        private void endMove()
        {
            if (activeMove == null)
                return;

            activeMove = null;
            beatmap.UpdateAllHitObjects();
            beatmap.EndChange();
        }

        private void startPreview(double time)
        {
            previewTrack ??= createPreviewTrack();

            if (previewTrack == null)
                return;

            if (editorClock.IsRunning)
            {
                resumeAfterPreview = true;
                editorClock.Stop();
            }

            previewOffset = editorClock.CurrentTime - workingBeatmap.Value.Track.CurrentTime;
            previewTrack.Seek(Math.Max(0, time - previewOffset));
            previewTrack.Start();
        }

        private void endPreview()
        {
            if (previewTrack?.IsRunning != true)
                return;

            previewTrack.Stop();

            if (resumeAfterPreview)
                editorClock.Start();

            resumeAfterPreview = false;
        }

        private void updatePreviewLine()
        {
            if (previewTrack?.IsRunning != true)
            {
                previewLine.Alpha = 0;
                return;
            }

            previewLine.X = (float)(previewTrack.CurrentTime + previewOffset);
            previewLine.Alpha = 0.7f;
        }

        private Track? createPreviewTrack()
        {
            var working = workingBeatmap.Value;
            string? audioFile = working.Metadata?.AudioFile;

            if (string.IsNullOrEmpty(audioFile))
                return null;

            string? path = working.BeatmapSetInfo?.GetPathForFile(audioFile);

            if (path == null)
                return null;

            var track = beatmapManager.BeatmapTrackStore.Get(path);

            // play at the same speed as the editor.
            track?.BindAdjustments(editorClock.AudioAdjustments);
            return track;
        }

        private void selectMovedGroup()
        {
            if (activeMove != null)
                selectedGroup.Value = beatmap.ControlPointInfo.GroupAt(activeMove.Point.Time);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            previewTrack?.Dispose();
        }

        private static string bpm(double beatLength) => $"{60000 / beatLength:0.###} BPM";
    }
}
