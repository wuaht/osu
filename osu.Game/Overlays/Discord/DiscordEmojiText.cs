// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// A single line of text in which unicode emoji are shown as images, since the fonts only contain single characters and show emoji as placeholder dots.
    /// Like <see cref="TruncatingSpriteText"/>, the text is truncated if it has a relative width or a <see cref="MaxWidth"/>, and the full text is shown as tooltip when truncated.
    /// </summary>
    public partial class DiscordEmojiText : CompositeDrawable, IHasTooltip
    {
        private readonly FillFlowContainer flow;

        private LocalisableString text;
        private ILocalisedBindableString? localisedText;

        private FontUsage font = FontUsage.Default;
        private ColourInfo textColour = Color4.White;

        private bool truncated;

        /// <summary>
        /// The text, which may contain unicode emoji.
        /// </summary>
        public LocalisableString Text
        {
            get => text;
            set
            {
                if (value.Equals(text))
                    return;

                text = value;

                if (localisedText != null)
                    localisedText.Text = value;
            }
        }

        public FontUsage Font
        {
            get => font;
            set
            {
                font = value;
                rebuild();
            }
        }

        /// <summary>
        /// The colour of the text, which unlike <see cref="Drawable.Colour"/> doesn't tint the emoji.
        /// </summary>
        public ColourInfo TextColour
        {
            get => textColour;
            set
            {
                textColour = value;

                foreach (var part in flow)
                {
                    if (part is SpriteText)
                        part.Colour = value;
                }
            }
        }

        /// <summary>
        /// The width after which the text is truncated if it isn't relatively sized.
        /// </summary>
        public float MaxWidth { get; set; } = float.PositiveInfinity;

        public LocalisableString TooltipText => truncated ? text : default;

        public override bool HandlePositionalInput => truncated;

        public new MarginPadding Padding
        {
            get => base.Padding;
            set => base.Padding = value;
        }

        public override Axes RelativeSizeAxes
        {
            get => base.RelativeSizeAxes;
            set
            {
                // A relatively sized axis can't be automatically sized as well.
                AutoSizeAxes = Axes.Both & ~value;
                base.RelativeSizeAxes = value;
            }
        }

        public DiscordEmojiText()
        {
            AutoSizeAxes = Axes.Both;

            InternalChild = flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
            };
        }

        [BackgroundDependencyLoader]
        private void load(LocalisationManager localisation)
        {
            localisedText = localisation.GetLocalisedBindableString(text);
            localisedText.BindValueChanged(_ => rebuild(), true);
        }

        private void rebuild()
        {
            if (localisedText == null)
                return;

            flow.Clear();

            foreach (var (part, emojiUrl) in DiscordEmoji.Split(localisedText.Value))
            {
                if (emojiUrl != null)
                {
                    flow.Add(new DrawableDiscordEmoji(emojiUrl, part, part, font.Size)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    });

                    continue;
                }

                flow.Add(new TruncatingSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = part,
                    Font = font,
                    Colour = textColour,
                    // The whole text is shown as tooltip instead, see TooltipText.
                    ShowTooltip = false,
                });
            }
        }

        protected override void Update()
        {
            base.Update();

            truncated = false;

            float remaining = (AutoSizeAxes & Axes.X) != 0 ? MaxWidth - Padding.TotalHorizontal : ChildSize.X;

            if (float.IsPositiveInfinity(remaining))
                return;

            // The parts are laid out one after another, and the first part which doesn't fit is truncated (text) or hidden (emoji) along with all parts after it.

            foreach (var part in flow)
            {
                if (truncated)
                {
                    part.Alpha = 0;
                    continue;
                }

                if (part is SpriteText spriteText)
                {
                    spriteText.MaxWidth = Math.Max(0, remaining);
                    remaining -= spriteText.LayoutSize.X;
                    truncated = spriteText.IsTruncated;
                    part.Alpha = 1;
                    continue;
                }

                float width = part.LayoutSize.X;
                truncated = width > remaining;
                part.Alpha = truncated ? 0 : 1;
                remaining -= width;
            }
        }
    }

    public static class DiscordEmojiTextExtensions
    {
        /// <summary>
        /// Adds text to a text flow, with unicode emoji shown as images.
        /// </summary>
        public static void AddTextWithEmoji(this OsuTextFlowContainer flow, string text, float emojiSize, Action<SpriteText>? creationParameters = null)
        {
            foreach (var (part, emojiUrl) in DiscordEmoji.Split(text))
            {
                if (emojiUrl == null)
                    flow.AddText(part, creationParameters);
                else
                    flow.AddArbitraryDrawable(new DrawableDiscordEmoji(emojiUrl, part, part, emojiSize));
            }
        }
    }
}
