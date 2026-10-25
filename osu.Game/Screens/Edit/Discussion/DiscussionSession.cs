// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Online.API;

namespace osu.Game.Screens.Edit.Discussion
{
    public enum DiscussionSessionState
    {
        Loading,
        Failed,
        Ready,
    }

    public enum DiscussionRefreshState
    {
        Checking,
        HasUpdates,
        NoUpdates,
        Updating,
    }

    /// <summary>
    /// The discussion of the beatmap set open in the editor, loaded from the discussion page of the website.
    /// Like on the website, updates are checked for every 10 seconds and only applied when refreshing.
    /// </summary>
    public partial class DiscussionSession : Component
    {
        private const int check_updates_interval = 10000;
        private const int request_timeout = 30000;

        public readonly Bindable<DiscussionSessionState> State = new Bindable<DiscussionSessionState>(DiscussionSessionState.Loading);

        /// <summary>
        /// The beatmap set with its discussion, once loaded.
        /// </summary>
        public readonly Bindable<DiscussionBeatmapSet?> BeatmapSet = new Bindable<DiscussionBeatmapSet?>();

        public readonly Bindable<DiscussionRefreshState> RefreshState = new Bindable<DiscussionRefreshState>(DiscussionRefreshState.NoUpdates);

        /// <summary>
        /// Why loading failed.
        /// </summary>
        public string? Message { get; private set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private Editor? editor { get; set; }

        /// <summary>
        /// Incremented whenever the beatmap set is loaded again, such that results of earlier loads are dropped.
        /// </summary>
        private int generation;

        private CancellationTokenSource? cancellation;
        private ScheduledDelegate? scheduledCheck;

        private bool active;
        private bool updating;

        public int BeatmapSetId => editorBeatmap.BeatmapInfo.BeatmapSet?.OnlineID ?? -1;

        /// <summary>
        /// Starts or stops loading and checking for updates, which only happens while the screen is visible.
        /// </summary>
        public void SetActive(bool value)
        {
            if (active == value)
                return;

            active = value;

            if (active)
            {
                if (BeatmapSet.Value == null && editor?.DiscussionState.LoadedBeatmapSet is DiscussionBeatmapSet loaded && loaded.Id == BeatmapSetId)
                {
                    BeatmapSet.Value = loaded;
                    State.Value = DiscussionSessionState.Ready;
                }

                if (BeatmapSet.Value == null)
                    Reload();
                else
                    Refresh();
            }
            else
            {
                scheduledCheck?.Cancel();
                cancellation?.Cancel();
                updating = false;

                if (RefreshState.Value is DiscussionRefreshState.Checking or DiscussionRefreshState.Updating)
                    RefreshState.Value = DiscussionRefreshState.NoUpdates;
            }
        }

        /// <summary>
        /// Loads the discussion from scratch, showing the loading state until it's loaded.
        /// </summary>
        public void Reload()
        {
            BeatmapSet.Value = null;
            State.Value = DiscussionSessionState.Loading;
            load(false);
        }

        /// <summary>
        /// Loads the discussion again, keeping the current one visible until the new one arrives.
        /// </summary>
        public void Refresh()
        {
            if (BeatmapSet.Value == null)
            {
                Reload();
                return;
            }

            if (updating)
                return;

            load(true);
        }

        private void load(bool isRefresh)
        {
            int currentGeneration = ++generation;

            scheduledCheck?.Cancel();
            cancellation?.Cancel();
            var token = (cancellation = new CancellationTokenSource()).Token;

            updating = true;

            if (isRefresh)
                RefreshState.Value = DiscussionRefreshState.Updating;

            int id = BeatmapSetId;

            Task.Run(async () =>
            {
                try
                {
                    string json = await getString($@"{api.Endpoints.WebsiteUrl}/beatmapsets/{id}/discussion", token).ConfigureAwait(false);
                    var response = JsonConvert.DeserializeObject<DiscussionResponse>(json);

                    if (response?.BeatmapSet == null)
                        throw new InvalidOperationException(@"The discussion has no beatmap set.");

                    Schedule(() =>
                    {
                        if (currentGeneration != generation)
                            return;

                        updating = false;
                        BeatmapSet.Value = response.BeatmapSet;

                        if (editor != null)
                            editor.DiscussionState.LoadedBeatmapSet = response.BeatmapSet;

                        State.Value = DiscussionSessionState.Ready;
                        RefreshState.Value = DiscussionRefreshState.NoUpdates;
                        scheduleCheck();
                    });
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested)
                        return;

                    Logger.Log($@"Failed to load the discussion of beatmap set {id}: {e.Message}", LoggingTarget.Network);

                    Schedule(() =>
                    {
                        if (currentGeneration != generation)
                            return;

                        updating = false;

                        if (isRefresh && BeatmapSet.Value != null)
                        {
                            // like on the website, a failed refresh keeps the current discussion and checks again later.
                            RefreshState.Value = DiscussionRefreshState.NoUpdates;
                            scheduleCheck();
                            return;
                        }

                        Message = e.Message;
                        State.Value = DiscussionSessionState.Failed;
                    });
                }
            }, token);
        }

        private void scheduleCheck()
        {
            scheduledCheck?.Cancel();

            if (!active || BeatmapSet.Value?.DeletedAt != null)
                return;

            scheduledCheck = Scheduler.AddDelayed(checkUpdates, check_updates_interval);
        }

        private void checkUpdates()
        {
            var beatmapSet = BeatmapSet.Value;

            if (!active || beatmapSet == null || updating)
                return;

            int currentGeneration = generation;
            var token = cancellation?.Token ?? CancellationToken.None;
            int id = beatmapSet.Id;

            RefreshState.Value = DiscussionRefreshState.Checking;

            Task.Run(async () =>
            {
                DateTimeOffset? lastUpdate = null;

                try
                {
                    string json = await getString($@"{api.Endpoints.WebsiteUrl}/beatmapsets/{id}/discussion-last-update", token).ConfigureAwait(false);
                    lastUpdate = JsonConvert.DeserializeObject<LastUpdateResponse>(json)?.LastUpdate;
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested)
                        return;

                    Logger.Log($@"Failed to check the discussion of beatmap set {id} for updates: {e.Message}", LoggingTarget.Network);
                }

                Schedule(() =>
                {
                    if (currentGeneration != generation || BeatmapSet.Value != beatmapSet)
                        return;

                    bool hasUpdates = lastUpdate != null && lastUpdate > lastUpdateOf(beatmapSet);
                    RefreshState.Value = hasUpdates ? DiscussionRefreshState.HasUpdates : DiscussionRefreshState.NoUpdates;

                    // stop checking once there is an update, otherwise keep checking.
                    if (!hasUpdates)
                        scheduleCheck();
                });
            }, token);
        }

        /// <summary>
        /// The time of the last change of the beatmap set, its discussions or events, see <c>lastUpdateDate</c> on the website.
        /// </summary>
        private static DateTimeOffset lastUpdateOf(DiscussionBeatmapSet beatmapSet)
        {
            var times = beatmapSet.Discussions.Select(d => d.UpdatedAt ?? d.CreatedAt)
                                  .Concat(beatmapSet.Events.Select(e => e.CreatedAt))
                                  .Append(beatmapSet.LastUpdated ?? DateTimeOffset.MinValue);

            return times.Max();
        }

        private static async Task<string> getString(string url, CancellationToken token)
        {
            using var request = new OsuWebRequest(url)
            {
                Method = HttpMethod.Get,
                Timeout = request_timeout,
            };

            request.AddHeader(@"Accept", @"application/json");

            await request.PerformAsync(token).ConfigureAwait(false);

            return request.GetResponseString() ?? throw new InvalidOperationException(@"The response is empty.");
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation?.Cancel();
            base.Dispose(isDisposing);
        }

        private class LastUpdateResponse
        {
            [JsonProperty(@"last_update")]
            public DateTimeOffset? LastUpdate { get; set; }
        }
    }
}
