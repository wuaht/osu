// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    public partial class PreciseScalePopover : OsuPopover
    {
        private readonly OsuSelectionScaleHandler scaleHandler;

        private readonly OsuGridToolboxGroup gridToolbox;

        private readonly Bindable<PreciseScaleInfo> scaleInfo = new Bindable<PreciseScaleInfo>(new PreciseScaleInfo(1, EditorOrigin.GridCentre, true, true, SliderScaleMode.Default));

        private FormSliderBar<float> scaleInput { get; set; } = null!;
        private BindableNumber<float> scaleInputBindable = null!;
        private readonly CompactRadioButtonGroup scaleOrigin = new CompactRadioButtonGroup();

        private CompactRadioButton gridCentreButton = null!;
        private CompactRadioButton playfieldCentreButton = null!;
        private CompactRadioButton selectionCentreButton = null!;

        private readonly CompactRadioButtonGroup sliderScaleMode = new CompactRadioButtonGroup();
        private CompactRadioButton defaultModeButton = null!;

        private CompactCheckbox xCheckBox = null!;
        private CompactCheckbox yCheckBox = null!;

        private Bindable<EditorOrigin> configScaleOrigin = null!;

        private BindableList<HitObject> selectedItems { get; } = new BindableList<HitObject>();

        public PreciseScalePopover(OsuSelectionScaleHandler scaleHandler, OsuGridToolboxGroup gridToolbox)
        {
            this.scaleHandler = scaleHandler;
            this.gridToolbox = gridToolbox;

            AllowableAnchors = new[] { Anchor.CentreLeft, Anchor.CentreRight };
        }

        [BackgroundDependencyLoader]
        private void load(EditorBeatmap editorBeatmap, OsuConfigManager config)
        {
            selectedItems.BindTo(editorBeatmap.SelectedHitObjects);

            configScaleOrigin = config.GetBindable<EditorOrigin>(OsuSetting.EditorScaleOrigin);

            Child = new FillFlowContainer
            {
                Width = 300,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(6),
                Children = new Drawable[]
                {
                    scaleInput = new FormSliderBar<float>
                    {
                        Caption = "Scale",
                        Current = scaleInputBindable = new BindableNumber<float>
                        {
                            MinValue = 0.05f,
                            MaxValue = 2,
                            Precision = 0.001f,
                            Value = 1,
                            Default = 1,
                        },
                        KeyboardStep = 0.01f,
                        TabbableContentContainer = this
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 6),
                            new Dimension(GridSizeMode.Absolute, 100),
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new PopoverGroupBox("Origin")
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        gridCentreButton = scaleOrigin.Add(new CompactRadioButton("Grid centre", () => setOrigin(EditorOrigin.GridCentre))),
                                        playfieldCentreButton = scaleOrigin.Add(new CompactRadioButton("Playfield centre", () => setOrigin(EditorOrigin.PlayfieldCentre))),
                                        selectionCentreButton = scaleOrigin.Add(new CompactRadioButton("Selection centre", () => setOrigin(EditorOrigin.SelectionCentre))),
                                    }
                                },
                                Empty(),
                                // matches the height of the origin box, so that both boxes line up.
                                new PopoverGroupBox("Apply to")
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        xCheckBox = new CompactCheckbox("X-axis") { Current = { Value = true } },
                                        yCheckBox = new CompactCheckbox("Y-axis") { Current = { Value = true } },
                                    }
                                },
                            }
                        }
                    },
                    new PopoverGroupBox("Slider paths", FillDirection.Horizontal)
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            defaultModeButton = sliderScaleMode.Add(new CompactRadioButton("Default", () => setMode(SliderScaleMode.Default))),
                            sliderScaleMode.Add(new CompactRadioButton("Adjust SV", () => setMode(SliderScaleMode.AdjustSV))
                            {
                                TooltipText = "Scale slider paths and keep their durations by adjusting slider velocity. Hold Ctrl while dragging to use this mode.",
                            }),
                            sliderScaleMode.Add(new CompactRadioButton("Ignore paths", () => setMode(SliderScaleMode.IgnorePaths))
                            {
                                TooltipText = "Only scale object positions, leaving slider paths untouched. Hold P while dragging to use this mode.",
                            }),
                        }
                    },
                }
            };

            gridCentreButton.Selected.DisabledChanged += isDisabled =>
            {
                gridCentreButton.TooltipText = isDisabled ? "The current selection cannot be scaled relative to grid centre." : string.Empty;
            };
            playfieldCentreButton.Selected.DisabledChanged += isDisabled =>
            {
                playfieldCentreButton.TooltipText = isDisabled ? "The current selection cannot be scaled relative to playfield centre." : string.Empty;
            };
            selectionCentreButton.Selected.DisabledChanged += isDisabled =>
            {
                selectionCentreButton.TooltipText = isDisabled ? "The current selection cannot be scaled relative to its centre." : string.Empty;
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ScheduleAfterChildren(() => scaleInput.TakeFocus());
            scaleInput.Current.BindValueChanged(scale => scaleInfo.Value = scaleInfo.Value with { Scale = scale.NewValue });

            xCheckBox.Current.BindValueChanged(_ =>
            {
                if (!xCheckBox.Current.Value && !yCheckBox.Current.Value)
                {
                    yCheckBox.Current.Value = true;
                    return;
                }

                updateAxes();
            });
            yCheckBox.Current.BindValueChanged(_ =>
            {
                if (!xCheckBox.Current.Value && !yCheckBox.Current.Value)
                {
                    xCheckBox.Current.Value = true;
                    return;
                }

                updateAxes();
            });

            selectionCentreButton.Selected.Disabled = !(scaleHandler.CanScaleX.Value || scaleHandler.CanScaleY.Value);
            playfieldCentreButton.Selected.Disabled = scaleHandler.IsScalingSlider.Value && !selectionCentreButton.Selected.Disabled;
            gridCentreButton.Selected.Disabled = playfieldCentreButton.Selected.Disabled;

            bool didSelect = false;

            configScaleOrigin.BindValueChanged(_ =>
            {
                switch (configScaleOrigin.Value)
                {
                    case EditorOrigin.GridCentre:
                        if (!gridCentreButton.Selected.Disabled)
                        {
                            gridCentreButton.Select();
                            didSelect = true;
                        }

                        break;

                    case EditorOrigin.PlayfieldCentre:
                        if (!playfieldCentreButton.Selected.Disabled)
                        {
                            playfieldCentreButton.Select();
                            didSelect = true;
                        }

                        break;

                    case EditorOrigin.SelectionCentre:
                        if (!selectionCentreButton.Selected.Disabled)
                        {
                            selectionCentreButton.Select();
                            didSelect = true;
                        }

                        break;
                }
            }, true);

            if (!didSelect)
                scaleOrigin.Items.First(b => !b.Selected.Disabled).Select();

            defaultModeButton.Select();

            gridCentreButton.Selected.BindValueChanged(b =>
            {
                if (b.NewValue) configScaleOrigin.Value = EditorOrigin.GridCentre;
            });
            playfieldCentreButton.Selected.BindValueChanged(b =>
            {
                if (b.NewValue) configScaleOrigin.Value = EditorOrigin.PlayfieldCentre;
            });
            selectionCentreButton.Selected.BindValueChanged(b =>
            {
                if (b.NewValue) configScaleOrigin.Value = EditorOrigin.SelectionCentre;
            });

            scaleInfo.BindValueChanged(scale =>
            {
                // can happen if the popover is dismissed by a keyboard key press while dragging UI controls
                if (!scaleHandler.OperationInProgress.Value)
                    return;

                var newScale = new Vector2(scale.NewValue.Scale, scale.NewValue.Scale);
                scaleHandler.ScaleMode.Value = scale.NewValue.Mode;
                scaleHandler.Update(newScale, getOriginPosition(scale.NewValue), getAdjustAxis(scale.NewValue), getRotation(scale.NewValue));
            });
        }

        private void updateAxes()
        {
            scaleInfo.Value = scaleInfo.Value with { XAxis = xCheckBox.Current.Value, YAxis = yCheckBox.Current.Value };
            updateMinMaxScale();
        }

        private void updateAxisCheckBoxesEnabled()
        {
            if (scaleInfo.Value.Origin != EditorOrigin.SelectionCentre)
            {
                toggleAxisAvailable(xCheckBox.Current, true);
                toggleAxisAvailable(yCheckBox.Current, true);
            }
            else
            {
                toggleAxisAvailable(xCheckBox.Current, scaleHandler.CanScaleX.Value);
                toggleAxisAvailable(yCheckBox.Current, scaleHandler.CanScaleY.Value);
            }
        }

        private void toggleAxisAvailable(Bindable<bool> axisBindable, bool available)
        {
            // enable the bindable to allow setting the value
            axisBindable.Disabled = false;
            // restore the presumed default value given the axis's new availability state
            axisBindable.Value = available;
            axisBindable.Disabled = !available;
        }

        private void updateMinMaxScale()
        {
            if (!scaleHandler.OriginalSurroundingQuad.HasValue)
                return;

            const float min_scale = 0.05f;
            const float max_scale = 10;

            var scale = scaleHandler.ClampScaleToPlayfieldBounds(new Vector2(max_scale), getOriginPosition(scaleInfo.Value), getAdjustAxis(scaleInfo.Value), getRotation(scaleInfo.Value));

            if (!scaleInfo.Value.XAxis)
                scale.X = max_scale;
            if (!scaleInfo.Value.YAxis)
                scale.Y = max_scale;

            scaleInputBindable.MaxValue = MathF.Max(1, MathF.Min(scale.X, scale.Y));

            scale = scaleHandler.ClampScaleToPlayfieldBounds(new Vector2(min_scale), getOriginPosition(scaleInfo.Value), getAdjustAxis(scaleInfo.Value), getRotation(scaleInfo.Value));

            if (!scaleInfo.Value.XAxis)
                scale.X = min_scale;
            if (!scaleInfo.Value.YAxis)
                scale.Y = min_scale;

            scaleInputBindable.MinValue = MathF.Min(1, MathF.Max(scale.X, scale.Y));
        }

        private void setOrigin(EditorOrigin origin)
        {
            scaleInfo.Value = scaleInfo.Value with { Origin = origin };
            updateMinMaxScale();
            updateAxisCheckBoxesEnabled();
        }

        private void setMode(SliderScaleMode mode)
        {
            // set ahead of the update so that the playfield bounds clamping below already considers the new mode.
            scaleHandler.ScaleMode.Value = mode;
            scaleInfo.Value = scaleInfo.Value with { Mode = mode };
            updateMinMaxScale();
        }

        private Vector2? getOriginPosition(PreciseScaleInfo scale)
        {
            switch (scale.Origin)
            {
                case EditorOrigin.GridCentre:
                    return gridToolbox.StartPosition.Value;

                case EditorOrigin.PlayfieldCentre:
                    return OsuPlayfield.BASE_SIZE / 2;

                case EditorOrigin.SelectionCentre:
                    if (selectedItems.Count == 1 && selectedItems.First() is Slider slider)
                        return slider.Position;

                    return null;

                default:
                    throw new ArgumentOutOfRangeException(nameof(scale));
            }
        }

        private Axes getAdjustAxis(PreciseScaleInfo scale)
        {
            var result = Axes.None;

            if (scale.XAxis)
                result |= Axes.X;

            if (scale.YAxis)
                result |= Axes.Y;

            return result;
        }

        private float getRotation(PreciseScaleInfo scale) => scale.Origin == EditorOrigin.GridCentre ? gridToolbox.GridLinesRotation.Value : 0;

        protected override void PopIn()
        {
            base.PopIn();
            scaleHandler.Begin();
            updateMinMaxScale();
        }

        protected override void PopOut()
        {
            base.PopOut();

            if (IsLoaded) scaleHandler.Commit();
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Select && !e.Repeat)
            {
                this.HidePopover();
                return true;
            }

            return base.OnPressed(e);
        }
    }

    public record PreciseScaleInfo(float Scale, EditorOrigin Origin, bool XAxis, bool YAxis, SliderScaleMode Mode);
}
