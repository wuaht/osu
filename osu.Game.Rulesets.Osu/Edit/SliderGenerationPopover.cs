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
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// An invisible drawable next to the slider generator button, which hosts a <see cref="SliderGenerationPopover"/>.
    /// </summary>
    public partial class SliderGenerationAnchor : DraggablePopoverAnchor
    {
        private readonly Action onClosed;

        // the popover is hosted by the popover container outside of the composer, so it can't resolve the toolbox itself.
        [Resolved]
        private OsuSliderVelocityToolboxGroup? sliderVelocityToolbox { get; set; }

        public SliderGenerationAnchor(Action onClosed)
        {
            this.onClosed = onClosed;
        }

        protected override DraggablePopover CreatePopover() => new SliderGenerationPopover(sliderVelocityToolbox) { Closed = onClosed };
    }

    /// <summary>
    /// Previews and creates a slider shaped like the ones Slider Painter generates.
    /// </summary>
    public partial class SliderGenerationPopover : DraggablePopover
    {
        private FormSliderBar<int> segmentsInput = null!;
        private FormSliderBar<double> arcChanceInput = null!;
        private FormSliderBar<double> lengthInput = null!;
        private FormSliderBar<double> velocityInput = null!;
        private FormSliderBar<float> rotationInput = null!;
        private FormNumberBox seedInput = null!;
        private RoundedButton createButton = null!;

        private readonly IBindable<double> toolboxVelocity = new Bindable<double>(1);

        private Slider? slider;
        private HitObject[] previousSelection = Array.Empty<HitObject>();
        private bool began;
        private bool committed;

        [Resolved]
        private IBeatSnapProvider beatSnapProvider { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private readonly OsuSliderVelocityToolboxGroup? sliderVelocityToolbox;

        public SliderGenerationPopover(OsuSliderVelocityToolboxGroup? sliderVelocityToolbox)
        {
            this.sliderVelocityToolbox = sliderVelocityToolbox;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };

            Child = new FillFlowContainer
            {
                Width = 240,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = SlopEditorStrings.SliderGenerator,
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                        Alpha = 0.6f,
                        Margin = new MarginPadding { Bottom = 3 },
                    },
                    segmentsInput = new FormSliderBar<int>
                    {
                        Caption = SlopEditorStrings.SliderGeneratorSegments,
                        Current = new BindableNumber<int>(6)
                        {
                            MinValue = 1,
                            MaxValue = 32,
                            Precision = 1,
                        },
                        TabbableContentContainer = this
                    },
                    arcChanceInput = new FormSliderBar<double>
                    {
                        Caption = SlopEditorStrings.SliderGeneratorArcChance,
                        Current = new BindableNumber<double>(0.5)
                        {
                            MinValue = 0,
                            MaxValue = 1,
                            Precision = 0.05,
                        },
                        TabbableContentContainer = this
                    },
                    lengthInput = new FormSliderBar<double>
                    {
                        Caption = SlopEditorStrings.SliderGeneratorLength,
                        Current = new BindableNumber<double>(2)
                        {
                            MinValue = 0.25,
                            MaxValue = 16,
                            Precision = 0.25,
                        },
                        TabbableContentContainer = this
                    },
                    velocityInput = new FormSliderBar<double>
                    {
                        Caption = SlopEditorStrings.SliderVelocity,
                        Current = new BindableNumber<double>(1)
                        {
                            MinValue = 0.1,
                            MaxValue = 10,
                            Precision = 0.01,
                        },
                        TabbableContentContainer = this
                    },
                    rotationInput = new FormSliderBar<float>
                    {
                        Caption = SlopEditorStrings.SliderGeneratorRotation,
                        Current = new BindableNumber<float>
                        {
                            MinValue = -180,
                            MaxValue = 180,
                            Precision = 1,
                        },
                        TabbableContentContainer = this
                    },
                    seedInput = new FormNumberBox
                    {
                        Caption = SlopEditorStrings.SliderGeneratorSeed,
                        Current = { Value = newSeed() },
                        TabbableContentContainer = this
                    },
                    new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = SlopEditorStrings.SliderGeneratorNewSeed,
                        Action = () => seedInput.Current.Value = newSeed(),
                    },
                    createButton = new RoundedButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Text = SlopEditorStrings.SliderGeneratorCreate,
                        Action = commit,
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            changeHandler?.BeginChange();
            began = true;

            // the toolbox can't be clicked while the window is open, but its velocity still changes through the hotkeys.
            if (sliderVelocityToolbox != null)
                toolboxVelocity.BindTo(sliderVelocityToolbox.SliderVelocity);
            toolboxVelocity.BindValueChanged(v => velocityInput.Current.Value = v.NewValue, true);

            segmentsInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateSlider));
            arcChanceInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateSlider));
            lengthInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateSlider));
            velocityInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateSlider));
            rotationInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateSlider));
            seedInput.Current.BindValueChanged(_ => Scheduler.AddOnce(updateSlider));
            updateSlider();
        }

        private static string newSeed() => RNG.Next(1_000_000).ToString(CultureInfo.InvariantCulture);

        private void updateSlider()
        {
            if (!int.TryParse(seedInput.Current.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
            {
                createButton.Enabled.Value = false;
                return;
            }

            double startTime = beatSnapProvider.SnapTime(editorClock.CurrentTime);
            double sliderVelocity = velocityInput.Current.Value;

            // like Slider Painter, the arcs are sized so that the whole shape is about as long as the slider.
            double length = lengthInput.Current.Value * OsuHitObject.BASE_SCORING_DISTANCE * editorBeatmap.Difficulty.SliderMultiplier * sliderVelocity;
            int segmentCount = segmentsInput.Current.Value;
            float radius = (float)(length / (segmentCount * PaintedSliderShapes.SEGMENT_LENGTH_RATIO));

            float rotation = MathHelper.DegreesToRadians(rotationInput.Current.Value);
            var segments = PaintedSliderShapes.Generate(segmentCount, arcChanceInput.Current.Value, radius, new Random(seed))
                                              .Select(s => (P0: rotate(s.P0, rotation), P1: rotate(s.P1, rotation), P2: rotate(s.P2, rotation)))
                                              .ToList();
            var head = OsuPlayfield.BASE_SIZE / 2 + segments[0].P0;

            var controlPoints = new List<PathControlPoint>();

            foreach (var segment in segments)
            {
                // every segment starts at a typed control point, so all joins between segments are red anchors.
                controlPoints.Add(new PathControlPoint(segment.P0 - segments[0].P0, PathType.BEZIER));
                controlPoints.Add(new PathControlPoint(segment.P1 - segments[0].P0));
            }

            controlPoints.Add(new PathControlPoint(segments[^1].P2 - segments[0].P0));

            if (slider == null)
            {
                slider = new Slider();
                slider.Samples.Add(slider.CreateHitSampleInfo());
                editorBeatmap.Add(slider);

                // selecting the slider shows its control points while it is being generated.
                previousSelection = editorBeatmap.SelectedHitObjects.ToArray();
                editorBeatmap.SelectedHitObjects.Clear();
                editorBeatmap.SelectedHitObjects.Add(slider);
            }

            slider.StartTime = startTime;
            slider.Position = head;
            slider.SliderVelocityMultiplier = sliderVelocity;
            slider.Path.ControlPoints.Clear();
            slider.Path.ControlPoints.AddRange(controlPoints);
            slider.Path.ExpectedDistance.Value = length;
            editorBeatmap.Update(slider);

            var pathPositions = new List<Vector2>();
            slider.Path.GetPathToProgress(pathPositions, 0, 1);

            createButton.Enabled.Value = pathPositions.Select(p => head + p).All(p =>
                p.X >= 0 && p.Y >= 0 && p.X <= OsuPlayfield.BASE_SIZE.X && p.Y <= OsuPlayfield.BASE_SIZE.Y);
        }

        // the shape is centred on the origin, so this rotates it around its centre.
        private static Vector2 rotate(Vector2 v, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

        private void commit()
        {
            if (!createButton.Enabled.Value)
                return;

            changeHandler?.EndChange();
            committed = true;
            this.HidePopover();
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

        protected override void PopOut()
        {
            base.PopOut();

            if (began && !committed)
            {
                if (slider != null)
                {
                    editorBeatmap.Remove(slider);
                    editorBeatmap.SelectedHitObjects.Clear();
                    editorBeatmap.SelectedHitObjects.AddRange(previousSelection.Where(editorBeatmap.HitObjects.Contains));
                }

                changeHandler?.EndChange();
                began = false;
            }
        }
    }
}
