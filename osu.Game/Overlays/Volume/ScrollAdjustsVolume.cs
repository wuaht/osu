// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osuTK;

namespace osu.Game.Overlays.Volume
{
    /// <summary>
    /// Add to a container or screen to make scrolling anywhere in the container cause the global game volume to be adjusted.
    /// </summary>
    /// <remarks>
    /// This is generally expected behaviour in many locations in osu!stable.
    /// </remarks>
    public partial class ScrollAdjustsVolume : Container
    {
        private readonly bool requireAltPressed;

        [Resolved]
        private VolumeOverlay? volumeOverlay { get; set; }

        public ScrollAdjustsVolume(bool requireAltPressed = false)
        {
            this.requireAltPressed = requireAltPressed;
            RelativeSizeAxes = Axes.Both;
        }

        private bool blockedAtLastPosition;
        private Vector2? lastPosition;

        protected override void Update()
        {
            base.Update();

            if (!requireAltPressed)
                return;

            var inputManager = GetContainingInputManager();

            if (inputManager == null)
                return;

            // hovered drawables alone aren't enough, as hover stops at the first drawable which handles it,
            // hiding any drawable behind it (e.g. the editor timeline) even though it would still receive the scroll.
            // the positional input queue can't be built while a scroll is being propagated through it, so it is checked ahead of time.
            // this isn't limited to while alt is held, as alt may be pressed in the same frame as the scroll.
            lastPosition = inputManager.CurrentState.Mouse.Position;
            blockedAtLastPosition = inputManager.PositionalInputQueue.Any(blocksGlobalAltScrollVolume);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (requireAltPressed)
            {
                if (!e.AltPressed || e.ControlPressed || e.ShiftPressed || e.SuperPressed)
                    return false;

                if (lastPosition == e.ScreenSpaceMousePosition && blockedAtLastPosition)
                    return false;

                var hoveredDrawables = GetContainingInputManager()?.HoveredDrawables;

                // covers the mouse having moved since the last check.
                // hover only propagates up to the first drawable which handles it (e.g. a hit object on the editor timeline),
                // so the ancestors of hovered drawables have to be checked as well.
                if (hoveredDrawables?.Any(blocksGlobalAltScrollVolume) == true)
                    return false;
            }

            if (e.ScrollDelta.Y == 0)
                return false;

            // forward any unhandled mouse scroll events to the volume control.
            return volumeOverlay?.Adjust(GlobalAction.IncreaseVolume, e.ScrollDelta.Y, e.IsPrecise) ?? false;
        }

        private static bool blocksGlobalAltScrollVolume(Drawable drawable)
        {
            for (Drawable? d = drawable; d != null; d = d.Parent)
            {
                if (d is IBlockGlobalAltScrollVolume)
                    return true;
            }

            return false;
        }
    }
}
