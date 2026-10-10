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
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Displays the volumes of the hitsounds as bars, which can be drawn with the mouse like the event editor of FL Studio:
    /// left dragging draws, right dragging draws a straight line, shift + dragging levels to the volume at which the drag started,
    /// and alt + dragging resets to the volume which most hitsounds use.
    /// </summary>
    public partial class HitsoundVolumeArea : HitsoundTimelinePart, IHasHitsoundTooltip
    {
        private readonly Container<VolumePiece> pieceContainer;
        private readonly Box linePreview;

        private readonly Dictionary<int, VolumePiece> visiblePieces = new Dictionary<int, VolumePiece>();
        private readonly Stack<VolumePiece> freePieces = new Stack<VolumePiece>();
        private readonly HashSet<int> wantedKeys = new HashSet<int>();

        private HitsoundMap? displayedMap;
        private bool displayInvalidated = true;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public HitsoundVolumeArea()
        {
            pieceContainer = new Container<VolumePiece> { RelativeSizeAxes = Axes.Both };

            linePreview = new Box
            {
                Height = 2,
                Origin = Anchor.CentreLeft,
                Colour = Colour4.White,
                Alpha = 0,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var guides = new Container { RelativeSizeAxes = Axes.Both };

            foreach (float volume in new[] { 0.25f, 0.5f, 0.75f, 1 })
            {
                guides.Add(new Box
                {
                    RelativeSizeAxes = Axes.X,
                    RelativePositionAxes = Axes.Y,
                    Height = 1,
                    Y = 1 - volume,
                    Colour = colourProvider.Background1,
                    Alpha = volume == 0.5f ? 0.5f : 0.25f,
                });
            }

            InternalChildren = new Drawable[]
            {
                guides,
                new HitsoundTicks(0.3f),
                pieceContainer,
                linePreview,
                new HitsoundSelectionBoxDisplay(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            HitsoundEditor.SelectionChanged += invalidateDisplay;
        }

        private void invalidateDisplay() => displayInvalidated = true;

        protected override void Dispose(bool isDisposing)
        {
            if (HitsoundEditor.IsNotNull())
                HitsoundEditor.SelectionChanged -= invalidateDisplay;

            base.Dispose(isDisposing);
        }

        protected override void Update()
        {
            base.Update();

            var map = HitsoundEditor.Map;

            if (map != displayedMap)
            {
                displayedMap = map;
                displayInvalidated = true;
            }

            var (start, end) = GetVisibleRange(INTERACTION_DISTANCE * 2);

            wantedKeys.Clear();

            foreach (var column in map.GetColumnsInRange(start, end))
            {
                wantedKeys.Add(column.Key);

                if (!visiblePieces.TryGetValue(column.Key, out var piece))
                {
                    piece = freePieces.Count > 0 ? freePieces.Pop() : addPiece();
                    visiblePieces[column.Key] = piece;
                    piece.Apply(column, HitsoundEditor.IsSelected(column.Key));
                }
                else if (displayInvalidated)
                    piece.Apply(column, HitsoundEditor.IsSelected(column.Key));

                piece.X = TimeToX(column.Time);
            }

            foreach (int key in visiblePieces.Keys.Where(k => !wantedKeys.Contains(k)).ToArray())
            {
                visiblePieces[key].Alpha = 0;
                freePieces.Push(visiblePieces[key]);
                visiblePieces.Remove(key);
            }

            displayInvalidated = false;

            // the stroke may have been interrupted (e.g. by losing focus), in which case the mouse up event never arrives.
            if (drawButton is MouseButton button && GetContainingInputManager()?.CurrentState.Mouse.IsPressed(button) == false)
                endDrawing();
        }

        private VolumePiece addPiece()
        {
            var piece = new VolumePiece();
            pieceContainer.Add(piece);
            return piece;
        }

        #region Drawing

        private enum DrawMode
        {
            /// <summary>
            /// The volumes under the cursor are set to the volume at the cursor.
            /// </summary>
            Draw,

            /// <summary>
            /// All selected hitsounds are set to the volume at the cursor.
            /// </summary>
            Selection,

            /// <summary>
            /// The volumes under the cursor are set to the volume at which the drag started.
            /// </summary>
            Level,

            /// <summary>
            /// The volumes under the cursor are reset to the volume which most hitsounds use.
            /// </summary>
            Reset,

            /// <summary>
            /// The volumes between the start of the drag and the cursor are set along a straight line between them.
            /// </summary>
            Line,
        }

        /// <summary>
        /// The button which is drawing, or <c>null</c> if not drawing.
        /// </summary>
        private MouseButton? drawButton;

        private bool drawing => drawButton != null;

        /// <summary>
        /// Whether volumes are being edited (or about to be), in which case the area is expanded such that volumes can be drawn more precisely.
        /// </summary>
        public bool IsActive => IsHovered || drawing;

        private DrawMode drawMode;

        private float lastX;
        private int lastVolume;

        private Vector2 lineStart;
        private int lineStartVolume;

        /// <summary>
        /// The volumes of the hitsounds changed by the current line before it was started, such that they can be restored when the line doesn't cover them anymore.
        /// </summary>
        private readonly Dictionary<int, int> lineOriginalVolumes = new Dictionary<int, int>();

        private Vector2? hoverPosition;

        /// <summary>
        /// The volume at a vertical position. Volumes are rounded to multiples of 5, unless control is held.
        /// </summary>
        private int volumeAt(float y, bool precise)
        {
            float volume = Math.Clamp(1 - y / DrawHeight, 0, 1) * 100;
            int rounded = precise ? (int)Math.Round(volume) : (int)Math.Round(volume / 5) * 5;

            return Math.Clamp(rounded, DrawableHitObject.MINIMUM_SAMPLE_VOLUME, 100);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (drawing || (e.Button != MouseButton.Left && e.Button != MouseButton.Right))
                return false;

            var position = GetMousePosition(e);
            var column = FindColumnAt(position.X);
            int volume = volumeAt(position.Y, e.ControlPressed);

            drawButton = e.Button;
            lastX = position.X;
            lastVolume = volume;

            HitsoundEditor.BeginStroke();

            if (e.Button == MouseButton.Right)
            {
                drawMode = DrawMode.Line;
                lineStart = position;
                lineStartVolume = volume;
                lineOriginalVolumes.Clear();
                updateLine(position, volume);
            }
            else if (e.AltPressed)
            {
                drawMode = DrawMode.Reset;
                lastVolume = HitsoundEditor.GetBaseHitsounds().volume;
                drawBetween(position.X, position.X, lastVolume, lastVolume);
            }
            else if (e.ShiftPressed)
            {
                drawMode = DrawMode.Level;
                drawBetween(position.X, position.X, volume, volume);
            }
            else if (column != null && HitsoundEditor.IsSelected(column.Key))
            {
                drawMode = DrawMode.Selection;
                HitsoundEditor.SetVolume(HitsoundEditor.SelectedColumns, volume);
            }
            else
            {
                drawMode = DrawMode.Draw;
                drawBetween(position.X, position.X, volume, volume);
            }

            if (column != null && drawMode != DrawMode.Line)
                Playback.PlayColumn(column);

            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => drawing;

        protected override void OnDrag(DragEvent e) => drawTo(GetMousePosition(e), e.ControlPressed);

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            hoverPosition = GetMousePosition(e);

            // the right button doesn't always start a drag, so lines are also updated by moving the mouse.
            if (drawButton == MouseButton.Right)
                drawTo(hoverPosition.Value, e.ControlPressed);

            return base.OnMouseMove(e);
        }

        private void drawTo(Vector2 position, bool precise)
        {
            if (!drawing)
                return;

            float x = position.X;
            int volume = volumeAt(position.Y, precise);

            switch (drawMode)
            {
                case DrawMode.Selection:
                    if (volume != lastVolume)
                        HitsoundEditor.SetVolume(HitsoundEditor.SelectedColumns, volume);
                    break;

                case DrawMode.Draw:
                    drawBetween(lastX, x, lastVolume, volume);
                    break;

                case DrawMode.Level:
                case DrawMode.Reset:
                    // the volume stays the same for the whole drag.
                    volume = lastVolume;
                    drawBetween(lastX, x, volume, volume);
                    break;

                case DrawMode.Line:
                    updateLine(position, volume);
                    break;
            }

            lastX = x;
            lastVolume = volume;
            hoverPosition = position;
        }

        /// <summary>
        /// Sets the volumes of the hitsounds between two positions, interpolating between the volumes at both positions, such that fast movements draw a line.
        /// </summary>
        private void drawBetween(float fromX, float toX, int fromVolume, int toVolume)
        {
            double tolerance = Timeline.PixelsToDuration(INTERACTION_DISTANCE);
            double fromTime = XToTime(Math.Min(fromX, toX)) - tolerance;
            double toTime = XToTime(Math.Max(fromX, toX)) + tolerance;

            var volumes = HitsoundEditor.Map.GetColumnsInRange(fromTime, toTime)
                                        .Select(c => (column: c, volume: interpolate(TimeToX(c.Time), fromX, toX, fromVolume, toVolume)))
                                        .Where(v => v.column.Volume != v.volume)
                                        .ToList();

            HitsoundEditor.SetVolumes(volumes);
        }

        /// <summary>
        /// Sets the volumes of the hitsounds between the start of the line and the given position along the line,
        /// and restores the volumes of hitsounds which were changed by the line before, but aren't covered by it anymore.
        /// </summary>
        private void updateLine(Vector2 position, int volume)
        {
            double fromTime = XToTime(Math.Min(lineStart.X, position.X));
            double toTime = XToTime(Math.Max(lineStart.X, position.X));

            var covered = HitsoundEditor.Map.GetColumnsInRange(fromTime, toTime).ToList();

            // a click without moving changes the hitsound under the cursor.
            if (covered.Count == 0 && FindColumnAt(position.X) is HitsoundColumn clicked)
                covered.Add(clicked);

            var coveredKeys = covered.Select(c => c.Key).ToHashSet();
            var volumes = new List<(HitsoundColumn column, int volume)>();

            foreach (var (key, original) in lineOriginalVolumes.Where(kvp => !coveredKeys.Contains(kvp.Key)).ToArray())
            {
                if (HitsoundEditor.Map.GetColumn(key) is HitsoundColumn column && column.Volume != original)
                    volumes.Add((column, original));

                lineOriginalVolumes.Remove(key);
            }

            foreach (var column in covered)
            {
                lineOriginalVolumes.TryAdd(column.Key, column.Volume);

                int target = interpolate(TimeToX(column.Time), lineStart.X, position.X, lineStartVolume, volume);

                if (column.Volume != target)
                    volumes.Add((column, target));
            }

            HitsoundEditor.SetVolumes(volumes);

            var startPoint = new Vector2(lineStart.X, (1 - lineStartVolume / 100f) * DrawHeight);
            var endPoint = new Vector2(position.X, (1 - volume / 100f) * DrawHeight);
            var delta = endPoint - startPoint;

            linePreview.Alpha = delta.Length > 1 ? 0.8f : 0;
            linePreview.Position = startPoint;
            linePreview.Width = delta.Length;
            linePreview.Rotation = MathHelper.RadiansToDegrees(MathF.Atan2(delta.Y, delta.X));
        }

        private static int interpolate(float x, float fromX, float toX, int fromVolume, int toVolume)
        {
            float progress = Math.Abs(toX - fromX) < 1 ? 1 : Math.Clamp((x - fromX) / (toX - fromX), 0, 1);
            return (int)Math.Round(fromVolume + (toVolume - fromVolume) * progress);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button == drawButton)
                endDrawing();

            base.OnMouseUp(e);
        }

        private void endDrawing()
        {
            drawButton = null;
            linePreview.Alpha = 0;
            lineOriginalVolumes.Clear();
            HitsoundEditor.EndStroke();
        }

        #endregion

        #region Tooltip

        protected override bool OnHover(HoverEvent e)
        {
            hoverPosition = GetMousePosition(e);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverPosition = null;
            base.OnHoverLost(e);
        }

        public LocalisableString TooltipText
        {
            get
            {
                if (drawing)
                    return SlopHitsoundEditorStrings.VolumeValue(lastVolume);

                if (hoverPosition is not Vector2 position || FindColumnAt(position.X) is not HitsoundColumn column)
                    return default;

                return SlopHitsoundEditorStrings.VolumeValue(column.Volume);
            }
        }

        #endregion

        private partial class VolumePiece : CompositeDrawable
        {
            private readonly Box bar;
            private readonly Circle dot;
            private readonly Container fill;

            public VolumePiece()
            {
                RelativeSizeAxes = Axes.Y;
                Width = 8;
                Origin = Anchor.TopCentre;

                InternalChild = fill = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Children = new Drawable[]
                    {
                        bar = new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 2,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                        },
                        dot = new Circle
                        {
                            Size = new Vector2(8),
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.Centre,
                        },
                    }
                };
            }

            public void Apply(HitsoundColumn column, bool selected)
            {
                Alpha = 1;

                fill.Height = column.Volume / 100f;

                var colour = selected ? Colour4.White : HitsoundLane.GetBankColour(column.NormalBank ?? string.Empty);

                bar.Colour = colour;
                bar.Alpha = selected ? 0.9f : 0.6f;
                dot.Colour = colour;
            }
        }
    }
}
