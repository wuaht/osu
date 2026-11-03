// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A form control for picking a hue (in degrees, 0-360) on a rainbow slider.
    /// </summary>
    public partial class FormHueSlider : CompositeDrawable, IHasCurrentValue<int>, IFormControl
    {
        public Bindable<int> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        private readonly BindableNumberWithCurrent<int> current = new BindableNumberWithCurrent<int>
        {
            MinValue = 0,
            MaxValue = 360,
        };

        public LocalisableString Caption { get; init; }

        public LocalisableString HintText { get; init; }

        private FormControlBackground background = null!;
        private HueSelector selector = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChildren = new Drawable[]
            {
                background = new FormControlBackground(),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Padding = new MarginPadding(9),
                    Children = new Drawable[]
                    {
                        new FormFieldCaption
                        {
                            Caption = Caption,
                            TooltipText = HintText,
                            Colour = colourProvider.Content2,
                        },
                        selector = new HueSelector(),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            current.BindValueChanged(c =>
            {
                selector.Hue.Value = c.NewValue / 360f;
                background.FlashOnCommit();
                ValueChanged?.Invoke();
            }, true);

            selector.Hue.BindValueChanged(h => current.Value = (int)Math.Round(h.NewValue * 360));
        }

        public IEnumerable<LocalisableString> FilterTerms => new[] { Caption, HintText };

        public event Action? ValueChanged;

        public bool IsDefault => Current.IsDefault;

        public void SetDefault() => Current.SetDefault();

        public bool IsDisabled => Current.Disabled;

        public float MainDrawHeight => DrawHeight;

        private partial class HueSelector : HSVColourPicker.HueSelector
        {
            public HueSelector()
            {
                SliderBar.Height = 20;
                SliderBar.CornerRadius = 10;
                SliderBar.Masking = true;
            }

            protected override Drawable CreateSliderNub() => new Nub(Hue);

            private partial class Nub : CompositeDrawable
            {
                private readonly Bindable<float> hue;
                private readonly Box fill;

                public Nub(Bindable<float> hue)
                {
                    this.hue = hue.GetBoundCopy();

                    InternalChild = new CircularContainer
                    {
                        Height = 28,
                        Width = 10,
                        Origin = Anchor.Centre,
                        Anchor = Anchor.CentreLeft,
                        Masking = true,
                        BorderColour = Colour4.White,
                        BorderThickness = 3,
                        EdgeEffect = new EdgeEffectParameters
                        {
                            Type = EdgeEffectType.Shadow,
                            Offset = new Vector2(0, 1),
                            Radius = 3,
                            Colour = Colour4.Black.Opacity(0.3f),
                        },
                        Child = fill = new Box { RelativeSizeAxes = Axes.Both },
                    };
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();
                    hue.BindValueChanged(h => fill.Colour = Colour4.FromHSV(h.NewValue, 1, 1), true);
                }
            }
        }
    }
}
