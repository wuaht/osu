// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API
{
    public interface ILocalUserState
    {
        IBindable<APIUser> User { get; }
        IBindableList<APIRelation> Friends { get; }
        IBindableList<APIRelation> Blocks { get; }
        IBindableList<int> FavouriteBeatmapSets { get; }

        /// <summary>
        /// Sets the user which is the local user while not logged in, instead of a guest (used for offline profiles).
        /// </summary>
        /// <param name="user">The user, or <c>null</c> to use a guest.</param>
        void SetOfflineProfileUser(APIUser? user);

        void UpdateFriends();
        void UpdateBlocks();
        void UpdateFavouriteBeatmapSets();
    }
}
