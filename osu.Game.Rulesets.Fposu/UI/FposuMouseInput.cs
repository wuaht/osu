// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Platform;
using osuTK;

namespace osu.Game.Rulesets.Fposu.UI
{
    /// <summary>
    /// Collects raw relative mouse movement, which (unlike the cursor position) isn't limited by the bounds of the window.
    /// </summary>
    public partial class FposuMouseInput : Component
    {
        private readonly object accumulationLock = new object();

        private Vector2 accumulatedMovement;

        private MouseHandler? mouseHandler;

        /// <summary>
        /// Whether raw relative movement is currently available. Otherwise, the camera has to follow the cursor position instead.
        /// </summary>
        public bool IsAvailable => AvailabilityOverride ?? mouseHandler?.IsRelativeModeActive == true;

        /// <summary>
        /// Overrides <see cref="IsAvailable"/>, e.g. for testing without a mouse.
        /// </summary>
        public bool? AvailabilityOverride { get; set; }

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            mouseHandler = host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault();

            if (mouseHandler != null)
                mouseHandler.RawRelativeMovement += onRawRelativeMovement;
        }

        /// <summary>
        /// Returns the movement in device units since the last call, and resets it.
        /// </summary>
        public Vector2 ConsumeMovement()
        {
            lock (accumulationLock)
            {
                Vector2 movement = accumulatedMovement;
                accumulatedMovement = Vector2.Zero;
                return movement;
            }
        }

        /// <summary>
        /// Adds movement in device units. Thread-safe.
        /// </summary>
        public void AddMovement(Vector2 movement)
        {
            lock (accumulationLock)
                accumulatedMovement += movement;
        }

        // invoked on the thread handling window events.
        private void onRawRelativeMovement(Vector2 movement) => AddMovement(movement);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (mouseHandler != null)
                mouseHandler.RawRelativeMovement -= onRawRelativeMovement;
        }
    }
}
