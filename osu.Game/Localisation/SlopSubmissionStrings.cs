// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopSubmissionStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopSubmission";

        /// <summary>
        /// "Difficulties to upload"
        /// </summary>
        public static LocalisableString UploadedDifficulties => new TranslatableString(getKey(@"uploaded_difficulties"), @"Difficulties to upload");

        /// <summary>
        /// "{0} (hitsound difficulty)"
        /// </summary>
        public static LocalisableString HitsoundDifficultyCaption(string name) => new TranslatableString(getKey(@"hitsound_difficulty_caption"), @"{0} (hitsound difficulty)", name);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
