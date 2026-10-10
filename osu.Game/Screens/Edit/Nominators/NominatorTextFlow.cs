// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text.RegularExpressions;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// Text written by nominators (e.g. how to request them) or the mapper (comments), which may contain BBCode and markdown.
    /// Links and images become links, other formatting is removed.
    /// </summary>
    public partial class NominatorTextFlow : LinkFlowContainer
    {
        private static readonly Regex link_regex = new Regex(
            @"\[url=(?<url1>https?://[^\]\s]+)\](?<label1>.*?)\[/url\]"
            + @"|\[url\](?<url2>https?://[^\[\s]+)\[/url\]"
            + @"|\[img\](?<image1>https?://[^\[\s]+)\[/img\]"
            + @"|!\[[^\]]*\]\((?<image2>https?://[^\s)]+)\)"
            + @"|\[(?<label3>[^\]]+)\]\((?<url3>https?://[^\s)]+)\)"
            + @"|(?<url4>https?://[^\s\[\]<>""]+)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex formatting_regex = new Regex(
            @"\[/?(b|i|u|s|strike|color|colour|size|centre|center|quote|spoiler|spoilerbox|box|heading|list|code|notice|\*)(=[^\]]*)?\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly float textSize;

        public NominatorTextFlow(float textSize = 14)
            : base(s => s.Font = OsuFont.Default.With(size: textSize))
        {
            this.textSize = textSize;
        }

        /// <summary>
        /// Replaces the text.
        /// </summary>
        public void SetText(string text)
        {
            Clear();

            int position = 0;

            foreach (Match match in link_regex.Matches(text))
            {
                if (match.Index > position)
                    AddText(removeFormatting(text[position..match.Index]));

                string? image = value(match, @"image1") ?? value(match, @"image2");

                if (image != null)
                    AddLink(SlopNominatorsStrings.Image, image, setFont);
                else
                {
                    string url = (value(match, @"url1") ?? value(match, @"url2") ?? value(match, @"url3") ?? value(match, @"url4"))!;

                    // trailing punctuation usually ends the sentence rather than belonging to the link.
                    string trailing = string.Empty;

                    if (match.Groups[@"url4"].Success)
                    {
                        string trimmed = url.TrimEnd('.', ',', ')', '!', '?', ':', ';');
                        trailing = url[trimmed.Length..];
                        url = trimmed;
                    }

                    string label = removeFormatting(value(match, @"label1") ?? value(match, @"label3") ?? url).Trim();

                    AddLink(label.Length > 0 ? label : url, url, setFont);

                    if (trailing.Length > 0)
                        AddText(trailing);
                }

                position = match.Index + match.Length;
            }

            if (position < text.Length)
                AddText(removeFormatting(text[position..]));
        }

        private void setFont(SpriteText s) => s.Font = OsuFont.Default.With(size: textSize);

        private static string? value(Match match, string group) => match.Groups[group].Success ? match.Groups[group].Value : null;

        private static string removeFormatting(string text) => formatting_regex.Replace(text, string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
