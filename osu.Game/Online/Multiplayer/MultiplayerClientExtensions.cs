// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ExceptionExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Logging;
using osu.Game.Utils;

namespace osu.Game.Online.Multiplayer
{
    public static class MultiplayerClientExtensions
    {
        /// <summary>
        /// Whether the user has been notified that the server does not support realtime functionality for this build.
        /// </summary>
        private static int unsupportedVersionNotified;

        /// <summary>
        /// Whether the user has already been notified that the server does not support realtime functionality in an earlier session of the installed version,
        /// in which case the message is only logged.
        /// </summary>
        public static bool UnsupportedVersionNotifiedBefore { get; set; }

        /// <summary>
        /// Invoked when the user is notified that the server does not support realtime functionality, such that it can be remembered for later sessions.
        /// May be invoked from any thread.
        /// </summary>
        public static Action? OnUnsupportedVersionNotified { get; set; }

        public static void FireAndForget(this Task task, Action? onSuccess = null, Action<Exception>? onError = null) =>
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.Assert(t.Exception != null);
                    Exception exception = t.Exception.AsSingular();

                    onError?.Invoke(exception);

                    // OnlineStatusNotifier is already letting users know about interruptions to connections.
                    // Silence these because it gets very spammy otherwise.
                    if (SentryLogger.IsLocalUserConnectivityException(exception))
                        return;

                    if (exception.GetHubExceptionMessage() is string message)
                    {
                        // The server rejects every realtime call of unofficial builds (such as slop!) with the same message.
                        // As realtime calls happen frequently (e.g. on every activity change), only notify the user once.
                        if (isUnsupportedVersionMessage(message))
                        {
                            if (UnsupportedVersionNotifiedBefore || Interlocked.Exchange(ref unsupportedVersionNotified, 1) == 1)
                            {
                                Logger.Log(message);
                                return;
                            }

                            OnUnsupportedVersionNotified?.Invoke();
                        }

                        // Hub exceptions generally contain something we can show the user directly.
                        Logger.Log(message, level: LogLevel.Important);
                        return;
                    }

                    Logger.Error(exception, $"Unobserved exception occurred via {nameof(FireAndForget)} call: {exception.Message}");
                }
                else
                {
                    onSuccess?.Invoke();
                }
            });

        /// <summary>
        /// Start a background process to disconnect/reconnect as soon as a specific condition is met.
        /// </summary>
        /// <remarks>
        /// If a reconnect happens via another means, this will abort attempts.
        /// We only want to reconnect once.
        /// </remarks>
        /// <param name="client">The client to operate on.</param>
        /// <param name="isConnected">Connected state of client.</param>
        /// <param name="readyFunction">The condition which should be <c>true</c> to continue with the shutdown.</param>
        /// <param name="reconnectFunction">The method to run to perform the reconnect.</param>
        public static void ReconnectWhenReady(this IStatefulUserHubClient client, IBindable<bool> isConnected, Func<bool> readyFunction, Func<Task> reconnectFunction)
        {
            Task.Run(async () =>
            {
                bool didReconnect = false;
                var connected = isConnected.GetBoundCopy();
                connected.ValueChanged += _ => didReconnect = true;

                string clientName = client.GetType().ReadableName();

                Logger.Log($"{clientName} has signalled shutdown");

                while (!readyFunction())
                {
                    Logger.Log($"{clientName} shutdown waiting for idle conditions...");
                    await Task.Delay(10000).ConfigureAwait(false);
                }

                Logger.Log($"{clientName} disconnecting due to shutdown signal");
                if (!didReconnect)
                    await reconnectFunction().ConfigureAwait(false);

                connected.UnbindAll();
            }).FireAndForget();
        }

        private static bool isUnsupportedVersionMessage(string message) =>
            message.Contains(@"not supported on this version of the game", StringComparison.OrdinalIgnoreCase);

        public static string? GetHubExceptionMessage(this Exception exception)
        {
            if (exception is HubException hubException)
                // HubExceptions arrive with additional message context added, but we want to display the human readable message:
                // "An unexpected error occurred invoking 'AddPlaylistItem' on the server.InvalidStateException: Can't enqueue more than 3 items at once."
                // We generally use the message field for a user-parseable error (eventually to be replaced), so drop the first part for now.
                return hubException.Message.Substring(exception.Message.IndexOf(':') + 1).Trim();

            return null;
        }
    }
}
