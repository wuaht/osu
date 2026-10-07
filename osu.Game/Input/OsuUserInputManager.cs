// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.States;
using osu.Game.Screens.Play;
using osuTK.Input;

namespace osu.Game.Input
{
    public partial class OsuUserInputManager : UserInputManager
    {
        protected override bool AllowRightClickFromLongTouch => PlayingState.Value != LocalUserPlayingState.Playing;

        public readonly IBindable<LocalUserPlayingState> PlayingState = new Bindable<LocalUserPlayingState>();

        internal OsuUserInputManager()
        {
        }

        protected override MouseButtonEventManager CreateButtonEventManagerFor(MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    return new LeftMouseManager(button);

                case MouseButton.Right:
                    return new RightMouseManager(button);
            }

            return base.CreateButtonEventManagerFor(button);
        }

        /// <summary>
        /// Matches the framework's default left mouse button handling,
        /// with the addition of allowing drags to begin immediately for drawables implementing <see cref="IRequestImmediateDrag"/>.
        /// </summary>
        private class LeftMouseManager : MouseButtonEventManager
        {
            public LeftMouseManager(MouseButton button)
                : base(button)
            {
            }

            public override bool EnableDrag => true;
            public override bool EnableClick => true;
            public override bool ChangeFocusOnClick => true;

            private bool immediateDrag;

            public override float ClickDragDistance => immediateDrag ? 0 : base.ClickDragDistance;

            protected override Drawable? HandleButtonDown(InputState state, List<Drawable> targets)
            {
                var handledBy = base.HandleButtonDown(state, targets);

                immediateDrag = handledBy is IRequestImmediateDrag immediateDragTarget && immediateDragTarget.RequestsImmediateDrag;

                return handledBy;
            }
        }

        private class RightMouseManager : MouseButtonEventManager
        {
            public RightMouseManager(MouseButton button)
                : base(button)
            {
            }

            public override bool EnableDrag => true; // allow right-mouse dragging for absolute scroll in scroll containers.
            public override bool EnableClick => false;
            public override bool ChangeFocusOnClick => false;
        }
    }
}
