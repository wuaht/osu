// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopNotificationsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopNotifications";

        /// <summary>
        /// "This client temporarily uses its own database until osu!(lazer) is updated to the same database version. Until then, changes (scores, imports, collections) are only kept in this client."
        /// </summary>
        public static LocalisableString SeparateDatabaseInUse => new TranslatableString(getKey(@"separate_database_in_use"),
            @"This client temporarily uses its own database until osu!(lazer) is updated to the same database version. Until then, changes (scores, imports, collections) are only kept in this client.");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
