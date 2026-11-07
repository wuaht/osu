// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Input;
using osuTK;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Automation
{
    /// <summary>
    /// A localhost HTTP endpoint that lets an external tool (the automation MCP server) see and drive the running game
    /// without taking over the user's mouse and keyboard.
    /// Only enabled when <see cref="PORT_VARIABLE"/> and <see cref="TOKEN_VARIABLE"/> are set.
    /// </summary>
    public partial class AutomationBridge : Component
    {
        public const string PORT_VARIABLE = @"SLOP_AUTOMATION_PORT";
        public const string TOKEN_VARIABLE = @"SLOP_AUTOMATION_TOKEN";

        private const string token_header = @"X-Automation-Token";

        private static readonly MethodInfo trigger_text_input = typeof(TextInputSource).GetMethod(@"TriggerTextInput", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private readonly int port;
        private readonly string token;
        private readonly AutomationInputHandler inputHandler = new AutomationInputHandler();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        // the automated instance runs in the background next to the user's own client, so it stays silent.
        private readonly BindableDouble mute = new BindableDouble(0);

        private HttpListener? listener;
        private OsuUserInputManager inputManager = null!;

        /// <summary>
        /// Drawables listed by the last <c>/tree</c> request, indexed by their id.
        /// </summary>
        private List<WeakReference<Drawable>> lastTree = new List<WeakReference<Drawable>>();

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private TextInputSource textInput { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private LocalisationManager localisation { get; set; } = null!;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        public static AutomationBridge? FromEnvironment()
        {
            if (!int.TryParse(Environment.GetEnvironmentVariable(PORT_VARIABLE), out int port))
                return null;

            string? token = Environment.GetEnvironmentVariable(TOKEN_VARIABLE);

            if (string.IsNullOrEmpty(token))
            {
                Logger.Log($"Automation bridge not started: {TOKEN_VARIABLE} is not set.", LoggingTarget.Runtime, LogLevel.Error);
                return null;
            }

            return new AutomationBridge(port, token);
        }

        private AutomationBridge(int port, string token)
        {
            this.port = port;
            this.token = token;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Drawable? root = this;
            while (root != null && root is not OsuUserInputManager)
                root = root.Parent;

            inputManager = (OsuUserInputManager?)root ?? throw new InvalidOperationException($"{nameof(AutomationBridge)} must be placed below {nameof(OsuUserInputManager)}.");
            inputManager.EnableAutomation(inputHandler);
            audio.AddAdjustment(AdjustableProperty.Volume, mute);

            listener = new HttpListener();
            listener.Prefixes.Add($@"http://127.0.0.1:{port}/");
            listener.Start();

            Task.Factory.StartNew(listen, TaskCreationOptions.LongRunning);
            Logger.Log($"Automation bridge listening on port {port}.");
        }

        private async Task listen()
        {
            while (!cancellation.IsCancellationRequested && listener?.IsListening == true)
            {
                HttpListenerContext context;

                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (cancellation.IsCancellationRequested)
                {
                    return;
                }

                _ = Task.Run(() => handle(context));
            }
        }

        private async Task handle(HttpListenerContext context)
        {
            var response = context.Response;

            try
            {
                // Browsers cannot send custom headers cross-origin without a preflight we never answer, so this also blocks web pages.
                if (context.Request.Headers[token_header] != token)
                {
                    await writeJson(response, new { error = "invalid token" }, 403).ConfigureAwait(false);
                    return;
                }

                JObject body;

                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    string text = await reader.ReadToEndAsync().ConfigureAwait(false);
                    body = string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
                }

                switch (context.Request.Url!.AbsolutePath)
                {
                    case "/state":
                        await writeJson(response, await onUpdateThread(getState).ConfigureAwait(false)).ConfigureAwait(false);
                        break;

                    case "/screenshot":
                        await writeScreenshot(response, body.Value<int?>("maxWidth")).ConfigureAwait(false);
                        break;

                    case "/tree":
                        await writeJson(response, await onUpdateThread(() => getTree(body.Value<string>("query"))).ConfigureAwait(false)).ConfigureAwait(false);
                        break;

                    case "/click":
                        await click(body).ConfigureAwait(false);
                        await writeJson(response, new { ok = true }).ConfigureAwait(false);
                        break;

                    case "/hover":
                        await moveTo(body).ConfigureAwait(false);
                        await writeJson(response, new { ok = true }).ConfigureAwait(false);
                        break;

                    case "/scroll":
                        await moveTo(body).ConfigureAwait(false);
                        inputHandler.Enqueue(new MouseScrollRelativeInput { Delta = new Vector2(0, body.Value<float?>("amount") ?? -1) });
                        await writeJson(response, new { ok = true }).ConfigureAwait(false);
                        break;

                    case "/key":
                        await pressKeys(body.Value<string>("combo") ?? throw new ArgumentException("combo is required")).ConfigureAwait(false);
                        await writeJson(response, new { ok = true }).ConfigureAwait(false);
                        break;

                    case "/type":
                        string typed = body.Value<string>("text") ?? throw new ArgumentException("text is required");
                        await onUpdateThread(() => trigger_text_input.Invoke(textInput, new object[] { typed })).ConfigureAwait(false);
                        await writeJson(response, new { ok = true }).ConfigureAwait(false);
                        break;

                    case "/exit":
                        await writeJson(response, new { ok = true }).ConfigureAwait(false);
                        Schedule(host.Exit);
                        break;

                    default:
                        await writeJson(response, new { error = "unknown endpoint" }, 404).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception e)
            {
                try
                {
                    await writeJson(response, new { error = e.Message }, 400).ConfigureAwait(false);
                }
                catch
                {
                    // the client is gone.
                }
            }
        }

        private object getState()
        {
            var screenSize = inputManager.ScreenSpaceDrawQuad.AABBFloat.Size;

            return new
            {
                screen = game.ScreenStack.CurrentScreen?.GetType().Name,
                width = (int)screenSize.X,
                height = (int)screenSize.Y,
                windowActive = host.IsActive.Value,
                focused = inputManager.FocusedDrawable?.GetType().Name,
            };
        }

        private object getTree(string? query)
        {
            var screen = inputManager.ScreenSpaceDrawQuad.AABBFloat;
            var drawables = new List<WeakReference<Drawable>>();
            var nodes = new List<object>();

            foreach (var d in inputManager.ChildrenOfType<Drawable>())
            {
                bool interactive = isInteractive(d);

                // texts inside interactive drawables or text flows are reported as part of them.
                if (!interactive && !isTextNode(d))
                    continue;

                if (!isVisible(d))
                    continue;

                var rect = d.ScreenSpaceDrawQuad.AABBFloat;
                if (!rect.IntersectsWith(screen) || rect.Width < 1 || rect.Height < 1)
                    continue;

                string text = describe(d, interactive);
                string type = d.GetType().Name;

                if (!string.IsNullOrEmpty(query)
                    && !text.Contains(query, StringComparison.OrdinalIgnoreCase)
                    && !type.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;

                drawables.Add(new WeakReference<Drawable>(d));
                nodes.Add(new
                {
                    id = drawables.Count - 1,
                    type,
                    interactive,
                    text,
                    x = (int)rect.X,
                    y = (int)rect.Y,
                    w = (int)rect.Width,
                    h = (int)rect.Height,
                });
            }

            lastTree = drawables;
            return nodes;
        }

        private bool isVisible(Drawable d)
        {
            var rect = d.ScreenSpaceDrawQuad.AABBFloat;

            for (Drawable? current = d; current != null && current != inputManager; current = current.Parent)
            {
                if (!current.IsPresent)
                    return false;

                // scrolled out of a masking container (e.g. a list).
                if (current != d && current is CompositeDrawable { Masking: true } && !current.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(rect))
                    return false;
            }

            return true;
        }

        private string describe(Drawable d, bool interactive)
        {
            var parts = new List<string>();

            if (d is SpriteText sprite)
                parts.Add(localisation.GetLocalisedString(sprite.Text));

            if (d is TextBox textBox)
                parts.Add(string.IsNullOrEmpty(textBox.Text) ? $"<placeholder: {localisation.GetLocalisedString(textBox.PlaceholderText)}>" : textBox.Text);

            if (d is TextFlowContainer)
                parts.Add(string.Concat(d.ChildrenOfType<SpriteText>().Where(s => s.IsPresent).Select(s => localisation.GetLocalisedString(s.Text))).Trim());
            else if (interactive && d is not TextBox)
            {
                parts.AddRange(d.ChildrenOfType<SpriteText>()
                                .Where(s => s.IsPresent && !string.IsNullOrWhiteSpace(s.Text.ToString()))
                                .Select(s => localisation.GetLocalisedString(s.Text))
                                .Take(6));
            }

            if (d is IHasTooltip tooltip && !string.IsNullOrEmpty(tooltip.TooltipText.ToString()))
                parts.Add($"tooltip: {localisation.GetLocalisedString(tooltip.TooltipText)}");

            string text = string.Join(" | ", parts.Distinct());
            return text.Length > 160 ? text[..160] + "…" : text;
        }

        private bool isTextNode(Drawable d)
        {
            if (d is TextFlowContainer)
                return !hasAncestor(d, a => isInteractive(a) || a is TextFlowContainer);

            if (d is SpriteText sprite && !string.IsNullOrWhiteSpace(sprite.Text.ToString()))
                return !hasAncestor(d, a => isInteractive(a) || a is TextFlowContainer);

            return false;
        }

        private bool hasAncestor(Drawable d, Func<Drawable, bool> predicate)
        {
            for (Drawable? current = d.Parent; current != null && current != inputManager; current = current.Parent)
            {
                if (predicate(current))
                    return true;
            }

            return false;
        }

        private static readonly Dictionary<Type, bool> interactive_types = new Dictionary<Type, bool>();

        /// <summary>
        /// Whether the drawable's type reacts to clicks or presses, judged by which input handlers it overrides.
        /// </summary>
        private static bool isInteractive(Drawable d)
        {
            if (!d.HandlePositionalInput)
                return false;

            var type = d.GetType();

            lock (interactive_types)
            {
                if (!interactive_types.TryGetValue(type, out bool interactive))
                {
                    interactive_types[type] = interactive = overrides(type, @"OnClick", typeof(ClickEvent))
                                                            || overrides(type, @"OnMouseDown", typeof(MouseDownEvent));
                }

                return interactive;
            }

            static bool overrides(Type type, string name, Type eventType)
                => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, new[] { eventType })?.DeclaringType != typeof(Drawable);
        }

        private async Task<Vector2> resolvePosition(JObject body)
        {
            int? id = body.Value<int?>("id");

            if (id == null)
            {
                return new Vector2(body.Value<float?>("x") ?? throw new ArgumentException("id or x/y is required"),
                    body.Value<float?>("y") ?? throw new ArgumentException("id or x/y is required"));
            }

            return await onUpdateThread(() =>
            {
                if (id < 0 || id >= lastTree.Count || !lastTree[id.Value].TryGetTarget(out var drawable) || !drawable.IsAlive || !isVisible(drawable))
                    throw new ArgumentException($"drawable {id} is gone or hidden, request the tree again");

                return drawable.ScreenSpaceDrawQuad.Centre;
            }).ConfigureAwait(false);
        }

        private async Task moveTo(JObject body)
        {
            inputHandler.Enqueue(new MousePositionAbsoluteInput { Position = await resolvePosition(body).ConfigureAwait(false) });

            // let hover state settle before anything else arrives.
            await Task.Delay(50).ConfigureAwait(false);
        }

        private async Task click(JObject body)
        {
            var button = body.Value<string>("button") == "right" ? MouseButton.Right : MouseButton.Left;
            int count = body.Value<bool?>("double") == true ? 2 : 1;

            await moveTo(body).ConfigureAwait(false);

            for (int i = 0; i < count; i++)
            {
                inputHandler.Enqueue(new MouseButtonInput(button, true));
                await Task.Delay(30).ConfigureAwait(false);
                inputHandler.Enqueue(new MouseButtonInput(button, false));
                await Task.Delay(30).ConfigureAwait(false);
            }
        }

        private async Task pressKeys(string combo)
        {
            var keys = combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                            .Select(k => Enum.TryParse(k, true, out Key key) ? key : throw new ArgumentException($"unknown key \"{k}\""))
                            .ToArray();

            foreach (var key in keys)
                inputHandler.Enqueue(new KeyboardKeyInput(key, true));

            await Task.Delay(30).ConfigureAwait(false);

            foreach (var key in keys.Reverse())
                inputHandler.Enqueue(new KeyboardKeyInput(key, false));

            await Task.Delay(30).ConfigureAwait(false);
        }

        private async Task writeScreenshot(HttpListenerResponse response, int? maxWidth)
        {
            using (var image = await host.TakeScreenshotAsync().ConfigureAwait(false))
            {
                int originalWidth = image.Width;

                if (maxWidth > 0 && image.Width > maxWidth)
                    image.Mutate(c => c.Resize(maxWidth.Value, 0));

                response.ContentType = @"image/png";
                response.Headers[@"X-Scale"] = ((float)image.Width / originalWidth).ToString(System.Globalization.CultureInfo.InvariantCulture);

                using (var stream = new MemoryStream())
                {
                    await image.SaveAsPngAsync(stream).ConfigureAwait(false);
                    response.ContentLength64 = stream.Length;
                    stream.Position = 0;
                    await stream.CopyToAsync(response.OutputStream).ConfigureAwait(false);
                }
            }

            response.Close();
        }

        private static async Task writeJson(HttpListenerResponse response, object value, int status = 200)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));

            response.StatusCode = status;
            response.ContentType = @"application/json";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
            response.Close();
        }

        private Task<T> onUpdateThread<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            Schedule(() =>
            {
                try
                {
                    tcs.SetResult(func());
                }
                catch (Exception e)
                {
                    tcs.SetException(e is TargetInvocationException { InnerException: not null } ? e.InnerException : e);
                }
            });

            return tcs.Task;
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            listener?.Close();
            base.Dispose(isDisposing);
        }
    }
}
