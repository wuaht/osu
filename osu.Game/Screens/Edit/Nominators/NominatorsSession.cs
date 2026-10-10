// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.BeatmapMirrors;
using osu.Game.Online.BnTracker;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// The BN Tracker data of the beatmap set open in the editor.
    /// Beatmap sets which aren't tracked yet are previewed, and only tracked once something is changed.
    /// All requests run one after another, such that their results are applied in order.
    /// </summary>
    public partial class NominatorsSession : Component
    {
        private const int online_lookup_timeout = 30000;

        /// <summary>
        /// What the screen shows.
        /// </summary>
        public readonly Bindable<NominatorsSessionState> State = new Bindable<NominatorsSessionState>(NominatorsSessionState.Loading);

        /// <summary>
        /// The beatmap set and its nominators, while <see cref="State"/> is <see cref="NominatorsSessionState.Ready"/>.
        /// For beatmap sets which aren't tracked yet, this is a preview with an empty <see cref="BnBeatmapSet.Id"/>.
        /// </summary>
        public readonly Bindable<BnBeatmapSetWithNominators?> Data = new Bindable<BnBeatmapSetWithNominators?>();

        /// <summary>
        /// Whether a request is in progress.
        /// </summary>
        public readonly BindableBool IsBusy = new BindableBool();

        /// <summary>
        /// The reason of the current state, e.g. why loading or signing in failed.
        /// </summary>
        public string? Message { get; private set; }

        /// <summary>
        /// The user name of the owner of the beatmap set, while <see cref="State"/> is <see cref="NominatorsSessionState.NotOwned"/>.
        /// </summary>
        public string? OwnerName { get; private set; }

        /// <summary>
        /// The sign-in in the browser, while <see cref="State"/> is <see cref="NominatorsSessionState.SigningInWithBrowser"/>.
        /// </summary>
        public BnDeviceSignIn? DeviceSignIn { get; private set; }

        public bool IsTracked => Data.Value != null && Data.Value.BeatmapSet.Id != Guid.Empty;

        [Resolved]
        private BnTrackerClient client { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private BeatmapMirrorProvider mirrors { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        private readonly IBindable<bool> isSignedIn = new Bindable<bool>();
        private readonly IBindable<APIState> apiState = new Bindable<APIState>();
        private readonly IBindable<APIUser> localUser = new Bindable<APIUser>();
        private readonly Bindable<string> server = new Bindable<string>();

        /// <summary>
        /// The last request, which the next request waits for.
        /// </summary>
        private Task queue = Task.CompletedTask;

        private int pendingRequests;

        /// <summary>
        /// Incremented whenever the beatmap set is loaded again or the account changes, such that results of earlier loads are dropped.
        /// </summary>
        private int generation;

        /// <summary>
        /// The BN Tracker ID of the beatmap set once it's tracked.
        /// Only accessed by the requests in the queue, which run one after another, such that it always matches the state on the server.
        /// </summary>
        private Guid? trackedId;

        /// <summary>
        /// Whether signing in with the osu! login failed, in which case it isn't tried automatically again until the login changes.
        /// </summary>
        private bool osuSignInFailed;

        private ScheduledDelegate? scheduledPoll;
        private CancellationTokenSource? deviceSignInCancellation;

        private bool active;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopBnTrackerServer, server);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            isSignedIn.BindTo(client.IsSignedIn);
            apiState.BindTo(api.State);
            localUser.BindTo(api.LocalUser);

            // a revoked token is cleared by the client, signing in elsewhere (e.g. in the settings) sets one.
            isSignedIn.BindValueChanged(_ => refreshIfActive());
            server.BindValueChanged(_ => refreshIfActive());

            // the BN Tracker account follows the osu! account (see Refresh()).
            localUser.BindValueChanged(_ =>
            {
                osuSignInFailed = false;
                refreshIfActive();
            });

            apiState.BindValueChanged(s =>
            {
                if (s.NewValue == APIState.Online)
                {
                    osuSignInFailed = false;

                    if (State.Value == NominatorsSessionState.SignedOut)
                        refreshIfActive();
                }
            });
        }

        /// <summary>
        /// Starts or stops loading, which only happens while the screen is visible.
        /// </summary>
        public void SetActive(bool value)
        {
            active = value;

            if (active)
                Refresh();
        }

        private void refreshIfActive()
        {
            if (active)
                Refresh();
        }

        #region Loading

        /// <summary>
        /// Loads the beatmap set again. The current data stays visible until the new data arrives.
        /// </summary>
        public void Refresh()
        {
            int currentGeneration = ++generation;
            long onlineId = editorBeatmap.BeatmapInfo.BeatmapSet?.OnlineID ?? -1;

            cancelDeviceSignIn();

            if (onlineId <= 0)
            {
                setState(NominatorsSessionState.NotSubmitted);
                return;
            }

            if (client.ServerUrl == null)
            {
                setState(NominatorsSessionState.NoServer);
                return;
            }

            if (!client.IsSignedIn.Value)
            {
                if (client.CanSignInWithOsu && !osuSignInFailed)
                    SignInWithOsu();
                else
                    setState(NominatorsSessionState.SignedOut);

                return;
            }

            // the data of another beatmap set (e.g. after submitting the beatmap) isn't shown while loading.
            if (Data.Value?.BeatmapSet.OsuBeatmapSetId != onlineId)
                Data.Value = null;

            if (Data.Value == null)
                setState(NominatorsSessionState.Loading);

            int? localUserId = client.CanSignInWithOsu ? api.LocalUser.Value.OnlineID : null;

            enqueue(async () =>
            {
                var me = client.User.Value ?? await client.GetMeAsync().ConfigureAwait(false);

                // the BN Tracker account follows the osu! account the game is logged in with.
                if (localUserId > 1 && me.OsuId != localUserId)
                    me = await client.SignInWithOsuAsync().ConfigureAwait(false);

                var tracked = await client.GetBeatmapSetAsync(onlineId).ConfigureAwait(false);

                trackedId = tracked?.BeatmapSet.Id;

                if (tracked != null)
                    return new LoadResult(tracked, null);

                var onlineBeatmapSet = await lookUpOnlineBeatmapSetAsync((int)onlineId).ConfigureAwait(false);

                // the owner is the account which uploaded the beatmap set, which doesn't depend on e.g. the creator in the metadata.
                if (onlineBeatmapSet.AuthorID != me.OsuId)
                    return new LoadResult(null, onlineBeatmapSet.AuthorString);

                var nominators = await client.GetNominatorsAsync().ConfigureAwait(false);
                return new LoadResult(BnTrackerPreview.Create(onlineBeatmapSet, nominators), null);
            }, result =>
            {
                if (currentGeneration != generation)
                    return;

                if (result.Data == null)
                {
                    Data.Value = null;
                    OwnerName = result.OwnerName;
                    setState(NominatorsSessionState.NotOwned);
                    return;
                }

                Data.Value = result.Data;
                setState(NominatorsSessionState.Ready);
            }, e =>
            {
                if (currentGeneration != generation)
                    return;

                // a revoked token is handled by the change of the sign-in state.
                if (e is BnTrackerException { StatusCode: HttpStatusCode.Unauthorized } && !client.IsSignedIn.Value)
                    return;

                if (Data.Value != null)
                {
                    // keep the data visible.
                    postError(e.Message);
                    return;
                }

                setState(NominatorsSessionState.Failed, e.Message);
            });
        }

        private async Task<APIBeatmapSet> lookUpOnlineBeatmapSetAsync(int onlineId)
        {
            var tcs = new TaskCompletionSource<APIBeatmapSet>(TaskCreationOptions.RunContinuationsAsynchronously);

            Schedule(() =>
            {
                if (mirrors.IsActive)
                {
                    mirrors.PerformLookup(
                        mirror => new GetMirrorBeatmapSetRequest(mirror, onlineId),
                        s => tcs.TrySetResult(s),
                        e => tcs.TrySetException(new BnTrackerException(null, $@"The beatmap set couldn't be looked up online ({e.Message}).")));
                }
                else
                {
                    var request = new GetBeatmapSetRequest(onlineId);
                    request.Success += s => tcs.TrySetResult(s);
                    request.Failure += e => tcs.TrySetException(new BnTrackerException(null, $@"The beatmap set couldn't be looked up online ({e.Message})."));
                    api.Queue(request);
                }
            });

            // requests to the osu! servers wait while the connection is failing, which shouldn't block the following requests forever.
            if (await Task.WhenAny(tcs.Task, Task.Delay(online_lookup_timeout)).ConfigureAwait(false) != tcs.Task)
                throw new BnTrackerException(null, @"Looking up the beatmap set online timed out.");

            return await tcs.Task.ConfigureAwait(false);
        }

        private record LoadResult(BnBeatmapSetWithNominators? Data, string? OwnerName);

        #endregion

        #region Signing in

        /// <summary>
        /// Signs in with the osu! login of the game.
        /// </summary>
        public void SignInWithOsu()
        {
            int currentGeneration = ++generation;

            cancelDeviceSignIn();
            setState(NominatorsSessionState.SigningIn);

            enqueue(async () =>
            {
                await client.SignInWithOsuAsync().ConfigureAwait(false);
                return true;
            }, _ =>
            {
                if (currentGeneration == generation)
                    Refresh();
            }, e =>
            {
                if (currentGeneration != generation)
                    return;

                osuSignInFailed = true;
                setState(NominatorsSessionState.SignedOut, e.Message);
            });
        }

        /// <summary>
        /// Signs in in the browser, which also works while the game isn't logged in.
        /// </summary>
        public void SignInWithBrowser()
        {
            int currentGeneration = ++generation;

            cancelDeviceSignIn();
            setState(NominatorsSessionState.SigningIn);

            var cancellation = deviceSignInCancellation = new CancellationTokenSource();

            client.StartDeviceSignInAsync(cancellation.Token).ContinueWith(t => Schedule(() =>
            {
                if (currentGeneration != generation || cancellation.IsCancellationRequested)
                    return;

                if (t.Exception != null)
                {
                    setState(NominatorsSessionState.SignedOut, t.Exception.GetBaseException().Message);
                    return;
                }

                DeviceSignIn = t.GetResultSafely();
                setState(NominatorsSessionState.SigningInWithBrowser);

                OpenDeviceSignInPage();
                schedulePoll(DeviceSignIn, currentGeneration, cancellation, DeviceSignIn.Interval);
            }));
        }

        /// <summary>
        /// Opens the page on which the sign-in in the browser is allowed.
        /// </summary>
        public void OpenDeviceSignInPage()
        {
            if (DeviceSignIn != null)
                game?.OpenUrlExternally(DeviceSignIn.VerificationUrlComplete, LinkWarnMode.NeverWarn);
        }

        public void CancelSignIn()
        {
            generation++;
            cancelDeviceSignIn();
            setState(NominatorsSessionState.SignedOut);
        }

        private void schedulePoll(BnDeviceSignIn signIn, int currentGeneration, CancellationTokenSource cancellation, int intervalSeconds)
        {
            scheduledPoll = Scheduler.AddDelayed(() =>
            {
                client.PollDeviceSignInAsync(signIn.DeviceCode, cancellation.Token).ContinueWith(t => Schedule(() =>
                {
                    if (currentGeneration != generation || cancellation.IsCancellationRequested)
                        return;

                    if (t.Exception != null)
                    {
                        // the connection may drop for a moment, the sign-in can still be allowed.
                        Logger.Log($@"Checking the BN Tracker sign-in failed: {t.Exception.GetBaseException().Message}");
                        schedulePoll(signIn, currentGeneration, cancellation, intervalSeconds);
                        return;
                    }

                    var poll = t.GetResultSafely();

                    switch (poll.Status)
                    {
                        case BnDeviceSignInPoll.STATUS_APPROVED:
                            DeviceSignIn = null;
                            // the client is signed in, which loads the beatmap set.
                            return;

                        case BnDeviceSignInPoll.STATUS_DENIED:
                            DeviceSignIn = null;
                            setState(NominatorsSessionState.SignedOut, @"The sign-in was denied.");
                            return;

                        case BnDeviceSignInPoll.STATUS_EXPIRED:
                            DeviceSignIn = null;
                            setState(NominatorsSessionState.SignedOut, @"The sign-in expired. Try again.");
                            return;

                        case BnDeviceSignInPoll.STATUS_SLOW_DOWN:
                            schedulePoll(signIn, currentGeneration, cancellation, Math.Max(intervalSeconds, poll.Interval));
                            return;

                        default:
                            schedulePoll(signIn, currentGeneration, cancellation, intervalSeconds);
                            return;
                    }
                }));
            }, Math.Max(1, intervalSeconds) * 1000);
        }

        private void cancelDeviceSignIn()
        {
            scheduledPoll?.Cancel();
            scheduledPoll = null;

            deviceSignInCancellation?.Cancel();
            deviceSignInCancellation = null;

            DeviceSignIn = null;
        }

        #endregion

        #region Changes

        /// <summary>
        /// Sets the status with a nominator. Tracks the beatmap set first if it isn't tracked yet.
        /// </summary>
        public void SetStatus(int nominatorId, BnNominationStatus status)
        {
            var current = findNominator(nominatorId);

            if (current == null || current.Status == status || current.Nominator.IsRemoved)
                return;

            change(async id => await client.SetStatusAsync(id, nominatorId, status).ConfigureAwait(false), applyChange);
        }

        /// <summary>
        /// Sets the status with multiple nominators at once.
        /// </summary>
        public void SetStatuses(IReadOnlyCollection<int> nominatorIds, BnNominationStatus status)
        {
            int[] ids = nominatorIds.Where(i => findNominator(i)?.Nominator.IsRemoved == false).ToArray();

            if (ids.Length == 0)
                return;

            change(async id => await client.SetStatusesAsync(id, ids, status).ConfigureAwait(false), applyFullData);
        }

        public void AddComment(int nominatorId, string text, Action? onSuccess = null)
        {
            if (findNominator(nominatorId) == null || string.IsNullOrWhiteSpace(text))
                return;

            change(async id => await client.AddCommentAsync(id, nominatorId, text.Trim()).ConfigureAwait(false), c =>
            {
                applyChange(c);
                onSuccess?.Invoke();
            });
        }

        public void DeleteComment(int nominatorId, Guid commentId)
        {
            if (!IsTracked)
                return;

            change(async id => await client.DeleteCommentAsync(id, nominatorId, commentId).ConfigureAwait(false), applyChange);
        }

        public void SetPriority(BnBeatmapPriority priority)
        {
            if (Data.Value == null || Data.Value.BeatmapSet.Priority == priority)
                return;

            change(async id => await client.SetPriorityAsync(id, priority).ConfigureAwait(false), applyBeatmapSet);
        }

        /// <summary>
        /// Stops tracking the beatmap set, which deletes all statuses, comments and activity of it.
        /// </summary>
        public void StopTracking()
        {
            if (!IsTracked)
                return;

            int currentGeneration = generation;

            enqueue(async () =>
            {
                if (trackedId is Guid id)
                    await client.StopTrackingBeatmapSetAsync(id).ConfigureAwait(false);

                trackedId = null;
                return true;
            }, _ =>
            {
                if (currentGeneration != generation)
                    return;

                Data.Value = null;
                Refresh();
            }, e => onChangeFailed(currentGeneration, e));
        }

        /// <summary>
        /// Makes a change to the beatmap set, tracking it first if it isn't tracked yet.
        /// </summary>
        private void change<T>(Func<Guid, Task<T>> request, Action<T> apply)
            where T : class
        {
            if (Data.Value == null)
                return;

            int currentGeneration = generation;
            long onlineId = Data.Value.BeatmapSet.OsuBeatmapSetId;

            enqueue(async () =>
            {
                BnBeatmapSetWithNominators? newlyTracked = null;

                if (trackedId == null)
                {
                    newlyTracked = await client.TrackBeatmapSetAsync(onlineId).ConfigureAwait(false);
                    trackedId = newlyTracked.BeatmapSet.Id;
                }

                return (newlyTracked, result: await request(trackedId.Value).ConfigureAwait(false));
            }, r =>
            {
                if (currentGeneration != generation)
                    return;

                if (r.newlyTracked != null)
                    Data.Value = r.newlyTracked;

                apply(r.result);
            }, e => onChangeFailed(currentGeneration, e));
        }

        private void onChangeFailed(int currentGeneration, Exception e)
        {
            if (currentGeneration != generation)
                return;

            postError(e.Message);

            // the controls show the value which was attempted, and go back to the actual one.
            Data.TriggerChange();
        }

        private void applyFullData(BnBeatmapSetWithNominators data) => Data.Value = data;

        private void applyBeatmapSet(BnBeatmapSet beatmapSet)
        {
            if (Data.Value == null)
                return;

            Data.Value = new BnBeatmapSetWithNominators
            {
                BeatmapSet = beatmapSet,
                Nominators = Data.Value.Nominators,
            };
        }

        private void applyChange(BnSetNominatorChange change)
        {
            if (Data.Value == null)
                return;

            var nominators = Data.Value.Nominators.ToList();
            int index = nominators.FindIndex(n => n.NominatorOsuId == change.Nominator.NominatorOsuId);

            if (index >= 0)
                nominators[index] = change.Nominator;
            else
                nominators.Add(change.Nominator);

            Data.Value = new BnBeatmapSetWithNominators
            {
                BeatmapSet = change.BeatmapSet,
                Nominators = nominators,
            };
        }

        private BnSetNominator? findNominator(int nominatorId) => Data.Value?.Nominators.FirstOrDefault(n => n.NominatorOsuId == nominatorId);

        #endregion

        /// <summary>
        /// Runs a request after the previous ones, and handles the result on the update thread.
        /// </summary>
        private void enqueue<T>(Func<Task<T>> request, Action<T> onSuccess, Action<Exception> onFailure)
        {
            pendingRequests++;
            IsBusy.Value = true;

            queue = queue.ContinueWith(_ => request(), TaskScheduler.Default).Unwrap().ContinueWith(t => Schedule(() =>
            {
                pendingRequests--;
                IsBusy.Value = pendingRequests > 0;

                if (t.Exception != null)
                {
                    var exception = t.Exception.GetBaseException();
                    Logger.Log($@"BN Tracker request failed: {exception.Message}");
                    onFailure(exception);
                }
                else if (t.IsCompletedSuccessfully)
                    onSuccess(t.GetResultSafely());
            }), TaskScheduler.Default);
        }

        private void setState(NominatorsSessionState state, string? message = null)
        {
            Message = message;

            // the state may stay the same while the message changes.
            if (State.Value == state)
                State.TriggerChange();
            else
                State.Value = state;
        }

        private void postError(string message) => notifications?.Post(new SimpleErrorNotification { Text = message });

        protected override void Dispose(bool isDisposing)
        {
            cancelDeviceSignIn();
            base.Dispose(isDisposing);
        }
    }

    public enum NominatorsSessionState
    {
        /// <summary>
        /// The beatmap set wasn't submitted yet, so it has no online ID.
        /// </summary>
        NotSubmitted,

        /// <summary>
        /// No BN Tracker server is set.
        /// </summary>
        NoServer,

        SignedOut,

        SigningIn,

        /// <summary>
        /// Waiting for the user to allow the sign-in in the browser.
        /// </summary>
        SigningInWithBrowser,

        Loading,

        Failed,

        /// <summary>
        /// The beatmap set isn't tracked and belongs to someone else, so it can't be tracked.
        /// </summary>
        NotOwned,

        Ready,
    }
}
