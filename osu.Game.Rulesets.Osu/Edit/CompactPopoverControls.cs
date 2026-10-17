// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// A titled, inset box grouping a few compact controls inside a popover.
    /// </summary>
    public partial class PopoverGroupBox : Container
    {
        private readonly Box background;
        private readonly OsuSpriteText titleText;
        private readonly FillFlowContainer content;

        protected override Container<Drawable> Content => content;

        public PopoverGroupBox(LocalisableString title, FillDirection direction = FillDirection.Vertical)
        {
            Masking = true;
            CornerRadius = 5;

            InternalChildren = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Padding = new MarginPadding { Horizontal = 10, Vertical = 8 },
                    Children = new Drawable[]
                    {
                        titleText = new OsuSpriteText
                        {
                            Text = title,
                            Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        },
                        content = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = direction,
                            Spacing = direction == FillDirection.Vertical ? new Vector2(0, 5) : new Vector2(14, 0),
                        },
                    }
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background5;
            titleText.Colour = colourProvider.Content2;
        }
    }

    /// <summary>
    /// A small radio button consisting of a round indicator and a label.
    /// </summary>
    public partial class CompactRadioButton : OsuClickableContainer
    {
        /// <summary>
        /// Whether this <see cref="CompactRadioButton"/> is selected.
        /// Disable this bindable to disable the button.
        /// </summary>
        public readonly BindableBool Selected = new BindableBool();

        private readonly Action? action;

        private readonly CircularContainer ring;
        private readonly Circle dot;
        private readonly OsuSpriteText label;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public CompactRadioButton(LocalisableString text, Action? action)
        {
            this.action = action;

            AutoSizeAxes = Axes.Both;
            Action = Select;

            Child = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(12),
                        Children = new Drawable[]
                        {
                            ring = new CircularContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                BorderThickness = 1.5f,
                                Child = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    AlwaysPresent = true,
                                },
                            },
                            dot = new Circle
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(0.5f),
                            },
                        }
                    },
                    label = new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = text,
                        Font = OsuFont.Default.With(size: 14),
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Selected.BindValueChanged(selected =>
            {
                updateState();
                if (selected.NewValue)
                    action?.Invoke();
            }, true);

            Selected.BindDisabledChanged(disabled =>
            {
                Enabled.Value = !disabled;
                updateState();
            }, true);
        }

        /// <summary>
        /// Selects this <see cref="CompactRadioButton"/>.
        /// </summary>
        public void Select() => Selected.Value = true;

        /// <summary>
        /// Deselects this <see cref="CompactRadioButton"/>.
        /// </summary>
        public void Deselect() => Selected.Value = false;

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        private void updateState()
        {
            if (!IsLoaded)
                return;

            bool highlighted = Selected.Value || (IsHovered && Enabled.Value);

            ring.BorderColour = highlighted ? colourProvider.Highlight1 : colourProvider.Light4;
            dot.Colour = colourProvider.Highlight1;
            dot.FadeTo(Selected.Value ? 1 : 0, 100, Easing.OutQuint);
            label.Colour = highlighted ? colourProvider.Content1 : colourProvider.Content2;
            this.FadeTo(Enabled.Value ? 1 : 0.3f, 100, Easing.OutQuint);
        }
    }

    /// <summary>
    /// Keeps at most one of a set of <see cref="CompactRadioButton"/>s selected.
    /// </summary>
    public class CompactRadioButtonGroup
    {
        private readonly List<CompactRadioButton> items = new List<CompactRadioButton>();

        public IEnumerable<CompactRadioButton> Items => items;

        private CompactRadioButton? currentlySelected;

        public CompactRadioButton Add(CompactRadioButton button)
        {
            button.Selected.ValueChanged += selected =>
            {
                if (selected.NewValue)
                {
                    currentlySelected?.Deselect();
                    currentlySelected = button;
                }
                else if (currentlySelected == button)
                    currentlySelected = null;
            };

            items.Add(button);
            return button;
        }
    }

    /// <summary>
    /// A small checkbox consisting of a square indicator and a label.
    /// </summary>
    public partial class CompactCheckbox : Checkbox
    {
        private readonly Container box;
        private readonly Box fill;
        private readonly SpriteIcon tick;
        private readonly OsuSpriteText label;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public CompactCheckbox(LocalisableString text)
        {
            AutoSizeAxes = Axes.Both;

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        box = new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(12),
                            Masking = true,
                            CornerRadius = 2,
                            BorderThickness = 1.5f,
                            Children = new Drawable[]
                            {
                                fill = new Box { RelativeSizeAxes = Axes.Both },
                                tick = new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Size = new Vector2(8),
                                    Icon = FontAwesome.Solid.Check,
                                },
                            }
                        },
                        label = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = OsuFont.Default.With(size: 14),
                        },
                    }
                },
                new HoverClickSounds(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(_ => updateState());
            Current.BindDisabledChanged(_ => updateState(), true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        private void updateState()
        {
            if (!IsLoaded)
                return;

            bool highlighted = Current.Value || (IsHovered && !Current.Disabled);

            box.BorderColour = highlighted ? colourProvider.Highlight1 : colourProvider.Light4;
            fill.Colour = Current.Value ? colourProvider.Highlight1 : Color4.Transparent;
            tick.Colour = colourProvider.Background5;
            tick.FadeTo(Current.Value ? 1 : 0, 100, Easing.OutQuint);
            label.Colour = highlighted ? colourProvider.Content1 : colourProvider.Content2;
            this.FadeTo(Current.Disabled ? 0.3f : 1, 100, Easing.OutQuint);
        }
    }
}
