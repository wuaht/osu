// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Discord;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.Containers.BBCode
{
    /// <summary>
    /// BBCode as the osu! website renders it (user pages and beatmap descriptions).
    /// This shows the HTML which the website creates from the BBCode (see osu-web's <c>BBCodeFromDB</c>),
    /// so that tags which are nested wrongly or never closed look the same as on the website, and images are loaded through the website's proxy.
    /// </summary>
    public partial class DrawableBBCode : FillFlowContainer
    {
        private static readonly Regex word_regex = new Regex(@"\S+\s*|\s+", RegexOptions.Compiled);
        private static readonly Regex whitespace_regex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex youtube_embed_regex = new Regex(@"youtube\.com/embed/(?<id>[^?/]+)", RegexOptions.Compiled);

        private readonly HtmlNode document;
        private readonly float textSize;

        private DiscordTextureStore? ownEmojiTextures;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ILinkHandler? linkHandler { get; set; }

        [Resolved]
        private GameHost host { get; set; } = null!;

        /// <param name="html">The BBCode as rendered by the website.</param>
        /// <param name="textSize">The size of regular text.</param>
        public DrawableBBCode(string html, float textSize = 14)
        {
            this.textSize = textSize;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;

            var html_document = new HtmlDocument();
            html_document.LoadHtml(html);
            document = html_document.DocumentNode;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // emojis are shown as images from the emoji CDN, which the game's texture store doesn't allow.
            if (parent.Get<DiscordTextureStore>() == null)
                dependencies.Cache(ownEmojiTextures = new DiscordTextureStore(parent.Get<GameHost>()));

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            render(document.ChildNodes, new Block(this, Anchor.TopLeft), new Style { Size = textSize });
        }

        private void render(IEnumerable<HtmlNode> nodes, Block block, Style style)
        {
            foreach (var node in nodes)
            {
                switch (node.NodeType)
                {
                    case HtmlNodeType.Text:
                        string text = WebUtility.HtmlDecode(((HtmlTextNode)node).Text);

                        if (style.Preformatted)
                        {
                            string[] lines = text.Split('\n');

                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (i > 0)
                                    newLine(block, style);

                                if (lines[i].Length > 0)
                                {
                                    addText(ensureFlow(block), lines[i], style);
                                    block.LineOpen = true;
                                }
                            }

                            break;
                        }

                        // like in a browser, whitespace is collapsed and doesn't start a line.
                        text = whitespace_regex.Replace(text, " ");

                        if (!block.LineOpen)
                            text = text.TrimStart();

                        if (text.Length > 0)
                        {
                            addText(ensureFlow(block), text, style);
                            block.LineOpen = true;
                        }

                        break;

                    case HtmlNodeType.Element:
                        renderElement(node, block, style);
                        break;
                }
            }
        }

        private void renderElement(HtmlNode element, Block block, Style style)
        {
            var children = element.ChildNodes;

            switch (element.Name)
            {
                case @"br":
                    newLine(block, style);
                    break;

                case @"strong":
                case @"b":
                    render(children, block, style with { Bold = true });
                    break;

                case @"em":
                case @"i":
                    render(children, block, style with { Italic = true });
                    break;

                case @"u":
                    render(children, block, style with { Underline = true });
                    break;

                case @"del":
                case @"s":
                case @"strike":
                    render(children, block, style with { Strike = true });
                    break;

                case @"code":
                    render(children, block, style with { Code = true });
                    break;

                case @"span":
                    render(children, block, element.HasClass(@"spoiler") ? style with { Spoiler = true } : applyCss(element, style));
                    break;

                case @"a":
                {
                    string href = WebUtility.HtmlDecode(element.GetAttributeValue(@"href", string.Empty));
                    render(children, block, href.Length > 0 && href != @"#" ? style with { Link = href } : style);
                    break;
                }

                case @"img":
                    addInline(block, new BBCodeImage(attribute(element, @"src"), cssPixels(element, @"width")) { Action = linkAction(style.Link) });
                    break;

                case @"iframe":
                {
                    string src = attribute(element, @"src");
                    var youtube = youtube_embed_regex.Match(src);
                    string url = youtube.Success ? $@"https://www.youtube.com/watch?v={youtube.Groups[@"id"].Value}" : src;
                    addText(ensureFlow(block), url, style with { Link = url });
                    block.LineOpen = true;
                    break;
                }

                case @"audio":
                {
                    string src = attribute(element, @"src");
                    addText(ensureFlow(block), src, style with { Link = src });
                    block.LineOpen = true;
                    break;
                }

                case @"h2":
                    renderBlock(children, block, style with { Bold = true, Size = style.Size * 1.5f });
                    break;

                case @"h4":
                    renderBlock(children, block, style with { Bold = true });
                    break;

                case @"pre":
                    startBlock(block, createCode(children, block, style));
                    break;

                case @"blockquote":
                    startBlock(block, createQuote(children, block, style));
                    break;

                case @"ol":
                case @"ul":
                    startBlock(block, createList(element, block, style));
                    break;

                case @"div":
                    if (element.HasClass(@"well"))
                        startBlock(block, createNotice(children, block, style));
                    else if (element.HasClass(@"bbcode-spoilerbox"))
                        startBlock(block, createBox(element, block, style));
                    else if (element.HasClass(@"imagemap"))
                        addImagemap(block, element);
                    else if (element.HasClass(@"bbcode__align-centre"))
                        renderBlock(children, block, style, Anchor.TopCentre);
                    else if (element.HasClass(@"bbcode__align-right"))
                        renderBlock(children, block, style, Anchor.TopRight);
                    else if (element.HasClass(@"bbcode__align-left"))
                        renderBlock(children, block, style, Anchor.TopLeft);
                    else
                        renderBlock(children, block, style);
                    break;

                default:
                    render(children, block, style);
                    break;
            }
        }

        /// <summary>
        /// Applies the inline styles which the website uses for <c>[color]</c> and <c>[size]</c>.
        /// </summary>
        private static Style applyCss(HtmlNode element, Style style)
        {
            foreach (var (property, value) in css(element))
            {
                switch (property)
                {
                    case @"color":
                        if (parseColour(value) is Color4 colour)
                            style = style with { Colour = colour };
                        break;

                    case @"font-size":
                        if (value.EndsWith('%') && float.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out float percentage))
                            style = style with { Size = style.Size * percentage / 100 };
                        break;
                }
            }

            return style;
        }

        #region Text

        private void newLine(Block block, Style style)
        {
            if (block.LineOpen)
            {
                block.Flow!.NewLine();
                block.LineOpen = false;
                return;
            }

            // an empty line needs some content to have a height.
            var flow = ensureFlow(block);
            flow.AddText(@"|", s =>
            {
                s.Font = font(style);
                s.Colour = Color4.Transparent;
            });
            flow.NewLine();
        }

        /// <summary>
        /// Adds text to a flow, showing emojis as images (the game's fonts don't contain them).
        /// </summary>
        private void addText(LinkFlowContainer flow, string text, Style style)
        {
            var run = new StringBuilder();

            foreach (string element in DiscordMarkdown.GetTextElements(text))
            {
                string? emojiUrl = DiscordEmoji.GetUnicodeEmojiUrl(element);

                if (emojiUrl == null)
                {
                    run.Append(element);
                    continue;
                }

                flushRun();
                flow.AddArbitraryDrawable(new DrawableDiscordEmoji(emojiUrl, element, element, style.Size * 1.2f));
            }

            flushRun();

            void flushRun()
            {
                if (run.Length > 0)
                    addRun(flow, run.ToString(), style);
                run.Clear();
            }
        }

        private void addRun(LinkFlowContainer flow, string text, Style style)
        {
            if (style.Link != null)
            {
                var link = MessageFormatter.GetLinkDetails(style.Link);
                flow.AddLink(text, link.Action, link.Argument, style.Link, s => applyStyle(s, style));
                return;
            }

            if (style.Underline || style.Strike || style.Spoiler || style.Code)
            {
                foreach (Match word in word_regex.Matches(text))
                    flow.AddArbitraryDrawable(new BBCodeWord(word.Value, font(style), style, colourProvider.Background6));
                return;
            }

            flow.AddText(text, s => applyStyle(s, style));
        }

        private void addInline(Block block, Drawable drawable)
        {
            ensureFlow(block).AddArbitraryDrawable(drawable);
            block.LineOpen = true;
        }

        private LinkFlowContainer ensureFlow(Block block)
        {
            if (block.Flow != null)
                return block.Flow;

            block.Target.Add(block.Flow = new LinkFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                TextAnchor = block.TextAnchor,
            });

            return block.Flow;
        }

        private static void applyStyle(SpriteText sprite, Style style)
        {
            sprite.Font = font(style);

            if (style.Colour != null)
                sprite.Colour = style.Colour.Value;
        }

        private static FontUsage font(Style style) => style.Code
            ? OsuFont.GetFont(Typeface.Inter, style.Size, FontWeight.Regular)
            : OsuFont.GetFont(size: style.Size, weight: style.Bold ? FontWeight.Bold : FontWeight.Regular, italics: style.Italic);

        #endregion

        #region Blocks

        private static FillFlowContainer createBlockFlow() => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
        };

        /// <summary>
        /// Adds a block element, which ends the current line.
        /// </summary>
        private static void startBlock(Block block, Drawable drawable)
        {
            block.Flow = null;
            block.LineOpen = false;
            block.Target.Add(drawable);
        }

        private void renderBlock(IEnumerable<HtmlNode> children, Block block, Style style, Anchor? textAnchor = null)
        {
            var inner = new Block(createBlockFlow(), textAnchor ?? block.TextAnchor);
            startBlock(block, inner.Target);
            render(children, inner, style);

            // content after the block starts on a new line.
            block.Flow = null;
            block.LineOpen = false;
        }

        private Drawable createNotice(IEnumerable<HtmlNode> children, Block block, Style style)
        {
            var inner = new Block(createBlockFlow(), block.TextAnchor);
            render(children, inner, style);

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 4,
                Margin = new MarginPadding { Vertical = 5 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(10),
                        Child = inner.Target,
                    },
                },
            };
        }

        private Drawable createQuote(IEnumerable<HtmlNode> children, Block block, Style style)
        {
            var inner = new Block(createBlockFlow(), block.TextAnchor);
            render(children, inner, style);

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Margin = new MarginPadding { Vertical = 5 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = colourProvider.Light4,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 13 },
                        Child = inner.Target,
                    },
                },
            };
        }

        private Drawable createBox(HtmlNode element, Block block, Style style)
        {
            var titleContent = element.Descendants(@"span").FirstOrDefault(s => s.HasClass(@"bbcode-spoilerbox__link-text"))?.ChildNodes;
            var bodyContent = element.ChildNodes.FirstOrDefault(c => c.HasClass(@"bbcode-spoilerbox__body"))?.ChildNodes;

            var body = new Block(createBlockFlow(), block.TextAnchor);
            body.Target.Alpha = 0;
            body.Target.Padding = new MarginPadding { Left = style.Size + 6 };

            if (bodyContent != null)
                render(bodyContent, body, style);

            var title = new Block(new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
            }, Anchor.TopLeft);

            title.Flow = new LinkFlowContainer { AutoSizeAxes = Axes.Both };
            title.Target.Add(title.Flow);

            if (titleContent != null)
                render(titleContent, title, style with { Bold = true });

            SpriteIcon icon;

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Margin = new MarginPadding { Vertical = 2 },
                Children = new Drawable[]
                {
                    new OsuClickableContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Child = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            Children = new Drawable[]
                            {
                                icon = new SpriteIcon
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Icon = FontAwesome.Solid.ChevronRight,
                                    Size = new Vector2(style.Size * 0.7f),
                                },
                                title.Target,
                            },
                        },
                        Action = () =>
                        {
                            // hidden content isn't part of the layout, which collapses the box.
                            body.Target.Alpha = body.Target.Alpha > 0 ? 0 : 1;
                            icon.Icon = body.Target.Alpha > 0 ? FontAwesome.Solid.ChevronDown : FontAwesome.Solid.ChevronRight;
                        },
                    },
                    body.Target,
                },
            };
        }

        private Drawable createCode(IEnumerable<HtmlNode> children, Block block, Style style)
        {
            var inner = new Block(createBlockFlow(), block.TextAnchor);
            inner.Target.Padding = new MarginPadding(10);
            render(children, inner, style with { Code = true, Preformatted = true });

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 4,
                Margin = new MarginPadding { Vertical = 5 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                    },
                    inner.Target,
                },
            };
        }

        private Drawable createList(HtmlNode list, Block block, Style style)
        {
            var target = createBlockFlow();
            var items = list.ChildNodes.Where(c => c.Name == @"li");

            // text before the first item of a list is its title.
            if (list.HasClass(@"bbcode__list-title"))
            {
                foreach (var item in items)
                    render(item.ChildNodes, new Block(target, block.TextAnchor), style);

                return target;
            }

            bool numbered = list.Name == @"ol" && !list.HasClass(@"unordered");
            int number = 0;

            foreach (var item in items)
            {
                var body = new Block(createBlockFlow(), block.TextAnchor);
                body.Target.Padding = new MarginPadding { Left = style.Size * 1.75f };

                target.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = numbered ? $@"{++number}." : @"•",
                            Font = font(style),
                        },
                        body.Target,
                    },
                });

                render(item.ChildNodes, body, style);
            }

            return target;
        }

        /// <summary>
        /// Adds an imagemap on its own line, aligned like text (as the website shows it).
        /// </summary>
        private void addImagemap(Block block, HtmlNode element)
        {
            var image = element.ChildNodes.FirstOrDefault(c => c.Name == @"img");

            if (image == null)
                return;

            var imagemap = new BBCodeImage(attribute(image, @"src"), int.TryParse(attribute(image, @"width"), out int width) ? width : null);

            imagemap.AddAreas(element.ChildNodes.Where(c => c.HasClass(@"imagemap__link")).Select(area =>
            {
                var position = css(area).ToDictionary(p => p.property, p => p.value);
                string href = attribute(area, @"href");

                return new OsuClickableContainer
                {
                    RelativePositionAxes = Axes.Both,
                    RelativeSizeAxes = Axes.Both,
                    Position = new Vector2(percentage(@"left"), percentage(@"top")),
                    Size = new Vector2(percentage(@"width"), percentage(@"height")),
                    TooltipText = attribute(area, @"title"),
                    Action = href.Length > 0 && href != @"#" ? linkAction(href) : null,
                };

                float percentage(string property) =>
                    position.TryGetValue(property, out string? value) && float.TryParse(value.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
                        ? result / 100
                        : 0;
            }));

            if (block.LineOpen)
                block.Flow!.NewLine();

            var flow = ensureFlow(block);
            flow.AddArbitraryDrawable(imagemap);
            flow.NewLine();
            block.LineOpen = false;
        }

        #endregion

        private Action? linkAction(string? url)
        {
            if (url == null)
                return null;

            return () =>
            {
                if (linkHandler != null)
                    linkHandler.HandleLink(url);
                else
                    host.OpenUrlExternally(url);
            };
        }

        private static string attribute(HtmlNode element, string name) => WebUtility.HtmlDecode(element.GetAttributeValue(name, string.Empty));

        private static IEnumerable<(string property, string value)> css(HtmlNode element) =>
            attribute(element, @"style").Split(';')
                                       .Select(d => d.Split(':', 2))
                                       .Where(d => d.Length == 2)
                                       .Select(d => (d[0].Trim().ToLowerInvariant(), d[1].Trim()));

        private static float? cssPixels(HtmlNode element, string property)
        {
            string? value = css(element).FirstOrDefault(p => p.property == property).value;

            if (value == null || !value.EndsWith(@"px", StringComparison.Ordinal))
                return null;

            return float.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result : null;
        }

        private static Color4? parseColour(string colour)
        {
            try
            {
                return Color4Extensions.FromHex(colour.TrimStart('#'));
            }
            catch
            {
                var named = System.Drawing.Color.FromName(colour);
                return named.IsKnownColor ? new Color4(named.R, named.G, named.B, 255) : null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            ownEmojiTextures?.Dispose();
        }

        private record struct Style
        {
            public bool Bold { get; init; }
            public bool Italic { get; init; }
            public bool Underline { get; init; }
            public bool Strike { get; init; }
            public bool Spoiler { get; init; }
            public bool Code { get; init; }
            public bool Preformatted { get; init; }
            public Color4? Colour { get; init; }
            public float Size { get; init; }
            public string? Link { get; init; }
        }

        /// <summary>
        /// A vertical container which text and block elements are added to.
        /// </summary>
        private class Block
        {
            public readonly FillFlowContainer Target;
            public readonly Anchor TextAnchor;

            /// <summary>
            /// The text flow of the current paragraph, which ends at the next block element.
            /// </summary>
            public LinkFlowContainer? Flow;

            /// <summary>
            /// Whether the current line of <see cref="Flow"/> has content, so that a line break ends it instead of adding an empty line.
            /// </summary>
            public bool LineOpen;

            public Block(FillFlowContainer target, Anchor textAnchor)
            {
                Target = target;
                TextAnchor = textAnchor;
            }
        }

        /// <summary>
        /// A word with decorations which sprite text doesn't support.
        /// </summary>
        private partial class BBCodeWord : CompositeDrawable, IHasLineBaseHeight
        {
            private readonly OsuSpriteText spriteText;
            private readonly Box? cover;

            public float LineBaseHeight => spriteText.LineBaseHeight;

            public BBCodeWord(string text, FontUsage font, Style style, Color4 codeBackground)
            {
                AutoSizeAxes = Axes.Both;

                if (style.Code)
                    AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = codeBackground });

                AddInternal(spriteText = new OsuSpriteText { Text = text, Font = font });

                var colour = style.Colour ?? Color4.White;
                spriteText.Colour = colour;

                // trailing spaces are not decorated.
                float trailing = text.EndsWith(' ') ? font.Size * 0.25f : 0;
                float thickness = Math.Max(1, font.Size / 14);

                if (style.Underline)
                    AddInternal(line(Anchor.BottomLeft, new Vector2(0, -font.Size * 0.1f)));

                if (style.Strike)
                    AddInternal(line(Anchor.CentreLeft, Vector2.Zero));

                if (style.Spoiler)
                    AddInternal(cover = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black });

                Drawable line(Anchor anchor, Vector2 position) => new Container
                {
                    Anchor = anchor,
                    Origin = anchor,
                    RelativeSizeAxes = Axes.X,
                    Height = thickness,
                    Position = position,
                    Padding = new MarginPadding { Right = trailing },
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                cover?.FadeOut(100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                cover?.FadeIn(100);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// An image which is as wide as the website shows it (or its texture), but no wider than the text.
        /// Images which can't be loaded aren't shown, like on the website.
        /// </summary>
        private partial class BBCodeImage : CompositeDrawable
        {
            public Action? Action { get; init; }

            private readonly string url;
            private readonly float? width;
            private readonly Sprite sprite;
            private readonly Container areas;
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

            [Resolved]
            private LargeTextureStore textures { get; set; } = null!;

            /// <param name="url">The URL of the image.</param>
            /// <param name="width">The width which the website shows the image with.</param>
            public BBCodeImage(string url, float? width)
            {
                this.url = url;
                this.width = width;

                InternalChildren = new Drawable[]
                {
                    sprite = new Sprite { RelativeSizeAxes = Axes.Both },
                    areas = new Container { RelativeSizeAxes = Axes.Both },
                };
            }

            public void AddAreas(IEnumerable<Drawable> drawables) => areas.AddRange(drawables);

            protected override void LoadComplete()
            {
                base.LoadComplete();

                if (string.IsNullOrEmpty(url))
                    return;

                textures.GetAsync(url, cancellation.Token).ContinueWith(t => Schedule(() =>
                {
                    sprite.Texture = t.GetResultSafely();
                    sprite.FadeInFromZero(200);
                }), cancellation.Token, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            }

            protected override void Update()
            {
                base.Update();

                Texture? texture = sprite.Texture;

                if (texture == null || Parent == null)
                    return;

                float displayWidth = Math.Min(width ?? texture.DisplayWidth, Parent.ChildSize.X);
                Size = new Vector2(displayWidth, displayWidth * texture.DisplayHeight / texture.DisplayWidth);
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (Action == null)
                    return false;

                Action();
                return true;
            }

            protected override void Dispose(bool isDisposing)
            {
                cancellation.Cancel();
                cancellation.Dispose();
                base.Dispose(isDisposing);
            }
        }
    }
}
