// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;

namespace osu.Game.Screens.Edit.Timing
{
    public partial class ControlPointList : CompositeDrawable
    {
        public Action? SelectClosestTimingPoint { get; init; }

        private ControlPointTable table = null!;
        private Container controls = null!;
        private OsuButton deleteButton = null!;
        private RoundedButton addButton = null!;
        private RoundedButton detectButton = null!;

        private CancellationTokenSource? detectionCancellation;

        [Resolved]
        private IBindable<WorkingBeatmap> workingBeatmap { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        protected EditorBeatmap Beatmap { get; private set; } = null!;

        [Resolved]
        private Bindable<ControlPointGroup?> selectedGroup { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? editorChangeHandler { get; set; }

        [Resolved]
        private BindableList<ControlPointGroup>? selectedGroups { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, OverlayColourProvider colourProvider)
        {
            RelativeSizeAxes = Axes.Both;

            const float margins = 10;
            InternalChildren = new Drawable[]
            {
                table = new ControlPointTable
                {
                    RelativeSizeAxes = Axes.Both,
                    Groups = { BindTarget = Beatmap.ControlPointInfo.Groups, },
                },
                controls = new Container
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background2,
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Spacing = new Vector2(5),
                            Padding = new MarginPadding { Left = margins, Vertical = margins, },
                            Children = new Drawable[]
                            {
                                new RoundedButton
                                {
                                    Text = "Select closest to current time",
                                    Action = SelectClosestTimingPoint,
                                    Size = new Vector2(220, 30),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                },
                                detectButton = new RoundedButton
                                {
                                    Text = detect_button_text,
                                    Action = detectTiming,
                                    Size = new Vector2(180, 30),
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    TooltipText = "Detects the BPM and offset of the song from its audio, assuming it has a single timing section. "
                                                  + "Applied to the selected timing point, or the first one.",
                                },
                            }
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Spacing = new Vector2(5),
                            Padding = new MarginPadding { Right = margins, Vertical = margins, },
                            Children = new Drawable[]
                            {
                                deleteButton = new RoundedButton
                                {
                                    Text = "-",
                                    Size = new Vector2(30, 30),
                                    Action = delete,
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    BackgroundColour = colours.Red3,
                                },
                                addButton = new RoundedButton
                                {
                                    Action = addNew,
                                    Size = new Vector2(160, 30),
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                },
                            }
                        },
                    }
                },
            };

            if (editorChangeHandler != null)
                editorChangeHandler.OnStateChange += onUndoRedo;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedGroup.BindValueChanged(selected =>
            {
                deleteButton.Enabled.Value = selected.NewValue != null;

                addButton.Text = selected.NewValue != null
                    ? "+ Clone to current time"
                    : "+ Add at current time";
            }, true);
        }

        protected override bool OnClick(ClickEvent e)
        {
            selectedGroup.Value = null;
            return true;
        }

        protected override void Update()
        {
            base.Update();

            addButton.Enabled.Value = clock.CurrentTimeAccurate != selectedGroup.Value?.Time;
            table.Padding = new MarginPadding { Bottom = controls.DrawHeight };
        }

        private void delete()
        {
            if (selectedGroup.Value == null)
                return;

            var groupsToDelete = selectedGroups?.Count > 0 ? selectedGroups.ToArray() : new[] { selectedGroup.Value };

            editorChangeHandler?.BeginChange();

            foreach (var group in groupsToDelete)
                Beatmap.ControlPointInfo.RemoveGroup(group);

            editorChangeHandler?.EndChange();

            selectedGroup.Value = Beatmap.ControlPointInfo.Groups.FirstOrDefault(g => g.Time >= clock.CurrentTime);
        }

        private void addNew()
        {
            bool isFirstControlPoint = !Beatmap.ControlPointInfo.TimingPoints.Any();

            var group = Beatmap.ControlPointInfo.GroupAt(clock.CurrentTime, true);

            if (isFirstControlPoint)
                group.Add(new TimingControlPoint());
            else
            {
                // Try and create matching types from the currently selected control point.
                var selected = selectedGroup.Value;

                if (selected != null && !ReferenceEquals(selected, group))
                {
                    foreach (var controlPoint in selected.ControlPoints)
                    {
                        group.Add(controlPoint.DeepClone());
                    }
                }
            }

            selectedGroup.Value = group;
        }

        private const string detect_button_text = "Detect BPM and offset";

        private void detectTiming()
        {
            var stream = EditorWaveformData.GetAudioStream(workingBeatmap.Value);

            if (stream == null)
            {
                notifications?.Post(new SimpleErrorNotification { Text = "The beatmap has no audio to detect the timing of." });
                return;
            }

            detectionCancellation?.Cancel();
            detectionCancellation = new CancellationTokenSource();
            var cancellationToken = detectionCancellation.Token;

            detectButton.Enabled.Value = false;
            detectButton.Text = "Detecting...";

            Task.Run(() => TimingDetection.Detect(stream, cancellationToken), cancellationToken).ContinueWith(task => Schedule(() =>
            {
                detectButton.Enabled.Value = true;
                detectButton.Text = detect_button_text;

                if (task.IsCanceled)
                    return;

                if (task.IsFaulted)
                {
                    Logger.Error(task.Exception, "Failed to detect the timing");
                    return;
                }

                applyDetectedTiming(task.GetResultSafely());
            }), CancellationToken.None);
        }

        private void applyDetectedTiming(TimingDetectionResult? result)
        {
            if (result?.BeatLength == null)
            {
                notifications?.Post(new SimpleErrorNotification
                {
                    Text = result == null
                        ? "The audio could not be read."
                        : "Could not detect the timing. The song may be too short, or it may have drifting BPM or multiple timing sections."
                });
                return;
            }

            string sampleRateWarning = result.SampleRateSupported ? string.Empty : " The audio isn't 44.1 kHz, so the result may be less accurate.";

            if (result.Offset == null)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = $"Could not determine the accurate BPM of the song. Approximate BPM: {result.BPM:0.0}.{sampleRateWarning}"
                });
                return;
            }

            double beatLength = result.BeatLength.Value;

            ControlPointGroup? group = null;

            if (selectedGroup.Value?.ControlPoints.Any(c => c is TimingControlPoint) == true)
                group = selectedGroup.Value;
            else if (Beatmap.ControlPointInfo.TimingPoints.Count > 0)
                group = Beatmap.ControlPointInfo.GroupAt(Beatmap.ControlPointInfo.TimingPoints[0].Time);

            double time = result.Offset.Value;

            // keep an existing timing point at the same beat of the song, rather than moving it to the first beat.
            if (group != null)
                time += Math.Round((group.Time - time) / beatLength) * beatLength;

            time = Math.Round(time);

            editorChangeHandler?.BeginChange();

            var controlPoints = group?.ControlPoints.ToArray() ?? Array.Empty<ControlPoint>();

            if (group != null)
                Beatmap.ControlPointInfo.RemoveGroup(group);

            var newGroup = Beatmap.ControlPointInfo.GroupAt(time, true);

            foreach (var controlPoint in controlPoints)
                newGroup.Add(controlPoint);

            if (!newGroup.ControlPoints.OfType<TimingControlPoint>().Any())
                newGroup.Add(new TimingControlPoint());

            foreach (var timingPoint in newGroup.ControlPoints.OfType<TimingControlPoint>())
                timingPoint.BeatLength = beatLength;

            editorChangeHandler?.EndChange();

            selectedGroup.Value = newGroup;

            notifications?.Post(new SimpleNotification
            {
                Text = $"Detected {result.BPM:0.###} BPM with an offset of {time} ms.{sampleRateWarning}"
            });

            string? warnings = describeWarnings(result.Warnings);

            if (warnings != null)
                notifications?.Post(new SimpleNotification { Text = warnings, Icon = FontAwesome.Solid.ExclamationTriangle });
        }

        /// <summary>
        /// Lists the parts of the song which have a warning, merging adjacent sections with the same warning.
        /// </summary>
        private static string? describeWarnings(TimingWarning[] warnings)
        {
            var lines = new List<string>();

            for (int start = 0; start < warnings.Length;)
            {
                int end = start + 1;

                while (end < warnings.Length && warnings[end] == warnings[start])
                    end++;

                string? description = warnings[start] switch
                {
                    TimingWarning.NeedsTimingSection => "probably needs an additional timing section",
                    TimingWarning.Late => "probably has a slightly later offset",
                    TimingWarning.Early => "probably has a slightly earlier offset",
                    TimingWarning.Jitter => "may have BPM drifts or offset jitter",
                    _ => null,
                };

                if (description != null)
                {
                    var from = TimeSpan.FromMilliseconds(start * TimingDetection.WARNING_SECTION_LENGTH);
                    var to = TimeSpan.FromMilliseconds(end * TimingDetection.WARNING_SECTION_LENGTH);

                    lines.Add($"{from:m\\:ss}–{to:m\\:ss} {description}");
                }

                start = end;
            }

            if (lines.Count == 0)
                return null;

            return "Timing warnings:\n"
                   + string.Join('\n', lines)
                   + "\nThe detected timing probably can't be relied on. Check it by ear, especially in these parts.";
        }

        private void onUndoRedo()
        {
            // Best effort. We have no tracking of control points through undo/redo changes.
            // If we don't deselect, things like offset changes could spawn groups to be added from previous states (see https://github.com/ppy/osu/issues/31098).
            if (selectedGroup.Value != null && !Beatmap.ControlPointInfo.Groups.Contains(selectedGroup.Value))
                selectedGroup.Value = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            detectionCancellation?.Cancel();

            if (editorChangeHandler != null)
                editorChangeHandler.OnStateChange -= onUndoRedo;
        }
    }
}
