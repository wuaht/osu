// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Online.API;

namespace osu.Game.Online.BnTracker
{
    /// <summary>
    /// The connection to a BN Tracker server, which tracks the Beatmap Nominators a mapper has asked to nominate their beatmap sets.
    /// Signing in trades the osu! login of the game for a BN Tracker token while logged in, and otherwise uses a sign-in in the browser.
    /// </summary>
    public partial class BnTrackerClient : Component
    {
        public const string DEFAULT_SERVER = @"https://wort.ee";

        /// <summary>
        /// The name of the token of this client on the BN Tracker. Signing in again replaces the token with the same name.
        /// </summary>
        private const string client_name = @"osu! editor";

        private static readonly string[] scopes = { @"read", @"write" };

        /// <summary>
        /// The signed in account, or <c>null</c> if not signed in or not known yet.
        /// </summary>
        public IBindable<BnMe?> User => user;

        private readonly Bindable<BnMe?> user = new Bindable<BnMe?>();

        /// <summary>
        /// Whether a token is stored. The token might still turn out to be revoked, in which case it is cleared.
        /// </summary>
        public IBindable<bool> IsSignedIn => isSignedIn;

        private readonly BindableBool isSignedIn = new BindableBool();

        private Bindable<string> server = null!;
        private Bindable<string> token = null!;

        /// <summary>
        /// The values of the settings, which are read from the threads the requests run on.
        /// </summary>
        private volatile string serverUrl = string.Empty;

        private volatile string currentToken = string.Empty;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            server = config.GetBindable<string>(OsuSetting.SlopBnTrackerServer);
            token = config.GetBindable<string>(OsuSetting.SlopBnTrackerToken);

            serverUrl = NormaliseServerUrl(server.Value) ?? string.Empty;
            currentToken = token.Value;
            isSignedIn.Value = !string.IsNullOrEmpty(token.Value);

            server.BindValueChanged(s =>
            {
                serverUrl = NormaliseServerUrl(s.NewValue) ?? string.Empty;

                // the token belongs to the previous server.
                if (NormaliseServerUrl(s.OldValue) != NormaliseServerUrl(s.NewValue))
                    token.Value = string.Empty;
            });

            token.BindValueChanged(t =>
            {
                currentToken = t.NewValue;
                isSignedIn.Value = !string.IsNullOrEmpty(t.NewValue);
                user.Value = null;
            });
        }

        /// <summary>
        /// The base URL of the configured server, or <c>null</c> if none is configured.
        /// </summary>
        public string? ServerUrl => string.IsNullOrEmpty(serverUrl) ? null : serverUrl;

        /// <summary>
        /// Turns a server address as entered by the user (e.g. "wort.ee/") into a base URL without a trailing slash (e.g. "https://wort.ee").
        /// </summary>
        /// <returns>The base URL, or <c>null</c> if the address is empty or invalid.</returns>
        public static string? NormaliseServerUrl(string? address)
        {
            string trimmed = address?.Trim().TrimEnd('/') ?? string.Empty;

            if (trimmed.Length == 0)
                return null;

            if (!trimmed.StartsWith(@"https://", StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase))
                trimmed = @"https://" + trimmed;

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
                return null;

            return trimmed;
        }

        #region Signing in

        /// <summary>
        /// Whether the game is logged in to the official osu! servers, such that its login can be traded for a BN Tracker token.
        /// </summary>
        public bool CanSignInWithOsu => api.State.Value == APIState.Online && api.Endpoints is not DevelopmentEndpointConfiguration;

        /// <summary>
        /// Signs in with the osu! login of the game, replacing the current sign-in. See <see cref="CanSignInWithOsu"/>.
        /// </summary>
        /// <returns>The signed in account.</returns>
        public async Task<BnMe> SignInWithOsuAsync(CancellationToken cancellationToken = default)
        {
            string previousToken = currentToken;

            // requesting the access token may refresh it, which blocks.
            string? accessToken = await Task.Run(() => api.AccessToken, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrEmpty(accessToken))
                throw new BnTrackerException(HttpStatusCode.Unauthorized, @"The game isn't logged in to osu!.");

            var response = await sendAsync<BnOsuSignInResponse>(HttpMethod.Post, @"auth/osu", new
            {
                accessToken,
                clientName = client_name,
                scopes,
            }, false, cancellationToken).ConfigureAwait(false);

            var me = new BnMe { OsuId = response.OsuId, Username = response.Username };
            setSignedIn(response.Token, me);

            // signing in replaces the previous token of the same account, but not one of another account.
            if (!string.IsNullOrEmpty(previousToken) && previousToken != response.Token)
                await revokeAsync(previousToken).ConfigureAwait(false);

            return me;
        }

        /// <summary>
        /// Starts a sign-in in the browser, for when the game isn't logged in to osu!.
        /// The user has to open <see cref="BnDeviceSignIn.VerificationUrlComplete"/> and allow the sign-in, while <see cref="PollDeviceSignInAsync"/> is called regularly.
        /// </summary>
        public Task<BnDeviceSignIn> StartDeviceSignInAsync(CancellationToken cancellationToken = default)
            => sendAsync<BnDeviceSignIn>(HttpMethod.Post, @"auth/device", new { clientName = client_name, scopes }, false, cancellationToken);

        /// <summary>
        /// Checks whether a sign-in in the browser was allowed, in which case the client is signed in.
        /// </summary>
        public async Task<BnDeviceSignInPoll> PollDeviceSignInAsync(string deviceCode, CancellationToken cancellationToken = default)
        {
            var poll = await sendAsync<BnDeviceSignInPoll>(HttpMethod.Post, @"auth/device/token", new { deviceCode }, false, cancellationToken).ConfigureAwait(false);

            if (poll.Status == BnDeviceSignInPoll.STATUS_APPROVED && !string.IsNullOrEmpty(poll.Token))
                setSignedIn(poll.Token, null);

            return poll;
        }

        /// <summary>
        /// Revokes the token on the server and forgets it.
        /// </summary>
        public async Task SignOutAsync()
        {
            string tokenToRevoke = currentToken;

            if (string.IsNullOrEmpty(tokenToRevoke))
                return;

            currentToken = string.Empty;
            Schedule(() => token.Value = string.Empty);

            await revokeAsync(tokenToRevoke).ConfigureAwait(false);
        }

        private async Task revokeAsync(string tokenToRevoke)
        {
            try
            {
                await sendAsync(HttpMethod.Delete, @"auth/token", null, tokenToRevoke, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // the token is forgotten either way.
                Logger.Log($@"Revoking a BN Tracker token failed: {e.Message}");
            }
        }

        private void setSignedIn(string newToken, BnMe? me)
        {
            // set immediately for requests following on other threads, the setting is updated on the update thread.
            currentToken = newToken;

            Schedule(() =>
            {
                token.Value = newToken;
                user.Value = me;
            });
        }

        #endregion

        #region Requests

        /// <summary>
        /// Looks up the signed in account.
        /// </summary>
        public async Task<BnMe> GetMeAsync(CancellationToken cancellationToken = default)
        {
            string requestToken = currentToken;
            var me = await sendAsync<BnMe>(HttpMethod.Get, @"me", null, true, cancellationToken).ConfigureAwait(false);

            Schedule(() =>
            {
                // the account may have changed in the meantime.
                if (token.Value == requestToken)
                    user.Value = me;
            });

            return me;
        }

        /// <summary>
        /// All current BN and NAT members, without the ones who left.
        /// </summary>
        public Task<List<BnNominator>> GetNominatorsAsync(CancellationToken cancellationToken = default)
            => sendAsync<List<BnNominator>>(HttpMethod.Get, @"nominators", null, true, cancellationToken);

        /// <summary>
        /// Looks up a tracked beatmap set by its online ID.
        /// </summary>
        /// <returns>The beatmap set with all relevant nominators, or <c>null</c> if it isn't tracked.</returns>
        public async Task<BnBeatmapSetWithNominators?> GetBeatmapSetAsync(long onlineId, CancellationToken cancellationToken = default)
        {
            try
            {
                return await sendAsync<BnBeatmapSetWithNominators>(HttpMethod.Get, $@"beatmapsets/osu/{onlineId}?nominators=true", null, true, cancellationToken).ConfigureAwait(false);
            }
            catch (BnTrackerException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        /// <summary>
        /// Starts tracking a beatmap set by its online ID, if it isn't tracked already.
        /// </summary>
        public Task<BnBeatmapSetWithNominators> TrackBeatmapSetAsync(long onlineId, CancellationToken cancellationToken = default)
            => sendAsync<BnBeatmapSetWithNominators>(HttpMethod.Put, $@"beatmapsets/osu/{onlineId}", null, true, cancellationToken);

        /// <summary>
        /// Stops tracking a beatmap set, which deletes all statuses, comments and activity of it.
        /// </summary>
        public Task StopTrackingBeatmapSetAsync(Guid id, CancellationToken cancellationToken = default)
            => sendAsync(HttpMethod.Delete, $@"beatmapsets/{id}", null, true, cancellationToken);

        public Task<BnBeatmapSet> SetPriorityAsync(Guid id, BnBeatmapPriority priority, CancellationToken cancellationToken = default)
            => sendAsync<BnBeatmapSet>(HttpMethod.Put, $@"beatmapsets/{id}/priority", new { priority }, true, cancellationToken);

        public Task<BnSetNominatorChange> SetStatusAsync(Guid id, int nominatorId, BnNominationStatus status, CancellationToken cancellationToken = default)
            => sendAsync<BnSetNominatorChange>(HttpMethod.Put, $@"beatmapsets/{id}/nominators/{nominatorId}/status", new { status }, true, cancellationToken);

        public Task<BnBeatmapSetWithNominators> SetStatusesAsync(Guid id, IEnumerable<int> nominatorIds, BnNominationStatus status, CancellationToken cancellationToken = default)
            => sendAsync<BnBeatmapSetWithNominators>(HttpMethod.Put, $@"beatmapsets/{id}/nominators/status", new { nominatorOsuIds = nominatorIds, status }, true, cancellationToken);

        public Task<BnSetNominatorChange> AddCommentAsync(Guid id, int nominatorId, string text, CancellationToken cancellationToken = default)
            => sendAsync<BnSetNominatorChange>(HttpMethod.Post, $@"beatmapsets/{id}/nominators/{nominatorId}/comments", new { text }, true, cancellationToken);

        public Task<BnSetNominatorChange> DeleteCommentAsync(Guid id, int nominatorId, Guid commentId, CancellationToken cancellationToken = default)
            => sendAsync<BnSetNominatorChange>(HttpMethod.Delete, $@"beatmapsets/{id}/nominators/{nominatorId}/comments/{commentId}", null, true, cancellationToken);

        private async Task<T> sendAsync<T>(HttpMethod method, string path, object? body, bool authenticated, CancellationToken cancellationToken)
            where T : class
        {
            string response = await sendAsync(method, path, body, authenticated, cancellationToken).ConfigureAwait(false);

            T? result;

            try
            {
                result = JsonConvert.DeserializeObject<T>(response);
            }
            catch (JsonException e)
            {
                throw new BnTrackerException(null, $@"The BN Tracker sent an unexpected response ({e.Message}).");
            }

            return result ?? throw new BnTrackerException(null, @"The BN Tracker sent an empty response.");
        }

        private Task<string> sendAsync(HttpMethod method, string path, object? body, bool authenticated, CancellationToken cancellationToken)
        {
            if (!authenticated)
                return sendAsync(method, path, body, null, cancellationToken);

            string requestToken = currentToken;

            if (string.IsNullOrEmpty(requestToken))
                throw new BnTrackerException(HttpStatusCode.Unauthorized, @"Not signed in to the BN Tracker.");

            return sendAsync(method, path, body, requestToken, cancellationToken);
        }

        /// <param name="method">The HTTP method.</param>
        /// <param name="path">The path relative to the API.</param>
        /// <param name="body">The body, serialised to JSON.</param>
        /// <param name="requestToken">The token to authenticate with, or <c>null</c> for an unauthenticated request.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        private async Task<string> sendAsync(HttpMethod method, string path, object? body, string? requestToken, CancellationToken cancellationToken)
        {
            string baseUrl = serverUrl;
            bool authenticated = requestToken != null;

            if (string.IsNullOrEmpty(baseUrl))
                throw new BnTrackerException(null, @"No BN Tracker server is set.");

            using var request = new BnTrackerWebRequest($@"{baseUrl}/api/v1/{path}")
            {
                Method = method,
                // only explicitly entered http:// addresses (e.g. a local server) are requested insecurely.
                AllowInsecureRequests = baseUrl.StartsWith(@"http://", StringComparison.OrdinalIgnoreCase),
                // retrying could e.g. add a comment twice.
                AllowRetryOnTimeout = method == HttpMethod.Get,
            };

            if (authenticated)
                request.AddHeader(@"Authorization", $@"Bearer {requestToken}");

            if (body != null)
            {
                request.ContentType = @"application/json";
                request.AddRaw(JsonConvert.SerializeObject(body));
            }

            try
            {
                await request.PerformAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                var exception = createException(request, e);

                // the token was revoked or expired, so the user has to sign in again.
                if (authenticated && exception.StatusCode == HttpStatusCode.Unauthorized)
                {
                    Schedule(() =>
                    {
                        if (token.Value == requestToken)
                            token.Value = string.Empty;
                    });
                }

                throw exception;
            }

            return request.GetResponseString() ?? string.Empty;
        }

        private static BnTrackerException createException(BnTrackerWebRequest request, Exception exception)
        {
            var statusCode = request.ResponseStatusCode;

            if (statusCode == null)
                return new BnTrackerException(null, $@"The BN Tracker couldn't be reached ({exception.Message}).");

            string? message = null;

            try
            {
                string? response = request.GetResponseString();

                if (!string.IsNullOrEmpty(response))
                    message = JObject.Parse(response)[@"error"]?.ToString();
            }
            catch
            {
                // not every error response has a message (e.g. errors of a proxy in front of the server).
            }

            if (string.IsNullOrEmpty(message))
            {
                message = statusCode switch
                {
                    HttpStatusCode.TooManyRequests => @"Too many requests were sent to the BN Tracker. Try again in a moment.",
                    HttpStatusCode.Unauthorized => @"The BN Tracker sign-in expired. Sign in again.",
                    HttpStatusCode.Forbidden => @"The BN Tracker denied access.",
                    _ => $@"The BN Tracker responded with an error ({(int)statusCode.Value} {statusCode.Value}).",
                };
            }

            return new BnTrackerException(statusCode, message);
        }

        private class BnTrackerWebRequest : OsuWebRequest
        {
            public BnTrackerWebRequest(string uri)
                : base(uri)
            {
            }

            protected override string Accept => @"application/json";
        }

        #endregion
    }

    /// <summary>
    /// A failed request to the BN Tracker.
    /// </summary>
    public class BnTrackerException : Exception
    {
        /// <summary>
        /// The status code of the response, or <c>null</c> if there was no response.
        /// </summary>
        public readonly HttpStatusCode? StatusCode;

        public BnTrackerException(HttpStatusCode? statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
