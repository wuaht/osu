// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Online.API
{
    /// <summary>
    /// A browser in which the website can be signed into like normally, including everything the website may ask for, such as a captcha.
    /// </summary>
    public interface IWebSessionBrowser
    {
        /// <summary>
        /// Opens the website for signing in.
        /// </summary>
        /// <param name="websiteUrl">The URL of the website.</param>
        /// <param name="cancellationToken">A token to close the browser.</param>
        /// <returns>The session once the website is signed into, or null if the browser was closed before.</returns>
        Task<WebBrowserSession?> SignInAsync(string websiteUrl, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A session on the website which was signed into in a browser.
    /// </summary>
    /// <param name="Cookies">The cookies of the website.</param>
    /// <param name="CsrfToken">The CSRF token of the page.</param>
    /// <param name="Username">The username of the signed in user.</param>
    public record WebBrowserSession(IReadOnlyDictionary<string, string> Cookies, string CsrfToken, string? Username);
}
