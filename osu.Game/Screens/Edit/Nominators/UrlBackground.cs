// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// An image from a URL which fills its parent, e.g. the cover of a beatmap set.
    /// </summary>
    [LongRunningLoad]
    public partial class UrlBackground : Sprite
    {
        private readonly string url;

        public UrlBackground(string url)
        {
            this.url = url;

            RelativeSizeAxes = Axes.Both;
            FillMode = FillMode.Fill;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            Texture = textures.Get(url);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
