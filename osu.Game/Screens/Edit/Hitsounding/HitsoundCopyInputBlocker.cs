// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Input.Bindings;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Blocks all input to the editor (including its menu and bottom bars) while hitsounds are being copied to other difficulties
    /// (see <see cref="HitsoundDifficultySync.Running"/>), as e.g. saving the current difficulty again would write its outdated beatmap set back to the database.
    /// </summary>
    public partial class HitsoundCopyInputBlocker : Drawable, IKeyBindingHandler<GlobalAction>, IKeyBindingHandler<PlatformAction>
    {
        public HitsoundCopyInputBlocker()
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        private static bool blocking => HitsoundDifficultySync.Running?.IsCompleted == false;

        public override bool PropagatePositionalInputSubTree => blocking;

        public override bool PropagateNonPositionalInputSubTree => blocking;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        protected override bool Handle(UIEvent e)
        {
            if (e is ScrollEvent or MouseEvent or KeyDownEvent)
                return true;

            return base.Handle(e);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e) => true;

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public bool OnPressed(KeyBindingPressEvent<PlatformAction> e) => true;

        public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
        {
        }
    }
}
