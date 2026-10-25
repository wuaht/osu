// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Localisation;
using osu.Game.Rulesets.Edit;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// Colours, icons, texts and links of the discussion page, as on the website.
    /// </summary>
    public static class DiscussionDisplay
    {
        #region Colours

        // the "basic" colour theme of the website (colors.less).
        private static Color4 basic(int hue, float saturation, float lightness) => Colour4.FromHSL(hue / 360f, saturation, lightness);

        public static readonly Color4 LIME_1 = basic(90, 1, 0.7f);
        public static readonly Color4 LIME_3 = basic(90, 0.6f, 0.5f);
        public static readonly Color4 BLUE_1 = basic(200, 1, 0.7f);
        public static readonly Color4 BLUE_3 = basic(200, 0.6f, 0.5f);
        public static readonly Color4 RED_1 = basic(0, 1, 0.7f);
        public static readonly Color4 RED_3 = basic(0, 0.6f, 0.5f);
        public static readonly Color4 ORANGE_1 = basic(45, 1, 0.7f);
        public static readonly Color4 ORANGE_2 = basic(45, 0.8f, 0.6f);
        public static readonly Color4 ORANGE_3 = basic(45, 0.6f, 0.5f);
        public static readonly Color4 PURPLE_1 = basic(255, 1, 0.7f);
        public static readonly Color4 PINK_1 = basic(333, 1, 0.7f);

        // the legacy palette of the website.
        public static readonly Color4 PINK_DARKER = Color4Extensions.FromHex(@"bb1177");
        public static readonly Color4 BLUE_DARKER = Color4Extensions.FromHex(@"2299bb");
        public static readonly Color4 YELLOW_DARKER = Color4Extensions.FromHex(@"cc6600");
        public static readonly Color4 GREEN = Color4Extensions.FromHex(@"88b300");

        public static Color4 TypeColour(string messageType, OverlayColourProvider colourProvider)
        {
            switch (messageType)
            {
                case @"hype":
                case @"praise":
                    return BLUE_3;

                case @"mapper_note":
                    return PURPLE_1;

                case @"problem":
                    return RED_3;

                case @"review":
                    return colourProvider.Content2;

                case @"suggestion":
                    return ORANGE_3;

                default:
                    return colourProvider.Content1;
            }
        }

        public static IconUsage TypeIcon(string messageType)
        {
            switch (messageType)
            {
                case @"hype":
                    return FontAwesome.Solid.Bullhorn;

                case @"mapper_note":
                    return FontAwesome.Regular.StickyNote;

                case @"praise":
                    return FontAwesome.Solid.Heart;

                case @"problem":
                    return FontAwesome.Solid.ExclamationCircle;

                case @"review":
                    return FontAwesome.Solid.Tasks;

                default:
                    return FontAwesome.Regular.Circle;
            }
        }

        public static IconUsage ResolvedIcon => FontAwesome.Regular.CheckCircle;

        public static LocalisableString TypeName(string messageType)
        {
            switch (messageType)
            {
                case @"hype":
                    return SlopDiscussionStrings.TypeHype;

                case @"mapper_note":
                    return SlopDiscussionStrings.TypeMapperNote;

                case @"praise":
                    return SlopDiscussionStrings.TypePraise;

                case @"problem":
                    return SlopDiscussionStrings.TypeProblem;

                case @"review":
                    return SlopDiscussionStrings.TypeReview;

                default:
                    return SlopDiscussionStrings.TypeSuggestion;
            }
        }

        public static Color4 FilterColour(DiscussionFilter filter, OverlayColourProvider colourProvider)
        {
            switch (filter)
            {
                case DiscussionFilter.Pending:
                    return PINK_DARKER;

                case DiscussionFilter.Praises:
                    return BLUE_DARKER;

                case DiscussionFilter.MapperNotes:
                    return PURPLE_1;

                case DiscussionFilter.Mine:
                    return YELLOW_DARKER;

                case DiscussionFilter.Resolved:
                    return GREEN;

                default:
                    return Color4.White;
            }
        }

        public static LocalisableString FilterName(DiscussionFilter filter)
        {
            switch (filter)
            {
                case DiscussionFilter.Mine:
                    return SlopDiscussionStrings.StatsMine;

                case DiscussionFilter.MapperNotes:
                    return SlopDiscussionStrings.StatsMapperNotes;

                case DiscussionFilter.Resolved:
                    return SlopDiscussionStrings.StatsResolved;

                case DiscussionFilter.Pending:
                    return SlopDiscussionStrings.StatsPending;

                case DiscussionFilter.Praises:
                    return SlopDiscussionStrings.StatsPraises;

                default:
                    return SlopDiscussionStrings.StatsTotal;
            }
        }

        public static IconUsage RulesetIcon(string ruleset)
        {
            switch (ruleset)
            {
                case @"taiko":
                    return OsuIcon.RulesetTaiko;

                case @"fruits":
                    return OsuIcon.RulesetCatch;

                case @"mania":
                    return OsuIcon.RulesetMania;

                default:
                    return OsuIcon.RulesetOsu;
            }
        }

        public static string RulesetName(string ruleset)
        {
            switch (ruleset)
            {
                case @"taiko":
                    return @"osu!taiko";

                case @"fruits":
                    return @"osu!catch";

                case @"mania":
                    return @"osu!mania";

                default:
                    return @"osu!";
            }
        }

        #endregion

        #region Groups

        /// <summary>
        /// The group shown for the owner of the beatmap set.
        /// </summary>
        public static readonly APIUserGroup MAPPER_GROUP = new APIUserGroup
        {
            Identifier = @"mapper",
            Name = @"Beatmap Mapper",
            ShortName = @"MAPPER",
            Colour = null,
        };

        /// <summary>
        /// The group shown for guest owners of the discussed difficulty.
        /// </summary>
        public static readonly APIUserGroup GUEST_GROUP = new APIUserGroup
        {
            Identifier = @"guest",
            Name = @"Difficulty Guest",
            ShortName = @"GUEST",
            Colour = null,
        };

        /// <summary>
        /// The colour of a group, which is light 1 for the mapper and guest groups.
        /// </summary>
        public static Color4 GroupColour(APIUserGroup? group, OverlayColourProvider colourProvider, Color4 fallback)
        {
            if (group == null)
                return fallback;

            if (group == MAPPER_GROUP || group == GUEST_GROUP || string.IsNullOrEmpty(group.Colour))
                return colourProvider.Light1;

            return Color4Extensions.FromHex(group.Colour);
        }

        /// <summary>
        /// The group shown next to a user in a discussion, see <c>badgeGroup()</c> on the website.
        /// </summary>
        public static APIUserGroup? BadgeGroup(DiscussionsState state, DiscussionUser? user, BeatmapDiscussion? discussion)
        {
            if (user == null)
                return null;

            if (user.Id == state.BeatmapSet.UserId)
                return MAPPER_GROUP;

            if (discussion?.BeatmapId != null && discussion.BeatmapId == state.CurrentBeatmapId
                && state.CurrentBeatmap is DiscussionBeatmap beatmap && state.IsBeatmapOwner(user.Id, beatmap))
                return GUEST_GROUP;

            return user.Groups?.FirstOrDefault();
        }

        #endregion

        #region Texts

        /// <summary>
        /// Formats a timestamp like on the website, e.g. "01:02:345".
        /// </summary>
        public static string FormatTimestamp(int value)
        {
            int ms = value % 1000;
            int s = value / 1000 % 60;
            int m = value / 1000 / 60;

            return $@"{m:00}:{s:00}:{ms:000}";
        }

        public static string FormatNumber(double value, int maxDecimals = 2) => value.ToString($@"#,0.{new string('#', maxDecimals)}", CultureInfo.CurrentCulture);

        public static string FormatDuration(int seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            return time.TotalHours >= 1 ? $@"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $@"{time.Minutes}:{time.Seconds:00}";
        }

        private static readonly Regex placeholder_regex = new Regex(@"\{(\w+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Adds a text which contains named placeholders such as <c>{user}</c> to a flow, adding the parts given for the placeholders in between.
        /// Placeholders without a part are added as text.
        /// </summary>
        public static void AddPattern(LinkFlowContainer flow, TranslatableString pattern, IReadOnlyDictionary<string, Action<LinkFlowContainer>> parts, Action<osu.Framework.Graphics.Sprites.SpriteText>? creationParameters = null)
        {
            string format = pattern.Format;

            int index = 0;

            foreach (Match match in placeholder_regex.Matches(format))
            {
                if (match.Index > index)
                    flow.AddText(format[index..match.Index], creationParameters);

                if (parts.TryGetValue(match.Groups[1].Value, out var addPart))
                    addPart(flow);
                else
                    flow.AddText(match.Value, creationParameters);

                index = match.Index + match.Length;
            }

            if (index < format.Length)
                flow.AddText(format[index..], creationParameters);
        }

        /// <summary>
        /// The text of a markdown message without formatting, see <c>PlainTextPreview</c> on the website.
        /// </summary>
        public static string PlainTextPreview(string markdown, int? maxLength = null)
        {
            string text = Regex.Replace(markdown, @"!\[([^\]]*)\]\([^)]*\)", "$1");
            text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
            text = Regex.Replace(text, @"(\*\*|__|\*|_|~~|`)", string.Empty);
            text = Regex.Replace(text, @"^\s*(#+|>|[-*+]|\d+\.)\s+", string.Empty, RegexOptions.Multiline);
            text = Regex.Replace(text, @"\s+", " ").Trim();

            if (maxLength != null && text.Length > maxLength)
                text = text[..maxLength.Value] + @"...";

            return text;
        }

        #endregion

        #region Markdown

        private static readonly Regex code_regex = new Regex(@"(```[\s\S]*?(```|$)|`[^`\n]*`)", RegexOptions.Compiled);

        private static readonly Regex discussion_url_regex = new Regex(@"(?<![\(\[<])\bhttps?://osu\.ppy\.sh/beatmapsets/(?<set>\d+)/discussion(/[^\s#)\]]*)?#/(?<discussion>\d+)(/(?<post>\d+))?", RegexOptions.Compiled);

        /// <summary>
        /// Prepares a discussion message for <see cref="DiscussionMarkdown"/>:
        /// editor timestamps become links which open the editor (see <c>timestampDecorator()</c> on the website),
        /// links to discussions of the beatmap set are shortened (see <c>propsFromHref()</c> on the website),
        /// and single line breaks are kept (like <c>remark-breaks</c>).
        /// </summary>
        public static string PrepareMarkdown(string markdown, int beatmapSetId)
        {
            var result = new StringBuilder();
            int index = 0;

            foreach (Match code in code_regex.Matches(markdown))
            {
                result.Append(prepareText(markdown[index..code.Index], beatmapSetId));
                result.Append(code.Value);
                index = code.Index + code.Length;
            }

            result.Append(prepareText(markdown[index..], beatmapSetId));

            return result.ToString();
        }

        private static string prepareText(string text, int beatmapSetId)
        {
            text = EditorTimestampParser.TIME_REGEX_STRICT.Replace(text, m => $@"[{m.Value}]({EDITOR_LINK_PREFIX}{Uri.EscapeDataString(m.Value)} ""{SlopDiscussionStrings.ShowInEditor}"")");

            text = discussion_url_regex.Replace(text, m =>
            {
                string hash = m.Groups[@"post"].Success ? $@"{m.Groups[@"discussion"].Value}/{m.Groups[@"post"].Value}" : m.Groups[@"discussion"].Value;
                string label = m.Groups[@"set"].Value == beatmapSetId.ToString(CultureInfo.InvariantCulture) ? $@"#{hash}" : $@"{m.Groups[@"set"].Value}#{hash}";
                return $@"[{label}]({m.Value})";
            });

            // a line break which isn't a paragraph break is a hard break (two trailing spaces).
            return Regex.Replace(text, @"(?<!\n)\n(?!\n)", "  \n");
        }

        public const string EDITOR_LINK_PREFIX = @"osu://edit/";

        private static readonly Regex discussion_link_regex = new Regex(@"^https?://osu\.ppy\.sh/beatmapsets/(?<set>\d+)/discussion(/[^#]*)?#/(?<discussion>\d+)(/(?<post>\d+))?", RegexOptions.Compiled);

        /// <summary>
        /// Parses a link to a discussion, e.g. "https://osu.ppy.sh/beatmapsets/1/discussion/-/generalAll#/2".
        /// </summary>
        public static bool TryParseDiscussionLink(string url, out int beatmapSetId, out int discussionId)
        {
            var match = discussion_link_regex.Match(url);

            beatmapSetId = match.Success ? int.Parse(match.Groups[@"set"].Value, CultureInfo.InvariantCulture) : 0;
            discussionId = match.Success ? int.Parse(match.Groups[@"discussion"].Value, CultureInfo.InvariantCulture) : 0;

            return match.Success;
        }

        #endregion

        #region Links

        public static string PageName(DiscussionPage page)
        {
            switch (page)
            {
                case DiscussionPage.Reviews:
                    return @"reviews";

                case DiscussionPage.General:
                    return @"general";

                case DiscussionPage.Timeline:
                    return @"timeline";

                case DiscussionPage.Events:
                    return @"events";

                default:
                    return @"generalAll";
            }
        }

        /// <summary>
        /// The link to the discussion page on the website, see <c>makeUrl()</c> on the website.
        /// </summary>
        public static string DiscussionPageUrl(string websiteUrl, int beatmapSetId, int? beatmapId = null, DiscussionPage page = DiscussionPage.GeneralAll)
        {
            string beatmap = beatmapId == null || page is DiscussionPage.Events or DiscussionPage.GeneralAll or DiscussionPage.Reviews
                ? @"-"
                : beatmapId.Value.ToString(CultureInfo.InvariantCulture);

            return $@"{websiteUrl}/beatmapsets/{beatmapSetId}/discussion/{beatmap}/{PageName(page)}";
        }

        public static string DiscussionUrl(string websiteUrl, BeatmapDiscussion discussion, BeatmapDiscussionPost? post = null)
        {
            string url = $@"{DiscussionPageUrl(websiteUrl, discussion.BeatmapSetId, discussion.BeatmapId, DiscussionsState.PageOf(discussion))}#/{discussion.Id}";

            if (post != null)
                url += $@"/{post.Id}";

            return url;
        }

        public static string UserUrl(string websiteUrl, int userId) => $@"{websiteUrl}/users/{userId}";

        public static string ModdingHistoryUrl(string websiteUrl, int userId) => $@"{websiteUrl}/users/{userId}/modding";

        #endregion
    }
}
