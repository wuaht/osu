// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osuTK;

namespace osu.Game.Rulesets.Fposu.UI
{
    /// <summary>
    /// The icon of the FPoSu ruleset, which is white on transparent (like the icons of the other rulesets) so that it can be coloured.
    /// </summary>
    public partial class FposuRulesetIcon : Sprite
    {
        private const string texture_name = @"Textures/RulesetFps";

        /// <summary>
        /// The textures of the ruleset by renderer, as icons are created often (e.g. for each difficulty in beatmap lists).
        /// </summary>
        private static readonly ConditionalWeakTable<IRenderer, TextureStore> texture_stores = new ConditionalWeakTable<IRenderer, TextureStore>();

        private readonly FposuRuleset ruleset;

        public FposuRulesetIcon(FposuRuleset ruleset)
        {
            this.ruleset = ruleset;

            // the size of the icon font glyphs used by the other rulesets, which callers usually resize.
            Size = new Vector2(20);
            FillMode = FillMode.Fit;
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            var textures = texture_stores.GetValue(host.Renderer, renderer => new TextureStore(renderer, host.CreateTextureLoaderStore(ruleset.CreateResourceStore())));
            Texture = textures.Get(texture_name);
        }
    }
}
