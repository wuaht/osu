// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Configuration;

namespace osu.Game.Online.API
{
    public enum WebSessionState
    {
        LoggedOut,
        LoggingIn,
        LoggedIn,
    }

    /// <summary>
    /// A session on the website, for everything which can't be done with the API, such as posting in beatmap discussions.
    /// Like a browser, it signs in with the username and password (when logging into the game) and keeps the session cookie.
    /// </summary>
    public partial class WebSession : Component
    {
        private const int request_timeout = 30000;

        private static readonly HttpClient client = new HttpClient(new HttpClientHandler
        {
            UseCookies = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        })
        {
            Timeout = TimeSpan.FromMilliseconds(request_timeout),
        };

        private static readonly Regex csrf_meta_regex = new Regex(@"<meta\s+name=""csrf-token""\s+content=""([^""]+)""", RegexOptions.Compiled);

        private readonly Bindable<WebSessionState> state = new Bindable<WebSessionState>();

        public IBindable<WebSessionState> State => state;

        /// <summary>
        /// Why signing in failed the last time.
        /// </summary>
        public string? LastError { get; private set; }

        /// <summary>
        /// The username of the signed in user.
        /// </summary>
        public string? Username { get; private set; }

        private readonly OsuConfigManager config;
        private readonly string websiteUrl;

        private readonly object sync = new object();
        private readonly Dictionary<string, string> cookies = new Dictionary<string, string>();
        private string? csrfToken;

        /// <summary>
        /// Whether the cookies are those of a signed in session, rather than those of a guest while signing in.
        /// </summary>
        private bool signedIn;

        /// <summary>
        /// The login started when logging into the game, which the verification waits for.
        /// </summary>
        private Task currentLogin = Task.CompletedTask;

        /// <summary>
        /// Until when the website wants a captcha for signing in, during which signing in when logging into the game is skipped,
        /// as every further attempt would only keep the captcha required for longer.
        /// </summary>
        private DateTimeOffset captchaRequiredUntil;

        private static readonly TimeSpan captcha_backoff = TimeSpan.FromMinutes(15);

        /// <summary>
        /// Incremented whenever the session is replaced, such that results of earlier logins are dropped.
        /// </summary>
        private int generation;

        public WebSession(OsuConfigManager config, string websiteUrl)
        {
            this.config = config;
            this.websiteUrl = websiteUrl.TrimEnd('/');

            restore();
        }

        public bool IsLoggedIn => state.Value == WebSessionState.LoggedIn;

        /// <summary>
        /// The browser in which the website can be signed into, for everything the website may ask for while signing in, such as a captcha.
        /// </summary>
        public IWebSessionBrowser? Browser { get; set; }

        /// <summary>
        /// Signs into the website in the <see cref="Browser"/>, replacing the current session.
        /// </summary>
        public async Task LoginWithBrowserAsync(CancellationToken cancellationToken = default)
        {
            if (Browser == null)
                throw new WebSessionException(HttpStatusCode.OK, @"Signing in on the website isn't available here.");

            var session = await Browser.SignInAsync(websiteUrl, cancellationToken).ConfigureAwait(false);

            if (session == null)
                throw new WebSessionException(HttpStatusCode.OK, @"The website was closed before signing in.");

            if (!session.Cookies.ContainsKey(session_cookie))
                throw new WebSessionException(HttpStatusCode.OK, @"The website didn't return a session.");

            lock (sync)
            {
                generation++;
                cookies.Clear();

                foreach (var cookie in session.Cookies)
                    cookies[cookie.Key] = cookie.Value;

                csrfToken = session.CsrfToken;
                Username = session.Username;
                LastError = null;
                captchaRequiredUntil = default;
                signedIn = true;
                save();
            }

            setState(WebSessionState.LoggedIn);
            Logger.Log(@"Signed into the website in the browser.", LoggingTarget.Network);
        }

        /// <summary>
        /// Signs into the website, replacing the current session.
        /// </summary>
        public async Task LoginAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            int currentGeneration;

            lock (sync)
            {
                currentGeneration = ++generation;
                cookies.Clear();
                csrfToken = null;
                signedIn = false;
                save();
            }

            setState(WebSessionState.LoggingIn);

            try
            {
                // like a browser, the website is loaded first, as signing in requires its CSRF token and guest session.
                await refreshCsrfTokenAsync(cancellationToken).ConfigureAwait(false);

                string body = await sendAsync(HttpMethod.Post, @"/session", new[]
                {
                    new KeyValuePair<string, string>(@"username", username),
                    new KeyValuePair<string, string>(@"password", password),
                }, cancellationToken).ConfigureAwait(false);

                var response = JObject.Parse(body);

                lock (sync)
                {
                    if (currentGeneration != generation)
                        return;

                    csrfToken = response[@"csrf_token"]?.Value<string>();
                    Username = response[@"user"]?[@"username"]?.Value<string>() ?? username;

                    if (string.IsNullOrEmpty(csrfToken) || !cookies.ContainsKey(session_cookie))
                        throw new WebSessionException(HttpStatusCode.OK, @"The website didn't return a session.");

                    LastError = null;
                    signedIn = true;
                    save();
                }

                setState(WebSessionState.LoggedIn);
                Logger.Log(@"Signed into the website.", LoggingTarget.Network);
            }
            catch (Exception e)
            {
                lock (sync)
                {
                    if (currentGeneration != generation)
                        throw;

                    cookies.Clear();
                    csrfToken = null;
                    signedIn = false;
                    LastError = e.Message;
                    save();
                }

                setState(WebSessionState.LoggedOut);
                Logger.Log($@"Signing into the website failed: {e.Message}", LoggingTarget.Network);
                throw;
            }
        }

        /// <summary>
        /// Signs into the website, replacing the current session.
        /// Only when the website wants a captcha, it's opened in the <see cref="Browser"/> for solving it, as it can only be solved on the website itself.
        /// </summary>
        public async Task LoginOrSolveCaptchaAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            // another attempt would only keep the captcha required for longer.
            if (Browser != null && DateTimeOffset.UtcNow < captchaRequiredUntil)
            {
                await LoginWithBrowserAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                await LoginAsync(username, password, cancellationToken).ConfigureAwait(false);
            }
            catch (WebSessionCaptchaException) when (Browser != null)
            {
                await LoginWithBrowserAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Signs into the website in the background, for when logging into the game.
        /// </summary>
        public void Login(string username, string password)
        {
            if (Browser == null && DateTimeOffset.UtcNow < captchaRequiredUntil)
            {
                Logger.Log(@"Not signing into the website, as it wants a captcha after too many attempts.", LoggingTarget.Network);
                return;
            }

            currentLogin = Task.Run(async () =>
            {
                try
                {
                    await LoginOrSolveCaptchaAsync(username, password).ConfigureAwait(false);
                }
                catch
                {
                    // already logged, the website can still be signed into when it's needed.
                }
            });
        }

        /// <summary>
        /// Verifies the session in the background with the code used for verifying the game, for when logging into the game.
        /// This works with the authenticator app, while a code from the email may only belong to the game, in which case the website asks for its own one when it's needed.
        /// </summary>
        public void Verify(string key) => Task.Run(async () =>
        {
            try
            {
                await currentLogin.ConfigureAwait(false);

                if (!isSignedIn)
                    return;

                await VerifyAsync(key).ConfigureAwait(false);
                Logger.Log(@"Verified the website session.", LoggingTarget.Network);
            }
            catch (Exception e)
            {
                Logger.Log($@"Verifying the website session failed: {e.Message}", LoggingTarget.Network);
            }
        });

        private bool isSignedIn
        {
            get
            {
                lock (sync)
                    return signedIn;
            }
        }

        /// <summary>
        /// Verifies the session with the code from the verification email or the authenticator app.
        /// </summary>
        public Task VerifyAsync(string key, CancellationToken cancellationToken = default)
            => SendAsync(HttpMethod.Post, @"/home/account/verify", new[] { new KeyValuePair<string, string>(@"verification_key", key) }, cancellationToken);

        /// <summary>
        /// Sends the verification email again.
        /// </summary>
        public Task ReissueVerificationAsync(CancellationToken cancellationToken = default)
            => SendAsync(HttpMethod.Post, @"/home/account/reissue-code", null, cancellationToken);

        /// <summary>
        /// Sends a request to the website within the session, like a request of the website itself.
        /// </summary>
        /// <param name="method">The method of the request.</param>
        /// <param name="path">The path of the request, relative to the website, e.g. "/beatmapsets/1/discussion".</param>
        /// <param name="form">The parameters of the request, which are sent like a form.</param>
        /// <param name="cancellationToken">A token to cancel the request.</param>
        /// <returns>The body of the response.</returns>
        /// <exception cref="WebSessionVerificationException">If the session has to be verified first.</exception>
        /// <exception cref="WebSessionLoginRequiredException">If the session is gone and the website has to be signed into again.</exception>
        /// <exception cref="WebSessionException">If the request failed.</exception>
        public async Task<string> SendAsync(HttpMethod method, string path, IEnumerable<KeyValuePair<string, string>>? form = null, CancellationToken cancellationToken = default)
        {
            var parameters = form?.ToList();

            try
            {
                return await sendAsync(method, path, parameters, cancellationToken).ConfigureAwait(false);
            }
            catch (WebSessionException e) when (e.StatusCode == (HttpStatusCode)419)
            {
                // the token of the session doesn't match anymore, which is fixed by getting the current one like reloading the page.
                await refreshCsrfTokenAsync(cancellationToken).ConfigureAwait(false);
                return await sendAsync(method, path, parameters, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Signs out of the website and forgets the session.
        /// </summary>
        public void Logout()
        {
            bool hadSession;

            lock (sync)
            {
                hadSession = cookies.ContainsKey(session_cookie);

                if (hadSession)
                {
                    var request = createRequest(HttpMethod.Delete, @"/session", null);
                    Task.Run(async () =>
                    {
                        try
                        {
                            using var _ = await client.SendAsync(request).ConfigureAwait(false);
                        }
                        catch
                        {
                            // the session is forgotten anyway.
                        }
                        finally
                        {
                            request.Dispose();
                        }
                    });
                }

                generation++;
                cookies.Clear();
                csrfToken = null;
                signedIn = false;
                Username = null;
                LastError = null;
                save();
            }

            setState(WebSessionState.LoggedOut);
        }

        private const string session_cookie = @"osu_session";

        private async Task refreshCsrfTokenAsync(CancellationToken cancellationToken)
        {
            using var request = createRequest(HttpMethod.Get, @"/home", null);
            request.Headers.Accept.ParseAdd(@"text/html");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            storeCookies(response);

            string html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var match = csrf_meta_regex.Match(html);

            if (!match.Success)
                return;

            lock (sync)
            {
                csrfToken = WebUtility.HtmlDecode(match.Groups[1].Value);
                save();
            }
        }

        private async Task<string> sendAsync(HttpMethod method, string path, IEnumerable<KeyValuePair<string, string>>? form, CancellationToken cancellationToken)
        {
            HttpRequestMessage request;

            lock (sync)
                request = createRequest(method, path, form);

            using (request)
            {
                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

                storeCookies(response);

                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                    return body;

                throw createException(response.StatusCode, body);
            }
        }

        private HttpRequestMessage createRequest(HttpMethod method, string path, IEnumerable<KeyValuePair<string, string>>? form)
        {
            var request = new HttpRequestMessage(method, websiteUrl + path);

            request.Headers.Accept.ParseAdd(@"application/json");
            request.Headers.UserAgent.ParseAdd(@"osu!");
            request.Headers.Add(@"X-Requested-With", @"XMLHttpRequest");

            // like requests of the website itself, which is required for signing in.
            request.Headers.Add(@"Origin", websiteUrl);
            request.Headers.Referrer = new Uri(websiteUrl + @"/");

            if (csrfToken != null)
                request.Headers.Add(@"X-CSRF-Token", csrfToken);

            if (cookies.Count > 0)
                request.Headers.Add(@"Cookie", string.Join(@"; ", cookies.Select(c => $@"{c.Key}={c.Value}")));

            if (form != null || method != HttpMethod.Get)
                request.Content = new FormUrlEncodedContent(form ?? Enumerable.Empty<KeyValuePair<string, string>>());

            return request;
        }

        private void storeCookies(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues(@"Set-Cookie", out var values))
                return;

            lock (sync)
            {
                foreach (string header in values)
                {
                    string[] parts = header.Split(';');
                    int separator = parts[0].IndexOf('=');

                    if (separator <= 0)
                        continue;

                    string name = parts[0][..separator].Trim();
                    string value = parts[0][(separator + 1)..].Trim();

                    bool expired = string.IsNullOrEmpty(value) || value == @"deleted" || parts.Skip(1).Any(isExpiredAttribute);

                    if (expired)
                        cookies.Remove(name);
                    else
                        cookies[name] = value;
                }

                save();
            }
        }

        private static bool isExpiredAttribute(string attribute)
        {
            string[] pair = attribute.Split('=', 2);
            string name = pair[0].Trim();

            if (pair.Length < 2)
                return false;

            if (name.Equals(@"Max-Age", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(pair[1].Trim(), out int maxAge) && maxAge <= 0;

            if (name.Equals(@"Expires", StringComparison.OrdinalIgnoreCase))
                return DateTimeOffset.TryParse(pair[1].Trim(), out var expires) && expires < DateTimeOffset.UtcNow;

            return false;
        }

        private WebSessionException createException(HttpStatusCode statusCode, string body)
        {
            JObject? json = null;

            try
            {
                json = JObject.Parse(body);
            }
            catch
            {
                // not every error has a JSON body.
            }

            string? authentication = json?[@"authentication"]?.Value<string>();

            string? error = json?[@"error"]?.Value<string>();

            // after too many failed attempts, the website wants a captcha, which can only be solved in a browser.
            if (json?[@"captcha_triggered"]?.Value<bool>() == true || error?.Contains(@"captcha", StringComparison.OrdinalIgnoreCase) == true)
            {
                captchaRequiredUntil = DateTimeOffset.UtcNow + captcha_backoff;
                return new WebSessionCaptchaException(statusCode, Browser != null
                    ? @"The website wants a captcha after too many sign in attempts. Sign in on the website instead, where it can be solved."
                    : @"The website wants a captcha after too many sign in attempts, which can't be solved here. "
                      + @"Sign in once on the website in your browser, or wait a few minutes, then try again.");
            }

            if (statusCode == HttpStatusCode.Unauthorized && authentication == @"verify")
                return new WebSessionVerificationException(statusCode, @"The session has to be verified.");

            if (statusCode == HttpStatusCode.Unauthorized)
            {
                lock (sync)
                {
                    generation++;
                    cookies.Clear();
                    csrfToken = null;
                    signedIn = false;
                    save();
                }

                setState(WebSessionState.LoggedOut);
                return new WebSessionLoginRequiredException(statusCode, @"Signing into the website is required.");
            }

            string message = error
                             ?? json?[@"message"]?.Value<string>()
                             ?? (json?[@"form_error"] as JObject)?.Values().SelectMany(v => v.Values<string>()).FirstOrDefault()
                             ?? $@"The website returned {(int)statusCode} ({statusCode}).";

            return new WebSessionException(statusCode, message);
        }

        private void setState(WebSessionState newState) => Schedule(() => state.Value = newState);

        private void save()
        {
            // like the token of the game, the session is only kept when the password is saved.
            if (!signedIn || !config.Get<bool>(OsuSetting.SavePassword) || cookies.Count == 0 || csrfToken == null)
            {
                config.SetValue(OsuSetting.SlopWebSession, string.Empty);
                return;
            }

            config.SetValue(OsuSetting.SlopWebSession, JsonConvert.SerializeObject(new StoredSession
            {
                Cookies = new Dictionary<string, string>(cookies),
                CsrfToken = csrfToken,
                Username = Username,
            }));
        }

        private void restore()
        {
            string stored = config.Get<string>(OsuSetting.SlopWebSession);

            if (string.IsNullOrEmpty(stored))
                return;

            try
            {
                var session = JsonConvert.DeserializeObject<StoredSession>(stored);

                if (session?.Cookies == null || session.CsrfToken == null || !session.Cookies.ContainsKey(session_cookie))
                    return;

                foreach (var cookie in session.Cookies)
                    cookies[cookie.Key] = cookie.Value;

                csrfToken = session.CsrfToken;
                Username = session.Username;
                signedIn = true;
                state.Value = WebSessionState.LoggedIn;
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to restore the website session: {e.Message}", LoggingTarget.Network);
            }
        }

        private class StoredSession
        {
            [JsonProperty(@"cookies")]
            public Dictionary<string, string>? Cookies { get; set; }

            [JsonProperty(@"csrf_token")]
            public string? CsrfToken { get; set; }

            [JsonProperty(@"username")]
            public string? Username { get; set; }
        }
    }

    public class WebSessionException : Exception
    {
        public readonly HttpStatusCode StatusCode;

        public WebSessionException(HttpStatusCode statusCode, string message)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }

    /// <summary>
    /// The session has to be verified with the code from the verification email or the authenticator app.
    /// The website sends the email when this happens.
    /// </summary>
    public class WebSessionVerificationException : WebSessionException
    {
        public WebSessionVerificationException(HttpStatusCode statusCode, string message)
            : base(statusCode, message)
        {
        }
    }

    /// <summary>
    /// The website wants a captcha for signing in after too many attempts, which can only be solved in a browser.
    /// </summary>
    public class WebSessionCaptchaException : WebSessionException
    {
        public WebSessionCaptchaException(HttpStatusCode statusCode, string message)
            : base(statusCode, message)
        {
        }
    }

    /// <summary>
    /// The session is gone, such that the website has to be signed into again.
    /// </summary>
    public class WebSessionLoginRequiredException : WebSessionException
    {
        public WebSessionLoginRequiredException(HttpStatusCode statusCode, string message)
            : base(statusCode, message)
        {
        }
    }
}
