// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

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
        };
    }

    public partial class StreamConversionPopover : OsuPopover
    {
        private static readonly int[] divisors = { 1, 2, 3, 4, 6, 8, 12, 16 };

        /// <summary>
        /// The divisor chosen last time, so that it stays the same across conversions.
        /// </summary>
        private static int? savedDivisor;

        public Action? Closed { get; init; }

        private readonly Slider slider;

        private FormEnumDropdown<StreamSpacingMode> spacingModeInput = null!;
        private FormSliderBar<double> distanceSnapInput = null!;
        private FormSliderBar<int> countInput = null!;
        private FormDropdown<int> divisorInput = null!;
        private FormSliderBar<double> accelerationInput = null!;
        private FormEnumDropdown<StreamAccelerationMode> accelerationModeInput = null!;
        private FormSliderBar<double> wiggleInput = null!;
        private FormEnumDropdown<StreamWiggleMode> wiggleModeInput = null!;
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

            int defaultDivisor = savedDivisor ?? (editorBeatmap.BeatDivisor is 3 or 6 ? 3 : 4);

            Child = new FillFlowContainer
            {
                Width = 260,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    spacingModeInput = new FormEnumDropdown<StreamSpacingMode>
                    {
                        Caption = "Spacing",
                    },
                    distanceSnapInput = new FormSliderBar<double>
                    {
                        Caption = "Distance snap",
                        Current = new BindableNumber<double>(1)
                        {
                            MinValue = 0.1,
                            MaxValue = 9,
                            Precision = 0.01,
                        },
                        TabbableContentContainer = this
                    },
                    countInput = new FormSliderBar<int>
                    {
                        Caption = "Object count",
                        Current = new BindableNumber<int>(5)
                        {
                            MinValue = 1,
                            MaxValue = 64,
                            Precision = 1,
                        },
                        TabbableContentContainer = this
                    },
                    divisorInput = new DivisorDropdown
                    {
                        Caption = "Beat snap divisor",
                        Items = divisors,
                        Current = { Value = defaultDivisor },
                    },
                    accelerationInput = new FormSliderBar<double>
                    {
                        Caption = "Acceleration",
                        Current = new BindableNumber<double>
                        {
                            MinValue = -2,
                            MaxValue = 2,
                            Precision = 0.01,
                        },
                        TabbableContentContainer = this
                    },
                    accelerationModeInput = new FormEnumDropdown<StreamAccelerationMode>
                    {
                        Caption = "Acceleration mode",
                    },
                    spacingText = new OsuSpriteText
                    {
                        Font = OsuFont.Default.With(size: 14),
                        Margin = new MarginPadding { Horizontal = 9 },
                    },
                    wiggleInput = new FormSliderBar<double>
                    {
                        Caption = "Wiggle",
                        Current = new BindableNumber<double>
                        {
                            MinValue = -1,
                            MaxValue = 1,
                            Precision = 0.01,
                        },
                        TabbableContentContainer = this
                    },
                    wiggleModeInput = new FormEnumDropdown<StreamWiggleMode>
                    {
                        Caption = "Wiggle mode",
                    },
                    new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = "Convert",
                        Action = commit
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            changeHandler?.BeginChange();
            began = true;

            editorBeatmap.Remove(slider);

            spacingModeInput.Current.BindValueChanged(mode =>
            {
                distanceSnapInput.Alpha = mode.NewValue == StreamSpacingMode.DistanceSnap ? 1 : 0;
                countInput.Alpha = mode.NewValue == StreamSpacingMode.Count ? 1 : 0;
                Scheduler.AddOnce(updateStream);
            }, true);

            distanceSnapInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            countInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            divisorInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            accelerationInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            accelerationModeInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            wiggleInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
            wiggleModeInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateStream));
        }

        private void updateStream()
        {
            savedDivisor = divisorInput.Current.Value;

            double length = slider.Path.Distance;
            double timeSpacing = editorBeatmap.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength / divisorInput.Current.Value;

            int count;
            double spacing;

            if (spacingModeInput.Current.Value == StreamSpacingMode.Count)
            {
                count = countInput.Current.Value;
                spacing = length / Math.Max(1, count - 1);
            }
            else
            {
                spacing = slider.Velocity * timeSpacing * distanceSnapInput.Current.Value;
                count = spacing > 0 ? (int)((length + 1) / spacing) + 1 : 1;
            }

            var positions = StreamGenerator.Generate(slider, count, spacing, accelerationInput.Current.Value, accelerationModeInput.Current.Value,
                wiggleInput.Current.Value, wiggleModeInput.Current.Value, out double[] distances);

            editorBeatmap.RemoveRange(insertedCircles);
            insertedCircles.Clear();

            for (int i = 0; i < positions.Length; i++)
            {
                insertedCircles.Add(new HitCircle
                {
                    StartTime = slider.StartTime + i * timeSpacing,
                    Position = positions[i],
                    NewCombo = i == 0 && slider.NewCombo,
                    Samples = slider.HeadCircle.Samples.Select(s => s.With()).ToList()
                });
            }

            editorBeatmap.AddRange(insertedCircles);

            if (count > 1)
            {
                double snapDistance = slider.Velocity * timeSpacing;
                bool mirror = accelerationModeInput.Current.Value == StreamAccelerationMode.Mirror;
                double first = (distances[1] - distances[0]) / snapDistance;
                double last = mirror && count > 2
                    ? (distances[count / 2 + 1] - distances[count / 2]) / snapDistance
                    : (distances[^1] - distances[^2]) / snapDistance;

                spacingText.Text = $"Spacing: {first:F2}x {(mirror ? "<->" : "->")} {last:F2}x";
            }
            else
                spacingText.Text = string.Empty;
        }

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
            changeHandler?.EndChange();
            committed = true;
            editorBeatmap.SelectedHitObjects.Clear();
            editorBeatmap.SelectedHitObjects.AddRange(insertedCircles);
            Hide();
        }

        protected override void PopOut()
        {
            base.PopOut();

            if (began && !committed)
            {
                editorBeatmap.RemoveRange(insertedCircles);
                editorBeatmap.Add(slider);
                changeHandler?.EndChange();
                began = false;
            }

            Closed?.Invoke();
        }

        private partial class DivisorDropdown : FormDropdown<int>
        {
            protected override LocalisableString GenerateItemText(int item) => $"1/{item}";
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
