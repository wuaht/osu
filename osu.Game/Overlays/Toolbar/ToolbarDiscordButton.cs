// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Discord;
using osuTK;

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
        private void load(DiscordOverlay? discord, TextureStore textures)
        {
            StateContainer = discord;

            var texture = textures.Get(@"Slop/discord");

            if (texture != null)
            {
                SetIcon(new Sprite
                {
                    Texture = texture,
                    Size = new Vector2(20 * texture.Width / texture.Height, 20),
                });
            }
        }
    }
}
