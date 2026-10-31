// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Edit.Hitsounding;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Timing
{
    /// <summary>
    /// Displays the track as one row per bar, stacked vertically, such that the beats of all bars line up (similar to Tempora).
    /// Timing points are placed by clicking onto the audio (snapped to onsets), and beats of the audio are dragged onto beats of the grid,
    /// recalculating the BPMs of the surrounding timing points (see <see cref="VariableTiming.TimingPointMove"/>).
    /// </summary>
    public partial class MeasureTimingDisplay : CompositeDrawable
    {
        private const int default_visible_rows = 6;
        private const int min_visible_rows = 2;
        private const int max_visible_rows = 16;
        private const float gutter_width = 52;
        private const float status_height = 34;

        /// <summary>
        /// How much of the previous and next bar is displayed on either side of a bar, relative to the length of the bar.
        /// </summary>
        private const float overlap = 0.1f;

        /// <summary>
        /// The resolution of the waveforms, which are scaled to the length of each bar.
        /// </summary>
        private const float waveform_pixels_per_millisecond = 0.5f;

        private const int max_rows = 10000;

        /// <summary>
        /// How far from a timing point it can be grabbed, in pixels.
        /// </summary>
        private const float marker_grab_distance = 6;

        /// <summary>
        /// How much the offset of a timing point changes per pixel while Ctrl+dragging it.
        /// </summary>
        private const double offset_drag_milliseconds_per_pixel = 0.2;

        /// <summary>
        /// How long a reason for not placing a timing point is displayed, in milliseconds.
        /// </summary>
        private const double rejection_display_duration = 4000;

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private Bindable<ControlPointGroup?> selectedGroup { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private TimingAnalyser? analyser { get; set; }

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        private Bindable<bool> adjustHitObjects = null!;
        private Bindable<EditorWaveformStyle> waveformStyle = null!;
        private Bindable<float> waveformOpacity = null!;

        /// <summary>
        /// Whether the timing point list and the timing point settings are shown next to this display.
        /// </summary>
        public readonly BindableBool ShowPanels = new BindableBool();

        private InputManager inputManager = null!;

        private Container<RowDisplay> rowContainer = null!;
        private readonly List<RowDisplay> rowDisplays = new List<RowDisplay>();
        private TruncatingSpriteText statusText = null!;
        private IconButton panelsButton = null!;

        /// <summary>
        /// The size of a screen pixel in the space of the rows, such that lines are as thin as the ticks of the timeline.
        /// </summary>
        private float pixelWidth = 1;

        /// <summary>
        /// The bars of the track, recalculated every frame.
        /// </summary>
        private readonly List<MeasureRow> rows = new List<MeasureRow>();

        /// <summary>
        /// The number of rows before the first timing point, which are extrapolated from it.
        /// </summary>
        private int extrapolatedRows;

        /// <summary>
        /// The index of the topmost displayed row (fractional while scrolling).
        /// </summary>
        private double scroll;

        private double targetScroll;
        private double lastClockTime = double.NaN;

        /// <summary>
        /// The time under the cursor (snapped to an onset), displayed in all rows.
        /// </summary>
        private double? hoverTime;

        private string? rejection;
        private double rejectionTime;

        /// <summary>
        /// The rows at the time the left mouse button was pressed. Drags target the beats of these rows, such that the grid doesn't shift under the cursor while the BPMs change.
        /// </summary>
        private List<MeasureRow>? pressedRows;

        private TimingControlPoint? pressedPoint;
        private double pressedTime;
        private bool pressedSnapped;
        private bool dragStarted;

        private VariableTiming.TimingPointMove? activeMove;
        private bool isOffsetDrag;
        private double moveAnchorTime;
        private bool moveSnapped;
        private int previousRow;
        private int nextRow;
        private (int row, int beat)? moveTarget;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OverlayColourProvider colourProvider)
        {
            adjustHitObjects = config.GetBindable<bool>(OsuSetting.EditorAdjustExistingObjectsOnTimingChanges);
            waveformStyle = config.GetBindable<EditorWaveformStyle>(OsuSetting.SlopEditorWaveformStyle);
            waveformOpacity = config.GetBindable<float>(OsuSetting.EditorWaveformOpacity);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Bottom = status_height },
                    Child = rowContainer = new Container<RowDisplay>
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                    },
                },
                new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = status_height,
                    Padding = new MarginPadding { Horizontal = 8 },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Right = 64 },
                            Child = statusText = new TruncatingSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                RelativeSizeAxes = Axes.X,
                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                            },
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(4, 0),
                            Children = new Drawable[]
                            {
                                new GuideIcon(),
                                panelsButton = new IconButton
                                {
                                    Icon = FontAwesome.Solid.SlidersH,
                                    Size = new Vector2(26),
                                    IconScale = new Vector2(0.8f),
                                    Action = () => ShowPanels.Toggle(),
                                },
                            }
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;

            editorClock.TrackChanged += onTrackChanged;
            waveformStyle.BindValueChanged(_ =>
            {
                foreach (var display in rowDisplays)
                    applyWaveformStyle(display);
            });

            ShowPanels.BindValueChanged(show => panelsButton.TooltipText = show.NewValue ? SlopEditorStrings.HideTimingPointPanels : SlopEditorStrings.ShowTimingPointPanels, true);
        }

        [Resolved]
        private GlobalActionContainer? globalActions { get; set; }

        /// <summary>
        /// How many rows fit into the height of the view, changed by zooming.
        /// </summary>
        private int visibleRowCount = default_visible_rows;

        private float rowHeight => Math.Max(1, rowContainer.DrawHeight) / visibleRowCount;

        private bool shiftPressed => inputManager.CurrentState.Keyboard.ShiftPressed;
        private bool controlPressed => inputManager.CurrentState.Keyboard.ControlPressed;
        private bool altPressed => inputManager.CurrentState.Keyboard.AltPressed;

        private float areaWidth => Math.Max(1, rowContainer.DrawWidth - gutter_width);

        /// <summary>
        /// The position of a fraction of a bar within the area of a row, in pixels.
        /// </summary>
        private float pixelAt(double fraction) => (float)((fraction + overlap) / (1 + 2 * overlap) * areaWidth);

        private double millisecondsPerPixel(MeasureRow row) => (1 + 2 * overlap) * row.Duration / areaWidth;

        #region Layout

        private readonly record struct MeasureRow(double Start, double Duration, double End, double BeatLength, int Meter, TimingControlPoint? Point, bool IsSectionStart)
        {
            /// <summary>
            /// The number of beats in this row, which is less than <see cref="Meter"/> if the next timing point cuts the bar short.
            /// </summary>
            public int Beats => VariableTiming.CountBeats(Start, End, BeatLength);

            public double FractionAt(double time) => (time - Start) / Duration;

            public double TimeAt(double fraction) => Start + fraction * Duration;
        }

        /// <summary>
        /// Lays out the bars of the track. Each timing point starts a new bar, and the last bar before a timing point may be cut short.
        /// </summary>
        private void computeRows()
        {
            rows.Clear();
            extrapolatedRows = 0;

            var points = beatmap.ControlPointInfo.TimingPoints;

            if (points.Count == 0)
            {
                addSection(0, TimingControlPoint.DEFAULT_BEAT_LENGTH, TimingControlPoint.DEFAULT.TimeSignature.Numerator, null, null);
                return;
            }

            var first = points[0];
            double firstDuration = first.BeatLength * meterOf(first);

            if (first.Time > 0 && firstDuration > 0)
            {
                extrapolatedRows = Math.Min(max_rows, (int)Math.Ceiling(first.Time / firstDuration));

                for (int i = extrapolatedRows; i > 0; i--)
                    rows.Add(new MeasureRow(first.Time - i * firstDuration, firstDuration, first.Time - (i - 1) * firstDuration, first.BeatLength, meterOf(first), first, false));
            }

            for (int i = 0; i < points.Count; i++)
                addSection(points[i].Time, points[i].BeatLength, meterOf(points[i]), points[i], i + 1 < points.Count ? points[i + 1].Time : null);

            static int meterOf(TimingControlPoint point) => Math.Max(1, point.TimeSignature.Numerator);
        }

        private void addSection(double start, double beatLength, int meter, TimingControlPoint? point, double? end)
        {
            double duration = beatLength * meter;

            if (duration <= 0)
                return;

            // the last section continues until the end of the track.
            double sectionEnd = end ?? Math.Max(editorClock.TrackLength, start + duration);

            for (int i = 0; rows.Count < max_rows; i++)
            {
                double rowStart = start + i * duration;

                if (rowStart >= sectionEnd - 0.5)
                    break;

                double rowEnd = end == null ? rowStart + duration : Math.Min(rowStart + duration, end.Value);
                rows.Add(new MeasureRow(rowStart, duration, rowEnd, beatLength, meter, point, i == 0 && point != null));
            }
        }

        private int rowIndexAt(double time)
        {
            int low = 0;
            int high = rows.Count - 1;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;

                if (rows[mid].Start <= time)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low;
        }

        private static int findSectionStart(List<MeasureRow> rowList, TimingControlPoint point)
        {
            for (int i = 0; i < rowList.Count; i++)
            {
                if (rowList[i].IsSectionStart && rowList[i].Point == point)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Finds the row and the fraction of its bar at a screen space position.
        /// </summary>
        private (int index, double fraction)? positionAt(Vector2 screenSpacePosition)
        {
            Vector2 local = rowContainer.ToLocalSpace(screenSpacePosition);
            int index = (int)Math.Floor(local.Y / rowHeight + scroll);
            double fraction = (local.X - gutter_width) / areaWidth * (1 + 2 * overlap) - overlap;

            if (index < 0 || index >= rows.Count || local.Y < 0 || local.Y > rowContainer.DrawHeight || fraction < -overlap || fraction > 1 + overlap)
                return null;

            return (index, fraction);
        }

        /// <summary>
        /// Finds the timing point displayed closest to a position in a row, if within <see cref="marker_grab_distance"/>.
        /// </summary>
        private TimingControlPoint? findPointAt(int index, double fraction)
        {
            var row = rows[index];
            TimingControlPoint? closest = null;
            float closestDistance = marker_grab_distance;

            foreach (var point in beatmap.ControlPointInfo.TimingPoints)
            {
                float distance = Math.Abs(pixelAt(row.FractionAt(point.Time)) - pixelAt(fraction));

                if (distance <= closestDistance)
                {
                    closest = point;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        #endregion

        #region Display

        protected override void Update()
        {
            base.Update();

            computeRows();
            updateScroll();
            updateHover();

            float screenWidth = rowContainer.ScreenSpaceDrawQuad.Width;

            if (screenWidth > 0)
                pixelWidth = rowContainer.DrawWidth / screenWidth;

            int firstIndex = (int)Math.Floor(scroll);
            int count = visibleRowCount + 1;

            while (rowDisplays.Count < count)
                createRowDisplay();

            for (int i = 0; i < rowDisplays.Count; i++)
            {
                var display = rowDisplays[i];

                // every display keeps showing the same row while scrolling, such that its cached waveform only has to be redrawn for newly visible rows.
                int index = firstIndex + mod(i - firstIndex, rowDisplays.Count);

                if (index >= firstIndex + count || index < 0 || index >= rows.Count)
                {
                    display.Alpha = 0;
                    continue;
                }

                display.Alpha = 1;
                display.Y = (float)((index - scroll) * rowHeight);
                display.Height = rowHeight;
                updateRowDisplay(display, index, rows[index]);
            }

            static int mod(int value, int divisor) => (value % divisor + divisor) % divisor;
        }

        private void updateScroll()
        {
            double time = editorClock.CurrentTime;

            // follow the playback and seeks, but keep the position scrolled to by the user otherwise.
            if (activeMove == null && rows.Count > 0 && (editorClock.IsRunning || time != lastClockTime))
            {
                targetScroll = rowIndexAt(time) - visibleRowCount / 3;
            }

            lastClockTime = time;

            targetScroll = Math.Clamp(targetScroll, 0, Math.Max(0, rows.Count - 1));
            scroll = Interpolation.DampContinuously(scroll, targetScroll, 40, Time.Elapsed);

            if (Math.Abs(scroll - targetScroll) < 0.001)
                scroll = targetScroll;
        }

        private void updateHover()
        {
            hoverTime = null;

            string? status = null;

            if (activeMove != null)
            {
                var point = activeMove.Point;
                hoverTime = isOffsetDrag ? point.Time : moveAnchorTime;

                status = $"{bpm(point.BeatLength)}";

                if (activeMove.Previous != null)
                    status += $" · previous: {bpm(activeMove.Previous.BeatLength)} ({activeMove.PreviousBeats} beats)";

                if (!isOffsetDrag && activeMove.AnchorBeat > 0)
                    status += $" · beat {activeMove.AnchorBeat + 1} of the bar";
            }
            else if (IsHovered && positionAt(inputManager.CurrentState.Mouse.Position) is var (index, fraction))
            {
                var row = rows[index];

                if (findPointAt(index, fraction) is TimingControlPoint point)
                {
                    hoverTime = point.Time;
                    status = $"{bpm(point.BeatLength)} · drag onto a beat, Ctrl+drag to change the offset, right-click to remove";
                }
                else
                {
                    var (time, snapped) = snapToOnset(row.TimeAt(fraction), row);
                    hoverTime = time;
                    status = $"Bar {index - extrapolatedRows}, beat {fraction * row.Meter + 1:0.00}" + (snapped ? " · snapped to onset" : string.Empty);
                }
            }

            if (rejection != null && Time.Current - rejectionTime < rejection_display_duration)
            {
                statusText.Text = rejection;
                statusText.Colour = colours.Red1;
            }
            else
            {
                rejection = null;
                statusText.Text = status ?? string.Empty;
                statusText.Colour = Color4.White;
            }
        }

        private void createRowDisplay()
        {
            var display = new RowDisplay(colours);

            display.Waveform.Waveform = workingBeatmap.Value.Waveform;
            applyWaveformStyle(display);

            rowDisplays.Add(display);
            rowContainer.Add(display);
        }

        private void onTrackChanged()
        {
            foreach (var display in rowDisplays)
            {
                display.Waveform.Waveform = workingBeatmap.Value.Waveform;
                display.StyledWaveformLoaded = false;
                applyWaveformStyle(display);
            }
        }

        /// <summary>
        /// Displays the waveform style selected in the settings, like the timeline does.
        /// </summary>
        private void applyWaveformStyle(RowDisplay display)
        {
            var style = waveformStyle.Value;

            display.StyledWaveform.Style = style;

            if (style != EditorWaveformStyle.Default && !display.StyledWaveformLoaded)
            {
                display.StyledWaveformLoaded = true;
                display.StyledWaveform.LoadTrack(workingBeatmap.Value);
            }

            display.Waveform.Alpha = style == EditorWaveformStyle.Default ? 1 : 0;
            display.StyledWaveform.Alpha = style == EditorWaveformStyle.Default ? 0 : 1;
            display.WaveformBuffer.ForceRedraw();
        }

        private void updateRowDisplay(RowDisplay display, int index, MeasureRow row)
        {
            display.IndexText.Text = (index - extrapolatedRows).ToString();
            display.IndexText.Colour = row.IsSectionStart ? colours.Red1 : Color4.White.Opacity(0.6f);

            // the waveforms span the whole track and are positioned and scaled such that this bar fills the row.
            double trackLength = workingBeatmap.Value.Track.Length;

            if (trackLength > 0)
            {
                display.WaveformBuffer.Alpha = waveformOpacity.Value;
                display.SetWaveformLayout(
                    pixelAt(row.FractionAt(-Editor.WAVEFORM_VISUAL_OFFSET)),
                    (float)(trackLength * waveform_pixels_per_millisecond),
                    (float)(1 / (millisecondsPerPixel(row) * waveform_pixels_per_millisecond)));
                display.StyledWaveform.TrackLength = trackLength;
            }
            else
                display.WaveformBuffer.Alpha = 0;

            double cut = row.FractionAt(row.End);
            display.CutShade.X = (float)cut;
            display.CutShade.Width = (float)(1 + overlap - cut);

            updateTicks(display, row);
            updateMarkers(display, row);

            display.BpmText.Alpha = row.IsSectionStart ? 1 : 0;

            if (row.IsSectionStart)
                display.BpmText.Text = $"{bpm(row.BeatLength)} {row.Meter}/4";

            showLine(display.PlaybackLine, row, editorClock.CurrentTime);
            showLine(display.HoverLine, row, hoverTime);
        }

        private static void showLine(Box line, MeasureRow row, double? time)
        {
            double fraction = time == null ? double.NaN : row.FractionAt(time.Value);

            if (double.IsNaN(fraction) || fraction < -overlap || fraction > 1 + overlap)
            {
                line.Alpha = 0;
                return;
            }

            line.Alpha = 1;
            line.X = (float)fraction;
        }

        private void updateTicks(RowDisplay display, MeasureRow row)
        {
            int divisor = Math.Max(1, beatDivisor.Value);
            int ticksPerBar = row.Meter * divisor;

            int firstTick = (int)Math.Ceiling(-overlap * ticksPerBar);
            int lastTick = (int)Math.Floor((1 + overlap) * ticksPerBar);
            int used = 0;

            for (int tick = firstTick; tick <= lastTick; tick++)
            {
                int indexInBar = ((tick % ticksPerBar) + ticksPerBar) % ticksPerBar;
                bool isBarLine = indexInBar == 0;
                bool isBeat = indexInBar % divisor == 0;

                var line = display.GetTick(used++);
                int tickDivisor = BindableBeatDivisor.GetDivisorForBeatIndex(indexInBar, divisor);

                // one screen pixel wide, like the ticks of the timeline.
                line.X = (float)tick / ticksPerBar;
                line.Width = pixelWidth;
                line.Height = isBarLine ? 1 : BindableBeatDivisor.GetSize(tickDivisor).Y;
                line.Colour = isBarLine ? Color4.White : BindableBeatDivisor.GetColourFor(tickDivisor, colours);
                line.Alpha = 1;
            }

            display.HideTicksFrom(used);
        }

        private void updateMarkers(RowDisplay display, MeasureRow row)
        {
            int used = 0;

            foreach (var point in beatmap.ControlPointInfo.TimingPoints)
            {
                double fraction = row.FractionAt(point.Time);

                if (fraction < -overlap || fraction > 1 + overlap)
                    continue;

                bool isSelected = selectedGroup.Value?.Time == point.Time;
                var marker = display.GetMarker(used++);

                marker.X = (float)fraction;
                marker.Width = 2 * pixelWidth;
                marker.Colour = isSelected ? colours.RedLighter : colours.Red1;
            }

            display.HideMarkersFrom(used);
        }

        private partial class RowDisplay : CompositeDrawable
        {
            public readonly WaveformBuffer WaveformBuffer;
            public readonly WaveformGraph Waveform;
            public readonly EditorWaveformGraph StyledWaveform;
            public readonly Box CutShade;
            public readonly Box PlaybackLine;
            public readonly Box HoverLine;
            public readonly OsuSpriteText IndexText;
            public readonly OsuSpriteText BpmText;

            /// <summary>
            /// Whether the audio of the current track has been passed to <see cref="StyledWaveform"/>.
            /// </summary>
            public bool StyledWaveformLoaded;

            private readonly Container<Box> ticks;
            private readonly Container<Box> markers;

            public RowDisplay(OsuColour colours)
            {
                RelativeSizeAxes = Axes.X;

                InternalChildren = new Drawable[]
                {
                    IndexText = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 8,
                        Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = gutter_width, Vertical = 1 },
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black.Opacity(0.3f),
                                },
                                // faded by the waveform opacity of the timeline, such that the beat lines remain visible on top of loud parts.
                                WaveformBuffer = new WaveformBuffer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        Waveform = new WaveformGraph
                                        {
                                            RelativeSizeAxes = Axes.Y,
                                            BaseColour = colours.Blue.Opacity(0.2f),
                                            LowColour = colours.BlueLighter,
                                            MidColour = colours.BlueDark,
                                            HighColour = colours.BlueDarker,
                                        },
                                        StyledWaveform = new EditorWaveformGraph
                                        {
                                            RelativeSizeAxes = Axes.Y,
                                            Alpha = 0,
                                        },
                                    }
                                },
                                // positions within the grid are fractions of the bar.
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    RelativeChildOffset = new Vector2(-overlap, 0),
                                    RelativeChildSize = new Vector2(1 + 2 * overlap, 1),
                                    Children = new Drawable[]
                                    {
                                        // the overlapping parts of the neighbouring bars, and the part of the bar after the next timing point.
                                        new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            RelativePositionAxes = Axes.X,
                                            X = -overlap,
                                            Width = overlap,
                                            Colour = Color4.Black.Opacity(0.45f),
                                        },
                                        CutShade = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            RelativePositionAxes = Axes.X,
                                            Colour = Color4.Black.Opacity(0.45f),
                                        },
                                        // relatively positioned, such that the overlap offset applies to them.
                                        ticks = new Container<Box> { RelativeSizeAxes = Axes.Both, RelativePositionAxes = Axes.Both },
                                        markers = new Container<Box> { RelativeSizeAxes = Axes.Both, RelativePositionAxes = Axes.Both },
                                        HoverLine = createLine(colours.Yellow),
                                        PlaybackLine = createLine(Color4.White),
                                        BpmText = new OsuSpriteText
                                        {
                                            RelativePositionAxes = Axes.X,
                                            Margin = new MarginPadding { Left = 6, Top = 2 },
                                            Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                                            Colour = colours.Red1,
                                        },
                                    }
                                },
                            }
                        }
                    },
                };
            }

            /// <summary>
            /// Creates a line as thin as the centre marker of the timeline.
            /// </summary>
            private static Box createLine(Color4 colour) => new Box
            {
                RelativeSizeAxes = Axes.Y,
                RelativePositionAxes = Axes.X,
                Origin = Anchor.TopCentre,
                Width = 1.4f,
                EdgeSmoothness = new Vector2(1, 0),
                Colour = colour,
                Alpha = 0,
            };

            /// <summary>
            /// Positions and scales both waveforms, redrawing them if anything changed.
            /// </summary>
            public void SetWaveformLayout(float x, float width, float scale)
            {
                if (Waveform.X == x && Waveform.Width == width && Waveform.Scale.X == scale)
                    return;

                foreach (var waveform in new Drawable[] { Waveform, StyledWaveform })
                {
                    waveform.X = x;
                    waveform.Width = width;
                    waveform.Scale = new Vector2(scale, 1);
                }

                WaveformBuffer.ForceRedraw();
            }

            public Box GetTick(int index) => getLine(ticks, index);

            public Box GetMarker(int index)
            {
                var marker = getLine(markers, index);
                marker.Alpha = 1;
                return marker;
            }

            public void HideTicksFrom(int index) => hideFrom(ticks, index);

            public void HideMarkersFrom(int index) => hideFrom(markers, index);

            private static Box getLine(Container<Box> container, int index)
            {
                while (container.Count <= index)
                {
                    container.Add(new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        RelativePositionAxes = Axes.X,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                    });
                }

                return container[index];
            }

            private static void hideFrom(Container<Box> container, int index)
            {
                for (int i = index; i < container.Count; i++)
                    container[i].Alpha = 0;
            }
        }

        /// <summary>
        /// Caches the drawn waveforms of a row, as drawing the waveforms of all rows every frame is too slow.
        /// Redrawn when the waveforms are moved (see <see cref="RowDisplay.SetWaveformLayout"/>) or invalidate their draw node (e.g. once loaded).
        /// </summary>
        private partial class WaveformBuffer : BufferedContainer
        {
            public WaveformBuffer()
                : base(cachedFrameBuffer: true)
            {
            }

            // the waveforms load and resample asynchronously, which is finished in their update.
            protected override bool RequiresChildrenUpdate => true;
        }

        /// <summary>
        /// Explains the controls of the display and how to time a song with a varying tempo in its tooltip.
        /// </summary>
        private partial class GuideIcon : CompositeDrawable, IHasHitsoundTooltip
        {
            public LocalisableString TooltipText => SlopEditorStrings.TimingGuide;

            public GuideIcon()
            {
                Size = new Vector2(26);

                InternalChild = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Icon = FontAwesome.Solid.QuestionCircle,
                };
            }
        }

        #endregion

        #region Placement

        private Func<double, double, double?>? estimateBeatLength => analyser?.Envelope != null ? analyser.Envelope.EstimateBeatLength : null;

        /// <summary>
        /// Snaps a time to the closest onset in the audio, unless Shift is held.
        /// </summary>
        private (double time, bool snapped) snapToOnset(double time, MeasureRow row)
        {
            var envelope = analyser?.Envelope;

            if (envelope == null || shiftPressed)
                return (Math.Round(time), false);

            double radius = Math.Clamp(VariableTimingOverlay.ONSET_SNAP_DISTANCE * millisecondsPerPixel(row),
                VariableTimingOverlay.MIN_ONSET_SNAP_RADIUS, VariableTimingOverlay.MAX_ONSET_SNAP_RADIUS);
            double? onset = envelope.FindOnset(time, radius);

            return onset == null ? (Math.Round(time), false) : (onset.Value, true);
        }

        /// <summary>
        /// Fits a beat which was snapped to an onset to the onsets of all beats since <paramref name="start"/> (see <see cref="OnsetEnvelope.FitAnchor"/>).
        /// </summary>
        private double fitToBeats(double anchorTime, double? start, int beats, bool snapped)
        {
            var envelope = analyser?.Envelope;

            if (envelope == null || !snapped || start == null)
                return anchorTime;

            return envelope.FitAnchor(start.Value, beats, anchorTime, out _);
        }

        private VariableTiming.PlacementPreview calculatePlacement(double time, bool snapped)
        {
            var preview = VariableTiming.CalculatePlacement(beatmap.ControlPointInfo, time, estimateBeatLength);

            double fitted = fitToBeats(time, preview.Previous?.Time, preview.PreviousBeats + preview.AnchorBeat, snapped);

            if (fitted != time)
                preview = VariableTiming.CalculatePlacement(beatmap.ControlPointInfo, fitted, estimateBeatLength);

            return preview;
        }

        /// <summary>
        /// Whether a placement can be made, showing why not otherwise.
        /// </summary>
        private bool canPlace(VariableTiming.PlacementPreview preview)
        {
            if (altPressed || VariableTiming.FindLargeTempoChange(beatmap.ControlPointInfo, preview) is not var (oldBeatLength, newBeatLength))
                return true;

            double percentage = (oldBeatLength / newBeatLength - 1) * 100;

            rejection = $"Not placed: changes {bpm(oldBeatLength)} to {bpm(newBeatLength)} ({percentage:+0;-0}%), probably not on the right beat. Hold Alt to place anyway";
            rejectionTime = Time.Current;
            return false;
        }

        private void place(double time, bool snapped)
        {
            var preview = calculatePlacement(time, snapped);

            if (!canPlace(preview))
                return;

            beatmap.BeginChange();

            var move = VariableTiming.TimingPointMove.Place(beatmap, preview.AnchorTime, adjustHitObjects.Value, estimateBeatLength);

            beatmap.UpdateAllHitObjects();
            beatmap.EndChange();

            selectedGroup.Value = beatmap.ControlPointInfo.GroupAt(move.Point.Time);
        }

        /// <summary>
        /// Moves a timing point by a few milliseconds, recalculating the BPMs of it and the preceding timing point.
        /// </summary>
        private void nudge(TimingControlPoint point, double amount)
        {
            beatmap.BeginChange();

            var move = VariableTiming.TimingPointMove.Begin(beatmap, point, adjustHitObjects.Value);
            move.MoveTo(point.Time + amount);

            beatmap.UpdateAllHitObjects();
            beatmap.EndChange();

            selectedGroup.Value = beatmap.ControlPointInfo.GroupAt(move.Point.Time);
        }

        #endregion

        #region Input

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (activeMove != null || positionAt(e.ScreenSpaceMouseDownPosition) is not var (index, fraction))
                return false;

            var row = rows[index];
            var point = findPointAt(index, fraction);

            switch (e.Button)
            {
                case MouseButton.Left:
                    pressedRows = new List<MeasureRow>(rows);
                    pressedPoint = point;
                    (pressedTime, pressedSnapped) = snapToOnset(row.TimeAt(fraction), row);
                    dragStarted = false;
                    return true;

                case MouseButton.Right:
                    if (point != null)
                    {
                        VariableTiming.RemoveTimingPoint(beatmap, point, adjustHitObjects.Value);

                        if (selectedGroup.Value != null && !beatmap.ControlPointInfo.Groups.Contains(selectedGroup.Value))
                            selectedGroup.Value = null;
                    }
                    else
                        editorClock.SeekSmoothlyTo(row.TimeAt(fraction));

                    return true;
            }

            return false;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left || pressedRows == null)
                return false;

            dragStarted = true;

            if (controlPressed)
            {
                if (pressedPoint == null)
                    return false;

                beatmap.BeginChange();
                activeMove = VariableTiming.TimingPointMove.Begin(beatmap, pressedPoint, adjustHitObjects.Value);
                isOffsetDrag = true;
                moveAnchorTime = pressedPoint.Time;
                selectMovedGroup();
                return true;
            }

            if (pressedPoint != null)
            {
                beatmap.BeginChange();
                activeMove = VariableTiming.TimingPointMove.Begin(beatmap, pressedPoint, adjustHitObjects.Value);
                moveAnchorTime = pressedPoint.Time;
                moveSnapped = false;
            }
            else
            {
                var preview = calculatePlacement(pressedTime, pressedSnapped);

                if (!canPlace(preview))
                    return false;

                beatmap.BeginChange();
                activeMove = VariableTiming.TimingPointMove.Place(beatmap, preview.AnchorTime, adjustHitObjects.Value, estimateBeatLength);

                // the audio under the cursor stays where it is, and is moved onto the beat of the grid under the cursor.
                moveAnchorTime = activeMove.AnchorBeat == 0 ? activeMove.Point.Time : preview.AnchorTime;
                moveSnapped = pressedSnapped && activeMove.AnchorBeat > 0;
            }

            isOffsetDrag = false;
            moveTarget = null;
            previousRow = activeMove.Previous == null ? -1 : findSectionStart(pressedRows, activeMove.Previous);
            nextRow = activeMove.Next == null ? pressedRows.Count : findSectionStart(pressedRows, activeMove.Next);

            selectMovedGroup();
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (activeMove == null || pressedRows == null)
                return;

            if (isOffsetDrag)
            {
                activeMove.MoveTo(moveAnchorTime + (e.MousePosition.X - e.MouseDownPosition.X) * offset_drag_milliseconds_per_pixel);
                selectMovedGroup();
                return;
            }

            if (beatTargetAt(pressedRows, e.ScreenSpaceMousePosition) is not var (row, beat) || moveTarget == (row, beat))
                return;

            int previousBeats = 0;
            int nextBeats = 0;

            if (activeMove.Previous != null)
            {
                for (int i = previousRow; i < row; i++)
                    previousBeats += pressedRows[i].Beats;
            }

            if (activeMove.Next != null)
            {
                for (int i = row; i < nextRow; i++)
                    nextBeats += pressedRows[i].Beats;

                if (beat >= nextBeats)
                    return;
            }

            moveTarget = (row, beat);

            activeMove.SetBeats(previousBeats, beat, nextBeats);
            activeMove.MoveTo(fitToBeats(moveAnchorTime, activeMove.Previous?.Time, previousBeats + beat, moveSnapped));
            selectMovedGroup();
        }

        /// <summary>
        /// Finds the beat of <paramref name="rowList"/> closest to a screen space position, between the timing points surrounding the moved one.
        /// </summary>
        private (int row, int beat)? beatTargetAt(List<MeasureRow> rowList, Vector2 screenSpacePosition)
        {
            Vector2 local = rowContainer.ToLocalSpace(screenSpacePosition);
            int row = (int)Math.Floor(local.Y / rowHeight + scroll);

            if (row < 0 || row >= rowList.Count)
                return null;

            double fraction = (local.X - gutter_width) / areaWidth * (1 + 2 * overlap) - overlap;
            int beat = (int)Math.Round(fraction * rowList[row].Meter);

            // beats in the overlapping parts belong to the neighbouring rows.
            while (beat < 0 && row > 0)
                beat += rowList[--row].Beats;

            while (row < rowList.Count && beat >= rowList[row].Beats)
                beat -= rowList[row++].Beats;

            if (beat < 0 || row >= rowList.Count)
                return null;

            // the moved timing point stays between its neighbours.
            if (row <= previousRow || row >= nextRow)
                return null;

            return (row, beat);
        }

        protected override void OnDragEnd(DragEndEvent e) => endMove();

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button == MouseButton.Left && pressedRows != null)
            {
                if (activeMove != null)
                    endMove();
                else if (!dragStarted)
                {
                    if (pressedPoint != null)
                        selectedGroup.Value = beatmap.ControlPointInfo.GroupAt(pressedPoint.Time);
                    else
                        place(pressedTime, pressedSnapped);
                }

                pressedRows = null;
                pressedPoint = null;
            }

            base.OnMouseUp(e);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (ZoomableScrollContainer.GetZoomScrollAmount(e, globalActions) is float zoom)
            {
                // zooming in shows fewer, bigger rows, keeping the row under the cursor in place.
                float localY = Math.Clamp(rowContainer.ToLocalSpace(e.ScreenSpaceMousePosition).Y, 0, rowContainer.DrawHeight);
                double anchor = localY / rowHeight + scroll;

                visibleRowCount = Math.Clamp(visibleRowCount - Math.Sign(zoom), min_visible_rows, max_visible_rows);
                scroll = targetScroll = Math.Max(0, anchor - localY / rowHeight);
                return true;
            }

            float delta = e.ScrollDelta.Y != 0 ? e.ScrollDelta.Y : e.ScrollDelta.X;

            if (controlPressed)
            {
                if (activeMove == null && positionAt(e.ScreenSpaceMousePosition) is var (index, _) && rows[index].Point is TimingControlPoint point && delta != 0)
                    nudge(point, Math.Sign(delta) * (altPressed ? 10 : 1));

                return true;
            }

            targetScroll -= delta;
            return true;
        }

        private void endMove()
        {
            if (activeMove == null)
                return;

            activeMove = null;
            beatmap.UpdateAllHitObjects();
            beatmap.EndChange();
        }

        private void selectMovedGroup()
        {
            if (activeMove != null)
                selectedGroup.Value = beatmap.ControlPointInfo.GroupAt(activeMove.Point.Time);
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorClock.IsNotNull())
                editorClock.TrackChanged -= onTrackChanged;
        }

        private static string bpm(double beatLength) => $"{60000 / beatLength:0.###} BPM";
    }
}
