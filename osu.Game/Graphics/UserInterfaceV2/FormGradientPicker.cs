// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Graphics.ColourEffects;
using osuTK;

namespace osu.Game.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A form control for editing a list of gradient colours stored as a comma-separated hex string,
    /// with an animated preview of the resulting effect.
    /// </summary>
    public partial class FormGradientPicker : CompositeDrawable, IHasCurrentValue<string>, IFormControl
    {
        private const int preview_segments = 64;

        public LocalisableString Caption { get; init; }
        public LocalisableString HintText { get; init; }

        /// <summary>
        /// The mode used to render the preview.
        /// </summary>
        public Bindable<ColourEffectMode> Mode { get; } = new Bindable<ColourEffectMode>(ColourEffectMode.Gradient);

        private readonly BindableWithCurrent<string> current = new BindableWithCurrent<string>(string.Empty);

        public Bindable<string> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        [Resolved(CanBeNull = true)]
        private ColourEffectProvider? colourEffectProvider { get; set; }

        private FormColourPalette palette = null!;
        private FillFlowContainer<Box> previewSegments = null!;
        private Container previewMarker = null!;

        private bool updatingFromCurrent;
        private List<Colour4> stops = new List<Colour4>();

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    palette = new FormColourPalette
                    {
                        Caption = Caption,
                        HintText = HintText,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 12,
                        Masking = true,
                        CornerRadius = 4,
                        Children = new Drawable[]
                        {
                            previewSegments = new FillFlowContainer<Box>
                            {
                                RelativeSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                            },
                            previewMarker = new Container
                            {
                                RelativePositionAxes = Axes.X,
                                RelativeSizeAxes = Axes.Y,
                                Width = 3,
                                Origin = Anchor.TopCentre,
                                Masking = true,
                                CornerRadius = 1.5f,
                                BorderThickness = 1,
                                BorderColour = Colour4.Black,
                                Child = new Box { RelativeSizeAxes = Axes.Both },
                            },
                        },
                    },
                },
            };

            for (int i = 0; i < preview_segments; i++)
            {
                previewSegments.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = 1f / preview_segments,
                });
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(v =>
            {
                stops = ColourEffect.ParseGradient(v.NewValue);

                if (ColourEffect.SerialiseGradient(palette.Colours) != ColourEffect.SerialiseGradient(stops))
                {
                    updatingFromCurrent = true;
                    palette.Colours.Clear();
                    palette.Colours.AddRange(stops);
                    updatingFromCurrent = false;
                }

                updatePreview();
                ValueChanged?.Invoke();
            }, true);

            palette.Colours.BindCollectionChanged((_, _) =>
            {
                if (updatingFromCurrent || Current.Disabled)
                    return;

                Current.Value = ColourEffect.SerialiseGradient(palette.Colours);
            });

            Mode.BindValueChanged(_ => updatePreview(), true);
        }

        protected override void Update()
        {
            base.Update();

            previewMarker.X = colourEffectProvider?.Phase ?? 0;
        }

        private void updatePreview()
        {
            for (int i = 0; i < preview_segments; i++)
                previewSegments.Children[i].Colour = ColourEffect.GetColour(Mode.Value, stops, (float)i / preview_segments);
        }

        public IEnumerable<LocalisableString> FilterTerms => new[] { Caption, HintText };

        public event Action? ValueChanged;

        public bool IsDefault => Current.IsDefault;

        public void SetDefault() => Current.SetDefault();

        public bool IsDisabled => Current.Disabled;

        public float MainDrawHeight => DrawHeight;
    }
}
