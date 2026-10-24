// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// A parser of the markdown flavour of Discord messages.
    /// </summary>
    public static class DiscordMarkdown
    {
        private static readonly Regex code_block_regex = new Regex(@"```(?:([a-zA-Z0-9_+\-]*)\n)?([\s\S]*?)```", RegexOptions.Compiled);

        private static readonly Regex user_mention_regex = new Regex(@"\G<@!?(\d+)>", RegexOptions.Compiled);
        private static readonly Regex role_mention_regex = new Regex(@"\G<@&(\d+)>", RegexOptions.Compiled);
        private static readonly Regex channel_mention_regex = new Regex(@"\G<#(\d+)>", RegexOptions.Compiled);
        private static readonly Regex custom_emoji_regex = new Regex(@"\G<(a?):(\w+):(\d+)>", RegexOptions.Compiled);
        private static readonly Regex timestamp_regex = new Regex(@"\G<t:(-?\d+)(?::([tTdDfFR]))?>", RegexOptions.Compiled);
        private static readonly Regex suppressed_url_regex = new Regex(@"\G<(https?://[^\s>]+)>", RegexOptions.Compiled);
        private static readonly Regex masked_link_regex = new Regex(@"\G\[([^\[\]\n]+)\]\(<?(https?://[^\s)>]+)>?\)", RegexOptions.Compiled);
        private static readonly Regex url_regex = new Regex(@"\Ghttps?://[^\s<]+[^<.,:;""'\)\]\s]", RegexOptions.Compiled);
        private static readonly Regex everyone_regex = new Regex(@"\G@(everyone|here)\b", RegexOptions.Compiled);

        private static readonly Regex heading_regex = new Regex(@"^(#{1,3}) (.+)$", RegexOptions.Compiled);
        private static readonly Regex subtext_regex = new Regex(@"^-# (.+)$", RegexOptions.Compiled);
        private static readonly Regex list_regex = new Regex(@"^(\s*)([-*]|\d+\.) (.+)$", RegexOptions.Compiled);

        /// <summary>
        /// Parses the blocks of a message.
        /// </summary>
        public static List<MarkdownBlock> ParseBlocks(string content, bool allowQuotes = true)
        {
            var blocks = new List<MarkdownBlock>();

            int position = 0;

            foreach (Match match in code_block_regex.Matches(content))
            {
                parseTextBlocks(content[position..match.Index], blocks, allowQuotes);

                string code = match.Groups[2].Value;

                // A single line code block without a language keeps its first word as code.
                if (!match.Groups[1].Success && code.StartsWith('\n'))
                    code = code[1..];

                blocks.Add(new MarkdownBlock(MarkdownBlockType.CodeBlock, code.TrimEnd('\n')));
                position = match.Index + match.Length;
            }

            parseTextBlocks(content[position..], blocks, allowQuotes);

            return blocks;
        }

        private static void parseTextBlocks(string text, List<MarkdownBlock> blocks, bool allowQuotes)
        {
            if (text.Length == 0)
                return;

            // A code block swallows the line break after it.
            if (text.StartsWith('\n') && blocks.Count > 0 && blocks[^1].Type == MarkdownBlockType.CodeBlock)
                text = text[1..];

            if (text.Trim().Length == 0)
                return;

            string[] lines = text.Split('\n');

            var paragraph = new List<string>();
            var quote = new List<string>();

            void flushParagraph()
            {
                if (paragraph.Count == 0)
                    return;

                // Leading and trailing empty lines around code blocks and quotes are not shown.
                while (paragraph.Count > 0 && paragraph[0].Trim().Length == 0)
                    paragraph.RemoveAt(0);
                while (paragraph.Count > 0 && paragraph[^1].Trim().Length == 0)
                    paragraph.RemoveAt(paragraph.Count - 1);

                if (paragraph.Count > 0)
                    blocks.Add(new MarkdownBlock(MarkdownBlockType.Paragraph, string.Join('\n', paragraph)));

                paragraph.Clear();
            }

            void flushQuote()
            {
                if (quote.Count == 0)
                    return;

                blocks.Add(new MarkdownBlock(MarkdownBlockType.Quote, string.Join('\n', quote)));
                quote.Clear();
            }

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (allowQuotes && (line.StartsWith(@">>> ", StringComparison.Ordinal) || line == @">>>"))
                {
                    flushParagraph();
                    flushQuote();

                    // Everything until the end of the message is quoted.
                    var rest = new List<string> { line.Length > 4 ? line[4..] : string.Empty };
                    rest.AddRange(lines.Skip(i + 1));

                    blocks.Add(new MarkdownBlock(MarkdownBlockType.Quote, string.Join('\n', rest)));
                    return;
                }

                if (allowQuotes && (line.StartsWith(@"> ", StringComparison.Ordinal) || line == @">"))
                {
                    flushParagraph();
                    quote.Add(line.Length > 2 ? line[2..] : string.Empty);
                    continue;
                }

                flushQuote();

                var heading = heading_regex.Match(line);

                if (heading.Success)
                {
                    flushParagraph();
                    blocks.Add(new MarkdownBlock(MarkdownBlockType.Heading, heading.Groups[2].Value) { Level = heading.Groups[1].Length });
                    continue;
                }

                var subtext = subtext_regex.Match(line);

                if (subtext.Success)
                {
                    flushParagraph();
                    blocks.Add(new MarkdownBlock(MarkdownBlockType.Subtext, subtext.Groups[1].Value));
                    continue;
                }

                var list = list_regex.Match(line);

                if (list.Success)
                {
                    flushParagraph();

                    string marker = list.Groups[2].Value;

                    blocks.Add(new MarkdownBlock(MarkdownBlockType.ListItem, list.Groups[3].Value)
                    {
                        Level = Math.Min(list.Groups[1].Length / 2, 3),
                        Marker = marker == @"-" || marker == @"*" ? null : marker,
                    });
                    continue;
                }

                paragraph.Add(line);
            }

            flushParagraph();
            flushQuote();
        }

        /// <summary>
        /// Parses the inline formatting of a block.
        /// </summary>
        public static List<MarkdownInline> ParseInline(string text)
        {
            var result = new List<MarkdownInline>();
            int spoilerCounter = 0;
            parseInline(text, MarkdownStyle.None, -1, result, ref spoilerCounter);
            return result;
        }

        private static void parseInline(string text, MarkdownStyle style, int spoiler, List<MarkdownInline> output, ref int spoilerCounter)
        {
            var plain = new StringBuilder();

            void flush()
            {
                if (plain.Length == 0)
                    return;

                output.Add(new MarkdownInline(MarkdownInlineType.Text, plain.ToString(), style, spoiler));
                plain.Clear();
            }

            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                // Escaped markdown characters are shown as they are.
                if (c == '\\' && i + 1 < text.Length && isEscapable(text[i + 1]))
                {
                    plain.Append(text[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '\n')
                {
                    flush();
                    output.Add(new MarkdownInline(MarkdownInlineType.LineBreak, string.Empty, style, spoiler));
                    i++;
                    continue;
                }

                if (c == '`')
                {
                    int ticks = c == '`' && i + 1 < text.Length && text[i + 1] == '`' ? 2 : 1;
                    string delimiter = new string('`', ticks);
                    int end = text.IndexOf(delimiter, i + ticks, StringComparison.Ordinal);

                    if (end > i + ticks - 1 && end > i + ticks)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.Text, text[(i + ticks)..end].Trim(), style | MarkdownStyle.Code, spoiler));
                        i = end + ticks;
                        continue;
                    }
                }

                if (tryDelimited(text, ref i, @"||", out string? inner))
                {
                    flush();
                    parseInline(inner, style, spoilerCounter++, output, ref spoilerCounter);
                    continue;
                }

                if (tryDelimited(text, ref i, @"***", out inner))
                {
                    flush();
                    parseInline(inner, style | MarkdownStyle.Bold | MarkdownStyle.Italic, spoiler, output, ref spoilerCounter);
                    continue;
                }

                if (tryDelimited(text, ref i, @"**", out inner))
                {
                    flush();
                    parseInline(inner, style | MarkdownStyle.Bold, spoiler, output, ref spoilerCounter);
                    continue;
                }

                if (tryDelimited(text, ref i, @"__", out inner))
                {
                    flush();
                    parseInline(inner, style | MarkdownStyle.Underline, spoiler, output, ref spoilerCounter);
                    continue;
                }

                if (tryDelimited(text, ref i, @"~~", out inner))
                {
                    flush();
                    parseInline(inner, style | MarkdownStyle.Strikethrough, spoiler, output, ref spoilerCounter);
                    continue;
                }

                if (c == '*' && tryDelimited(text, ref i, @"*", out inner))
                {
                    flush();
                    parseInline(inner, style | MarkdownStyle.Italic, spoiler, output, ref spoilerCounter);
                    continue;
                }

                // Underscores only start italics at word boundaries, so that snake_case stays as it is.
                if (c == '_' && (i == 0 || !char.IsLetterOrDigit(text[i - 1])))
                {
                    int start = i;

                    if (tryDelimited(text, ref i, @"_", out inner))
                    {
                        if (i >= text.Length || !char.IsLetterOrDigit(text[i]))
                        {
                            flush();
                            parseInline(inner, style | MarkdownStyle.Italic, spoiler, output, ref spoilerCounter);
                            continue;
                        }

                        i = start;
                    }
                }

                if (c == '<')
                {
                    Match match;

                    if ((match = user_mention_regex.Match(text, i)).Success)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.UserMention, match.Value, style, spoiler) { Id = ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) });
                        i += match.Length;
                        continue;
                    }

                    if ((match = role_mention_regex.Match(text, i)).Success)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.RoleMention, match.Value, style, spoiler) { Id = ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) });
                        i += match.Length;
                        continue;
                    }

                    if ((match = channel_mention_regex.Match(text, i)).Success)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.ChannelMention, match.Value, style, spoiler) { Id = ulong.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) });
                        i += match.Length;
                        continue;
                    }

                    if ((match = custom_emoji_regex.Match(text, i)).Success)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.CustomEmoji, match.Groups[2].Value, style, spoiler)
                        {
                            Id = ulong.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture),
                            Animated = match.Groups[1].Length > 0,
                        });
                        i += match.Length;
                        continue;
                    }

                    if ((match = timestamp_regex.Match(text, i)).Success && long.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long unix))
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.Timestamp, match.Groups[2].Success ? match.Groups[2].Value : @"f", style, spoiler) { Unix = unix });
                        i += match.Length;
                        continue;
                    }

                    if ((match = suppressed_url_regex.Match(text, i)).Success)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.Link, match.Groups[1].Value, style, spoiler) { Url = match.Groups[1].Value });
                        i += match.Length;
                        continue;
                    }
                }

                if (c == '[')
                {
                    var match = masked_link_regex.Match(text, i);

                    if (match.Success)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.Link, match.Groups[1].Value, style, spoiler) { Url = match.Groups[2].Value });
                        i += match.Length;
                        continue;
                    }
                }

                if (c == 'h' && (i == 0 || !char.IsLetterOrDigit(text[i - 1])))
                {
                    var match = url_regex.Match(text, i);

                    if (match.Success)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.Link, match.Value, style, spoiler) { Url = match.Value });
                        i += match.Length;
                        continue;
                    }
                }

                if (c == '@')
                {
                    var match = everyone_regex.Match(text, i);

                    if (match.Success)
                    {
                        flush();
                        output.Add(new MarkdownInline(MarkdownInlineType.EveryoneMention, match.Value, style, spoiler));
                        i += match.Length;
                        continue;
                    }
                }

                plain.Append(c);
                i++;
            }

            flush();
        }

        /// <summary>
        /// Tries to read a span which starts at the current position with a delimiter and ends with the same delimiter.
        /// </summary>
        private static bool tryDelimited(string text, ref int position, string delimiter, out string inner)
        {
            inner = string.Empty;

            if (string.CompareOrdinal(text, position, delimiter, 0, delimiter.Length) != 0)
                return false;

            int start = position + delimiter.Length;

            // The content of a span can't start with whitespace, and the delimiter can't be followed by the same character (which would be a longer delimiter).
            if (start >= text.Length || char.IsWhiteSpace(text[start]) || text[start] == delimiter[0] && delimiter.Length == 1)
                return false;

            int end = start;

            while (true)
            {
                end = text.IndexOf(delimiter, end + 1, StringComparison.Ordinal);

                if (end < 0)
                    return false;

                // Skip escaped delimiters.
                if (text[end - 1] == '\\')
                    continue;

                // For single character delimiters, don't end on the start of a double delimiter (such as the first * of **).
                if (delimiter.Length == 1 && end + 1 < text.Length && text[end + 1] == delimiter[0])
                {
                    end++;
                    continue;
                }

                break;
            }

            inner = text[start..end];
            position = end + delimiter.Length;
            return true;
        }

        private static bool isEscapable(char c) => c is '*' or '_' or '~' or '`' or '|' or '>' or '\\' or '<' or '#' or '-' or '[' or ']' or '(' or ')' or ':';

        /// <summary>
        /// Splits text into text elements (user-perceived characters), so that emoji sequences stay together.
        /// </summary>
        public static IEnumerable<string> GetTextElements(string text)
        {
            var enumerator = StringInfo.GetTextElementEnumerator(text);

            while (enumerator.MoveNext())
                yield return enumerator.GetTextElement();
        }

        /// <summary>
        /// Whether a message only consists of emoji (and whitespace), in which case they are shown larger.
        /// </summary>
        public static bool IsEmojiOnly(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || content.Contains("```", StringComparison.Ordinal))
                return false;

            var inlines = ParseInline(content.Trim());
            int count = 0;

            foreach (var inline in inlines)
            {
                switch (inline.Type)
                {
                    case MarkdownInlineType.CustomEmoji:
                        count++;
                        break;

                    case MarkdownInlineType.LineBreak:
                        break;

                    case MarkdownInlineType.Text:
                        if (inline.Style.HasFlag(MarkdownStyle.Code))
                            return false;

                        foreach (string element in GetTextElements(inline.Text))
                        {
                            if (string.IsNullOrWhiteSpace(element))
                                continue;

                            if (!DiscordEmoji.IsEmoji(element))
                                return false;

                            count++;
                        }

                        break;

                    default:
                        return false;
                }
            }

            return count > 0 && count <= 30;
        }
    }

    public enum MarkdownBlockType
    {
        Paragraph,
        CodeBlock,
        Quote,
        Heading,
        Subtext,
        ListItem,
    }

    public class MarkdownBlock
    {
        public readonly MarkdownBlockType Type;
        public readonly string Text;

        /// <summary>
        /// The level of a heading (1 to 3) or the indentation of a list item.
        /// </summary>
        public int Level;

        /// <summary>
        /// The marker of an ordered list item (such as "1."), or null for a bullet.
        /// </summary>
        public string? Marker;

        public MarkdownBlock(MarkdownBlockType type, string text)
        {
            Type = type;
            Text = text;
        }
    }

    [Flags]
    public enum MarkdownStyle
    {
        None = 0,
        Bold = 1,
        Italic = 1 << 1,
        Underline = 1 << 2,
        Strikethrough = 1 << 3,
        Code = 1 << 4,
    }

    public enum MarkdownInlineType
    {
        Text,
        LineBreak,
        Link,
        UserMention,
        RoleMention,
        ChannelMention,
        EveryoneMention,
        CustomEmoji,
        Timestamp,
    }

    public class MarkdownInline
    {
        public readonly MarkdownInlineType Type;

        /// <summary>
        /// The text, the name of a custom emoji, or the format of a timestamp.
        /// </summary>
        public readonly string Text;

        public readonly MarkdownStyle Style;

        /// <summary>
        /// The index of the spoiler this is part of, or -1 if it's not part of a spoiler.
        /// </summary>
        public readonly int Spoiler;

        public ulong Id;
        public bool Animated;
        public long Unix;
        public string? Url;

        public MarkdownInline(MarkdownInlineType type, string text, MarkdownStyle style, int spoiler)
        {
            Type = type;
            Text = text;
            Style = style;
            Spoiler = spoiler;
        }
    }
}
