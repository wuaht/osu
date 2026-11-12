// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax.Inlines;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.Containers.Markdown.Footnotes;
using osu.Game.Overlays;
using osu.Game.Overlays.Discord;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using osuTK;

namespace osu.Game.Graphics.Containers.Markdown
{
    public partial class OsuMarkdownTextFlowContainer : MarkdownTextFlowContainer
    {
        private DiscordTextureStore? ownEmojiTextures;

        // shortcut: one texture store per text flow for emoji images, see OsuTextFlowContainer.
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
                if (emojiUrl == null)
                    last = base.AddText((LocalisableString)part, creationParameters);
                else
                    AddDrawable(new DrawableDiscordEmoji(emojiUrl, part, part, emojiSize));
            }

            return last!;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            ownEmojiTextures?.Dispose();
        }

        protected override void AddLinkText(string text, LinkInline linkInline)
            => AddDrawable(new OsuMarkdownLinkText(text, linkInline));

        protected override void AddAutoLink(AutolinkInline autolinkInline)
            => AddDrawable(new OsuMarkdownLinkText(autolinkInline));

        protected override void AddImage(LinkInline linkInline) => AddDrawable(new OsuMarkdownImage(linkInline));

        // TODO : Change font to monospace
        protected override void AddCodeInLine(CodeInline codeInline) => AddDrawable(new OsuMarkdownInlineCode
        {
            Text = codeInline.Content
        });

        protected override void AddFootnoteLink(FootnoteLink footnoteLink) => AddDrawable(new OsuMarkdownFootnoteLink(footnoteLink));

        protected override void AddFootnoteBacklink(FootnoteLink footnoteBacklink) => AddDrawable(new OsuMarkdownFootnoteBacklink(footnoteBacklink));

        protected override void ApplyEmphasisedCreationParameters(SpriteText spriteText, bool bold, bool italic)
        {
            base.ApplyEmphasisedCreationParameters(spriteText, bold, italic);

            spriteText.Font = spriteText.Font.With(weight: bold ? FontWeight.Bold : FontWeight.Regular, italics: italic);
        }

        protected override void AddCustomComponent(CustomContainerInline inline)
        {
            if (!(inline.FirstChild is LiteralInline literal))
            {
                base.AddCustomComponent(inline);
                return;
            }

            string[] attributes = literal.Content.ToString().Trim(' ', '{', '}').Split();
            string flagAttribute = attributes.SingleOrDefault(a => a.StartsWith(@"flag", StringComparison.Ordinal));

            if (flagAttribute == null)
            {
                base.AddCustomComponent(inline);
                return;
            }

            string flag = flagAttribute.Split('=').Last().Trim('"');

            if (!Enum.TryParse<CountryCode>(flag, out var countryCode))
                countryCode = CountryCode.Unknown;

            AddDrawable(new DrawableFlag(countryCode) { Size = new Vector2(20, 15) });
        }

        private partial class OsuMarkdownInlineCode : Container
        {
            [Resolved]
            private IMarkdownTextComponent parentTextComponent { get; set; }

            public string Text;

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                AutoSizeAxes = Axes.Both;
                CornerRadius = 4;
                Masking = true;
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                    },
                    parentTextComponent.CreateSpriteText().With(t =>
                    {
                        t.Colour = colourProvider.Light1;
                        t.Text = Text;
                        t.Padding = new MarginPadding
                        {
                            Vertical = 1,
                            Horizontal = 4,
                        };
                    }),
                };
            }
        }
    }
}
