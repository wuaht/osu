// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

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

        public TimingScreen()
            : base(EditorScreenMode.Timing)
        {
        }

        protected override Drawable CreateMainContent() => new GridContainer
        {
            RelativeSizeAxes = Axes.Both,
            ColumnDimensions = new[]
            {
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, 350),
            },
            Content = new[]
            {
                new Drawable[]
                {
                    new ControlPointList
                    {
                        SelectClosestTimingPoint = selectClosestTimingPoint,
                    },
                    new ControlPointSettings(),
                },
            }
        };

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

        protected override Drawable CreateTimelineContent() => new VariableTimingOverlay();

        protected override void ConfigureTimeline(TimelineArea timelineArea)
        {
            base.ConfigureTimeline(timelineArea);

            timelineArea.Timeline.AlwaysShowControlPoints = true;
        }
    }
}
