// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Markdig.Syntax;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Online.Chat;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// A discussion message, rendered like on the website with the markdown constructs which are allowed in discussions.
    /// Editor timestamps open the editor on the difficulty of the discussion.
    /// </summary>
    public partial class DiscussionMarkdown : OsuMarkdownContainer, ILinkHandler
    {
        private readonly float fontSize;
        private readonly int? beatmapId;

        [Resolved]
        private DiscussionScreen screen { get; set; } = null!;

        /// <param name="markdown">The message.</param>
        /// <param name="beatmapSetId">The ID of the beatmap set, to shorten links to its discussions.</param>
        /// <param name="beatmapId">The ID of the beatmap of the discussion (on the website), which timestamps refer to.</param>
        /// <param name="fontSize">The size of the text.</param>
        public DiscussionMarkdown(string markdown, int beatmapSetId, int? beatmapId, float fontSize)
        {
            this.beatmapId = beatmapId;
            this.fontSize = fontSize;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            DocumentMargin = new MarginPadding();
            DocumentPadding = new MarginPadding();
            LineSpacing = fontSize * 1.35f;

            Text = DiscussionDisplay.PrepareMarkdown(markdown, beatmapSetId);
        }

        protected override OsuMarkdownContainerOptions Options => new OsuMarkdownContainerOptions
        {
            Autolinks = true,
        };

        public override SpriteText CreateSpriteText() => new OsuSpriteText
        {
            Font = OsuFont.GetFont(Typeface.Inter, size: fontSize, weight: FontWeight.Regular),
        };

        protected override void AddMarkdownComponent(IMarkdownObject markdownObject, FillFlowContainer container, int level)
        {
            switch (markdownObject)
            {
                // headings and separators aren't allowed in discussions, so they're shown as they were written.
                case HeadingBlock headingBlock:
                {
                    var flow = CreateTextFlow();
                    flow.AddText(new string(headingBlock.HeaderChar, headingBlock.Level) + @" ");

                    if (headingBlock.Inline != null)
                        flow.AddInlineText(headingBlock.Inline);

                    container.Add(flow);
                    break;
                }

                case ThematicBreakBlock thematicBreak:
                {
                    var flow = CreateTextFlow();
                    flow.AddText(new string(thematicBreak.ThematicChar, Math.Max(3, thematicBreak.ThematicCharCount)));
                    container.Add(flow);
                    break;
                }

                default:
                    base.AddMarkdownComponent(markdownObject, container, level);
                    break;
            }
        }

        public void HandleLink(string url)
        {
            if (url.StartsWith(DiscussionDisplay.EDITOR_LINK_PREFIX, StringComparison.Ordinal))
            {
                string timestamp = Uri.UnescapeDataString(url[DiscussionDisplay.EDITOR_LINK_PREFIX.Length..]);
                screen.ShowTimestamp(timestamp, beatmapId);
                return;
            }

            screen.HandleLink(url);
        }

        public void HandleLink(LinkDetails link) => screen.HandleLink(link);
    }
}
