// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Extensions;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;

namespace osu.Game.Screens.Edit.Timing
{
    public partial class TimingScreen : EditorScreenWithTimeline, IKeyBindingHandler<PlatformAction>
    {
        [Cached]
        public readonly Bindable<ControlPointGroup> SelectedGroup = new Bindable<ControlPointGroup>();

        /// <summary>
        /// All selected groups, including <see cref="SelectedGroup"/>.
        /// More groups can be selected by Ctrl+clicking (or Shift+clicking, for a range) them in the table.
        /// </summary>
        [Cached]
        public readonly BindableList<ControlPointGroup> SelectedGroups = new BindableList<ControlPointGroup>();

        private readonly BindableList<ControlPointGroup> groups = new BindableList<ControlPointGroup>();

        [Cached]
        private readonly EditorMetronome metronome = new EditorMetronome();

        [Cached]
        private readonly TimingAnalyser analyser = new TimingAnalyser();

        private readonly Bindable<EditorScreenMode> currentEditorMode = new Bindable<EditorScreenMode>();

        [Resolved]
        private EditorClock? editorClock { get; set; }

        [Resolved]
        private Clipboard hostClipboard { get; set; } = null!;

        public TimingScreen()
            : base(EditorScreenMode.Timing)
        {
        }

        /// <summary>
        /// Whether the timing point list and settings are shown. Hidden by default, such that the bar view has as much room as possible.
        /// </summary>
        private readonly BindableBool showPanels = new BindableBool();

        private GridContainer mainGrid = null!;
        private GridContainer leftGrid = null!;
        private ControlPointList controlPointList = null!;
        private ControlPointSettings controlPointSettings = null!;
        private ListResizeBar listResizeBar = null!;

        /// <summary>
        /// The fraction of the height taken by the timing point list, changed by dragging the bar above it.
        /// </summary>
        private float listHeight = 0.35f;

        protected override Drawable CreateMainContent()
        {
            var measureDisplay = new MeasureTimingDisplay { RelativeSizeAxes = Axes.Both };
            measureDisplay.ShowPanels.BindTo(showPanels);

            return mainGrid = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                Content = new[]
                {
                    new Drawable[]
                    {
                        leftGrid = new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Content = new[]
                            {
                                new Drawable[] { measureDisplay },
                                new Drawable[]
                                {
                                    listResizeBar = new ListResizeBar
                                    {
                                        Resized = delta =>
                                        {
                                            listHeight = Math.Clamp(listHeight - delta / leftGrid.DrawHeight, 0.1f, 0.8f);
                                            updatePanels(showPanels.Value);
                                        },
                                    },
                                },
                                new Drawable[]
                                {
                                    controlPointList = new ControlPointList
                                    {
                                        SelectClosestTimingPoint = selectClosestTimingPoint,
                                    },
                                },
                            }
                        },
                        controlPointSettings = new ControlPointSettings(),
                    },
                }
            };
        }

        private void updatePanels(bool show)
        {
            mainGrid.ColumnDimensions = new[]
            {
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, show ? 350 : 0),
            };

            leftGrid.RowDimensions = new[]
            {
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, show ? ListResizeBar.HEIGHT : 0),
                new Dimension(GridSizeMode.Relative, show ? listHeight : 0),
            };

            // the panels would overflow their collapsed cells otherwise.
            controlPointList.Alpha = controlPointSettings.Alpha = listResizeBar.Alpha = show ? 1 : 0;
        }

        [BackgroundDependencyLoader]
        private void load(Editor? editor)
        {
            AddInternal(metronome);
            AddInternal(analyser);

            if (editor != null)
                currentEditorMode.BindTo(editor.Mode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            showPanels.BindValueChanged(show => updatePanels(show.NewValue), true);

            // selecting a single group in any other way replaces the selection.
            SelectedGroup.BindValueChanged(group =>
            {
                if (group.NewValue == null)
                    SelectedGroups.Clear();
                else if (!SelectedGroups.Contains(group.NewValue))
                {
                    SelectedGroups.Clear();
                    SelectedGroups.Add(group.NewValue);
                }
            }, true);

            ((IBindableList<ControlPointGroup>)groups).BindTo(EditorBeatmap.ControlPointInfo.Groups);
            groups.BindCollectionChanged((_, e) =>
            {
                if (e.OldItems == null)
                    return;

                foreach (ControlPointGroup removed in e.OldItems)
                    SelectedGroups.Remove(removed);
            });

            SelectedGroups.BindCollectionChanged((_, _) => CanCopy.Value = SelectedGroups.Count > 0, true);

            // When entering the timing screen, let's choose the closest valid timing point.
            // This will emulate the osu-stable behaviour where a metronome and timing information
            // are presented on entering the screen.
            currentEditorMode.BindValueChanged(mode =>
            {
                if (mode.NewValue == EditorScreenMode.Timing)
                    selectClosestTimingPoint();
            });
            selectClosestTimingPoint();
        }

        private void selectClosestTimingPoint()
        {
            if (editorClock == null)
                return;

            double accurateTime = editorClock.CurrentTimeAccurate;

            var activeTimingPoint = EditorBeatmap.ControlPointInfo.TimingPointAt(accurateTime);
            var activeEffectPoint = EditorBeatmap.ControlPointInfo.EffectPointAt(accurateTime);

            if (activeEffectPoint.Equals(EffectControlPoint.DEFAULT))
                SelectedGroup.Value = EditorBeatmap.ControlPointInfo.GroupAt(activeTimingPoint.Time);
            else
            {
                double latestActiveTime = Math.Max(activeTimingPoint.Time, activeEffectPoint.Time);
                SelectedGroup.Value = EditorBeatmap.ControlPointInfo.GroupAt(latestActiveTime);
            }
        }

        /// <summary>
        /// Copies the timestamps of the selected groups, rather than their full contents.
        /// </summary>
        public override void Copy()
        {
            if (!CanCopy.Value)
                return;

            hostClipboard.SetText(string.Join(", ", SelectedGroups.Select(g => g.Time).Order().Select(t => t.ToEditorFormattedString())));
        }

        public bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
        {
            if (e.Action != PlatformAction.SelectAll)
                return false;

            // text boxes with focus handle select all themselves before this.
            var allGroups = EditorBeatmap.ControlPointInfo.Groups.ToArray();

            if (allGroups.Length == 0)
                return true;

            SelectedGroups.Clear();
            SelectedGroups.AddRange(allGroups);

            if (SelectedGroup.Value == null || !SelectedGroups.Contains(SelectedGroup.Value))
                SelectedGroup.Value = allGroups[0];

            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
        {
        }

        /// <summary>
        /// A bar which resizes the timing point list below it when dragged up or down.
        /// </summary>
        private partial class ListResizeBar : CompositeDrawable
        {
            public const float HEIGHT = 8;

            /// <summary>
            /// Invoked with the distance dragged downwards since the last invocation.
            /// </summary>
            public Action<float>? Resized;

            private Box grip = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    grip = new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(40, 2),
                        Colour = colourProvider.Background1,
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateState();
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e) => updateState();

            protected override bool OnDragStart(DragStartEvent e)
            {
                updateState();
                return true;
            }

            protected override void OnDrag(DragEvent e) => Resized?.Invoke(e.Delta.Y);

            protected override void OnDragEnd(DragEndEvent e) => updateState();

            private void updateState()
            {
                bool active = IsHovered || IsDragged;

                grip.ResizeWidthTo(active ? 80 : 40, 200, Easing.OutQuint);
                grip.FadeColour(active ? colourProvider.Highlight1 : colourProvider.Background1, 200, Easing.OutQuint);
            }
        }

        protected override Drawable CreateTimelineContent() => new VariableTimingOverlay();

        protected override void ConfigureTimeline(TimelineArea timelineArea)
        {
            base.ConfigureTimeline(timelineArea);

            timelineArea.Timeline.AlwaysShowControlPoints = true;
        }
    }
}
