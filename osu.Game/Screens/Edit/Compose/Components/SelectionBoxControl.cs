// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Screens.Edit.Components;

namespace osu.Game.Screens.Edit.Compose.Components
{
    /// <summary>
    /// Represents the base appearance for UI controls of the <see cref="SelectionBox"/>,
    /// such as scale handles, rotation handles, buttons, etc...
    /// </summary>
    public abstract partial class SelectionBoxControl : CompositeDrawable
    {
        public const double TRANSFORM_DURATION = 100;

        public event Action OperationStarted;
        public event Action OperationEnded;

        /// <summary>
        /// The background shape of this control. Displayed as a circle unless changed via <see cref="EditorAnchorShapeContainer.Shape"/>.
        /// </summary>
        protected EditorAnchorShapeContainer Circle { get; private set; }

        /// <summary>
        /// Whether the user is currently holding the control with mouse.
        /// </summary>
        public bool IsHeld { get; private set; }

        [Resolved]
        protected OsuColour Colours { get; private set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Origin = Anchor.Centre;

            InternalChildren = new Drawable[]
            {
                Circle = new EditorAnchorShapeContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            UpdateHoverState();
            FinishTransforms(true);
        }

        protected override bool OnHover(HoverEvent e)
        {
            UpdateHoverState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            UpdateHoverState();
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            IsHeld = true;
            UpdateHoverState();
            return true;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            IsHeld = false;
            UpdateHoverState();
        }

        /// <summary>
        /// The opacity of the control while it's not interacted with, so that it obstructs the playfield less.
        /// </summary>
        protected virtual float IdleAlpha => 0.5f;

        protected virtual void UpdateHoverState()
        {
            if (IsHeld)
                Circle.FadeColour(Colours.GrayF, TRANSFORM_DURATION, Easing.OutQuint);
            else
                Circle.FadeColour(IsHovered ? Colours.Red : Colours.YellowDark, TRANSFORM_DURATION, Easing.OutQuint);

            // only the circle is faded (rather than the whole control), as the alpha of the control itself is used for showing / hiding it.
            Circle.FadeTo(IsHeld || IsHovered ? 1 : IdleAlpha, TRANSFORM_DURATION, Easing.OutQuint);

            this.ScaleTo(IsHeld || IsHovered ? 1.5f : 1, TRANSFORM_DURATION, Easing.OutQuint);
        }

        protected void TriggerOperationStarted() => OperationStarted?.Invoke();

        protected void TriggerOperationEnded() => OperationEnded?.Invoke();
    }
}
