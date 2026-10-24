// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Game.Online.Discord
{
    /// <summary>
    /// Creates the embeds of links in messages on the client, for clients which don't receive them from Discord (like the Social SDK).
    /// </summary>
    public static class DiscordLinkPreviews
    {
        /// <summary>
        /// Like Discord, only the first few links of a message are embedded.
        /// </summary>
        private const int max_links = 5;

        private const int max_page_size = 1024 * 1024;

        /// <summary>
        /// The amount of an image which is read to find out its size.
        /// </summary>
        private const int max_image_header_size = 256 * 1024;

        private const int max_description_length = 350;

        /// <summary>
        /// Many sites only provide link previews to known crawlers, so the user agent of the Discord crawler is used.
        /// </summary>
        private const string user_agent = @"Mozilla/5.0 (compatible; Discordbot/2.0; +https://discordapp.com)";

        // Links in angle brackets are not embedded by Discord either.
        private static readonly Regex link_regex = new Regex(@"(?<!<)\bhttps?://[^\s<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex meta_regex = new Regex(@"<meta\s[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex attribute_regex = new Regex(@"([\w:-]+)\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.Compiled);
        private static readonly Regex title_regex = new Regex(@"<title[^>]*>([^<]*)</title>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HttpClient http_client = createHttpClient();

        private static readonly ConcurrentDictionary<string, Task<DiscordEmbed?>> cache = new ConcurrentDictionary<string, Task<DiscordEmbed?>>();

        private static HttpClient createHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(user_agent);
            return client;
        }

        /// <summary>
        /// Finds the links of a message which should be embedded.
        /// </summary>
        public static IReadOnlyList<string> GetLinks(string content)
        {
            return link_regex.Matches(content)
                             .Select(m => m.Value.TrimEnd('.', ',', '!', '?', ':', ';', '\'', '"', ')', ']', '*', '_', '~', '|'))
                             .Where(l => Uri.IsWellFormedUriString(l, UriKind.Absolute))
                             .Distinct()
                             .Take(max_links)
                             .ToList();
        }

        /// <summary>
        /// Creates the embed of a link. Returns null if the link can't be embedded. Results are cached.
        /// </summary>
        public static Task<DiscordEmbed?> Resolve(string url) => cache.GetOrAdd(url, u => Task.Run(() => resolve(u)));

        private static async Task<DiscordEmbed?> resolve(string url)
        {
            try
            {
                using var response = await http_client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    return null;

                string? type = response.Content.Headers.ContentType?.MediaType;

                if (type == null)
                    return null;

                if (type.StartsWith(@"image/", StringComparison.OrdinalIgnoreCase))
                {
                    var (width, height) = identifyImage(await readStart(response, max_image_header_size).ConfigureAwait(false));

                    return new DiscordEmbed
                    {
                        Type = @"image",
                        Url = url,
                        Thumbnail = new DiscordEmbedMedia { Url = url, Width = width, Height = height },
                    };
                }

                if (type.StartsWith(@"video/", StringComparison.OrdinalIgnoreCase))
                {
                    return new DiscordEmbed
                    {
                        Type = @"video",
                        Url = url,
                        Video = new DiscordEmbedMedia { Url = url },
                    };
                }

                if (type.Equals(@"text/html", StringComparison.OrdinalIgnoreCase) || type.Equals(@"application/xhtml+xml", StringComparison.OrdinalIgnoreCase))
                {
                    string html = Encoding.UTF8.GetString(await readStart(response, max_page_size).ConfigureAwait(false));
                    return createPageEmbed(response.RequestMessage?.RequestUri ?? new Uri(url), url, html);
                }

                return null;
            }
            catch (Exception e)
            {
                Logger.Log($@"Creating the Discord embed of {url} failed: {e.Message}", LoggingTarget.Network, LogLevel.Debug);
                return null;
            }
        }

        private static async Task<byte[]> readStart(HttpResponseMessage response, int maxLength)
        {
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var buffer = new MemoryStream();

            byte[] chunk = new byte[16 * 1024];
            int read;

            while (buffer.Length < maxLength && (read = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, maxLength - buffer.Length))).ConfigureAwait(false)) > 0)
                buffer.Write(chunk, 0, read);

            return buffer.ToArray();
        }

        private static (int? width, int? height) identifyImage(byte[] data)
        {
            try
            {
                var info = SixLabors.ImageSharp.Image.Identify(data);
                return (info.Width, info.Height);
            }
            catch
            {
                // The size is not required, the image is shown in a default size instead.
                return (null, null);
            }
        }

        /// <summary>
        /// Creates an embed from the OpenGraph and Twitter card tags of a page.
        /// </summary>
        private static DiscordEmbed? createPageEmbed(Uri pageUri, string url, string html)
        {
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match meta in meta_regex.Matches(html))
            {
                string? key = null;
                string? value = null;

                foreach (Match attribute in attribute_regex.Matches(meta.Value))
                {
                    string name = attribute.Groups[1].Value;
                    string attributeValue = attribute.Groups[2].Success ? attribute.Groups[2].Value : attribute.Groups[3].Value;

                    if (name.Equals(@"property", StringComparison.OrdinalIgnoreCase) || name.Equals(@"name", StringComparison.OrdinalIgnoreCase))
                        key = attributeValue;
                    else if (name.Equals(@"content", StringComparison.OrdinalIgnoreCase))
                        value = WebUtility.HtmlDecode(attributeValue).Trim();
                }

                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    tags.TryAdd(key, value);
            }

            string? get(params string[] keys)
            {
                foreach (string key in keys)
                {
                    if (tags.TryGetValue(key, out string? value))
                        return value;
                }

                return null;
            }

            string? getUrl(params string[] keys)
            {
                string? value = get(keys);
                return value != null && Uri.TryCreate(pageUri, value, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
                    ? absolute.AbsoluteUri
                    : null;
            }

            int? getInt(params string[] keys) => int.TryParse(get(keys), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value > 0 ? value : null;

            string? video = getUrl(@"og:video:secure_url", @"og:video:url", @"og:video", @"twitter:player:stream");
            string? videoType = get(@"og:video:type", @"twitter:player:stream:content_type");

            // Players of sites like YouTube are pages themselves, which can't be played here.
            if (video != null && videoType?.StartsWith(@"video/", StringComparison.OrdinalIgnoreCase) != true && !hasVideoExtension(video))
                video = null;

            var image = getUrl(@"og:image:secure_url", @"og:image:url", @"og:image", @"twitter:image", @"twitter:image:src") is string imageUrl
                ? new DiscordEmbedMedia
                {
                    Url = imageUrl,
                    Width = getInt(@"og:image:width", @"twitter:image:width"),
                    Height = getInt(@"og:image:height", @"twitter:image:height"),
                }
                : null;

            // GIF sites are shown as looping videos without any text, like Discord does.
            if (video != null && isGifSite(pageUri))
            {
                return new DiscordEmbed
                {
                    Type = @"gifv",
                    Url = url,
                    Video = new DiscordEmbedMedia
                    {
                        Url = video,
                        Width = getInt(@"og:video:width", @"twitter:player:width") ?? image?.Width,
                        Height = getInt(@"og:video:height", @"twitter:player:height") ?? image?.Height,
                    },
                    Thumbnail = image,
                };
            }

            string? title = get(@"og:title", @"twitter:title");

            if (title == null)
            {
                var titleMatch = title_regex.Match(html);

                if (titleMatch.Success)
                    title = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
            }

            if (string.IsNullOrEmpty(title) && image == null)
                return null;

            string? description = get(@"og:description", @"twitter:description", @"description");

            if (description?.Length > max_description_length)
                description = description[..max_description_length].TrimEnd() + @"…";

            string? siteName = get(@"og:site_name", @"application-name");

            // Like on Discord, large images are only shown for sites which ask for them.
            string? card = get(@"twitter:card");
            bool largeImage = card == @"summary_large_image" || card == @"player";

            return new DiscordEmbed
            {
                Type = @"link",
                Url = url,
                Title = string.IsNullOrEmpty(title) ? null : title,
                Description = description,
                Colour = parseColour(get(@"theme-color")),
                Provider = siteName != null ? new DiscordEmbedAuthor { Name = siteName } : null,
                Image = largeImage ? image : null,
                Thumbnail = largeImage ? null : image,
            };
        }

        private static bool hasVideoExtension(string url)
        {
            string path = new Uri(url).AbsolutePath;

            return path.EndsWith(@".mp4", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(@".webm", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(@".mov", StringComparison.OrdinalIgnoreCase);
        }

        private static bool isGifSite(Uri uri)
        {
            string host = uri.Host;

            return host.Equals(@"tenor.com", StringComparison.OrdinalIgnoreCase) || host.EndsWith(@".tenor.com", StringComparison.OrdinalIgnoreCase)
                                                                                  || host.Equals(@"giphy.com", StringComparison.OrdinalIgnoreCase)
                                                                                  || host.EndsWith(@".giphy.com", StringComparison.OrdinalIgnoreCase);
        }

        private static int? parseColour(string? value)
        {
            if (value == null || value.Length != 7 || value[0] != '#')
                return null;

            return int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int colour) ? colour : null;
        }
    }
}
