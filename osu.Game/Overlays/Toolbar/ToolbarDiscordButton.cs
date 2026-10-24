// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Discord;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarDiscordButton : ToolbarOverlayToggleButton
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        public ToolbarDiscordButton()
        {
            Hotkey = GlobalAction.ToggleDiscord;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DiscordOverlay? discord)
        {
            StateContainer = discord;

            // The line icon matches the other toolbar icons.
            SetIcon(OsuIcon.Discord);
        }
    }
}
