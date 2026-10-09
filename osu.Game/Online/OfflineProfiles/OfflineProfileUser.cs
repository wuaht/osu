// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Online.API;

namespace osu.Game.Online.OfflineProfiles
{
    /// <summary>
    /// The user representing an <see cref="OfflineProfile"/>, which is the local user while not logged in and the profile is active.
    /// </summary>
    /// <remarks>
    /// A <see cref="GuestUser"/>, as online functionality isn't available for it (like for guests).
    /// </remarks>
    public class OfflineProfileUser : GuestUser
    {
        /// <summary>
        /// The <see cref="OfflineProfile.ID"/> of the profile.
        /// </summary>
        public Guid ProfileID { get; init; }

        /// <summary>
        /// Whether the given user ID belongs to an offline profile.
        /// Offline profiles use negative IDs, which never occur online.
        /// </summary>
        public static bool IsOfflineProfileID(int userId) => userId <= OfflineProfile.FIRST_USER_ID;
    }
}
