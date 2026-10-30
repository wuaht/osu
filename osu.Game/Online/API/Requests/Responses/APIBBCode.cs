// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;

namespace osu.Game.Online.API.Requests.Responses
{
    /// <summary>
    /// Text written in BBCode, such as a user page or a forum post.
    /// </summary>
    public class APIBBCode
    {
        /// <summary>
        /// The text as rendered by the website.
        /// </summary>
        [JsonProperty(@"html")]
        public string Html { get; set; } = string.Empty;

        /// <summary>
        /// The text as it was written.
        /// </summary>
        [JsonProperty(@"raw")]
        public string Raw { get; set; } = string.Empty;
    }
}
