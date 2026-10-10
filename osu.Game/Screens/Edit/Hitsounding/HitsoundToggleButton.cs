// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// A button which shows whether all, some or none of the targeted hitsounds have a property.
    /// </summary>
    public partial class HitsoundToggleButton : OsuClickableContainer, IHasHitsoundTooltip
    {
        private readonly LocalisableString label;
        private readonly Colour4 activeColour;

        private Box background = null!;
        private OsuSpriteText text = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private HitsoundToggleState state;

        /// <summary>
        /// Whether the active state is shown by an outline in the editor's accent colour instead of filling the button, for buttons whose active colour is too bright to fill them with.
        /// </summary>
        public bool Outlined { get; init; }

        public HitsoundToggleState State
        {
            get => state;
            set
            {
                if (state == value)
                    return;

                state = value;

                if (IsLoaded)
                    updateState();
            }
        }

        public HitsoundToggleButton(LocalisableString label, Colour4 activeColour)
        {
            this.label = label;
            this.activeColour = activeColour;

            RelativeSizeAxes = Axes.X;
            Height = 28;
            Masking = true;
            CornerRadius = 5;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddRange(new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                text = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold),
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Enabled.BindValueChanged(_ => updateState(), true);
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
            Alpha = Enabled.Value ? 1 : 0.4f;

            if (Outlined)
            {
                updateOutlinedState();
                return;
            }

            switch (state)
            {
                case HitsoundToggleState.On:
                    background.Colour = activeColour;
                    text.Colour = colourProvider.Background6;
                    break;

                case HitsoundToggleState.Mixed:
                    background.Colour = activeColour.Opacity(0.35f);
                    text.Colour = Colour4.White;
                    break;

                default:
                    background.Colour = IsHovered && Enabled.Value ? colourProvider.Background2 : colourProvider.Background3;
                    text.Colour = colourProvider.Content2;
                    break;
            }
        }

        private void updateOutlinedState()
        {
            background.Colour = IsHovered && Enabled.Value ? colourProvider.Background2 : colourProvider.Background3;

            var accent = colourProvider.Highlight1;

            switch (state)
            {
                case HitsoundToggleState.On:
                    BorderThickness = 2;
                    BorderColour = accent;
                    text.Colour = accent;
                    break;

                case HitsoundToggleState.Mixed:
                    BorderThickness = 2;
                    BorderColour = accent.Opacity(0.35f);
                    text.Colour = Colour4.White;
                    break;

                default:
                    BorderThickness = 0;
                    text.Colour = colourProvider.Content2;
                    break;
            }
        }
    }

    public enum HitsoundToggleState
    {
        Off,

        /// <summary>
        /// Some, but not all of the targeted hitsounds have the property.
        /// </summary>
        Mixed,

        On,
    }
}
