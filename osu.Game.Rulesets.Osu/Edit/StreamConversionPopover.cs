// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Overlays;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// An invisible drawable placed on top of a slider's head which hosts a <see cref="StreamConversionPopover"/>.
    /// The slider itself is removed while previewing, so its blueprint can't be the popover target.
    /// </summary>
    public partial class StreamConversionAnchor : Drawable, IHasPopover
    {
        private readonly Slider slider;

        public StreamConversionAnchor(Slider slider)
        {
            this.slider = slider;

            Origin = Anchor.Centre;
            Position = slider.StackedPosition;
            Size = new Vector2((float)slider.Radius * 2);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Schedule(this.ShowPopover);
        }

        public Popover GetPopover() => new StreamConversionPopover(slider)
        {
            Closed = () => this.Delay(500).Expire(),
            // the popover follows its target, so dragging it moves the anchor.
            Pinned = screenSpacePosition => Position = Parent!.ToLocalSpace(screenSpacePosition),
            Dragged = screenSpaceDelta => Position = Parent!.ToLocalSpace(Parent.ToScreenSpace(Position) + screenSpaceDelta),
        };
    }

    public partial class StreamConversionPopover : OsuPopover
    {
        private const float label_width = 110;
        private const float value_width = 50;
        private const float row_height = 28;

        private const float revert_width = 20;

        private static readonly int[] divisors = { 1, 2, 3, 4, 6, 8, 12, 16 };

        public Action? Closed { get; init; }

        /// <summary>
        /// Invoked with the screen space distance the popover was dragged by.
        /// </summary>
        public Action<Vector2>? Dragged { get; init; }

        /// <summary>
        /// Invoked with the screen space centre of the popover when it starts being dragged for the first time.
        /// From then on, the popover is centred on its target instead of picking a side next to it.
        /// </summary>
        public Action<Vector2>? Pinned { get; init; }

        private bool pinned;

        private readonly Slider slider;

        private readonly Bindable<StreamSpacingMode> spacingMode = new Bindable<StreamSpacingMode>();
        private readonly BindableNumber<double> distanceSnap = new BindableNumber<double>(1) { MinValue = 0.1, MaxValue = 9, Precision = 0.01 };
        private readonly BindableNumber<int> count = new BindableNumber<int>(5) { MinValue = 1, MaxValue = 64, Precision = 1 };
        private readonly Bindable<int> divisor = new Bindable<int>();
        private readonly BindableNumber<double> acceleration = new BindableNumber<double> { MinValue = -2, MaxValue = 2, Precision = 0.01 };
        private readonly Bindable<StreamAccelerationMode> accelerationMode = new Bindable<StreamAccelerationMode>();
        private readonly BindableNumber<double> wiggle = new BindableNumber<double> { MinValue = -1, MaxValue = 1, Precision = 0.01 };
        private readonly Bindable<StreamWiggleMode> wiggleMode = new Bindable<StreamWiggleMode>();

        private Drawable distanceSnapRow = null!;
        private Drawable countRow = null!;
        private OsuSpriteText spacingText = null!;

        private readonly List<HitCircle> insertedCircles = new List<HitCircle>();
        private bool began;
        private bool committed;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        public StreamConversionPopover(Slider slider)
        {
            this.slider = slider;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };

            // the stream uses the timeline's beat snap by default, which is offered even if it isn't one of the common divisors.
            divisor.Value = editorBeatmap.BeatDivisor;
            int[] availableDivisors = divisors.Append(divisor.Value).Distinct().Order().ToArray();

            Child = new FillFlowContainer
            {
                Width = 330,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(8),
                Children = new[]
                {
                    new OsuSpriteText
                    {
                        Text = "Convert to stream (drag to move)",
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                        Alpha = 0.6f,
                    },
                    createRow("Spacing", new ChoiceSelector<StreamSpacingMode>(Enum.GetValues<StreamSpacingMode>(), m => m == StreamSpacingMode.DistanceSnap ? "Distance" : "Count")
                    {
                        Current = spacingMode
                    }),
                    distanceSnapRow = createSliderRow("Distance snap", distanceSnap, v => $"{v:0.00}x"),
                    countRow = createSliderRow("Object count", count, v => $"{v}"),
                    createRow("Beat snap 1/", new ChoiceSelector<int>(availableDivisors, d => $"{d}")
                    {
                        Current = divisor
                    }),
                    createSliderRow("Acceleration", acceleration, v => $"{v:0.00}"),
                    createRow("Accel. mode", new ChoiceSelector<StreamAccelerationMode>(Enum.GetValues<StreamAccelerationMode>(), m => m.GetDescription())
                    {
                        Current = accelerationMode
                    }),
                    spacingText = new OsuSpriteText
                    {
                        Font = OsuFont.Default.With(size: 14),
                        Margin = new MarginPadding { Left = label_width },
                    },
                    createSliderRow("Wiggle", wiggle, v => $"{v:0.00}"),
                    createRow("Wiggle mode", new ChoiceSelector<StreamWiggleMode>(Enum.GetValues<StreamWiggleMode>(), m => m.GetDescription())
                    {
                        Current = wiggleMode
                    }),
                    new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 34,
                        Text = "Convert",
                        Action = commit
                    }
                }
            };
        }

        private static Drawable createRow(string label, Drawable content, Drawable? value = null, Drawable? revert = null) => new GridContainer
        {
            RelativeSizeAxes = Axes.X,
            Height = row_height,
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, label_width),
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, value == null ? 0 : value_width),
                new Dimension(GridSizeMode.Absolute, revert == null ? 0 : revert_width),
            },
            Content = new[]
            {
                new[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = label,
                        Font = OsuFont.Default.With(size: 15),
                    },
                    content,
                    value ?? Empty(),
                    revert ?? Empty(),
                }
            }
        };

        private static Drawable createSliderRow<T>(string label, BindableNumber<T> current, Func<T, string> format)
            where T : struct, System.Numerics.INumber<T>, System.Numerics.IMinMaxValue<T>
        {
            var valueText = new OsuSpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Font = OsuFont.Default.With(size: 15, weight: FontWeight.SemiBold),
            };

            current.BindValueChanged(v => valueText.Text = format(v.NewValue), true);

            return createRow(label, new RoundedSliderBar<T>
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                Padding = new MarginPadding { Right = 8 },
                Current = current,
                KeyboardStep = float.CreateTruncating(current.Precision),
            }, valueText, new RevertToDefaultButton<T>
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Current = current,
            });
        }

        protected override void PopIn()
        {
            base.PopIn();

            if (began)
                return;

            changeHandler?.BeginChange();
            began = true;

            editorBeatmap.Remove(slider);

            spacingMode.BindValueChanged(mode =>
            {
                distanceSnapRow.Alpha = mode.NewValue == StreamSpacingMode.DistanceSnap ? 1 : 0;
                countRow.Alpha = mode.NewValue == StreamSpacingMode.Count ? 1 : 0;
                Scheduler.AddOnce(updateStream);
            }, true);

            distanceSnap.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            count.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            divisor.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            acceleration.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            accelerationMode.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            wiggle.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            wiggleMode.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
        }

        private void updateStream()
        {
            if (committed)
                return;


            double length = slider.Path.Distance;
            double timeSpacing = editorBeatmap.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength / divisor.Value;

            int objectCount;
            double spacing;

            if (spacingMode.Value == StreamSpacingMode.Count)
            {
                objectCount = count.Value;
                spacing = length / Math.Max(1, objectCount - 1);
            }
            else
            {
                spacing = slider.Velocity * timeSpacing * distanceSnap.Value;
                objectCount = spacing > 0 ? (int)((length + 1) / spacing) + 1 : 1;
            }

            var positions = StreamGenerator.Generate(slider, objectCount, spacing, acceleration.Value, accelerationMode.Value,
                wiggle.Value, wiggleMode.Value, out double[] distances);

            // existing circles are updated in place, as re-adding all of them on every change makes them flicker.
            if (insertedCircles.Count > positions.Length)
            {
                var surplus = insertedCircles.GetRange(positions.Length, insertedCircles.Count - positions.Length);
                insertedCircles.RemoveRange(positions.Length, surplus.Count);
                editorBeatmap.RemoveRange(surplus);
            }

            var added = new List<HitCircle>();

            for (int i = 0; i < positions.Length; i++)
            {
                double startTime = slider.StartTime + i * timeSpacing;

                if (i < insertedCircles.Count)
                {
                    var circle = insertedCircles[i];

                    if (circle.StartTime == startTime && circle.Position == positions[i])
                        continue;

                    circle.StartTime = startTime;
                    circle.Position = positions[i];
                    editorBeatmap.Update(circle);
                    continue;
                }

                var newCircle = new HitCircle
                {
                    StartTime = startTime,
                    Position = positions[i],
                    NewCombo = i == 0 && slider.NewCombo,
                    Samples = slider.HeadCircle.Samples.Select(s => s.With()).ToList()
                };

                insertedCircles.Add(newCircle);
                added.Add(newCircle);
            }

            if (added.Count > 0)
                editorBeatmap.AddRange(added);

            if (objectCount > 1)
            {
                double snapDistance = slider.Velocity * timeSpacing;
                bool mirror = accelerationMode.Value == StreamAccelerationMode.Mirror;
                double first = (distances[1] - distances[0]) / snapDistance;
                double last = mirror && objectCount > 2
                    ? (distances[objectCount / 2 + 1] - distances[objectCount / 2]) / snapDistance
                    : (distances[^1] - distances[^2]) / snapDistance;

                spacingText.Text = $"Spacing {first:F2}x {(mirror ? "<->" : "->")} {last:F2}x";
            }
            else
                spacingText.Text = string.Empty;
        }

        // sliders and buttons handle their own drags, so this only triggers on the empty parts of the popover.
        protected override bool OnDragStart(DragStartEvent e)
        {
            if (Dragged == null)
                return false;

            // picking the side with the most space would make the popover jump around while it is dragged.
            if (!pinned && Pinned != null)
            {
                Pinned(Body.ScreenSpaceDrawQuad.Centre);
                AllowableAnchors = new[] { Anchor.Centre };
                pinned = true;
            }

            return true;
        }

        protected override void OnDrag(DragEvent e) => Dragged?.Invoke(e.ScreenSpaceMousePosition - e.ScreenSpaceLastMousePosition);

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Select && !e.Repeat)
            {
                commit();
                return true;
            }

            return base.OnPressed(e);
        }

        private void commit()
        {
            if (!began || committed)
                return;

            changeHandler?.EndChange();
            committed = true;
            editorBeatmap.SelectedHitObjects.Clear();
            editorBeatmap.SelectedHitObjects.AddRange(insertedCircles);
            Hide();
        }

        protected override void PopOut()
        {
            base.PopOut();

            // a popover which loads hidden pops out once before it was ever shown.
            if (!began)
                return;

            if (!committed)
            {
                editorBeatmap.RemoveRange(insertedCircles);
                editorBeatmap.Add(slider);
                changeHandler?.EndChange();
                committed = true;
            }

            Closed?.Invoke();
        }

        /// <summary>
        /// A compact row of toggle cells, one per item.
        /// </summary>
        private partial class ChoiceSelector<T> : CompositeDrawable, IHasCurrentValue<T>
        {
            private readonly BindableWithCurrent<T> current = new BindableWithCurrent<T>();

            public Bindable<T> Current
            {
                get => current.Current;
                set => current.Current = value;
            }

            private readonly T[] items;
            private readonly ChoiceCell[] cells;

            public ChoiceSelector(IEnumerable<T> items, Func<T, string> getText)
            {
                this.items = items.ToArray();

                RelativeSizeAxes = Axes.Both;

                cells = this.items.Select(item => new ChoiceCell(getText(item)) { Action = () => Current.Value = item }).ToArray();

                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = cells.Select(_ => new Dimension()).ToArray(),
                    Content = new[] { cells.Cast<Drawable>().ToArray() }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(v =>
                {
                    for (int i = 0; i < items.Length; i++)
                        cells[i].Selected = EqualityComparer<T>.Default.Equals(items[i], v.NewValue);
                }, true);
            }
        }

        private partial class ChoiceCell : OsuClickableContainer
        {
            private readonly Box background;
            private readonly OsuSpriteText text;

            [Resolved]
            private OsuColour colours { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider? colourProvider { get; set; }

            private bool selected;

            public bool Selected
            {
                set
                {
                    selected = value;
                    updateColours();
                }
            }

            public ChoiceCell(string label)
            {
                RelativeSizeAxes = Axes.Both;
                Padding = new MarginPadding(2);

                Child = new osu.Framework.Graphics.Containers.Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 4,
                    Children = new Drawable[]
                    {
                        background = new Box { RelativeSizeAxes = Axes.Both },
                        text = new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = label,
                            Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                updateColours();
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateColours();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateColours();
                base.OnHoverLost(e);
            }

            private void updateColours()
            {
                if (!IsLoaded)
                    return;

                Color4 idle = colourProvider?.Background3 ?? colours.Gray3;

                background.Colour = selected ? colourProvider?.Highlight1 ?? colours.Yellow : IsHovered ? idle.Lighten(0.3f) : idle;
                text.Colour = selected ? colourProvider?.Background5 ?? colours.Gray1 : Color4.White;
            }
        }
    }

    public static class StreamGenerator
    {
        /// <summary>
        /// Computes the positions of a stream laid along a slider's path.
        /// </summary>
        /// <param name="slider">The slider whose path the stream follows.</param>
        /// <param name="count">The number of objects.</param>
        /// <param name="spacing">The base distance between two consecutive objects along the path.</param>
        /// <param name="acceleration">How strongly spacing changes over the stream, in the range [-2, 2].</param>
        /// <param name="accelerationMode">Whether the spacing change goes one way or towards/away from the middle.</param>
        /// <param name="wiggle">How far objects alternate sideways from the path, in the range [-1, 1].</param>
        /// <param name="wiggleMode">How the wiggle and the acceleration interact.</param>
        /// <param name="distances">The distance along the path of each object, before wiggle is applied.</param>
        public static Vector2[] Generate(Slider slider, int count, double spacing, double acceleration, StreamAccelerationMode accelerationMode,
                                         double wiggle, StreamWiggleMode wiggleMode, out double[] distances)
        {
            double length = slider.Path.Distance;

            var positions = new Vector2[Math.Max(0, count)];
            distances = new double[positions.Length];

            if (length <= 0)
            {
                for (int i = 0; i < positions.Length; i++)
                    positions[i] = slider.Position;
                return positions;
            }

            double exponent = 1 + Math.Abs(acceleration);
            double wiggleAmount = wiggle * 2;
            bool mirror = accelerationMode == StreamAccelerationMode.Mirror;
            bool decelerate = acceleration < 0;
            double minProgress = spacing / length;

            for (int i = 0; i < positions.Length; i++)
            {
                double t = Math.Clamp(i * spacing / length, 0, 1);
                double progress;

                if (wiggleMode == StreamWiggleMode.LockSpeed)
                    progress = t;
                else if (!mirror)
                    progress = decelerate ? 1 - Math.Pow(1 - t, exponent) : Math.Pow(t, exponent);
                else if (decelerate)
                    progress = t < 0.5 ? 0.5 - Math.Pow(1 - t * 2, exponent) / 2 : 0.5 + Math.Pow(t * 2 - 1, exponent) / 2;
                else
                    progress = t < 0.5 ? Math.Pow(t * 2, exponent) / 2 : 1 - Math.Pow(2 - t * 2, exponent) / 2;

                distances[i] = progress * length;
                Vector2 position = slider.Position + slider.Path.PositionAt(progress);

                if (wiggleAmount != 0)
                {
                    double amplitude;

                    if (wiggleMode == StreamWiggleMode.LockWiggle)
                        amplitude = 1;
                    else if (!mirror)
                        amplitude = exponent * Math.Pow(Math.Max(minProgress, decelerate ? 1 - t : t), exponent - 1);
                    else if (decelerate)
                        amplitude = exponent * Math.Pow(Math.Max(minProgress, t < 0.5 ? 1 - t * 2 : t * 2 - 1), exponent - 1);
                    else
                        amplitude = exponent * Math.Pow(Math.Max(minProgress, t < 0.5 ? t * 2 : 2 - t * 2), exponent - 1);

                    amplitude *= Math.Max(spacing, slider.Radius / 2) * wiggleAmount;

                    double angle = angleAt(slider, progress) + (i % 2 == 0 ? -Math.PI / 2 : Math.PI / 2);
                    position += new Vector2((float)(Math.Cos(angle) * amplitude), (float)(Math.Sin(angle) * amplitude));
                }

                positions[i] = position;
            }

            return positions;
        }

        private static double angleAt(Slider slider, double progress)
        {
            const double delta = 0.001;

            Vector2 before = slider.Path.PositionAt(Math.Max(0, progress - delta));
            Vector2 after = slider.Path.PositionAt(Math.Min(1, progress + delta));
            Vector2 direction = after - before;

            return Math.Atan2(direction.Y, direction.X);
        }
    }

    public enum StreamSpacingMode
    {
        [Description("By distance snap")]
        DistanceSnap,

        [Description("By object count")]
        Count,
    }

    public enum StreamAccelerationMode
    {
        Normal,
        Mirror,
    }

    public enum StreamWiggleMode
    {
        Default,

        [Description("Lock speed")]
        LockSpeed,

        [Description("Lock wiggle")]
        LockWiggle,
    }
}
