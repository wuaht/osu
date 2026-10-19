// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Graphics.UserInterfaceV2
{
    /// <summary>
    /// A form control for picking a single colour, which is stored as a hex string.
    /// </summary>
    public partial class FormColourPicker : CompositeDrawable, IHasCurrentValue<string>, IFormControl
    {
        private const float swatch_width = 90;

        public LocalisableString Caption { get; init; }
        public LocalisableString HintText { get; init; }

        private readonly BindableWithCurrent<string> current = new BindableWithCurrent<string>(string.Empty);

        public Bindable<string> Current
        {
            get => current.Current;
            set => current.Current = value;
        }

        /// <summary>
        /// The colour edited by the colour picker, which is kept in sync with <see cref="Current"/>.
        /// </summary>
        private readonly Bindable<Colour4> colour = new Bindable<Colour4>(Colour4.White);

        private FormControlBackground background = null!;
        private FormFieldCaption caption = null!;
        private ColourSwatch swatch = null!;

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
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding(9),
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Right = swatch_width + 5 },
                            Child = caption = new FormFieldCaption
                            {
                                Caption = Caption,
                                TooltipText = HintText,
                            },
                        },
                        swatch = new ColourSwatch
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Size = new Vector2(swatch_width, 30),
                            Current = { BindTarget = colour },
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            current.BindValueChanged(v =>
            {
                colour.Value = SlopColours.Parse(v.NewValue, colour.Value);

                background.FlashOnCommit();
                ValueChanged?.Invoke();
            }, true);

            colour.BindValueChanged(c =>
            {
                if (Current.Disabled)
                    return;

                // avoid rewriting an equivalent value (e.g. differing in case), which would mark the setting as non-default.
                if (SlopColours.Parse(Current.Value, Colour4.Transparent) != c.NewValue)
                    Current.Value = c.NewValue.ToHex();
            });

            current.BindDisabledChanged(_ => updateState(), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            base.OnHoverLost(e);
            updateState();
        }

        protected override bool OnClick(ClickEvent e)
        {
            swatch.TriggerClick();
            return true;
        }

        private void updateState()
        {
            caption.Colour = Current.Disabled ? colourProvider.Background1 : colourProvider.Content2;
            swatch.Enabled.Value = !Current.Disabled;

            if (IsDisabled)
                background.VisualStyle = VisualStyle.Disabled;
            else if (IsHovered)
                background.VisualStyle = VisualStyle.Hovered;
            else
                background.VisualStyle = VisualStyle.Normal;
        }

        public IEnumerable<LocalisableString> FilterTerms => new[] { Caption, HintText };

        public event Action? ValueChanged;

        public bool IsDefault => Current.IsDefault;

        public void SetDefault() => Current.SetDefault();

        public bool IsDisabled => Current.Disabled;

        public float MainDrawHeight => DrawHeight;

        private partial class ColourSwatch : OsuClickableContainer, IHasPopover
        {
            public Bindable<Colour4> Current { get; } = new Bindable<Colour4>();

            private Box fill = null!;
            private OsuSpriteText hexCode = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                Masking = true;
                CornerRadius = 5;
                Action = this.ShowPopover;

                Children = new Drawable[]
                {
                    fill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    hexCode = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(c =>
                {
                    fill.Colour = c.NewValue;
                    hexCode.Text = c.NewValue.ToHex();
                    hexCode.Colour = OsuColour.ForegroundTextColourFor(c.NewValue);
                }, true);
            }

            public Popover GetPopover() => new OsuPopover(false)
            {
                Child = new OsuColourPicker
                {
                    Current = { BindTarget = Current },
                },
            };
        }
    }
}
