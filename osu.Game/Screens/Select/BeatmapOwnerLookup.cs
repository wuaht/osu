// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Resources.Localisation.Web;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Looks up the current owners (mappers) of a beatmap difficulty, using the <see cref="BeatmapOwnerStore"/>.
    /// </summary>
    /// <remarks>
    /// The author stored locally always is the beatmap set host as written in the .osu file,
    /// which is wrong for guest difficulties and may use an outdated username.
    /// </remarks>
    public partial class BeatmapOwnerLookup : Component
    {
        /// <summary>
        /// The owners of the current beatmap, or <see langword="null"/> if they are not (yet) known,
        /// in which case the locally stored author should be displayed instead.
        /// </summary>
        public IBindable<APIBeatmap.BeatmapOwner[]?> Owners => owners;

        private readonly Bindable<APIBeatmap.BeatmapOwner[]?> owners = new Bindable<APIBeatmap.BeatmapOwner[]?>();

        private readonly IBindable<APIState> apiState = new Bindable<APIState>();

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private BeatmapOwnerStore ownerStore { get; set; } = null!;

        private CancellationTokenSource? lookupCancellationSource;

        /// <summary>
        /// Whether <see cref="Owners"/> is up-to-date, i.e. no further lookups are required for the current beatmap.
        /// </summary>
        private bool isUpToDate;

        private int beatmapOnlineID = -1;

        /// <summary>
        /// The online ID of the beatmap difficulty to look up the owners of.
        /// </summary>
        public int BeatmapOnlineID
        {
            get => beatmapOnlineID;
            set
            {
                if (beatmapOnlineID == value)
                    return;

                beatmapOnlineID = value;

                lookupCancellationSource?.Cancel();
                lookupCancellationSource = null;
                isUpToDate = false;
                owners.Value = null;

                if (IsLoaded)
                    performLookup();
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            apiState.BindTo(api.State);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // retry once the user logs in (online lookups are not possible while logged out).
            apiState.BindValueChanged(state =>
            {
                if (state.NewValue == APIState.Online)
                    Schedule(performLookup);
            });

            ownerStore.OwnersCleared += onOwnersCleared;

            performLookup();
        }

        private void onOwnersCleared(IReadOnlyCollection<int>? beatmapIds) => Schedule(() =>
        {
            if (beatmapOnlineID <= 0 || (beatmapIds != null && !beatmapIds.Contains(beatmapOnlineID)))
                return;

            lookupCancellationSource?.Cancel();
            lookupCancellationSource = null;
            isUpToDate = false;
            owners.Value = null;

            performLookup();
        });

        private void performLookup()
        {
            if (beatmapOnlineID <= 0 || isUpToDate)
                return;

            // stored owners are available immediately, so apply them without waiting a frame to avoid the fallback flashing.
            var stored = ownerStore.GetStored(beatmapOnlineID);

            if (stored != null)
            {
                applyEntry(stored);

                if (isUpToDate)
                    return;
            }

            // nothing more can be retrieved until the user logs in (or a beatmap mirror can be used).
            if (ownerStore.HasLoadedFromDisk && !ownerStore.CanLookUpOnline)
                return;

            lookupCancellationSource?.Cancel();
            lookupCancellationSource = new CancellationTokenSource();

            var token = lookupCancellationSource.Token;

            ownerStore.GetAsync(beatmapOnlineID, token).ContinueWith(t => Schedule(() =>
            {
                if (token.IsCancellationRequested || !t.IsCompletedSuccessfully)
                    return;

                var entry = t.GetResultSafely();

                if (entry != null)
                    applyEntry(entry);
            }), token, TaskContinuationOptions.None, TaskScheduler.Default);
        }

        private void applyEntry(BeatmapOwnerStore.Entry entry)
        {
            isUpToDate = !entry.IsStale;
            owners.Value = entry.Owners.Length > 0 ? entry.Owners : null;
        }

        /// <summary>
        /// Joins the usernames of the given owners in the same way osu-web does (e.g. "a, b, and c").
        /// </summary>
        public static LocalisableString FormatUsernames(IReadOnlyList<APIBeatmap.BeatmapOwner> owners)
        {
            if (owners.Count == 0)
                return string.Empty;

            LocalisableString result = owners[0].Username;

            for (int i = 1; i < owners.Count; i++)
                result = LocalisableString.Interpolate($"{result}{GetConnector(i - 1, owners.Count)}{owners[i].Username}");

            return result;
        }

        /// <summary>
        /// Returns the connector to place between the owner at <paramref name="index"/> and the one following it (e.g. ", " or " and ").
        /// </summary>
        /// <param name="index">The index of the owner preceding the connector. Must be less than <paramref name="count"/> - 1.</param>
        /// <param name="count">The total number of owners.</param>
        public static LocalisableString GetConnector(int index, int count)
        {
            if (count == 2)
                return CommonStrings.ArrayAndTwoWordsConnector;

            return index == count - 2 ? CommonStrings.ArrayAndLastWordConnector : CommonStrings.ArrayAndWordsConnector;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            lookupCancellationSource?.Cancel();

            if (ownerStore.IsNotNull())
                ownerStore.OwnersCleared -= onOwnersCleared;
        }
    }
}
