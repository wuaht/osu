// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.BnTracker;
using osuTK;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// Shows the status with a nominator, and opens a popover to change it.
    /// </summary>
    public partial class StatusButton : OsuClickableContainer, IHasPopover
    {
        /// <summary>
        /// Invoked when a status is picked.
        /// </summary>
        public Action<BnNominationStatus>? StatusPicked;

        private BnNominationStatus status;

        public BnNominationStatus Status
        {
            get => status;
            set
            {
                status = value;

                if (IsLoaded)
                    updateDisplay();
            }
        }

        private Box background = null!;
        private Circle dot = null!;
        private OsuSpriteText label = null!;
        private SpriteIcon chevron = null!;

        private readonly float fontSize;

        public StatusButton(float height = 28)
        {
            Height = height;

            // the text scales with the button, such that small buttons (e.g. on cards) stay compact.
            fontSize = Math.Min(14, height * 0.5f);
            AutoSizeAxes = Axes.X;
            Masking = true;
            CornerRadius = height / 2;

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
                    Alpha = 0.25f,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.X,
                    RelativeSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Padding = new MarginPadding { Horizontal = Height * 0.4f },
                    Children = new Drawable[]
                    {
                        dot = new Circle
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(8),
                        },
                        label = new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = OsuFont.Default.With(size: fontSize, weight: FontWeight.Bold),
                        },
                        chevron = new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(9),
                            Icon = FontAwesome.Solid.ChevronDown,
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Enabled.BindValueChanged(e =>
            {
                chevron.Alpha = e.NewValue ? 1 : 0;
                TooltipText = e.NewValue ? SlopNominatorsStrings.ChangeStatus : SlopNominatorsStrings.RemovedStatusLocked;
            }, true);

            updateDisplay();
        }

        private void updateDisplay()
        {
            var colour = NominatorsDisplay.GetColour(status);

            background.Colour = colour;
            dot.Colour = colour;
            label.Colour = colour;
            chevron.Colour = colour;
            label.Text = NominatorsDisplay.GetName(status);
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (Enabled.Value)
                background.FadeTo(0.4f, 100);

            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeTo(0.25f, 100);
            base.OnHoverLost(e);
        }

        public Popover GetPopover() => new StatusPopover(status, s =>
        {
            this.HidePopover();
            StatusPicked?.Invoke(s);
        });

        /// <summary>
        /// A list of all statuses to pick from.
        /// </summary>
        public partial class StatusPopover : OsuPopover
        {
            public StatusPopover(BnNominationStatus current, Action<BnNominationStatus> picked)
                : base(false)
            {
                var flow = new FillFlowContainer
                {
                    Width = 170,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(5),
                };

                foreach (var s in Enum.GetValues<BnNominationStatus>())
                    flow.Add(new StatusOption(s, s == current) { Action = () => picked(s) });

                Child = flow;
            }
        }

        private partial class StatusOption : OsuClickableContainer
        {
            private readonly Box hover;

            public StatusOption(BnNominationStatus status, bool isCurrent)
            {
                RelativeSizeAxes = Axes.X;
                Height = 30;
                Masking = true;
                CornerRadius = 5;

                var colour = NominatorsDisplay.GetColour(status);

                Children = new Drawable[]
                {
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colour,
                        Alpha = isCurrent ? 0.25f : 0,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8, 0),
                        Padding = new MarginPadding { Horizontal = 10 },
                        Children = new Drawable[]
                        {
                            new Circle
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Size = new Vector2(8),
                                Colour = colour,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = NominatorsDisplay.GetName(status),
                                Font = OsuFont.Default.With(size: 14, weight: isCurrent ? FontWeight.Bold : FontWeight.Regular),
                            },
                        }
                    },
                };

                if (!isCurrent)
                {
                    hover.Colour = colour;
                    HoverAction = h => hover.FadeTo(h ? 0.15f : 0, 100);
                }
            }

            public Action<bool>? HoverAction;

            protected override bool OnHover(HoverEvent e)
            {
                HoverAction?.Invoke(true);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                HoverAction?.Invoke(false);
                base.OnHoverLost(e);
            }
        }
    }
}
