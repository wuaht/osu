// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Input.Handlers;
using osu.Framework.Input.StateChanges;
using osu.Framework.Platform;

namespace osu.Game.Input
{
    /// <summary>
    /// Feeds synthetic input into the game without touching the OS cursor or keyboard.
    /// </summary>
    public class AutomationInputHandler : InputHandler
    {
        public override bool Initialize(GameHost host) => true;

        public override bool IsActive => true;

        public void Enqueue(IInput input) => PendingInputs.Enqueue(input);
    }
}
