// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders.Components;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Edit.Streams
{
    /// <summary>
    /// Displays the path, anchors and densities of the selected <see cref="EditorStream"/> and allows editing them.
    /// </summary>
    public partial class StreamEditOverlay : CompositeDrawable
    {
        /// <summary>
        /// The maximum distance from the path at which ctrl+clicking inserts an anchor.
        /// </summary>
        private const float insertion_distance = 32;

        [Resolved]
        private EditorStreamManager streamManager { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private readonly IBindable<EditorStream?> selectedStream = new Bindable<EditorStream?>();

        private EditorStream? stream;
        private IBindable<int>? pathVersion;

        private Container content = null!;
        private StreamPathLine pathLine = null!;
        private Container<DensityLabel> densityLabels = null!;

        private int lastLineVersion = -1;
        private Vector2 lastLinePosition;

        public StreamEditOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = content = new Container { RelativeSizeAxes = Axes.Both };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedStream.BindTo(streamManager.SelectedStream);
            selectedStream.BindValueChanged(s => show(s.NewValue), true);
        }

        private void show(EditorStream? newStream)
        {
            pathVersion?.UnbindAll();
            pathVersion = null;
            content.Clear();
            lastLineVersion = -1;

            stream = newStream;

            if (stream == null)
                return;

            // the control point pieces depend on the radius of the slider.
            stream.Path.ApplyDefaults(editorBeatmap.ControlPointInfo, editorBeatmap.Difficulty);

            var currentStream = stream;

            content.AddRange(new Drawable[]
            {
                pathLine = new StreamPathLine(),
                new PathControlPointVisualiser<Slider>(stream.Path, true)
                {
                    LengthSnapping = false,
                    RemoveControlPointsRequested = removeControlPoints,
                },
                densityLabels = new Container<DensityLabel> { RelativeSizeAxes = Axes.Both },
            });

            pathVersion = stream.Path.Path.Version.GetBoundCopy();
            pathVersion.BindValueChanged(_ => streamManager.Invalidate(currentStream));
        }

        protected override void Update()
        {
            base.Update();

            if (stream == null)
                return;

            // moving the whole stream only changes its position, which doesn't bump the path version.
            if (lastLineVersion != stream.Path.Path.Version.Value || lastLinePosition != stream.Path.Position)
            {
                lastLineVersion = stream.Path.Path.Version.Value;
                lastLinePosition = stream.Path.Position;
                pathLine.UpdateFrom(stream.Path);
            }

            var anchors = stream.GetDensityAnchors();

            if (anchors.Count != densityLabels.Count || anchors.Where((a, i) => densityLabels[i].ControlPoint != a.Point).Any())
            {
                densityLabels.Clear();
                densityLabels.AddRange(anchors.Select(a => new DensityLabel(stream, a.Point)));
            }

            foreach (var label in densityLabels)
            {
                label.Position = stream.Path.Position + label.ControlPoint.Position + new Vector2(0, (float)stream.Path.Radius * 0.6f + 8);
                label.UpdateText();
            }
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (stream == null || e.Button != MouseButton.Left || !e.ControlPressed || e.ShiftPressed || e.AltPressed)
                return false;

            Vector2 position = e.MousePosition - stream.Path.Position;

            if (distanceToPath(stream.Path, position) > insertion_distance)
                return false;

            var controlPoints = stream.Path.Path.ControlPoints;

            changeHandler?.BeginChange();
            controlPoints.Insert(ControlPointInsertionPreview.GetInsertionIndex(controlPoints, position), new PathControlPoint { Position = position });
            changeHandler?.EndChange();

            return true;
        }

        private static float distanceToPath(Slider slider, Vector2 position)
        {
            var path = slider.Path.CalculatedPath;
            float minDistance = float.MaxValue;

            for (int i = 1; i < path.Count; i++)
            {
                Vector2 a = path[i - 1];
                Vector2 b = path[i];
                Vector2 ab = b - a;

                float t = ab.LengthSquared > 0 ? Math.Clamp(Vector2.Dot(position - a, ab) / ab.LengthSquared, 0, 1) : 0;
                minDistance = Math.Min(minDistance, Vector2.Distance(position, a + ab * t));
            }

            return minDistance;
        }

        private void removeControlPoints(List<PathControlPoint> toRemove)
        {
            if (stream == null || toRemove.Count == 0)
                return;

            var controlPoints = stream.Path.Path.ControlPoints;

            foreach (var c in toRemove)
            {
                // the first anchor must have a type, so take it from the previous first one.
                if (c == controlPoints[0] && controlPoints.Count > 1 && controlPoints[1].Type == null)
                    controlPoints[1].Type = controlPoints[0].Type;

                controlPoints.Remove(c);
            }

            if (controlPoints.Count <= 1 || stream.Path.Path.Distance <= 0)
            {
                streamManager.Delete(stream);
                return;
            }

            // the path starts at the stream's position, so the stream is moved along if the first anchor was removed.
            Vector2 first = controlPoints[0].Position;

            foreach (var c in controlPoints)
                c.Position -= first;

            stream.Path.Position += first;
        }

        /// <summary>
        /// Displays the density of an anchor below it. Scrolling changes the density, clicking opens a popover to set it precisely.
        /// </summary>
        private partial class DensityLabel : OsuClickableContainer, IHasPopover
        {
            public readonly PathControlPoint ControlPoint;

            private readonly EditorStream stream;

            [Resolved]
            private EditorStreamManager streamManager { get; set; } = null!;

            [Resolved]
            private IEditorChangeHandler? changeHandler { get; set; }

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            private OsuSpriteText text = null!;
            private Box background = null!;

            public DensityLabel(EditorStream stream, PathControlPoint controlPoint)
            {
                this.stream = stream;
                ControlPoint = controlPoint;

                Origin = Anchor.TopCentre;
                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 4;

                Action = this.ShowPopover;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.6f,
                    },
                    text = new OsuSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 4, Vertical = 1 },
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        Colour = colours.Yellow,
                    },
                };

                UpdateText();
            }

            public void UpdateText() => text.Text = $"{stream.GetDensity(ControlPoint).ToString("0.0#", CultureInfo.InvariantCulture)}x";

            public override LocalisableString TooltipText => "Stream density. Scroll to adjust (hold shift for finer steps), click to edit.";

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeTo(0.9f, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeTo(0.6f, 100);
                base.OnHoverLost(e);
            }

            protected override bool OnScroll(ScrollEvent e)
            {
                double step = e.ShiftPressed ? 0.01 : 0.1;
                double density = stream.GetDensity(ControlPoint);

                // shift+scroll arrives as a horizontal scroll on some platforms.
                float delta = e.ScrollDelta.Y != 0 ? e.ScrollDelta.Y : e.ScrollDelta.X;
                int steps = Math.Sign(delta) * (int)Math.Max(1, Math.Round(Math.Abs(delta)));

                // snap to the step so that scrolling from e.g. 1.05 doesn't stay off-grid.
                double newDensity = Math.Round(density / step) * step + steps * step;

                changeHandler?.BeginChange();
                stream.SetDensity(ControlPoint, newDensity);
                streamManager.Invalidate(stream);
                streamManager.ApplyPendingLayouts();
                changeHandler?.EndChange();

                UpdateText();
                return true;
            }

            // the popover is hosted by the editor's popover container, outside of the composer's dependencies.
            public Popover GetPopover() => new DensityPopover(stream, ControlPoint, streamManager, changeHandler);
        }

        private partial class DensityPopover : OsuPopover
        {
            private readonly EditorStream stream;
            private readonly PathControlPoint controlPoint;

            private readonly BindableDouble density = new BindableDouble(1)
            {
                MinValue = EditorStream.MIN_DENSITY,
                MaxValue = EditorStream.MAX_DENSITY,
                Precision = 0.01,
            };

            private readonly EditorStreamManager streamManager;
            private readonly IEditorChangeHandler? changeHandler;

            private OsuSpriteText valueText = null!;

            private bool changeStarted;

            public DensityPopover(EditorStream stream, PathControlPoint controlPoint, EditorStreamManager streamManager, IEditorChangeHandler? changeHandler)
            {
                this.stream = stream;
                this.controlPoint = controlPoint;
                this.streamManager = streamManager;
                this.changeHandler = changeHandler;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                density.Value = stream.GetDensity(controlPoint);

                Child = new FillFlowContainer
                {
                    Width = 240,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(4),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = "Density",
                            Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                        },
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            ColumnDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, 44),
                                new Dimension(GridSizeMode.AutoSize),
                            },
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    new RoundedSliderBar<double>
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        Padding = new MarginPadding { Right = 8 },
                                        Current = density,
                                        KeyboardStep = 0.1f,
                                    },
                                    valueText = new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Font = OsuFont.Default.With(size: 14),
                                    },
                                    new RevertToDefaultButton<double>
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Current = density,
                                    },
                                },
                            },
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                density.BindValueChanged(d =>
                {
                    valueText.Text = $"{d.NewValue.ToString("0.00", CultureInfo.InvariantCulture)}x";

                    if (d.NewValue == stream.GetDensity(controlPoint))
                        return;

                    stream.SetDensity(controlPoint, d.NewValue);
                    streamManager.Invalidate(stream);
                }, true);
            }

            protected override void PopIn()
            {
                base.PopIn();

                if (!changeStarted)
                {
                    changeHandler?.BeginChange();
                    changeStarted = true;
                }
            }

            protected override void PopOut()
            {
                base.PopOut();

                if (changeStarted)
                {
                    changeHandler?.EndChange();
                    changeStarted = false;
                }
            }
        }
    }
}
