// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Online.API;

namespace osu.Game.Online.Omdb
{
    /// <summary>
    /// The connection to OMDB (osu! map database), a website where players rate beatmaps.
    /// Requests are made with the API key of the user, which is shown on the OMDB settings page.
    /// </summary>
    public partial class OmdbClient : Component
    {
        public const string WEBSITE_URL = @"https://omdb.nyahh.net";

        public const string SETTINGS_URL = WEBSITE_URL + @"/settings";

        private const string api_url = WEBSITE_URL + @"/api";

        /// <summary>
        /// The score which removes the rating of the user.
        /// </summary>
        private const double remove_score = -2;

        public static string GetListingUrl(int beatmapSetId) => $@"{WEBSITE_URL}/mapset/{beatmapSetId}";

        /// <summary>
        /// The API key as entered by the user.
        /// </summary>
        public IBindable<string> ApiKey => apiKey;

        private Bindable<string> apiKey = null!;

        /// <summary>
        /// The trimmed API key, which is read from the threads the requests run on.
        /// </summary>
        private volatile string currentApiKey = string.Empty;

        /// <summary>
        /// Lookups of beatmaps by their online ID, including ones which are still running, such that each beatmap is only requested once.
        /// </summary>
        private readonly Dictionary<int, Task<OmdbBeatmap>> beatmaps = new Dictionary<int, Task<OmdbBeatmap>>();

        public bool HasApiKey => !string.IsNullOrEmpty(currentApiKey);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            apiKey = config.GetBindable<string>(OsuSetting.SlopOmdbApiKey);
            apiKey.BindValueChanged(k =>
            {
                string key = k.NewValue.Trim();

                // the key is part of the request URLs, which are logged.
                if (key.Length > 0)
                {
                    Logger.AddFilteredText(key);
                    Logger.AddFilteredText(Uri.EscapeDataString(key));
                }

                currentApiKey = key;

                // own ratings belong to the user of the previous key.
                lock (beatmaps)
                    beatmaps.Clear();
            }, true);
        }

        /// <summary>
        /// Looks up a beatmap by its online ID. Results are cached until the beatmap is rated through <see cref="RateAsync"/>.
        /// </summary>
        public Task<OmdbBeatmap> GetBeatmapAsync(int beatmapId)
        {
            lock (beatmaps)
            {
                // failed lookups are tried again.
                if (beatmaps.TryGetValue(beatmapId, out var existing) && !existing.IsFaulted && !existing.IsCanceled)
                    return existing;

                var task = fetchBeatmapAsync(beatmapId);

                // failures are expected (e.g. unknown beatmaps) and the cached task can outlive every caller, so observe it here once instead of relying on each caller reading the exception.
                task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);

                return beatmaps[beatmapId] = task;
            }
        }

        /// <summary>
        /// Rates a beatmap.
        /// </summary>
        /// <param name="beatmapId">The online ID of the beatmap.</param>
        /// <param name="score">The score from 0 to 5 in steps of 0.5, or <c>null</c> to remove the rating.</param>
        /// <returns>The beatmap with the new rating.</returns>
        public async Task<OmdbBeatmap> RateAsync(int beatmapId, double? score)
        {
            string scoreParameter = (score ?? remove_score).ToString(CultureInfo.InvariantCulture);

            await sendAsync(@"rate.php", $@"&id={beatmapId}&score={scoreParameter}", false).ConfigureAwait(false);

            lock (beatmaps)
                beatmaps.Remove(beatmapId);

            return await GetBeatmapAsync(beatmapId).ConfigureAwait(false);
        }

        private async Task<OmdbBeatmap> fetchBeatmapAsync(int beatmapId)
        {
            string response = await sendAsync(@"beatmap.php", $@"&id={beatmapId}", true).ConfigureAwait(false);

            OmdbBeatmap? beatmap;

            try
            {
                beatmap = JsonConvert.DeserializeObject<OmdbBeatmap>(response, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            }
            catch (JsonException e)
            {
                throw new OmdbException($@"OMDB sent an invalid response ({e.Message}).");
            }

            return beatmap ?? throw new OmdbException(@"OMDB sent an empty response.");
        }

        /// <returns>The response, which isn't an error.</returns>
        private async Task<string> sendAsync(string endpoint, string query, bool retryOnTimeout)
        {
            string key = currentApiKey;

            if (string.IsNullOrEmpty(key))
                throw new OmdbException(@"No OMDB API key is set. You can create one on the OMDB settings page.");

            using var request = new OmdbWebRequest($@"{api_url}/{endpoint}?key={Uri.EscapeDataString(key)}{query}")
            {
                // retrying a rating could rate twice.
                AllowRetryOnTimeout = retryOnTimeout,
            };

            try
            {
                await request.PerformAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                string? message = errorOf(request.GetResponseString());

                throw new OmdbException(message ?? (request.ResponseStatusCode == null
                    ? $@"OMDB couldn't be reached ({e.Message})."
                    : $@"OMDB responded with an error ({(int)request.ResponseStatusCode.Value} {request.ResponseStatusCode.Value})."));
            }

            string response = request.GetResponseString() ?? string.Empty;

            if (errorOf(response) is string error)
                throw new OmdbException(error);

            return response;
        }

        /// <summary>
        /// OMDB reports errors as an object with an "error" message, also in successful responses.
        /// </summary>
        private static string? errorOf(string? response)
        {
            if (string.IsNullOrEmpty(response))
                return null;

            try
            {
                if (JToken.Parse(response) is JObject obj && obj.TryGetValue(@"error", out var error))
                    return error.Type == JTokenType.String ? error.ToString() : @"Unknown error";
            }
            catch (JsonException)
            {
                // not every response is JSON (e.g. errors of a proxy in front of the server).
            }

            return null;
        }

        private class OmdbWebRequest : OsuWebRequest
        {
            public OmdbWebRequest(string uri)
                : base(uri)
            {
            }

            protected override string Accept => @"application/json";
        }
    }

    /// <summary>
    /// A failed request to OMDB.
    /// </summary>
    public class OmdbException : Exception
    {
        public OmdbException(string message)
            : base(message)
        {
        }
    }
}
