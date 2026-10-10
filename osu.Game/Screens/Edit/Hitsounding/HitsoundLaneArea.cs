// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The lanes of the hitsound editor, on which hitsounds are toggled by clicking, and painted by dragging along a lane.
    /// </summary>
    public partial class HitsoundLaneArea : HitsoundTimelinePart, IHasHitsoundTooltip
    {
        /// <summary>
        /// The maximum number of snapped times at which hitsounds are created during a single movement while painting.
        /// </summary>
        private const int max_creations_per_movement = 256;

        private readonly Container rowContainer;
        private readonly Container<HitsoundBodyPiece> bodyContainer;
        private readonly Container<HitsoundColumnPiece> columnContainer;
        private readonly Container hoverMarker;
        private readonly Box hoverMarkerFill;

        private readonly Dictionary<int, HitsoundColumnPiece> visibleColumns = new Dictionary<int, HitsoundColumnPiece>();
        private readonly Dictionary<HitsoundBody, HitsoundBodyPiece> visibleBodies = new Dictionary<HitsoundBody, HitsoundBodyPiece>();
        private readonly HashSet<int> wantedColumns = new HashSet<int>();
        private readonly HashSet<HitsoundBody> wantedBodies = new HashSet<HitsoundBody>();
        private readonly Stack<HitsoundColumnPiece> freeColumns = new Stack<HitsoundColumnPiece>();
        private readonly Stack<HitsoundBodyPiece> freeBodies = new Stack<HitsoundBodyPiece>();

        private HitsoundMap? displayedMap;
        private bool displayInvalidated = true;
        private float lastLaneHeight;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public HitsoundLaneArea()
        {
            InternalChildren = new Drawable[]
            {
                rowContainer = new Container { RelativeSizeAxes = Axes.Both },
                new HitsoundTicks(0.6f),
                bodyContainer = new Container<HitsoundBodyPiece> { RelativeSizeAxes = Axes.Both },
                columnContainer = new Container<HitsoundColumnPiece> { RelativeSizeAxes = Axes.Both },
                hoverMarker = new Container
                {
                    Origin = Anchor.Centre,
                    Masking = true,
                    CornerRadius = 3,
                    BorderThickness = 2,
                    Alpha = 0,
                    Child = hoverMarkerFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.25f,
                    },
                },
                new HitsoundSelectionBoxDisplay { VerticalRange = getBoxVerticalRange },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            HitsoundEditor.VisibleLanesChanged += onLanesChanged;
            HitsoundEditor.SelectionChanged += invalidateDisplay;
            HitsoundEditor.MutedLanes.BindCollectionChanged((_, _) => invalidateDisplay());
            HitsoundEditor.HitsoundDifficultyMode.BindValueChanged(_ => invalidateDisplay());

            onLanesChanged();
        }

        private void invalidateDisplay() => displayInvalidated = true;

        private void onLanesChanged()
        {
            rowContainer.Clear();

            var lanes = HitsoundEditor.VisibleLanes;

            for (int i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                bool firstOfBank = i == 0 || lanes[i - 1].Bank != lane.Bank;

                rowContainer.Add(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.Y,
                    Y = (float)i / lanes.Count,
                    Height = 1f / lanes.Count,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            // sub-lanes are darker, such that they are distinguishable from lanes.
                            Colour = lane.IsSubLane ? Colour4.Black : HitsoundLane.GetBankColour(lane.Bank),
                            Alpha = lane.IsSubLane ? 0.15f : i % 2 == 0 ? 0.06f : 0.03f,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = firstOfBank ? 2 : 1,
                            Colour = firstOfBank ? colourProvider.Background1 : colourProvider.Background3,
                            Alpha = i == 0 ? 0 : 1,
                        },
                    }
                });
            }

            invalidateDisplay();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (HitsoundEditor.IsNotNull())
            {
                HitsoundEditor.VisibleLanesChanged -= onLanesChanged;
                HitsoundEditor.SelectionChanged -= invalidateDisplay;
            }

            base.Dispose(isDisposing);
        }

        #region Display

        private float laneHeight => DrawHeight / Math.Max(1, HitsoundEditor.VisibleLanes.Count);

        protected override void Update()
        {
            base.Update();

            var map = HitsoundEditor.Map;

            if (map != displayedMap || laneHeight != lastLaneHeight)
            {
                displayedMap = map;
                lastLaneHeight = laneHeight;
                displayInvalidated = true;
            }

            var (start, end) = GetVisibleRange(INTERACTION_DISTANCE * 2);

            updateBodies(map, start, end);
            updateColumns(map, start, end);

            displayInvalidated = false;

            updateHover();
            updatePainting();
        }

        private void updateColumns(HitsoundMap map, double start, double end)
        {
            wantedColumns.Clear();

            foreach (var column in map.GetColumnsInRange(start, end))
            {
                wantedColumns.Add(column.Key);

                if (!visibleColumns.TryGetValue(column.Key, out var piece))
                {
                    piece = freeColumns.Count > 0 ? freeColumns.Pop() : addColumnPiece();
                    visibleColumns[column.Key] = piece;
                    applyColumn(piece, column);
                }
                else if (displayInvalidated)
                    applyColumn(piece, column);

                piece.X = TimeToX(column.Time);
            }

            foreach (int key in visibleColumns.Keys.Where(k => !wantedColumns.Contains(k)).ToArray())
            {
                visibleColumns[key].Alpha = 0;
                freeColumns.Push(visibleColumns[key]);
                visibleColumns.Remove(key);
            }
        }

        private HitsoundColumnPiece addColumnPiece()
        {
            var piece = new HitsoundColumnPiece();
            columnContainer.Add(piece);
            return piece;
        }

        private void applyColumn(HitsoundColumnPiece piece, HitsoundColumn column)
            => piece.Apply(column, HitsoundEditor.VisibleLanes, HitsoundEditor.IsSelected(column.Key), HitsoundEditor.MutedLanes, laneHeight);

        private void updateBodies(HitsoundMap map, double start, double end)
        {
            wantedBodies.Clear();

            foreach (var body in map.Bodies)
            {
                if (body.StartTime > end || body.EndTime < start)
                    continue;

                wantedBodies.Add(body);

                if (!visibleBodies.TryGetValue(body, out var piece))
                {
                    piece = freeBodies.Count > 0 ? freeBodies.Pop() : addBodyPiece();
                    visibleBodies[body] = piece;
                    piece.Apply(body, HitsoundEditor.VisibleLanes, laneHeight);
                }
                else if (displayInvalidated)
                    piece.Apply(body, HitsoundEditor.VisibleLanes, laneHeight);

                piece.X = TimeToX(body.StartTime);
                piece.Width = Math.Max(1, (float)((body.EndTime - body.StartTime) * Timeline.CurrentZoom));
            }

            foreach (var body in visibleBodies.Keys.Where(b => !wantedBodies.Contains(b)).ToArray())
            {
                visibleBodies[body].Alpha = 0;
                freeBodies.Push(visibleBodies[body]);
                visibleBodies.Remove(body);
            }
        }

        private HitsoundBodyPiece addBodyPiece()
        {
            var piece = new HitsoundBodyPiece();
            bodyContainer.Add(piece);
            return piece;
        }

        #endregion

        #region Hover

        private Vector2? hoverPosition;

        private HitsoundColumn? hoveredColumn;

        protected override bool OnHover(HoverEvent e)
        {
            hoverPosition = GetMousePosition(e);
            return true;
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            hoverPosition = GetMousePosition(e);

            if (paintButton == MouseButton.Right)
                paintTo(hoverPosition.Value);

            return base.OnMouseMove(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverPosition = null;
            base.OnHoverLost(e);
        }

        private void updateHover()
        {
            hoveredColumn = null;

            if (hoverPosition is not Vector2 position || laneAt(position.Y) is not HitsoundLane lane || selectingBox)
            {
                hoverMarker.Alpha = 0;
                return;
            }

            int laneIndex = indexOf(lane);
            float? x = null;

            hoveredColumn = FindColumnAt(position.X);

            if (hoveredColumn != null)
                x = TimeToX(hoveredColumn.Time);
            else if (HitsoundEditor.Map.FindBody(XToTime(position.X)) != null && HitsoundBody.SupportsLane(lane))
                x = position.X;
            else if (HitsoundEditor.HitsoundDifficultyMode.Value)
                x = TimeToX(EditorBeatmap.SnapTime(XToTime(position.X), null));

            if (x == null)
            {
                hoverMarker.Alpha = 0;
                return;
            }

            var colour = HitsoundLane.GetBankColour(lane.Bank);
            float size = Math.Min(laneHeight - 4, 22);

            hoverMarker.Alpha = 1;
            hoverMarker.Size = new Vector2(size);
            hoverMarker.Position = new Vector2(x.Value, (laneIndex + 0.5f) * laneHeight);
            hoverMarker.BorderColour = colour;
            hoverMarkerFill.Colour = colour;
        }

        public LocalisableString TooltipText => hoveredColumn != null && paintButton == null && !selectingBox
            ? HitsoundColumnDescription.Describe(hoveredColumn)
            : default;

        #endregion

        #region Lanes

        private int indexOf(HitsoundLane lane)
        {
            var lanes = HitsoundEditor.VisibleLanes;

            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i] == lane)
                    return i;
            }

            return -1;
        }

        private HitsoundLane? laneAt(float y)
        {
            var lanes = HitsoundEditor.VisibleLanes;
            int index = (int)Math.Floor(y / laneHeight);

            return index >= 0 && index < lanes.Count ? lanes[index] : null;
        }

        #endregion

        #region Painting

        /// <summary>
        /// The button which is painting, or <c>null</c> if not painting.
        /// </summary>
        private MouseButton? paintButton;

        private HitsoundLane paintLane;

        /// <summary>
        /// Whether the sample of <see cref="paintLane"/> is turned on (or off) by painting.
        /// </summary>
        private bool paintState;

        /// <summary>
        /// Whether hitsounds are created at empty snapped times by painting, which is only done in hitsound difficulty mode when starting to paint at an empty time.
        /// </summary>
        private bool paintCreates;

        /// <summary>
        /// Whether the lane which is painted follows the cursor, as opposed to staying the lane at which painting started.
        /// </summary>
        private bool paintAcrossLanes;

        private Vector2 lastPaintPosition;

        /// <summary>
        /// The keys of the columns which have been painted on each lane during the current stroke, such that they aren't painted twice.
        /// </summary>
        private readonly HashSet<(int key, HitsoundLane lane)> paintedKeys = new HashSet<(int, HitsoundLane)>();

        private bool selectingBox;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (paintButton != null || selectingBox)
                return false;

            if (e.Button != MouseButton.Left && e.Button != MouseButton.Right)
                return false;

            var position = GetMousePosition(e);

            if (laneAt(position.Y) is not HitsoundLane lane)
                return false;

            float x = position.X;

            if (e.Button == MouseButton.Left && (e.ShiftPressed || e.ControlPressed))
            {
                // clicking toggles (ctrl) or extends (shift) the selection, dragging adds a rectangle to the selection.
                beginBoxSelection(position, additive: true, clickMode: e.ControlPressed ? BoxClickMode.Toggle : BoxClickMode.Range);
                return true;
            }

            var column = FindColumnAt(x);

            if (e.Button == MouseButton.Right)
            {
                // right dragging removes the samples of all lanes and hitsounds it passes over, like in a piano roll.
                beginPainting(MouseButton.Right, lane, false, false, position);
                paintAcrossLanes = true;

                if (column != null)
                    paintColumn(column, lane);
                else if (HitsoundEditor.Map.FindBody(XToTime(x)) is HitsoundBody rightBody && HitsoundBody.SupportsLane(lane) && rightBody.Has(lane))
                    HitsoundEditor.SetBodyLane(rightBody, lane, false);

                return true;
            }

            if (column != null)
            {
                // like in a piano roll, left clicking a sample which is already on selects or deselects its hitsound instead of turning it off.
                if (column.Has(lane))
                {
                    HitsoundEditor.ToggleSelection(column.Key);
                    Playback.PlayLane(lane, column.Volume, column.CustomIndex);
                    return true;
                }

                beginPainting(e.Button, lane, true, false, position);
                paintColumn(column, lane);
                return true;
            }

            var body = HitsoundEditor.Map.FindBody(XToTime(x));

            if (body != null && HitsoundBody.SupportsLane(lane))
            {
                if (!body.Has(lane))
                {
                    HitsoundEditor.SetBodyLane(body, lane, true);
                    Playback.PlayLane(lane, body.Volume);
                }

                return true;
            }

            // with a selection, clicking empty space deselects (or selects a rectangle when dragging) instead of creating hitsounds.
            if (HitsoundEditor.HitsoundDifficultyMode.Value && HitsoundEditor.SelectedKeys.Count == 0)
            {
                beginPainting(e.Button, lane, true, true, position);
                createAt(XToTime(x));
                return true;
            }

            beginBoxSelection(position, additive: false, clickMode: BoxClickMode.Clear);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => paintButton != null || selectingBox;

        protected override void OnDrag(DragEvent e)
        {
            var position = GetMousePosition(e);

            if (selectingBox)
                updateBoxSelection(position);
            else
                paintTo(position);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button == paintButton)
                endPainting();
            else if (e.Button == MouseButton.Left && selectingBox)
                endBoxSelection(GetMousePosition(e));

            base.OnMouseUp(e);
        }

        private void beginPainting(MouseButton button, HitsoundLane lane, bool state, bool creates, Vector2 position)
        {
            paintButton = button;
            paintLane = lane;
            paintState = state;
            paintCreates = creates;
            paintAcrossLanes = false;
            lastPaintPosition = position;
            paintedKeys.Clear();

            HitsoundEditor.BeginStroke();
        }

        private void endPainting()
        {
            paintButton = null;
            HitsoundEditor.EndStroke();
        }

        private void updatePainting()
        {
            // the stroke may have been interrupted (e.g. by losing focus), in which case the mouse up event never arrives.
            if (paintButton is MouseButton button && GetContainingInputManager()?.CurrentState.Mouse.IsPressed(button) == false)
                endPainting();
        }

        /// <summary>
        /// Paints all hitsounds between the previous and the current position of the cursor.
        /// </summary>
        private void paintTo(Vector2 position)
        {
            if (paintButton == null)
                return;

            var lastPosition = lastPaintPosition;
            float x = position.X;

            double tolerance = Timeline.PixelsToDuration(INTERACTION_DISTANCE);
            double from = XToTime(Math.Min(lastPosition.X, x));
            double to = XToTime(Math.Max(lastPosition.X, x));

            lastPaintPosition = position;

            foreach (var column in HitsoundEditor.Map.GetColumnsInRange(from - tolerance, to + tolerance).ToArray())
            {
                if (!paintAcrossLanes)
                {
                    paintColumn(column, paintLane);
                    continue;
                }

                // the cursor may have moved across lanes, so the lane is taken from where the cursor passed the hitsound.
                float columnX = TimeToX(column.Time);
                float progress = Math.Abs(x - lastPosition.X) < 1 ? 1 : Math.Clamp((columnX - lastPosition.X) / (x - lastPosition.X), 0, 1);
                float y = lastPosition.Y + (position.Y - lastPosition.Y) * progress;

                if (laneAt(Math.Clamp(y, 0, DrawHeight - 1)) is HitsoundLane lane)
                    paintColumn(column, lane);
            }

            // when moving vertically over a hitsound, all lanes in between are passed.
            if (paintAcrossLanes && FindColumnAt(x) is HitsoundColumn current)
            {
                float top = Math.Clamp(Math.Min(lastPosition.Y, position.Y), 0, DrawHeight - 1);
                float bottom = Math.Clamp(Math.Max(lastPosition.Y, position.Y), 0, DrawHeight - 1);

                for (float y = top; y < bottom + laneHeight / 2; y += laneHeight / 2)
                {
                    if (laneAt(Math.Min(y, bottom)) is HitsoundLane lane)
                        paintColumn(current, lane);
                }
            }

            if (!paintCreates)
                return;

            double time = EditorBeatmap.SnapTime(from, null);

            for (int i = 0; i < max_creations_per_movement && time <= to; i++)
            {
                if (time >= from)
                    createAt(time);

                double step = EditorBeatmap.GetBeatLengthAtTime(time);

                if (step <= 0)
                    break;

                double next = EditorBeatmap.SnapTime(time + step, null);

                // the snapped time may not advance at timing point boundaries.
                time = next > time ? next : time + step;
            }
        }

        private void paintColumn(HitsoundColumn column, HitsoundLane lane)
        {
            if (!paintedKeys.Add((column.Key, lane)) || column.Has(lane) == paintState)
                return;

            // turning off a hitnormal only deletes objects in hitsound difficulty mode, and does nothing otherwise.
            if (!lane.IsAddition && !paintState && !HitsoundEditor.HitsoundDifficultyMode.Value)
                return;

            HitsoundEditor.SetLane(column, lane, paintState);

            if (paintState)
                Playback.PlayLane(lane, column.Volume, column.CustomIndex);
        }

        private void createAt(double time)
        {
            double snapped = EditorBeatmap.SnapTime(time, null);
            var existing = HitsoundEditor.Map.FindClosestColumn(snapped, HitsoundMap.COLUMN_MERGE_DISTANCE);

            if (existing != null)
            {
                paintColumn(existing, paintLane);
                return;
            }

            if (!paintedKeys.Add(((int)Math.Floor(snapped + 0.5), paintLane)))
                return;

            if (HitsoundEditor.CreateColumn(snapped, paintLane))
            {
                var created = HitsoundEditor.Map.FindClosestColumn(snapped, HitsoundMap.COLUMN_MERGE_DISTANCE);

                if (created != null)
                {
                    paintedKeys.Add((created.Key, paintLane));
                    Playback.PlayLane(paintLane, created.Volume, created.CustomIndex);
                }
            }
        }

        #endregion

        #region Box selection

        private enum BoxClickMode
        {
            /// <summary>
            /// Clicking without dragging clears the selection.
            /// </summary>
            Clear,

            /// <summary>
            /// Clicking a hitsound without dragging adds it to or removes it from the selection.
            /// </summary>
            Toggle,

            /// <summary>
            /// Clicking a hitsound without dragging selects all hitsounds from the last clicked one to it.
            /// </summary>
            Range,
        }

        private bool boxAdditive;
        private BoxClickMode boxClickMode;
        private Vector2 boxStart;
        private Vector2 boxEnd;
        private double boxStartTime;

        private void beginBoxSelection(Vector2 position, bool additive, BoxClickMode clickMode)
        {
            selectingBox = true;
            boxAdditive = additive;
            boxClickMode = clickMode;
            boxStart = boxEnd = position;
            boxStartTime = XToTime(position.X);
        }

        private void updateBoxSelection(Vector2 position)
        {
            boxEnd = position;

            double time = XToTime(position.X);
            Timeline.SelectionBox.Value = (Math.Min(boxStartTime, time), Math.Max(boxStartTime, time));
        }

        /// <summary>
        /// The indices of the first and last lane covered by the box.
        /// </summary>
        private (int first, int last) getBoxLanes()
        {
            int count = HitsoundEditor.VisibleLanes.Count;

            int first = Math.Clamp((int)Math.Floor(Math.Min(boxStart.Y, boxEnd.Y) / laneHeight), 0, count - 1);
            int last = Math.Clamp((int)Math.Floor(Math.Max(boxStart.Y, boxEnd.Y) / laneHeight), 0, count - 1);

            return (first, last);
        }

        private (float top, float bottom)? getBoxVerticalRange()
        {
            if (!selectingBox)
                return null;

            var (first, last) = getBoxLanes();
            return (first * laneHeight, (last + 1) * laneHeight);
        }

        private void endBoxSelection(Vector2 position)
        {
            selectingBox = false;
            Timeline.SelectionBox.Value = null;

            if (Math.Abs(position.X - boxStart.X) < 3 && Math.Abs(position.Y - boxStart.Y) < 3)
            {
                handleBoxClick(position);
                return;
            }

            boxEnd = position;

            var (first, last) = getBoxLanes();
            var lanes = HitsoundEditor.VisibleLanes.Skip(first).Take(last - first + 1).ToList();

            double time = XToTime(position.X);

            // only the hitsounds which play a sample of the covered lanes are selected.
            var keys = HitsoundEditor.Map.GetColumnsInRange(Math.Min(boxStartTime, time), Math.Max(boxStartTime, time))
                                     .Where(c => lanes.Any(c.Has))
                                     .Select(c => c.Key);

            HitsoundEditor.SetSelection(keys, boxAdditive);
        }

        private void handleBoxClick(Vector2 position)
        {
            var column = FindColumnAt(position.X);

            switch (boxClickMode)
            {
                case BoxClickMode.Clear:
                    HitsoundEditor.ClearSelection();
                    break;

                case BoxClickMode.Toggle:
                    if (column != null)
                        HitsoundEditor.ToggleSelection(column.Key);
                    break;

                case BoxClickMode.Range:
                    if (column != null)
                        HitsoundEditor.SelectRangeTo(column.Key);
                    break;
            }
        }

        #endregion
    }
}
