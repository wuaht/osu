// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.BeatmapMirrors;
using Realms;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// This component is designed to perform lookups of online data
    /// and store portions of it for later local use to the realm database.
    /// </summary>
    /// <example>
    /// This component is designed to locally persist potentially-volatile online information such as:
    /// <list type="bullet">
    /// <item>user tags assigned to difficulties of a beatmap,</item>
    /// <item>the beatmap's <see cref="BeatmapInfo.Status"/>,</item>
    /// <item>guest mappers assigned to difficulties of a beatmap,</item>
    /// <item>the local user's best score on a given beatmap.</item>
    /// </list>
    /// </example>
    public partial class RealmPopulatingOnlineLookupSource : Component
    {
        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private BeatmapMirrorProvider? mirrors { get; set; }

        /// <summary>
        /// How long a beatmap set isn't looked up on a beatmap mirror again after a successful lookup, to not hit the mirrors' rate limits.
        /// </summary>
        private static readonly TimeSpan mirror_lookup_cooldown = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The times of the last successful beatmap mirror lookups, keyed by the online ID of the looked up beatmap set.
        /// Only accessed from the update thread.
        /// </summary>
        private readonly Dictionary<int, DateTimeOffset> lastMirrorLookups = new Dictionary<int, DateTimeOffset>();

        public Task<APIBeatmapSet?> GetBeatmapSetAsync(int id, CancellationToken token = default)
        {
            if (mirrors?.IsActive == true)
                return getBeatmapSetFromMirrorAsync(id, token);

            var request = new GetBeatmapSetRequest(id);
            var tcs = new TaskCompletionSource<APIBeatmapSet?>();

            token.Register(request.Cancel);

            // async request success callback is a bit of a dangerous game, but there's some reasoning for it.
            // - don't really want to use `IAPIAccess.PerformAsync()` because we still want to respect request queueing & online status checks
            // - we want the realm write here to be async because it is known to be slow for some users with large beatmap collections
            // - at the time of writing `RealmAccess.WriteAsync()` can only be safely called from update thread,
            //   and API request completion callbacks are automatically marshaled onto update thread scheduler,
            //   so calling `WriteAsync()` within the callback is a somewhat "nice" way of guaranteeing that the call is safe
            //   (rather than having to enforce that `GetBeatmapSetAsync()` can only be called from update thread, or locally scheduling)
            request.Success += async onlineBeatmapSet =>
            {
                if (token.IsCancellationRequested)
                {
                    tcs.SetCanceled(token);
                    return;
                }

                await realm.WriteAsync(r => updateRealmBeatmapSet(r, onlineBeatmapSet, true)).ConfigureAwait(true);
                tcs.SetResult(onlineBeatmapSet);
            };
            request.Failure += tcs.SetException;
            api.Queue(request);
            return tcs.Task;
        }

        /// <summary>
        /// Looks up the beatmap set on a beatmap mirror to update the local beatmaps, which notably allows detecting available updates without logging in.
        /// </summary>
        /// <returns>
        /// Always <c>null</c>. The mirror's beatmap set isn't returned, as mirrors don't provide all data the displays of online information expect
        /// (e.g. the genre or user-specific data), and the official servers aren't used while mirrors are.
        /// </returns>
        private Task<APIBeatmapSet?> getBeatmapSetFromMirrorAsync(int id, CancellationToken token)
        {
            Debug.Assert(mirrors != null);

            if (lastMirrorLookups.TryGetValue(id, out var lastLookup) && DateTimeOffset.Now - lastLookup < mirror_lookup_cooldown)
                return Task.FromResult<APIBeatmapSet?>(null);

            var tcs = new TaskCompletionSource<APIBeatmapSet?>();

            var lookup = mirrors.PerformLookup(
                mirror => new GetMirrorBeatmapSetRequest(mirror, id),
                async onlineBeatmapSet =>
                {
                    lastMirrorLookups[id] = DateTimeOffset.Now;

                    try
                    {
                        // see the comments in `GetBeatmapSetAsync()` regarding the realm write in the success callback.
                        // mirrors don't provide reliable user tag data, so the tags are left alone.
                        await realm.WriteAsync(r => updateRealmBeatmapSet(r, onlineBeatmapSet, false)).ConfigureAwait(true);
                        tcs.TrySetResult(null);
                    }
                    catch (Exception e)
                    {
                        tcs.TrySetException(e);
                    }
                },
                e => tcs.TrySetException(e));

            token.Register(() =>
            {
                lookup.Cancel();
                tcs.TrySetCanceled(token);
            });

            return tcs.Task;
        }

        /// <param name="r">The realm to write to.</param>
        /// <param name="onlineBeatmapSet">The beatmap set returned by the online lookup.</param>
        /// <param name="updateUserTags">Whether to update the user tags of the beatmaps.</param>
        private static void updateRealmBeatmapSet(Realm r, APIBeatmapSet onlineBeatmapSet, bool updateUserTags)
        {
            var onlineBeatmaps = onlineBeatmapSet.Beatmaps.ToDictionary(b => b.OnlineID);

            var dbBeatmapSets = r.All<BeatmapSetInfo>().Where(b => b.OnlineID == onlineBeatmapSet.OnlineID);

            foreach (var dbBeatmapSet in dbBeatmapSets)
            {
                // note that every single write to realm models is preceded by a guard, even if it technically would write the same value back.
                // the reason this matters is that doing so avoids triggering realm subscription callbacks.
                // unfortunately in terms of subscriptions realm treats *every* write to any realm object as a modification,
                // even if the write was redundant and had no observable effect.

                // notably, `LocallyModified` status is preserved on the set until the user performs an explicit action to get rid of it
                // (be it updating the set or deciding to discard their changes, removing the set and re-downloading it, etc.)
                if (dbBeatmapSet.Status != onlineBeatmapSet.Status && dbBeatmapSet.Status != BeatmapOnlineStatus.LocallyModified)
                    dbBeatmapSet.Status = onlineBeatmapSet.Status;

                foreach (var dbBeatmap in dbBeatmapSet.Beatmaps)
                {
                    // beatmaps imported while connected to the development server had their online IDs reset (as it doesn't have the official beatmaps).
                    // restore them if the content exactly matches the online version, as the ID is required for looking up e.g. the beatmap owners.
                    if (dbBeatmap.OnlineID <= 0)
                    {
                        var matchingBeatmap = onlineBeatmapSet.Beatmaps.FirstOrDefault(b => b.MD5Hash == dbBeatmap.MD5Hash);

                        if (matchingBeatmap != null && dbBeatmapSet.Beatmaps.All(b => b.OnlineID != matchingBeatmap.OnlineID))
                            dbBeatmap.OnlineID = matchingBeatmap.OnlineID;
                    }

                    if (onlineBeatmaps.TryGetValue(dbBeatmap.OnlineID, out var onlineBeatmap))
                    {
                        // compare `BeatmapUpdaterMetadataLookup`
                        if (dbBeatmap.OnlineMD5Hash != onlineBeatmap.MD5Hash)
                            dbBeatmap.OnlineMD5Hash = onlineBeatmap.MD5Hash;

                        if (dbBeatmap.LastOnlineUpdate != onlineBeatmap.LastUpdated)
                            dbBeatmap.LastOnlineUpdate = onlineBeatmap.LastUpdated;

                        if (dbBeatmap.MatchesOnlineVersion && dbBeatmap.Status != onlineBeatmap.Status)
                            dbBeatmap.Status = onlineBeatmap.Status;

                        if (!updateUserTags)
                            continue;

                        HashSet<string> userTags = onlineBeatmap.GetTopUserTags(confirmedOnly: true)
                                                                .Select(t => t.Tag.Name)
                                                                .ToHashSet();

                        if (!userTags.SetEquals(dbBeatmap.Metadata.UserTags))
                        {
                            dbBeatmap.Metadata.UserTags.Clear();
                            dbBeatmap.Metadata.UserTags.AddRange(userTags);
                        }
                    }
                }
            }
        }
    }
}
