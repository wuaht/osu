// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Discord;

namespace osu.Game.Graphics.Containers
{
    public partial class OsuTextFlowContainer : TextFlowContainer
    {
        private DiscordTextureStore? ownEmojiTextures;

        public OsuTextFlowContainer(Action<SpriteText>? defaultCreationParameters = null)
            : base(defaultCreationParameters)
        {
        }

        protected override SpriteText CreateSpriteText() => new OsuSpriteText();

        // shortcut: one texture store per text flow for emoji images, upgrade to a shared store if text flows become a measurable cost.
        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            if (parent.Get<DiscordTextureStore>() == null)
                dependencies.Cache(ownEmojiTextures = new DiscordTextureStore(parent.Get<GameHost>()));

            return dependencies;
        }

        /// <summary>
        /// Adds text, showing unicode emoji as images because the game's fonts don't contain them.
        /// </summary>
        public override ITextPart AddText(LocalisableString text, Action<SpriteText> creationParameters = null)
        {
            var parts = DiscordEmoji.Split(text.ToString()).ToList();

            if (!parts.Any(p => p.EmojiUrl != null))
                return base.AddText(text, creationParameters);

            var probe = CreateSpriteText();
            creationParameters?.Invoke(probe);
            float emojiSize = probe.Font.Size * 1.2f;

            ITextPart? last = null;

            foreach (var (part, emojiUrl) in parts)
            {
                last = emojiUrl == null
                    ? base.AddText((LocalisableString)part, creationParameters)
                    : AddArbitraryDrawable(new DrawableDiscordEmoji(emojiUrl, part, part, emojiSize));
            }

            return last!;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            ownEmojiTextures?.Dispose();
        }

        public ITextPart AddArbitraryDrawable(Drawable drawable) => AddPart(new TextPartManual(new ArbitraryDrawableWrapper(drawable).Yield()));

        public ITextPart AddIcon(IconUsage icon, Action<SpriteText>? creationParameters = null) => AddText(icon.Icon.ToString(), creationParameters);

        private partial class ArbitraryDrawableWrapper : Container, IHasLineBaseHeight
        {
            private readonly IHasLineBaseHeight? lineBaseHeightSource;

            public float LineBaseHeight => lineBaseHeightSource?.LineBaseHeight ?? DrawHeight;

            public ArbitraryDrawableWrapper(Drawable drawable)
            {
                Child = drawable;
                lineBaseHeightSource = drawable as IHasLineBaseHeight;
                AutoSizeAxes = Axes.Both;
            }
        }
    }
}
