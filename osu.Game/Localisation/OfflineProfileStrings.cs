// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class OfflineProfileStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.OfflineProfile";

        /// <summary>
        /// "Offline profiles"
        /// </summary>
        public static LocalisableString Header => new TranslatableString(getKey(@"header"), @"Offline profiles");

        /// <summary>
        /// "Recent Plays"
        /// </summary>
        public static LocalisableString RecentPlays => new TranslatableString(getKey(@"recent_plays"), @"Recent Plays");

        /// <summary>
        /// "No scores yet."
        /// </summary>
        public static LocalisableString NoScores => new TranslatableString(getKey(@"no_scores"), @"No scores yet.");

        /// <summary>
        /// "Play as"
        /// </summary>
        public static LocalisableString PlayAs => new TranslatableString(getKey(@"play_as"), @"Play as");

        /// <summary>
        /// "While not logged in, scores are set on this profile. It is like an osu! account which only exists on this computer."
        /// </summary>
        public static LocalisableString PlayAsDescription => new TranslatableString(getKey(@"play_as_description"),
            @"While not logged in, scores are set on this profile. It is like an osu! account which only exists on this computer.");

        /// <summary>
        /// "Guest"
        /// </summary>
        public static LocalisableString Guest => new TranslatableString(getKey(@"guest"), @"Guest");

        /// <summary>
        /// "New profile"
        /// </summary>
        public static LocalisableString NewProfile => new TranslatableString(getKey(@"new_profile"), @"New profile");

        /// <summary>
        /// "Create profile"
        /// </summary>
        public static LocalisableString CreateProfile => new TranslatableString(getKey(@"create_profile"), @"Create profile");

        /// <summary>
        /// "Username"
        /// </summary>
        public static LocalisableString Username => new TranslatableString(getKey(@"username"), @"Username");

        /// <summary>
        /// "Country"
        /// </summary>
        public static LocalisableString Country => new TranslatableString(getKey(@"country"), @"Country");

        /// <summary>
        /// "Avatar"
        /// </summary>
        public static LocalisableString Avatar => new TranslatableString(getKey(@"avatar"), @"Avatar");

        /// <summary>
        /// "Banner"
        /// </summary>
        public static LocalisableString Banner => new TranslatableString(getKey(@"banner"), @"Banner");



        /// <summary>
        /// "View profile"
        /// </summary>
        public static LocalisableString ViewProfile => new TranslatableString(getKey(@"view_profile"), @"View profile");

        /// <summary>
        /// "Manage profiles"
        /// </summary>
        public static LocalisableString ManageProfiles => new TranslatableString(getKey(@"manage_profiles"), @"Manage profiles");

        /// <summary>
        /// "Delete profile"
        /// </summary>
        public static LocalisableString DeleteProfile => new TranslatableString(getKey(@"delete_profile"), @"Delete profile");

        /// <summary>
        /// "Delete the profile {0}? Its scores are kept in the local leaderboards."
        /// </summary>
        public static LocalisableString DeleteProfileConfirmation(string username) => new TranslatableString(getKey(@"delete_profile_confirmation"),
            @"Delete the profile {0}? Its scores are kept in the local leaderboards.", username);


        /// <summary>
        /// "Count unranked beatmaps"
        /// </summary>
        public static LocalisableString IncludeUnrankedBeatmaps => new TranslatableString(getKey(@"include_unranked_beatmaps"), @"Count unranked beatmaps");

        /// <summary>
        /// "Scores on beatmaps which aren't ranked also award pp and count for the ranked score and grades of profiles."
        /// </summary>
        public static LocalisableString IncludeUnrankedBeatmapsDescription => new TranslatableString(getKey(@"include_unranked_beatmaps_description"),
            @"Scores on beatmaps which aren't ranked also award pp and count for the ranked score and grades of profiles.");

        /// <summary>
        /// "Offline profile"
        /// </summary>
        public static LocalisableString OfflineProfile => new TranslatableString(getKey(@"offline_profile"), @"Offline profile");

        /// <summary>
        /// "Signed in offline"
        /// </summary>
        public static LocalisableString SignedInOffline => new TranslatableString(getKey(@"signed_in_offline"), @"Signed in offline");

        /// <summary>
        /// "Ranking"
        /// </summary>
        public static LocalisableString Ranking => new TranslatableString(getKey(@"ranking"), @"Ranking");

        /// <summary>
        /// "Profiles without pp aren't ranked."
        /// </summary>
        public static LocalisableString UnrankedProfiles => new TranslatableString(getKey(@"unranked_profiles"), @"Profiles without pp aren't ranked.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
