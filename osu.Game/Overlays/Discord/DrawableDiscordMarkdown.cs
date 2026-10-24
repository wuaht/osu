// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.Chat;
using osu.Game.Online.Discord;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Provides what is needed to show mentions in a message.
    /// </summary>
    public class DiscordMarkdownContext
    {
        public Func<ulong, DiscordUser?> GetUser = _ => null;
        public Func<ulong, DiscordRole?> GetRole = _ => null;
        public Func<ulong, DiscordChannel?> GetChannel = _ => null;

        /// <summary>
        /// Invoked when a channel mention is clicked.
        /// </summary>
        public Action<DiscordChannel>? OpenChannel;

        public ulong LocalUserId;

        public static DiscordMarkdownContext FromClient(DiscordClient client, Action<DiscordChannel>? openChannel = null) => new DiscordMarkdownContext
        {
            GetUser = client.GetUser,
            GetRole = client.GetRole,
            GetChannel = client.GetChannel,
            OpenChannel = openChannel,
            LocalUserId = client.LocalUser.Value?.Id ?? 0,
        };
    }

    /// <summary>
    /// Shows the content of a Discord message with its markdown formatting.
    /// </summary>
    public partial class DrawableDiscordMarkdown : FillFlowContainer
    {
        private const float emoji_size = 20;
        private const float jumbo_emoji_size = 48;

        private readonly string content;
        private readonly DiscordMarkdownContext context;
        private readonly bool edited;
        private readonly bool allowQuotes;
        private readonly float textSize;

        /// <summary>
        /// The colour of the text, or null to use the default colour.
        /// </summary>
        public Color4? TextColour { get; init; }

        /// <summary>
        /// Whether emoji are shown larger if the content only consists of emoji.
        /// </summary>
        public bool AllowJumboEmoji { get; init; } = true;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public DrawableDiscordMarkdown(string content, DiscordMarkdownContext context, bool edited = false, bool allowQuotes = true, float textSize = DiscordStyle.MESSAGE_TEXT_SIZE)
        {
            this.content = content;
            this.context = context;
            this.edited = edited;
            this.allowQuotes = allowQuotes;
            this.textSize = textSize;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 2);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var blocks = DiscordMarkdown.ParseBlocks(content, allowQuotes);
            bool jumbo = AllowJumboEmoji && DiscordMarkdown.IsEmojiOnly(content);

            var spoilers = new Dictionary<int, BindableBool>();

            DiscordTextFlow? lastFlow = null;

            foreach (var block in blocks)
            {
                lastFlow = null;

                switch (block.Type)
                {
                    case MarkdownBlockType.CodeBlock:
                        Add(createCodeBlock(block.Text));
                        break;

                    case MarkdownBlockType.Quote:
                        Add(new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Left = 12 },
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Y,
                                    Width = 4,
                                    X = -12,
                                    Colour = colourProvider.Light4,
                                    Alpha = 0.5f,
                                },
                                new DrawableDiscordMarkdown(block.Text, context, allowQuotes: false, textSize: textSize)
                                {
                                    TextColour = TextColour,
                                    AllowJumboEmoji = false,
                                },
                            },
                        });
                        break;

                    case MarkdownBlockType.Heading:
                    {
                        float size = block.Level switch
                        {
                            1 => 24,
                            2 => 20,
                            _ => 17,
                        };

                        var flow = createFlow(block.Text, size, true, false, spoilers);
                        flow.Margin = new MarginPadding { Top = Count > 0 ? 6 : 0 };
                        Add(lastFlow = flow);
                        break;
                    }

                    case MarkdownBlockType.Subtext:
                    {
                        var flow = createFlow(block.Text, 12, false, false, spoilers);
                        flow.Colour = colourProvider.Content2;
                        Add(lastFlow = flow);
                        break;
                    }

                    case MarkdownBlockType.ListItem:
                    {
                        var flow = createFlow(block.Text, textSize, false, false, spoilers);
                        flow.Padding = new MarginPadding { Left = 18 };

                        Add(new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Left = 4 + block.Level * 18 },
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = block.Marker ?? (block.Level == 0 ? @"•" : @"◦"),
                                    Font = DiscordStyle.MessageFont(textSize),
                                    Colour = TextColour ?? colourProvider.Content1,
                                    Padding = new MarginPadding { Vertical = flow.LinePadding },
                                },
                                lastFlow = flow,
                            },
                        });
                        break;
                    }

                    default:
                        Add(lastFlow = createFlow(block.Text, textSize, false, jumbo, spoilers));
                        break;
                }
            }

            if (edited)
            {
                if (lastFlow == null)
                    Add(lastFlow = createFlow(string.Empty, textSize, false, false, spoilers));

                lastFlow.AddText(@" ");
                lastFlow.AddText(SlopDiscordStrings.Edited, t =>
                {
                    t.Font = DiscordStyle.MessageFont(10);
                    t.Colour = colourProvider.Content2;
                });
            }
        }

        private DiscordTextFlow createFlow(string text, float size, bool bold, bool jumbo, Dictionary<int, BindableBool> spoilers)
        {
            var flow = new DiscordTextFlow(size)
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Colour = TextColour ?? colourProvider.Content1,
            };

            foreach (var inline in DiscordMarkdown.ParseInline(text))
                addInline(flow, inline, size, bold, jumbo, spoilers);

            return flow;
        }

        private void addInline(DiscordTextFlow flow, MarkdownInline inline, float size, bool bold, bool jumbo, Dictionary<int, BindableBool> spoilers)
        {
            bool isBold = bold || inline.Style.HasFlag(MarkdownStyle.Bold);
            bool isItalic = inline.Style.HasFlag(MarkdownStyle.Italic);

            BindableBool? spoiler = null;

            if (inline.Spoiler >= 0 && !spoilers.TryGetValue(inline.Spoiler, out spoiler))
                spoilers[inline.Spoiler] = spoiler = new BindableBool();

            var font = inline.Style.HasFlag(MarkdownStyle.Code) ? DiscordStyle.CodeFont(size - 1) : DiscordStyle.MessageFont(size, isBold, isItalic);

            bool decorated = spoiler != null || inline.Style.HasFlag(MarkdownStyle.Code) || inline.Style.HasFlag(MarkdownStyle.Underline) || inline.Style.HasFlag(MarkdownStyle.Strikethrough);

            switch (inline.Type)
            {
                case MarkdownInlineType.LineBreak:
                    flow.NewLine();
                    return;

                case MarkdownInlineType.Text:
                    if (inline.Style.HasFlag(MarkdownStyle.Code))
                    {
                        addWords(flow, inline.Text, font, inline.Style, spoiler, codeBackground: true);
                        return;
                    }

                    addText(flow, inline.Text, font, inline.Style, spoiler, decorated, jumbo ? jumbo_emoji_size : emoji_size);
                    return;

                case MarkdownInlineType.Link:
                {
                    if (spoiler != null)
                    {
                        addWords(flow, inline.Text, font, inline.Style, spoiler, textColour: DiscordStyle.Link);
                        return;
                    }

                    var details = MessageFormatter.GetLinkDetails(inline.Url!);

                    // Masked links show their target as tooltip, so that the target can be seen before clicking.
                    string? tooltip = inline.Text == inline.Url ? null : inline.Url;

                    flow.AddLink(inline.Text, details.Action, details.Argument, tooltip, t => t.Font = font);
                    return;
                }

                case MarkdownInlineType.UserMention:
                {
                    var user = context.GetUser(inline.Id);
                    var mention = new MentionChip($@"@{user?.DisplayName ?? @"unknown-user"}", font, DiscordStyle.MentionText, DiscordStyle.MentionBackground)
                    {
                        Highlighted = inline.Id == context.LocalUserId,
                    };
                    addDecorated(flow, mention, spoiler);
                    return;
                }

                case MarkdownInlineType.RoleMention:
                {
                    var role = context.GetRole(inline.Id);
                    var colour = DiscordStyle.FromInteger(role?.Colour);

                    var mention = new MentionChip($@"@{role?.Name ?? @"unknown-role"}", font, colour ?? DiscordStyle.MentionText,
                        colour == null ? DiscordStyle.MentionBackground : colour.Value.Opacity(0.15f));
                    addDecorated(flow, mention, spoiler);
                    return;
                }

                case MarkdownInlineType.ChannelMention:
                {
                    var channel = context.GetChannel(inline.Id);

                    var mention = new MentionChip($@"#{channel?.DisplayName ?? @"unknown"}", font, DiscordStyle.MentionText, DiscordStyle.MentionBackground);

                    if (channel != null && context.OpenChannel != null && channel.IsTextBased)
                        mention.Action = () => context.OpenChannel(channel);

                    addDecorated(flow, mention, spoiler);
                    return;
                }

                case MarkdownInlineType.EveryoneMention:
                    addDecorated(flow, new MentionChip(inline.Text, font, DiscordStyle.MentionText, DiscordStyle.MentionBackground) { Highlighted = true }, spoiler);
                    return;

                case MarkdownInlineType.CustomEmoji:
                {
                    var emoji = new DrawableDiscordEmoji(DiscordEmoji.GetCustomEmojiUrl(inline.Id, inline.Animated), $@":{inline.Text}:", $@":{inline.Text}:",
                        jumbo ? jumbo_emoji_size : emoji_size, inline.Animated);
                    addDecorated(flow, emoji, spoiler);
                    return;
                }

                case MarkdownInlineType.Timestamp:
                {
                    var time = DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(inline.Unix, -62135596800, 253402300799)).ToLocalTime();

                    var chip = new MentionChip(formatTimestamp(time, inline.Text), font, colourProvider.Content1, colourProvider.Background3.Opacity(0.8f))
                    {
                        TooltipText = time.ToString(@"F", CultureInfo.CurrentCulture),
                    };
                    addDecorated(flow, chip, spoiler);
                    return;
                }
            }
        }

        /// <summary>
        /// Adds plain text, which may contain unicode emoji, osu! timestamps and links to osu! content.
        /// </summary>
        private void addText(DiscordTextFlow flow, string text, FontUsage font, MarkdownStyle style, BindableBool? spoiler, bool decorated, float emojiSize)
        {
            var run = new StringBuilder();

            void flushRun()
            {
                if (run.Length == 0)
                    return;

                string value = run.ToString();
                run.Clear();

                if (decorated)
                {
                    addWords(flow, value, font, style, spoiler);
                    return;
                }

                var formatted = MessageFormatter.FormatText(value);

                // Channel links are only meaningful in osu! chat.
                formatted.Links.RemoveAll(l => l.Action == LinkAction.OpenChannel);

                flow.AddLinks(formatted.Text, formatted.Links, t => t.Font = font);
            }

            foreach (string element in DiscordMarkdown.GetTextElements(text))
            {
                string? emojiUrl = DiscordEmoji.GetUnicodeEmojiUrl(element);

                if (emojiUrl == null)
                {
                    run.Append(element);
                    continue;
                }

                flushRun();
                addDecorated(flow, new DrawableDiscordEmoji(emojiUrl, element, element, emojiSize), spoiler);
            }

            flushRun();
        }

        /// <summary>
        /// Adds text as separate words, so that decorations (such as underlines or code backgrounds) can wrap with the text.
        /// </summary>
        private void addWords(DiscordTextFlow flow, string text, FontUsage font, MarkdownStyle style, BindableBool? spoiler, bool codeBackground = false, Color4? textColour = null)
        {
            int start = 0;

            while (start < text.Length)
            {
                int end = text.IndexOf(' ', start);
                end = end < 0 ? text.Length : end + 1;

                var word = new DecoratedWord(text[start..end], font, style)
                {
                    CodeBackground = codeBackground,
                    TextColour = textColour,
                    LinePadding = flow.LinePadding,
                };

                addDecorated(flow, word, spoiler);
                start = end;
            }
        }

        private void addDecorated(DiscordTextFlow flow, Drawable drawable, BindableBool? spoiler)
        {
            if (drawable is MentionChip chip)
                chip.Margin = new MarginPadding { Horizontal = 1, Vertical = flow.LinePadding };

            flow.AddArbitraryDrawable(spoiler == null ? drawable : new SpoilerCover(drawable, spoiler));
        }

        private static string formatTimestamp(DateTimeOffset time, string format)
        {
            var culture = CultureInfo.CurrentCulture;

            switch (format)
            {
                case @"t":
                    return time.ToString(@"t", culture);

                case @"T":
                    return time.ToString(@"T", culture);

                case @"d":
                    return time.ToString(@"d", culture);

                case @"D":
                    return time.ToString(@"D", culture);

                case @"F":
                    return time.ToString(@"dddd, ", culture) + time.ToString(@"D", culture) + @" " + time.ToString(@"t", culture);

                case @"R":
                    return HumanizerUtils.Humanize(time);

                default:
                    return time.ToString(@"D", culture) + @" " + time.ToString(@"t", culture);
            }
        }

        private Drawable createCodeBlock(string code)
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 4,
                BorderThickness = 1,
                BorderColour = colourProvider.Background2,
                Margin = new MarginPadding { Vertical = 2 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = DiscordStyle.CodeBackground,
                    },
                    new OsuTextFlowContainer(t =>
                    {
                        t.Font = DiscordStyle.CodeFont(textSize - 2);
                        t.Colour = colourProvider.Content1;
                    })
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(8),
                        Text = code.Replace("\t", @"    "),
                    },
                },
            };
        }

        public partial class DiscordTextFlow : LinkFlowContainer
        {
            /// <summary>
            /// The vertical padding of text, which makes every line <see cref="DiscordStyle.LineHeight"/> high.
            /// </summary>
            public readonly float LinePadding;

            public DiscordTextFlow(float textSize)
            {
                LinePadding = (DiscordStyle.LineHeight(textSize) - textSize) / 2;
            }

            protected override SpriteText CreateSpriteText() => new LineSpriteText { Padding = new MarginPadding { Vertical = LinePadding } };

            protected override DrawableLinkCompiler CreateLinkCompiler(ITextPart textPart) => new DrawableLinkCompiler(textPart)
            {
                IdleColour = DiscordStyle.Link,
                HoverColour = Color4.White,
            };
        }

        /// <summary>
        /// Text whose base line includes its padding, so that it lines up with the other content of a line.
        /// </summary>
        private partial class LineSpriteText : OsuSpriteText, IHasLineBaseHeight
        {
            float IHasLineBaseHeight.LineBaseHeight => Padding.Top + LineBaseHeight;
        }

        /// <summary>
        /// A word with a decoration, such as an underline or a code background.
        /// </summary>
        private partial class DecoratedWord : CompositeDrawable, IHasLineBaseHeight
        {
            public bool CodeBackground { get; init; }
            public Color4? TextColour { get; init; }

            /// <summary>
            /// The vertical padding around the word, so that it lines up with plain text.
            /// </summary>
            public float LinePadding
            {
                init => Padding = new MarginPadding { Vertical = value };
            }

            private readonly string text;
            private readonly FontUsage font;
            private readonly MarkdownStyle style;

            private OsuSpriteText spriteText = null!;

            public float LineBaseHeight => Padding.Top + spriteText.LineBaseHeight;

            public DecoratedWord(string text, FontUsage font, MarkdownStyle style)
            {
                this.text = text;
                this.font = font;
                this.style = style;

                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                if (CodeBackground)
                {
                    AddInternal(new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 3,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = DiscordStyle.CodeBackground,
                        },
                    });
                }

                AddInternal(spriteText = new OsuSpriteText
                {
                    Text = text,
                    Font = font,
                    Padding = CodeBackground ? new MarginPadding { Horizontal = 2 } : new MarginPadding(),
                });

                if (TextColour != null)
                    spriteText.Colour = TextColour.Value;

                // Trailing spaces are not decorated.
                float trailing = text.EndsWith(' ') ? font.Size * 0.25f : 0;

                if (style.HasFlag(MarkdownStyle.Underline))
                {
                    AddInternal(new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Y = -font.Size * 0.12f,
                        Padding = new MarginPadding { Right = trailing },
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = TextColour ?? Color4.White,
                        },
                    });
                }

                if (style.HasFlag(MarkdownStyle.Strikethrough))
                {
                    AddInternal(new Box
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Y = font.Size * 0.05f,
                        Colour = TextColour ?? Color4.White,
                    });
                }
            }
        }

        /// <summary>
        /// A rounded highlight for mentions and timestamps.
        /// </summary>
        private partial class MentionChip : OsuClickableContainer, IHasLineBaseHeight
        {
            private readonly OsuSpriteText spriteText;
            private readonly Box background;
            private readonly Color4 backgroundColour;

            public bool Highlighted { get; init; }

            public float LineBaseHeight => Margin.Top + spriteText.LineBaseHeight;

            public MentionChip(string text, FontUsage font, Color4 textColour, Color4 backgroundColour)
            {
                this.backgroundColour = backgroundColour;

                AutoSizeAxes = Axes.Both;
                Masking = true;
                CornerRadius = 3;
                Margin = new MarginPadding { Horizontal = 1 };

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = backgroundColour,
                    },
                    spriteText = new OsuSpriteText
                    {
                        Text = text,
                        Font = font.With(weight: @"SemiBold"),
                        Colour = textColour,
                        Padding = new MarginPadding { Horizontal = 2 },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (Action != null)
                    background.FadeColour(backgroundColour.Opacity(Math.Min(1, backgroundColour.A * 2)), 100);

                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeColour(backgroundColour, 100);
                base.OnHoverLost(e);
            }

            protected override bool OnClick(ClickEvent e)
            {
                // Mentions without an action don't consume clicks, so that a spoiler around them can still be revealed.
                return Action != null && base.OnClick(e);
            }
        }

        /// <summary>
        /// Hides a part of a spoiler until any part of the spoiler is clicked.
        /// </summary>
        private partial class SpoilerCover : CompositeDrawable, IHasLineBaseHeight, IHasTooltip
        {
            private readonly Drawable content;
            private readonly BindableBool revealed = new BindableBool();

            private readonly Container cover;

            public float LineBaseHeight => (content as IHasLineBaseHeight)?.LineBaseHeight ?? DrawHeight;

            public LocalisableString TooltipText => revealed.Value ? default : (LocalisableString)@"Spoiler";

            public SpoilerCover(Drawable content, BindableBool revealed)
            {
                this.content = content;
                this.revealed.BindTo(revealed);

                AutoSizeAxes = Axes.Both;

                InternalChildren = new[]
                {
                    content,
                    cover = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 3,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = DiscordStyle.Spoiler,
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // The content stays present (and only covered), as hidden children are not taken into account for auto-sizing.
                revealed.BindValueChanged(r => cover.FadeTo(r.NewValue ? 0 : 1, r.NewValue ? 150 : 0), true);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (revealed.Value)
                    return false;

                revealed.Value = true;
                return true;
            }
        }
    }
}
