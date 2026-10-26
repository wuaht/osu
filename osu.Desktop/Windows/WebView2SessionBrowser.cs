// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;
using osu.Game.Online.API;

namespace osu.Desktop.Windows
{
    /// <summary>
    /// Signs into the website in a window with WebView2, the browser engine of Edge which comes with Windows.
    /// The session of the window is kept, such that signing in again usually only takes a moment.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WebView2SessionBrowser : IWebSessionBrowser
    {
        private const string session_cookie = @"osu_session";

        // reads the signed in user and the CSRF token from the page, like the website itself does.
        private const string read_session_script = @"JSON.stringify({
            user: (window.currentUser && window.currentUser.username) || null,
            csrf: (document.querySelector('meta[name=""csrf-token""]') || {}).content || null
        })";

        private readonly string userDataFolder;

        private int open;

        public WebView2SessionBrowser(string userDataFolder)
        {
            this.userDataFolder = userDataFolder;
        }

        public Task<WebBrowserSession?> SignInAsync(string websiteUrl, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref open, 1) == 1)
                return Task.FromException<WebBrowserSession?>(new InvalidOperationException(@"The website is already open for signing in."));

            try
            {
                CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (Exception e)
            {
                open = 0;
                return Task.FromException<WebBrowserSession?>(new InvalidOperationException(@"Signing in on the website requires the WebView2 runtime of Edge, which isn't installed.", e));
            }

            var window = new SignInWindow(websiteUrl.TrimEnd('/'), userDataFolder, cancellationToken);

            var thread = new Thread(window.Run)
            {
                Name = @"WebView2SessionBrowser",
                IsBackground = true,
            };

            // WebView2 requires a single threaded apartment.
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            return window.Result.ContinueWith(t =>
            {
                open = 0;
                return t;
            }, TaskScheduler.Default).Unwrap();
        }

        private class SignInWindow
        {
            private const string class_name = @"osuWebSessionBrowser";

            private const uint ws_overlapped_window = 0x00CF0000;
            private const uint ws_visible = 0x10000000;
            private const int cw_use_default = unchecked((int)0x80000000);

            private const uint wm_destroy = 0x0002;
            private const uint wm_size = 0x0005;
            private const uint wm_close = 0x0010;
            private const uint wm_timer = 0x0113;
            private const uint wm_app = 0x8000;

            private readonly TaskCompletionSource<WebBrowserSession?> result = new TaskCompletionSource<WebBrowserSession?>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<WebBrowserSession?> Result => result.Task;

            private readonly string websiteUrl;
            private readonly string userDataFolder;
            private readonly CancellationToken cancellationToken;

            // kept such that the delegate isn't collected while the window uses it.
            private readonly WndProc wndProc;

            private readonly ConcurrentQueue<(SendOrPostCallback callback, object? state)> queue = new ConcurrentQueue<(SendOrPostCallback, object?)>();

            private IntPtr hwnd;
            private CoreWebView2Controller? controller;
            private bool checking;

            public SignInWindow(string websiteUrl, string userDataFolder, CancellationToken cancellationToken)
            {
                this.websiteUrl = websiteUrl;
                this.userDataFolder = userDataFolder;
                this.cancellationToken = cancellationToken;

                wndProc = windowProcedure;
            }

            public void Run()
            {
                try
                {
                    IntPtr instance = GetModuleHandle(null);

                    var windowClass = new WNDCLASSEX
                    {
                        cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                        lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
                        hInstance = instance,
                        hCursor = LoadCursor(IntPtr.Zero, 32512),
                        lpszClassName = class_name,
                    };

                    // fails harmlessly when the class is still registered from an earlier window.
                    RegisterClassEx(ref windowClass);

                    hwnd = CreateWindowEx(0, class_name, @"Sign into osu! - the window closes by itself once signed in", ws_overlapped_window | ws_visible,
                        cw_use_default, cw_use_default, 1100, 800, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

                    if (hwnd == IntPtr.Zero)
                        throw new InvalidOperationException($@"Creating the window failed ({Marshal.GetLastWin32Error()}).");

                    SetForegroundWindow(hwnd);

                    SynchronizationContext.SetSynchronizationContext(new WindowSynchronizationContext(this));

                    using var registration = cancellationToken.Register(() => PostMessage(hwnd, wm_close, IntPtr.Zero, IntPtr.Zero));

                    initialise();

                    while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                    {
                        TranslateMessage(ref message);
                        DispatchMessage(ref message);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"The website window failed.");
                    result.TrySetException(e);
                }
                finally
                {
                    controller?.Close();
                    result.TrySetResult(null);
                }
            }

            private async void initialise()
            {
                try
                {
                    var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);
                    controller = await environment.CreateCoreWebView2ControllerAsync(hwnd).ConfigureAwait(true);

                    updateBounds();

                    controller.CoreWebView2.Settings.AreDevToolsEnabled = false;
                    controller.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    controller.CoreWebView2.Navigate(websiteUrl + @"/home");

                    SetTimer(hwnd, IntPtr.Zero, 1000, IntPtr.Zero);
                }
                catch (Exception e)
                {
                    result.TrySetException(e);
                    DestroyWindow(hwnd);
                }
            }

            /// <summary>
            /// Checks whether the website was signed into, after which the session is taken and the window is closed.
            /// </summary>
            private async void checkSession()
            {
                if (checking || controller == null)
                    return;

                checking = true;

                try
                {
                    string json = await controller.CoreWebView2.ExecuteScriptAsync(read_session_script).ConfigureAwait(true);

                    // the result of the script is the string returned by it, in JSON.
                    var page = JObject.Parse(JToken.Parse(json).Value<string>() ?? @"{}");

                    string? username = page[@"user"]?.Value<string>();
                    string? csrfToken = page[@"csrf"]?.Value<string>();

                    if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(csrfToken))
                        return;

                    var cookies = await controller.CoreWebView2.CookieManager.GetCookiesAsync(websiteUrl).ConfigureAwait(true);
                    var values = new Dictionary<string, string>();

                    foreach (var cookie in cookies)
                        values[cookie.Name] = cookie.Value;

                    if (!values.ContainsKey(session_cookie))
                        return;

                    result.TrySetResult(new WebBrowserSession(values, csrfToken, username));
                    DestroyWindow(hwnd);
                }
                catch (Exception e)
                {
                    // the page may still be loading, it's checked again shortly.
                    Logger.Log($@"Checking the website session failed: {e.Message}", LoggingTarget.Network);
                }
                finally
                {
                    checking = false;
                }
            }

            private void updateBounds()
            {
                if (controller == null)
                    return;

                GetClientRect(hwnd, out var rect);
                controller.Bounds = new System.Drawing.Rectangle(0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top);
            }

            private IntPtr windowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
            {
                switch (message)
                {
                    case wm_size:
                        updateBounds();
                        return IntPtr.Zero;

                    case wm_timer:
                        checkSession();
                        return IntPtr.Zero;

                    case wm_app:
                        while (queue.TryDequeue(out var item))
                            item.callback(item.state);
                        return IntPtr.Zero;

                    case wm_close:
                        DestroyWindow(window);
                        return IntPtr.Zero;

                    case wm_destroy:
                        KillTimer(window, IntPtr.Zero);
                        PostQuitMessage(0);
                        return IntPtr.Zero;
                }

                return DefWindowProc(window, message, wParam, lParam);
            }

            /// <summary>
            /// Continues the asynchronous calls of WebView2 on the thread of the window, which it requires.
            /// </summary>
            private class WindowSynchronizationContext : SynchronizationContext
            {
                private readonly SignInWindow window;

                public WindowSynchronizationContext(SignInWindow window)
                {
                    this.window = window;
                }

                public override void Post(SendOrPostCallback d, object? state)
                {
                    window.queue.Enqueue((d, state));
                    PostMessage(window.hwnd, wm_app, IntPtr.Zero, IntPtr.Zero);
                }

                public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

                public override SynchronizationContext CreateCopy() => this;
            }
        }

        #region Win32

        private delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public uint cbSize;
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int ptX;
            public int ptY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? moduleName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassEx(ref WNDCLASSEX windowClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height,
                                                    IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DefWindowProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadCursor(IntPtr instance, int cursorName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetMessage(out MSG message, IntPtr hwnd, uint filterMin, uint filterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr DispatchMessage(ref MSG message);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void PostQuitMessage(int exitCode);

        [DllImport("user32.dll")]
        private static extern IntPtr SetTimer(IntPtr hwnd, IntPtr id, uint elapse, IntPtr timerProc);

        [DllImport("user32.dll")]
        private static extern bool KillTimer(IntPtr hwnd, IntPtr id);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

        #endregion
    }
}
