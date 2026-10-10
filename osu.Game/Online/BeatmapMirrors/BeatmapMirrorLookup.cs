// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Logging;
using osu.Game.Online.API;

namespace osu.Game.Online.BeatmapMirrors
{
    /// <summary>
    /// Performs a <see cref="BeatmapMirrorJsonRequest{T}"/>, retrying it on the next mirror whenever a mirror fails.
    /// </summary>
    /// <typeparam name="T">The type of the response.</typeparam>
    public class BeatmapMirrorLookup<T>
        where T : class
    {
        private readonly IAPIProvider api;
        private readonly IReadOnlyList<BeatmapMirror> mirrors;
        private readonly Func<BeatmapMirror, BeatmapMirrorJsonRequest<T>> createRequest;
        private readonly Action<T> onSuccess;
        private readonly Action<Exception> onFailure;

        private int nextMirrorIndex;
        private BeatmapMirrorJsonRequest<T>? currentRequest;
        private bool cancelled;

        /// <param name="api">The API to perform the requests with.</param>
        /// <param name="mirrors">The mirrors to try, in order.</param>
        /// <param name="createRequest">Creates the request for a mirror.</param>
        /// <param name="onSuccess">Invoked with the response of the first mirror which succeeded. Runs on the API's scheduler (the update thread).</param>
        /// <param name="onFailure">Invoked with the error of the last mirror if all mirrors failed. Runs on the API's scheduler (the update thread).</param>
        public BeatmapMirrorLookup(IAPIProvider api, IReadOnlyList<BeatmapMirror> mirrors, Func<BeatmapMirror, BeatmapMirrorJsonRequest<T>> createRequest,
                                   Action<T> onSuccess, Action<Exception> onFailure)
        {
            if (mirrors.Count == 0)
                throw new ArgumentException(@"At least one mirror is required.", nameof(mirrors));

            this.api = api;
            this.mirrors = mirrors;
            this.createRequest = createRequest;
            this.onSuccess = onSuccess;
            this.onFailure = onFailure;
        }

        public void Start() => performNext();

        /// <summary>
        /// Cancels the lookup. Neither of the callbacks will be invoked afterwards.
        /// </summary>
        public void Cancel()
        {
            cancelled = true;
            currentRequest?.Cancel();
        }

        private void performNext()
        {
            if (cancelled)
                return;

            var mirror = mirrors[nextMirrorIndex++];
            var request = currentRequest = createRequest(mirror);

            request.Success += response =>
            {
                if (!cancelled)
                    onSuccess(response);
            };

            request.Failure += e =>
            {
                if (cancelled || e is OperationCanceledException)
                    return;

                if (nextMirrorIndex < mirrors.Count)
                {
                    Logger.Log($@"{request} failed ({e.Message}), retrying on {mirrors[nextMirrorIndex]}.", LoggingTarget.Network);
                    performNext();
                    return;
                }

                onFailure(e);
            };

            api.PerformAsync(request);
        }
    }
}
