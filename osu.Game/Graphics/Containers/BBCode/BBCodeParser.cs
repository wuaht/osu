// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace osu.Game.Graphics.Containers.BBCode
{
    public abstract record BBCodeNode;

    public record BBCodeText(string Text) : BBCodeNode;

    /// <summary>
    /// A tag with its content. Tags whose content isn't formatted (such as <c>code</c> or <c>img</c>) have a single <see cref="BBCodeText"/> child.
    /// </summary>
    public record BBCodeTag(string Name, string? Argument, List<BBCodeNode> Children) : BBCodeNode
    {
        /// <summary>
        /// The tag as it was written, which is shown instead if the tag is never closed.
        /// </summary>
        public string Opening { get; init; } = string.Empty;
    }

    /// <summary>
    /// Parses BBCode as it is written on the osu! website (the "editor" form of forum posts and user pages).
    /// The supported tags match those which the website renders (see osu-web's <c>BBCodeFromDB</c>), other text stays as it was written.
    /// </summary>
    public static class BBCodeParser
    {
        private static readonly Regex tag_regex = new Regex(@"\G\[(?<close>/)?(?<name>\*|[a-zA-Z]+)(?:=(?<arg>[^\]\n]*))?\]", RegexOptions.Compiled);

        private static readonly HashSet<string> tags = new HashSet<string>
        {
            @"b", @"i", @"u", @"s", @"strike", @"color", @"size", @"spoiler", @"c", @"url", @"email", @"img", @"profile", @"audio", @"youtube",
            @"centre", @"left", @"right", @"box", @"spoilerbox", @"code", @"notice", @"quote", @"heading", @"list", @"*", @"imagemap",
        };

        /// <summary>
        /// Tags whose content is taken as it is, without formatting.
        /// </summary>
        private static readonly HashSet<string> raw_tags = new HashSet<string> { @"c", @"code", @"img", @"audio", @"youtube", @"imagemap", @"profile" };

        /// <summary>
        /// Tags which may (or must, if not a raw tag) have an argument.
        /// </summary>
        private static readonly HashSet<string> argument_tags = new HashSet<string> { @"color", @"size", @"url", @"email", @"profile", @"box", @"quote", @"list" };

        public static List<BBCodeNode> Parse(string text)
        {
            text = text.Replace("\r\n", "\n", StringComparison.Ordinal);

            var root = new BBCodeTag(string.Empty, null, new List<BBCodeNode>());
            var stack = new List<BBCodeTag> { root };

            int textStart = 0;
            int position = 0;

            void flushText(int end)
            {
                if (end > textStart)
                    addText(stack[^1].Children, text[textStart..end]);
            }

            while ((position = text.IndexOf('[', position)) >= 0)
            {
                var match = tag_regex.Match(text, position);

                if (!match.Success)
                {
                    position++;
                    continue;
                }

                string name = match.Groups[@"name"].Value.ToLowerInvariant();
                string? argument = match.Groups[@"arg"].Success ? match.Groups[@"arg"].Value : null;
                bool closing = match.Groups[@"close"].Success;

                if (!tags.Contains(name) || (argument != null && (closing || !argument_tags.Contains(name))) || (argument == null && !closing && (name is @"color" or @"size")))
                {
                    position++;
                    continue;
                }

                if (closing)
                {
                    int index = stack.FindLastIndex(t => t.Name == name);

                    if (index <= 0)
                    {
                        position++;
                        continue;
                    }

                    flushText(match.Index);

                    // tags which were opened inside and never closed are shown as they were written (list items don't need to be closed).
                    while (stack.Count - 1 > index)
                        unwrap(stack, stack.Count - 1);

                    stack.RemoveAt(stack.Count - 1);
                }
                else if (raw_tags.Contains(name))
                {
                    var end = new Regex($@"\[/{Regex.Escape(name)}\]", RegexOptions.IgnoreCase).Match(text, match.Index + match.Length);

                    if (!end.Success)
                    {
                        position++;
                        continue;
                    }

                    flushText(match.Index);

                    string content = text[(match.Index + match.Length)..end.Index];
                    stack[^1].Children.Add(new BBCodeTag(name, argument, new List<BBCodeNode> { new BBCodeText(content) }) { Opening = match.Value });

                    textStart = position = end.Index + end.Length;
                    continue;
                }
                else
                {
                    flushText(match.Index);

                    if (name == @"*")
                    {
                        if (!stack.Any(t => t.Name == @"list"))
                        {
                            textStart = match.Index;
                            position++;
                            continue;
                        }

                        // a new list item ends the previous one.
                        if (stack[^1].Name == @"*")
                            stack.RemoveAt(stack.Count - 1);
                    }

                    var tag = new BBCodeTag(name, argument, new List<BBCodeNode>()) { Opening = match.Value };
                    stack[^1].Children.Add(tag);
                    stack.Add(tag);
                }

                textStart = position = match.Index + match.Length;
            }

            flushText(text.Length);

            while (stack.Count > 1)
                unwrap(stack, stack.Count - 1);

            return root.Children;
        }

        /// <summary>
        /// Removes an unclosed tag from the stack, replacing it with its opening text and its content.
        /// </summary>
        private static void unwrap(List<BBCodeTag> stack, int index)
        {
            var tag = stack[index];
            stack.RemoveAt(index);

            // list items are closed implicitly.
            if (tag.Name == @"*")
                return;

            var siblings = stack[index - 1].Children;
            int position = siblings.IndexOf(tag);
            siblings.RemoveAt(position);

            var replacement = siblings.Take(position).ToList();
            addText(replacement, tag.Opening);

            foreach (var node in tag.Children.Concat(siblings.Skip(position)))
            {
                if (node is BBCodeText text)
                    addText(replacement, text.Text);
                else
                    replacement.Add(node);
            }

            siblings.Clear();
            siblings.AddRange(replacement);
        }

        private static void addText(List<BBCodeNode> nodes, string text)
        {
            if (nodes.Count > 0 && nodes[^1] is BBCodeText previous)
                nodes[^1] = new BBCodeText(previous.Text + text);
            else
                nodes.Add(new BBCodeText(text));
        }
    }
}
