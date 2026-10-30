// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Graphics.Containers.BBCode
{
    /// <summary>
    /// BBCode as the osu! website renders it (user pages and beatmap descriptions, see osu-web's <c>BBCodeFromDB</c>).
    /// </summary>
    public partial class DrawableBBCode : FillFlowContainer
    {
        private static readonly Regex url_regex = new Regex(@"(?<=^|[\s(])https?://[^\s<>\[\]""]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex word_regex = new Regex(@"\S+\s*|\s+", RegexOptions.Compiled);
        private static readonly Regex html_image_regex = new Regex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex html_src_regex = new Regex(@"\bsrc=""(?<src>[^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex imagemap_area_regex = new Regex(@"^(?<x>[0-9.]+) (?<y>[0-9.]+) (?<w>[0-9.]+) (?<h>[0-9.]+) (?<url>#|https?://\S+|mailto:\S+)(?: (?<title>.*))?$", RegexOptions.Compiled);

        private readonly List<BBCodeNode> nodes;
        private readonly float textSize;

        /// <summary>
        /// The image URLs as proxied by the website, in the order of the images. External images can't be loaded directly (see <see cref="TrustedDomainOnlineStore"/>).
        /// </summary>
        private readonly Queue<string>? proxiedImages;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ILinkHandler? linkHandler { get; set; }

        [Resolved]
        private GameHost host { get; set; } = null!;

        /// <param name="bbcode">The BBCode as it was written.</param>
        /// <param name="html">The BBCode as rendered by the website, which contains the proxied URLs of images.</param>
        /// <param name="textSize">The size of regular text.</param>
        public DrawableBBCode(string bbcode, string? html = null, float textSize = 14)
        {
            this.textSize = textSize;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;

            nodes = BBCodeParser.Parse(bbcode);

            if (html != null)
            {
                var images = html_image_regex.Matches(html)
                                             .Where(m => !m.Value.Contains(@"class=""smiley""", StringComparison.Ordinal))
                                             .Select(m => html_src_regex.Match(m.Value))
                                             .Where(m => m.Success)
                                             .Select(m => WebUtility.HtmlDecode(m.Groups[@"src"].Value))
                                             .ToList();

                // the images are only matched by their order, which is wrong if the website parsed the BBCode differently.
                if (images.Count == countImages(nodes))
                    proxiedImages = new Queue<string>(images);
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            render(nodes, new Block(this, Anchor.TopLeft), new Style { Size = textSize });
        }

        private void render(IEnumerable<BBCodeNode> content, Block block, Style style)
        {
            foreach (var node in content)
            {
                switch (node)
                {
                    case BBCodeText text:
                        addText(text.Text, block, style);
                        break;

                    case BBCodeTag tag:
                        renderTag(tag, block, style);
                        break;
                }
            }
        }

        private void renderTag(BBCodeTag tag, Block block, Style style)
        {
            var children = tag.Children;

            switch (tag.Name)
            {
                case @"b":
                    render(children, block, style with { Bold = true });
                    break;

                case @"i":
                    render(children, block, style with { Italic = true });
                    break;

                case @"u":
                    render(children, block, style with { Underline = true });
                    break;

                case @"s":
                case @"strike":
                    render(children, block, style with { Strike = true });
                    break;

                case @"spoiler":
                    render(children, block, style with { Spoiler = true });
                    break;

                case @"color":
                {
                    var colour = parseColour(tag.Argument!.Trim());

                    if (colour == null)
                        renderLiteral(tag, block, style);
                    else
                        render(children, block, style with { Colour = colour });
                    break;
                }

                case @"size":
                {
                    if (!int.TryParse(tag.Argument, NumberStyles.None, CultureInfo.InvariantCulture, out int percentage))
                        renderLiteral(tag, block, style);
                    else
                        render(children, block, style with { Size = textSize * Math.Clamp(percentage, 30, 200) / 100f });
                    break;
                }

                case @"c":
                    addText(plainText(children), block, style with { Code = true }, false);
                    break;

                case @"url":
                    render(children, block, style with { Link = normaliseUrl(tag.Argument ?? plainText(children)) });
                    break;

                case @"email":
                    render(children, block, style with { Link = @"mailto:" + (tag.Argument ?? plainText(children)) });
                    break;

                case @"profile":
                {
                    string name = plainText(children);
                    addText(name, block, style with { Link = $@"https://osu.ppy.sh/users/{Uri.EscapeDataString(tag.Argument ?? name)}" }, false);
                    break;
                }

                case @"audio":
                case @"youtube":
                {
                    string url = tag.Name == @"youtube" ? $@"https://www.youtube.com/watch?v={plainText(children).Trim()}" : plainText(children).Trim();
                    addText(url, block, style with { Link = url }, false);
                    break;
                }

                case @"img":
                    addInline(block, new BBCodeImage(nextImageUrl(plainText(children).Trim())) { Action = linkAction(style.Link) });
                    break;

                case @"centre":
                case @"left":
                case @"right":
                {
                    var anchor = tag.Name == @"centre" ? Anchor.TopCentre : tag.Name == @"right" ? Anchor.TopRight : Anchor.TopLeft;
                    var inner = new Block(createBlockFlow(), anchor);
                    startBlock(block, inner.Target);
                    render(trimNewlines(children, 1), inner, style);
                    endBlock(block, 1);
                    break;
                }

                case @"heading":
                {
                    var inner = new Block(createBlockFlow(), block.TextAnchor);
                    startBlock(block, inner.Target);
                    render(children, inner, style with { Bold = true, Size = style.Size * 1.5f });
                    endBlock(block, 1);
                    break;
                }

                case @"box":
                case @"spoilerbox":
                    startBlock(block, createBox(tag.Argument ?? @"SPOILER", children, block, style));
                    endBlock(block, 1);
                    break;

                case @"code":
                    startBlock(block, createCode(plainText(children).Trim('\n'), style));
                    endBlock(block, 1);
                    break;

                case @"notice":
                {
                    var inner = new Block(createBlockFlow(), block.TextAnchor);

                    startBlock(block, new Container
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
                    });

                    render(trimNewlines(children), inner, style);
                    endBlock(block, 1);
                    break;
                }

                case @"quote":
                {
                    var inner = new Block(createBlockFlow(), block.TextAnchor);

                    if (tag.Argument != null)
                    {
                        inner.Target.Add(new OsuSpriteText
                        {
                            Text = $@"{tag.Argument.Trim('"')} wrote:",
                            Font = font(style with { Bold = true }),
                            Margin = new MarginPadding { Bottom = 5 },
                        });
                    }

                    startBlock(block, new Container
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
                    });

                    render(trimNewlines(children), inner, style);
                    endBlock(block, 2);
                    break;
                }

                case @"list":
                    startBlock(block, createList(tag, block, style));
                    endBlock(block, 2);
                    break;

                case @"imagemap":
                {
                    var imagemap = createImagemap(plainText(children));

                    if (imagemap == null)
                    {
                        renderLiteral(tag, block, style);
                        break;
                    }

                    startBlock(block, imagemap);
                    endBlock(block, 1);
                    break;
                }

                default:
                    render(children, block, style);
                    break;
            }
        }

        /// <summary>
        /// Shows a tag as it was written, for tags which the website doesn't render (such as a colour which doesn't exist).
        /// </summary>
        private void renderLiteral(BBCodeTag tag, Block block, Style style)
        {
            addText(tag.Opening, block, style);
            render(tag.Children, block, style);
            addText($@"[/{tag.Name}]", block, style);
        }

        #region Text

        private void addText(string text, Block block, Style style, bool autolink = true)
        {
            string[] lines = text.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    newLine(block, style);

                if (lines[i].Length > 0)
                    addLine(lines[i], block, style, autolink);
            }
        }

        private void newLine(Block block, Style style)
        {
            // block elements swallow some of the line breaks after them.
            if (block.SkipNewlines > 0)
            {
                block.SkipNewlines--;
                return;
            }

            if (block.LineOpen)
            {
                block.Flow!.NewLine();
                block.LineOpen = false;
                return;
            }

            // an empty line needs some content to have a height.
            var flow = ensureFlow(block);
            flow.AddText(@" ", s => s.Font = font(style));
            flow.NewLine();
        }

        private void addLine(string text, Block block, Style style, bool autolink)
        {
            var flow = ensureFlow(block);
            block.LineOpen = true;
            block.SkipNewlines = 0;

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

            int position = 0;

            if (autolink)
            {
                foreach (Match match in url_regex.Matches(text))
                {
                    // trailing punctuation usually ends the sentence rather than belonging to the link.
                    string url = match.Value.TrimEnd('.', ',', ')', '!', '?', ':', ';');

                    if (match.Index > position)
                        flow.AddText(text[position..match.Index], s => applyStyle(s, style));

                    addLine(url, block, style with { Link = url }, false);
                    position = match.Index + url.Length;
                }
            }

            if (position < text.Length)
                flow.AddText(text[position..], s => applyStyle(s, style));
        }

        private void addInline(Block block, Drawable drawable)
        {
            ensureFlow(block).AddArbitraryDrawable(drawable);
            block.LineOpen = true;
            block.SkipNewlines = 0;
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
            block.SkipNewlines = 0;
            block.Target.Add(drawable);
        }

        private static void endBlock(Block block, int swallowedNewlines)
        {
            block.Flow = null;
            block.LineOpen = false;
            block.SkipNewlines = swallowedNewlines;
        }

        private Drawable createBox(string title, List<BBCodeNode> children, Block block, Style style)
        {
            var body = new Block(createBlockFlow(), block.TextAnchor);
            body.Target.Alpha = 0;
            body.Target.Padding = new MarginPadding { Left = style.Size + 6 };

            SpriteIcon icon;

            var box = new FillFlowContainer
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
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = title,
                                    Font = font(style with { Bold = true }),
                                },
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

            render(trimNewlines(children), body, style);
            return box;
        }

        private Drawable createCode(string code, Style style)
        {
            var flow = new OsuTextFlowContainer(s => s.Font = font(style with { Code = true }))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding(10),
            };

            foreach (string line in code.Split('\n'))
            {
                flow.AddText(line.Length > 0 ? line : @" ");
                flow.NewLine();
            }

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
                    flow,
                },
            };
        }

        private Drawable createList(BBCodeTag list, Block block, Style style)
        {
            var target = createBlockFlow();

            // text before the first item is the title of the list.
            var title = list.Children.TakeWhile(n => n is not BBCodeTag { Name: @"*" }).ToList();

            if (title.Any(n => n is not BBCodeText text || !string.IsNullOrWhiteSpace(text.Text)))
                render(trimNewlines(title), new Block(target, block.TextAnchor), style);

            int number = 0;

            foreach (var item in list.Children.OfType<BBCodeTag>().Where(t => t.Name == @"*"))
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
                            Text = list.Argument != null ? $@"{++number}." : @"•",
                            Font = font(style),
                        },
                        body.Target,
                    },
                });

                render(trimNewlines(item.Children), body, style);
            }

            return target;
        }

        private Drawable? createImagemap(string content)
        {
            string[] lines = content.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

            if (lines.Length < 2 || !lines[0].StartsWith(@"http", StringComparison.OrdinalIgnoreCase))
                return null;

            var areas = new List<Drawable>();

            foreach (string line in lines.Skip(1))
            {
                var match = imagemap_area_regex.Match(line);

                if (!match.Success)
                    return null;

                string url = match.Groups[@"url"].Value;

                areas.Add(new OsuClickableContainer
                {
                    RelativePositionAxes = Axes.Both,
                    RelativeSizeAxes = Axes.Both,
                    Position = new Vector2(percentage(match, @"x"), percentage(match, @"y")),
                    Size = new Vector2(percentage(match, @"w"), percentage(match, @"h")),
                    TooltipText = match.Groups[@"title"].Value,
                    Action = url == @"#" ? null : linkAction(url),
                });
            }

            var image = new BBCodeImage(nextImageUrl(lines[0]));
            image.AddAreas(areas);
            return image;

            static float percentage(Match match, string group) => float.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) / 100;
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

        private string nextImageUrl(string url) => proxiedImages?.Count > 0 ? proxiedImages.Dequeue() : url;

        private static int countImages(IEnumerable<BBCodeNode> content) =>
            content.OfType<BBCodeTag>().Sum(t => t.Name is @"img" or @"imagemap" ? 1 : countImages(t.Children));

        private static string plainText(IEnumerable<BBCodeNode> content) =>
            string.Concat(content.Select(n => n switch
            {
                BBCodeText text => text.Text,
                BBCodeTag tag => tag.Opening + plainText(tag.Children) + $@"[/{tag.Name}]",
                _ => string.Empty,
            }));

        /// <summary>
        /// Removes line breaks at the start and end of a block's content, which the website doesn't show.
        /// </summary>
        private static List<BBCodeNode> trimNewlines(List<BBCodeNode> content, int count = int.MaxValue)
        {
            var trimmed = content.ToList();

            if (trimmed.Count > 0 && trimmed[0] is BBCodeText first)
                trimmed[0] = new BBCodeText(trimStart(first.Text, count));

            if (trimmed.Count > 0 && trimmed[^1] is BBCodeText last)
                trimmed[^1] = new BBCodeText(count == int.MaxValue ? last.Text.TrimEnd('\n') : last.Text);

            return trimmed;

            static string trimStart(string text, int count)
            {
                int start = 0;
                while (start < text.Length && start < count && text[start] == '\n')
                    start++;
                return text[start..];
            }
        }

        private static string normaliseUrl(string url)
        {
            url = url.Trim();
            return url.Contains(@"://", StringComparison.Ordinal) || url.StartsWith(@"mailto:", StringComparison.OrdinalIgnoreCase) ? url : @"http://" + url;
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

        private record struct Style
        {
            public bool Bold { get; init; }
            public bool Italic { get; init; }
            public bool Underline { get; init; }
            public bool Strike { get; init; }
            public bool Spoiler { get; init; }
            public bool Code { get; init; }
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

            public int SkipNewlines;

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
        /// An image which is as wide as its texture, but no wider than the text.
        /// </summary>
        private partial class BBCodeImage : CompositeDrawable
        {
            public Action? Action { get; init; }

            private readonly string url;
            private readonly Sprite sprite;
            private readonly Container areas;
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

            [Resolved]
            private LargeTextureStore textures { get; set; } = null!;

            public BBCodeImage(string url)
            {
                this.url = url;

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

                textures.GetAsync(url, cancellation.Token).ContinueWith(t => Schedule(() =>
                {
                    if (t.GetResultSafely() == null)
                    {
                        // images which can't be loaded (like from external websites when not proxied) are shown as links.
                        AutoSizeAxes = Axes.Both;
                        AddInternal(new OsuSpriteText { Text = url, Colour = Color4.LightBlue });
                        return;
                    }

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

                float width = Math.Min(texture.DisplayWidth, Parent.ChildSize.X);
                Size = new Vector2(width, width * texture.DisplayHeight / texture.DisplayWidth);
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
