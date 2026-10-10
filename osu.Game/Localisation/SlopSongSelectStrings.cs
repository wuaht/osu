// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopSongSelectStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopSongSelect";

        /// <summary>
        /// "Refresh mappers"
        /// </summary>
        public static LocalisableString RefreshMappers => new TranslatableString(getKey(@"refresh_mappers"), @"Refresh mappers");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
