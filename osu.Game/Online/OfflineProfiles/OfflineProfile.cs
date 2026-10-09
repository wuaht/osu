// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Users;

namespace osu.Game.Online.OfflineProfiles
{
    /// <summary>
    /// A local profile which scores can be set on while not logged in, like an account which only exists on this computer.
    /// </summary>
    public class OfflineProfile
    {
        /// <summary>
        /// The user ID of the first profile. Further profiles get decreasing IDs.
        /// Clearly separate from the IDs used for guests (0) and scores of unknown provenance (1), as well as from -1 which is commonly used for "none".
        /// </summary>
        public const int FIRST_USER_ID = -1000;

        /// <summary>
        /// The maximum length of usernames (the same as for osu! accounts).
        /// </summary>
        public const int MAX_USERNAME_LENGTH = 15;

        public Guid ID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The user ID which scores set on this profile are stored with. Never reused, even after the profile is deleted.
        /// </summary>
        public int UserID { get; set; }

        public string Username { get; set; } = string.Empty;

        public CountryCode CountryCode { get; set; } = CountryCode.Unknown;

        public DateTimeOffset JoinDate { get; set; }

        /// <summary>
        /// The filename of the avatar within the directory of the profile, if set.
        /// </summary>
        public string? AvatarFilename { get; set; }

        /// <summary>
        /// The filename of the cover (banner) within the directory of the profile, if set.
        /// </summary>
        public string? CoverFilename { get; set; }

        /// <summary>
        /// The number of plays started on this profile, per ruleset (by short name).
        /// Includes failed and aborted plays, like the play count of osu! accounts.
        /// </summary>
        public Dictionary<string, int> PlayCounts { get; set; } = new Dictionary<string, int>();

        /// <summary>
        /// Creates the user representing this profile.
        /// </summary>
        public OfflineProfileUser CreateUser() => new OfflineProfileUser
        {
            ProfileID = ID,
            Id = UserID,
            Username = Username,
            CountryCode = CountryCode,
            JoinDate = JoinDate,
        };
    }
}
