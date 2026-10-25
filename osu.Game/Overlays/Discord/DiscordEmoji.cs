// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Discord;
using osuTK;

namespace osu.Game.Overlays.Discord
{
    public static class DiscordEmoji
    {
        public const string TWEMOJI_BASE_URL = @"https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/72x72/";

        private const string twemoji_url = TWEMOJI_BASE_URL + @"{0}.png";

        /// <summary>
        /// Returns the URL of the Twemoji image of a text element, or null if the text element is not an emoji.
        /// </summary>
        public static string? GetUnicodeEmojiUrl(string textElement)
        {
            if (!IsEmoji(textElement))
                return null;

            bool hasJoiner = textElement.Contains('‍');

            var codePoints = new List<string>();

            foreach (var rune in textElement.EnumerateRunes())
            {
                // Twemoji names leave out the variation selector unless the emoji is a sequence joined by zero width joiners.
                if (rune.Value == 0xfe0f && !hasJoiner)
                    continue;

                codePoints.Add(rune.Value.ToString(@"x"));
            }

            return string.Format(twemoji_url, string.Join('-', codePoints));
        }

        public static string GetCustomEmojiUrl(ulong id, bool animated) => $@"https://cdn.discordapp.com/emojis/{id}.{(animated ? @"gif" : @"png")}?size=96";

        /// <summary>
        /// Returns the URL of the image of a unicode or custom emoji.
        /// </summary>
        public static string? GetUrl(DiscordReactionEmoji emoji) =>
            emoji.Id is ulong id ? GetCustomEmojiUrl(id, emoji.Animated) : GetUnicodeEmojiUrl(emoji.Name ?? string.Empty);

        /// <summary>
        /// Creates the image of a unicode or custom emoji.
        /// </summary>
        public static Drawable CreateDrawable(DiscordReactionEmoji emoji, float size)
        {
            string name = emoji.Name ?? string.Empty;
            string tooltip = emoji.Id == null ? name : $@":{name}:";

            return new DrawableDiscordEmoji(GetUrl(emoji) ?? string.Empty, emoji.Id == null ? name : tooltip, tooltip, size, emoji.Animated)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        }

        /// <summary>
        /// The text which shows an emoji in a message.
        /// </summary>
        public static string ToMessageText(DiscordReactionEmoji emoji) =>
            emoji.Id is ulong id ? $@"<{(emoji.Animated ? @"a" : string.Empty)}:{emoji.Name}:{id}>" : emoji.Name ?? string.Empty;

        /// <summary>
        /// Whether a text element (a user-perceived character) is an emoji.
        /// </summary>
        public static bool IsEmoji(string textElement)
        {
            if (string.IsNullOrEmpty(textElement))
                return false;

            var first = Rune.GetRuneAt(textElement, 0);
            int value = first.Value;

            bool hasVariationSelector = textElement.Contains('️');

            // Keycaps, such as 1️⃣.
            if (textElement.Contains('⃣'))
                return true;

            // Pictographs, emoticons, transport, flags and other supplementary emoji blocks.
            if (value >= 0x1f000 && value <= 0x1faff)
                return true;

            // Miscellaneous symbols and dingbats, which mostly render as emoji in Discord.
            if (value >= 0x2600 && value <= 0x27bf)
                return true;

            switch (value)
            {
                case 0x231a:
                case 0x231b:
                case 0x23e9:
                case 0x23ea:
                case 0x23eb:
                case 0x23ec:
                case 0x23f0:
                case 0x23f3:
                case 0x2b50:
                case 0x2b55:
                case 0x2b1b:
                case 0x2b1c:
                case 0x2934:
                case 0x2935:
                case 0x3030:
                case 0x303d:
                case 0x3297:
                case 0x3299:
                    return true;
            }

            // Other symbols are only emoji when explicitly requested with the variation selector.
            return hasVariationSelector && value > 0x7f;
        }
    }

    /// <summary>
    /// An emoji image, which falls back to the text of the emoji if the image can't be loaded.
    /// </summary>
    public partial class DrawableDiscordEmoji : CompositeDrawable, IHasLineBaseHeight, IHasTooltip
    {
        private readonly string url;
        private readonly string fallbackText;
        private readonly bool animated;

        public LocalisableString TooltipText { get; }

        /// <summary>
        /// Places the emoji in the middle of a line of message text, so that it doesn't make the line higher.
        /// </summary>
        public float LineBaseHeight => DrawHeight * 0.725f;

        [Resolved]
        private DiscordTextureStore textures { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        /// <param name="url">The URL of the image.</param>
        /// <param name="fallbackText">The text shown if the image can't be loaded.</param>
        /// <param name="tooltip">The name of the emoji, shown as tooltip.</param>
        /// <param name="size">The size of the emoji.</param>
        /// <param name="animated">Whether the image is an animated GIF.</param>
        public DrawableDiscordEmoji(string url, string fallbackText, string tooltip, float size, bool animated = false)
        {
            this.url = url;
            this.fallbackText = fallbackText;
            this.animated = animated;

            TooltipText = tooltip;
            Size = new Vector2(size);
            Margin = new MarginPadding { Horizontal = 1 };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (animated)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        byte[] data = await DiscordMedia.Download(url, cancellation.Token).ConfigureAwait(false);
                        var frames = DiscordMedia.DecodeAnimation(renderer, data);

                        Schedule(() =>
                        {
                            if (frames == null || frames.Length == 0)
                            {
                                showFallback();
                                return;
                            }

                            var animation = new osu.Framework.Graphics.Animations.TextureAnimation
                            {
                                RelativeSizeAxes = Axes.Both,
                                FillMode = FillMode.Fit,
                                Loop = true,
                            };

                            foreach (var (texture, duration) in frames)
                                animation.AddFrame(texture, duration);

                            show(animation);
                        });
                    }
                    catch
                    {
                        if (!cancellation.IsCancellationRequested)
                            Schedule(showFallback);
                    }
                });

                return;
            }

            textures.GetTextureAsync(url).ContinueWith(t => Schedule(() =>
            {
                if (!t.IsCompletedSuccessfully || t.GetResultSafely() == null)
                {
                    showFallback();
                    return;
                }

                show(new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Texture = t.GetResultSafely(),
                });
            }), TaskContinuationOptions.ExecuteSynchronously);
        }

        private void show(Drawable content)
        {
            content.Alpha = 0;
            AddInternal(content);
            content.FadeIn(100);
        }

        private void showFallback()
        {
            AutoSizeAxes = Axes.X;

            AddInternal(new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = fallbackText,
                Font = DiscordStyle.MessageFont(Height * 0.8f),
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
