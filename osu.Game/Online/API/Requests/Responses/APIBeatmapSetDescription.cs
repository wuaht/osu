// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    public class APIBeatmapSetDescription
    {
        /// <summary>
        /// The description as rendered by the website.
        /// </summary>
        [JsonProperty(@"description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The description as it was written, which is only included for users who can edit it.
        /// </summary>
        [JsonProperty(@"bbcode")]
        public string? BBCode { get; set; }
    }
}
