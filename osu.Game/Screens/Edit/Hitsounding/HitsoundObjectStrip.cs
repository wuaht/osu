// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Displays the waveform and the hit objects above the lanes.
    /// Hitsounds are selected by clicking or dragging here, and clicking anywhere else seeks.
    /// </summary>
    public partial class HitsoundObjectStrip : HitsoundTimelinePart, IHasHitsoundTooltip
    {
        private readonly Container waveformContainer;
        private readonly Container<Box> bodyContainer;
        private readonly Container<ObjectPiece> pieceContainer;

        private WaveformGraph waveform = null!;

        private readonly Dictionary<int, ObjectPiece> visiblePieces = new Dictionary<int, ObjectPiece>();
        private readonly Stack<ObjectPiece> freePieces = new Stack<ObjectPiece>();
        private readonly HashSet<int> wantedKeys = new HashSet<int>();

        private HitsoundMap? displayedMap;
        private bool displayInvalidated = true;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public HitsoundObjectStrip()
        {
            waveformContainer = new Container
            {
                RelativeSizeAxes = Axes.Y,
                Height = 0.8f,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
            };

            bodyContainer = new Container<Box> { RelativeSizeAxes = Axes.Both };
            pieceContainer = new Container<ObjectPiece> { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            waveformContainer.Child = waveform = new WaveformGraph
            {
                RelativeSizeAxes = Axes.Both,
                BaseColour = colours.Blue.Opacity(0.2f),
                LowColour = colours.BlueLighter,
                MidColour = colours.BlueDark,
                HighColour = colours.BlueDarker,
                Alpha = 0.6f,
            };

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4,
                },
                waveformContainer,
                new HitsoundTicks(0.5f),
                bodyContainer,
                pieceContainer,
                new HitsoundSelectionBoxDisplay(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            HitsoundEditor.SelectionChanged += invalidateDisplay;

            beatmap.BindValueChanged(b => waveform.Waveform = b.NewValue.Waveform, true);
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

            // the waveform is offset like in the timeline, such that it visually matches the audio.
            waveformContainer.X = TimeToX(-Editor.WAVEFORM_VISUAL_OFFSET);
            waveformContainer.Width = (float)(EditorClock.TrackLength * Timeline.CurrentZoom);

            var map = HitsoundEditor.Map;

            if (map != displayedMap)
            {
                displayedMap = map;
                displayInvalidated = true;
            }

            var (start, end) = GetVisibleRange(INTERACTION_DISTANCE * 2);

            updateBodies(map, start, end);
            updatePieces(map, start, end);

            displayInvalidated = false;

            if (selecting && GetContainingInputManager()?.CurrentState.Mouse.IsPressed(MouseButton.Left) == false)
                endSelection(null);
        }

        private void updateBodies(HitsoundMap map, double start, double end)
        {
            int used = 0;

            foreach (var body in map.Bodies)
            {
                if (body.StartTime > end || body.EndTime < start)
                    continue;

                Box bar;

                if (used < bodyContainer.Count)
                    bar = bodyContainer[used];
                else
                {
                    bodyContainer.Add(bar = new Box
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Height = 6,
                        Colour = colourProvider.Light4,
                    });
                }

                used++;

                float startX = TimeToX(body.StartTime);

                bar.Alpha = 0.5f;
                bar.X = startX;
                bar.Width = Math.Max(1, TimeToX(body.EndTime) - startX);
            }

            for (int i = used; i < bodyContainer.Count; i++)
                bodyContainer[i].Alpha = 0;
        }

        private void updatePieces(HitsoundMap map, double start, double end)
        {
            wantedKeys.Clear();

            int firstIndex = map.IndexOfFirstColumnAtOrAfter(start);

            for (int i = firstIndex; i < map.Columns.Count && map.Columns[i].Time <= end; i++)
            {
                var column = map.Columns[i];

                wantedKeys.Add(column.Key);

                if (!visiblePieces.TryGetValue(column.Key, out var piece))
                {
                    piece = freePieces.Count > 0 ? freePieces.Pop() : addPiece();
                    visiblePieces[column.Key] = piece;
                    applyPiece(piece, map, i);
                }
                else if (displayInvalidated)
                    applyPiece(piece, map, i);

                piece.X = TimeToX(column.Time);
            }

            foreach (int key in visiblePieces.Keys.Where(k => !wantedKeys.Contains(k)).ToArray())
            {
                visiblePieces[key].Alpha = 0;
                freePieces.Push(visiblePieces[key]);
                visiblePieces.Remove(key);
            }
        }

        private void applyPiece(ObjectPiece piece, HitsoundMap map, int index)
        {
            var column = map.Columns[index];

            // custom indices usually stay the same for a while, so only changes are labelled, like timing changes.
            int? customIndexChange = index == 0 || map.Columns[index - 1].CustomIndex != column.CustomIndex ? column.CustomIndex : null;

            piece.Apply(column, HitsoundEditor.IsSelected(column.Key), customIndexChange);
        }

        private ObjectPiece addPiece()
        {
            var piece = new ObjectPiece();
            pieceContainer.Add(piece);
            return piece;
        }

        #region Selection

        private bool selecting;
        private bool selectionAdditive;
        private float selectionStartX;
        private double selectionStartTime;

        private Vector2? hoverPosition;

        /// <summary>
        /// Whether the hitsound which was pressed was already selected before, in which case clicking it seeks to it.
        /// </summary>
        private bool pressedSelectedColumn;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left || selecting)
                return false;

            var column = FindColumnAt(GetMousePosition(e).X);

            if (column != null)
            {
                pressedSelectedColumn = HitsoundEditor.IsSelected(column.Key);

                if (e.ControlPressed)
                    HitsoundEditor.ToggleSelection(column.Key);
                else if (e.ShiftPressed)
                    HitsoundEditor.SelectRangeTo(column.Key);
                else if (!HitsoundEditor.IsSelected(column.Key))
                    HitsoundEditor.SelectSingle(column.Key);

                Playback.PlayColumn(column);
                return true;
            }

            selecting = true;
            selectionAdditive = e.ShiftPressed || e.ControlPressed;
            selectionStartX = GetMousePosition(e).X;
            selectionStartTime = XToTime(GetMousePosition(e).X);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => selecting;

        protected override void OnDrag(DragEvent e)
        {
            double time = XToTime(GetMousePosition(e).X);
            Timeline.SelectionBox.Value = (Math.Min(selectionStartTime, time), Math.Max(selectionStartTime, time));
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            if (e.Button == MouseButton.Left && selecting)
                endSelection(GetMousePosition(e).X);

            base.OnMouseUp(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (FindColumnAt(GetMousePosition(e).X) is HitsoundColumn column && !e.ControlPressed && !e.ShiftPressed)
            {
                // clicking a selected hitsound (without dragging) selects only it, and seeks to it.
                HitsoundEditor.SelectSingle(column.Key);

                if (pressedSelectedColumn)
                    EditorClock.SeekSmoothlyTo(column.Time);

                return true;
            }

            return false;
        }

        /// <param name="x">The position at which the mouse button was released, or <c>null</c> if the selection was interrupted.</param>
        private void endSelection(float? x)
        {
            selecting = false;
            Timeline.SelectionBox.Value = null;

            if (x == null)
                return;

            // a click without dragging seeks to the clicked time and clears the selection.
            if (Math.Abs(x.Value - selectionStartX) < 3)
            {
                if (!selectionAdditive)
                    HitsoundEditor.ClearSelection();

                EditorClock.SeekSmoothlyTo(selectionStartTime);
                return;
            }

            double time = XToTime(x.Value);
            var keys = HitsoundEditor.Map.GetColumnsInRange(Math.Min(selectionStartTime, time), Math.Max(selectionStartTime, time)).Select(c => c.Key);

            HitsoundEditor.SetSelection(keys, selectionAdditive);
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverPosition = GetMousePosition(e);
            return true;
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            hoverPosition = GetMousePosition(e);
            return base.OnMouseMove(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverPosition = null;
            base.OnHoverLost(e);
        }

        public LocalisableString TooltipText => hoverPosition is Vector2 position && !selecting && FindColumnAt(position.X) is HitsoundColumn column
            ? HitsoundColumnDescription.Describe(column)
            : default;

        #endregion

        private partial class ObjectPiece : CompositeDrawable
        {
            private readonly Circle circle;
            private readonly Circle selectionRing;
            private readonly OsuSpriteText customIndexText;

            public ObjectPiece()
            {
                RelativeSizeAxes = Axes.Y;
                Width = 14;
                Origin = Anchor.TopCentre;

                InternalChildren = new Drawable[]
                {
                    selectionRing = new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(18),
                        Alpha = 0,
                    },
                    circle = new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(12),
                    },
                    customIndexText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopLeft,
                        X = 2,
                        Y = 1,
                        Font = OsuFont.Default.With(size: 11, weight: FontWeight.Bold),
                    },
                };
            }

            public void Apply(HitsoundColumn column, bool selected, int? customIndexChange)
            {
                Alpha = 1;

                circle.Colour = column.HasFileSamples ? Colour4.Gray : HitsoundLane.GetBankColour(column.NormalBank ?? string.Empty);
                selectionRing.Alpha = selected ? 1 : 0;

                customIndexText.Alpha = customIndexChange != null ? 1 : 0;
                customIndexText.Text = customIndexChange?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }
    }
}
