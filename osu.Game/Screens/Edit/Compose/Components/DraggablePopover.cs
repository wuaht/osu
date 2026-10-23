// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;

namespace osu.Game.Screens.Edit.Compose.Components
{
    /// <summary>
    /// An invisible drawable which hosts a <see cref="DraggablePopover"/> and is moved along when the popover is dragged.
    /// </summary>
    public abstract partial class DraggablePopoverAnchor : Drawable, IHasPopover
    {
        protected DraggablePopoverAnchor()
        {
            Origin = Anchor.Centre;
            Size = new Vector2(1);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Schedule(this.ShowPopover);
        }

        protected abstract DraggablePopover CreatePopover();

        public Popover GetPopover()
        {
            var popover = CreatePopover();

            popover.Closed += () => this.Delay(500).Expire();
            // the popover follows its target, so dragging it moves the anchor.
            popover.Pinned = screenSpacePosition => Position = Parent!.ToLocalSpace(screenSpacePosition);
            popover.Dragged = screenSpaceDelta => Position = Parent!.ToLocalSpace(Parent.ToScreenSpace(Position) + screenSpaceDelta);

            return popover;
        }
    }

    /// <summary>
    /// A popover which can be moved around by dragging its empty parts.
    /// </summary>
    public abstract partial class DraggablePopover : OsuPopover
    {
        public Action? Closed { get; set; }

        /// <summary>
        /// Invoked with the screen space distance the popover was dragged by.
        /// </summary>
        public Action<Vector2>? Dragged { get; set; }

        /// <summary>
        /// Invoked with the screen space centre of the popover when it starts being dragged for the first time.
        /// From then on, the popover is centred on its target instead of picking a side next to it.
        /// </summary>
        public Action<Vector2>? Pinned { get; set; }

        private bool pinned;

        // text boxes and buttons handle their own drags, so this only triggers on the empty parts of the popover.
        protected override bool OnDragStart(DragStartEvent e)
        {
            if (Dragged == null)
                return false;

            // picking the side with the most space would make the popover jump around while it is dragged.
            if (!pinned && Pinned != null)
            {
                Pinned(Body.ScreenSpaceDrawQuad.Centre);
                AllowableAnchors = new[] { Anchor.Centre };
                pinned = true;
            }

            return true;
        }

        protected override void OnDrag(DragEvent e) => Dragged?.Invoke(e.ScreenSpaceMousePosition - e.ScreenSpaceLastMousePosition);

        private bool wasShown;

        protected override void PopIn()
        {
            base.PopIn();
            wasShown = true;
        }

        protected override void PopOut()
        {
            base.PopOut();

            // PopOut is also called when the popover is loaded in its initial hidden state, which must not close it.
            if (wasShown)
                Closed?.Invoke();
        }
    }
}
