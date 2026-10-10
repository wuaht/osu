// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Online.BeatmapMirrors
{
    /// <summary>
    /// A request to the osu!-API-v2-compatible endpoints of a <see cref="BeatmapMirror"/>.
    /// </summary>
    /// <typeparam name="T">The type of the response.</typeparam>
    public abstract class BeatmapMirrorJsonRequest<T> : APIRequest
        where T : class
    {
        /// <summary>
        /// Mirrors leave out or null some fields which the osu! API always populates (e.g. <c>ratings</c> or <c>genre</c>),
        /// so null values are skipped to keep the defaults of the response models.
        /// </summary>
        private static readonly JsonSerializerSettings serializer_settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
        };

        public readonly BeatmapMirror Mirror;

        /// <summary>
        /// The deserialised response. May be null if the request or deserialisation failed.
        /// </summary>
        public T? Response { get; private set; }

        /// <summary>
        /// Invoked on successful completion of the request.
        /// This will be scheduled to the API's internal scheduler (run on update thread automatically).
        /// </summary>
        public new event APISuccessHandler<T>? Success;

        protected BeatmapMirrorJsonRequest(BeatmapMirror mirror)
        {
            Mirror = mirror;

            base.Success += () => Success?.Invoke(Response!);
        }

        protected override string Uri => $@"{Mirror.GetMetadataApiUrl() ?? throw new InvalidOperationException($@"{Mirror} has no metadata API.")}/{Target}";

        protected override bool TargetsOsuServer => false;

        protected override WebRequest CreateWebRequest() => new MirrorWebRequest(Uri);

        protected override void PostProcess()
        {
            base.PostProcess();

            try
            {
                string? response = WebRequest?.GetResponseString();

                if (response != null)
                    Response = JsonConvert.DeserializeObject<T>(response, serializer_settings);
            }
            catch (Exception e)
            {
                TriggerFailure(e);
                return;
            }

            if (Response == null)
                TriggerFailure(new ArgumentNullException(nameof(Response)));
        }

        public override string ToString() => $@"{GetType().Name} ({Mirror}: {Target})";

        private class MirrorWebRequest : OsuWebRequest
        {
            public MirrorWebRequest(string uri)
                : base(uri)
            {
            }

            protected override string Accept => @"application/json";
        }
    }
}
