// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Containers.BBCode;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osuTK.Graphics;

namespace osu.Game.Overlays.BeatmapSet
{
    /// <summary>
    /// The description of a beatmap set, which is the first post of its forum topic (after the separator the website adds).
    /// </summary>
    public partial class MetadataSectionDescription : Container
    {
        private const string separator = @"---------------";

        private static readonly Regex html_line_break_regex = new Regex(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex html_tag_regex = new Regex(@"<[^>]*>", RegexOptions.Compiled);

        public readonly Bindable<APIBeatmapSet?> BeatmapSet = new Bindable<APIBeatmapSet?>();

        private readonly OsuScrollContainer scroll;

        private GetForumTopicRequest? topicRequest;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        public MetadataSectionDescription()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Margin = new MarginPadding { Top = 15 },
                    Text = MetadataType.Description.GetLocalisableDescription(),
                    Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                },
                scroll = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 34, Bottom = 15 },
                    ScrollbarOverlapsContent = false,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            BeatmapSet.BindValueChanged(s => updateDescription(s.NewValue), true);
        }

        private void updateDescription(APIBeatmapSet? beatmapSet)
        {
            topicRequest?.Cancel();

            string html = beatmapSet?.Description?.Description ?? string.Empty;

            if (beatmapSet == null)
            {
                show(beatmapSet, string.Empty, html);
                return;
            }

            // the written description is only included for users who can edit it, otherwise it's taken from the forum topic.
            if (beatmapSet.Description?.BBCode != null)
            {
                show(beatmapSet, beatmapSet.Description.BBCode, html);
                return;
            }

            if (!long.TryParse(beatmapSet.LegacyThreadUrl?.Split('/').LastOrDefault(), out long topicId))
            {
                show(beatmapSet, stripHtml(html), null);
                return;
            }

            topicRequest = new GetForumTopicRequest(topicId);
            topicRequest.Success += topic =>
            {
                string post = topic.Posts.FirstOrDefault()?.Body.Raw ?? string.Empty;
                string[] parts = post.Split(separator, 2);
                show(beatmapSet, parts.Length > 1 ? parts[1].Trim() : string.Empty, html);
            };
            topicRequest.Failure += _ => show(beatmapSet, stripHtml(html), null);
            api.Queue(topicRequest);
        }

        private void show(APIBeatmapSet? beatmapSet, string bbcode, string? html) => Schedule(() =>
        {
            if (beatmapSet != BeatmapSet.Value)
                return;

            if (string.IsNullOrWhiteSpace(bbcode))
            {
                this.FadeOut(250);
                scroll.Clear();
                return;
            }

            LoadComponentAsync(new DrawableBBCode(bbcode, html)
            {
                Colour = Color4.White.Opacity(0.75f),
            }, loaded =>
            {
                if (beatmapSet != BeatmapSet.Value)
                    return;

                scroll.Clear();
                scroll.Add(loaded);
                this.FadeIn(250);
            });
        });

        private static string stripHtml(string html) => WebUtility.HtmlDecode(html_tag_regex.Replace(html_line_break_regex.Replace(html, "\n"), string.Empty)).Trim();

        protected override void Dispose(bool isDisposing)
        {
            topicRequest?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
