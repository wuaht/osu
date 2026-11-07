// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopOmdbStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopOmdb";

        /// <summary>
        /// "Open OMDB listing"
        /// </summary>
        public static LocalisableString OpenListing => new TranslatableString(getKey(@"open_listing"), @"Open OMDB listing");

        /// <summary>
        /// "Rate on OMDB"
        /// </summary>
        public static LocalisableString Rate => new TranslatableString(getKey(@"rate"), @"Rate on OMDB");

        /// <summary>
        /// "OMDB rating"
        /// </summary>
        public static LocalisableString OmdbRating => new TranslatableString(getKey(@"omdb_rating"), @"OMDB rating");

        /// <summary>
        /// "Your rating"
        /// </summary>
        public static LocalisableString YourRating => new TranslatableString(getKey(@"your_rating"), @"Your rating");

        /// <summary>
        /// "Remove rating"
        /// </summary>
        public static LocalisableString RemoveRating => new TranslatableString(getKey(@"remove_rating"), @"Remove rating");

        /// <summary>
        /// "Not rated yet. Drag the slider to rate."
        /// </summary>
        public static LocalisableString NotRatedYet => new TranslatableString(getKey(@"not_rated_yet"), @"Not rated yet. Drag the slider to rate.");

        /// <summary>
        /// "Loading..."
        /// </summary>
        public static LocalisableString Loading => new TranslatableString(getKey(@"loading"), @"Loading...");

        /// <summary>
        /// "Submitting..."
        /// </summary>
        public static LocalisableString Submitting => new TranslatableString(getKey(@"submitting"), @"Submitting...");

        /// <summary>
        /// "{0} / 5.00 from {1} votes"
        /// </summary>
        public static LocalisableString RatingSummary(string average, int count) => new TranslatableString(getKey(@"rating_summary"), @"{0} / 5.00 from {1} votes", average, count);

        /// <summary>
        /// "{0} / 5.00 from 1 vote"
        /// </summary>
        public static LocalisableString RatingSummarySingle(string average) => new TranslatableString(getKey(@"rating_summary_single"), @"{0} / 5.00 from 1 vote", average);

        /// <summary>
        /// "No ratings yet"
        /// </summary>
        public static LocalisableString NoRatings => new TranslatableString(getKey(@"no_ratings"), @"No ratings yet");

        /// <summary>
        /// "yours: {0}"
        /// </summary>
        public static LocalisableString OwnRatingSuffix(string rating) => new TranslatableString(getKey(@"own_rating_suffix"), @"yours: {0}", rating);

        /// <summary>
        /// "unavailable"
        /// </summary>
        public static LocalisableString Unavailable => new TranslatableString(getKey(@"unavailable"), @"unavailable");

        /// <summary>
        /// "Rating"
        /// </summary>
        public static LocalisableString Rating => new TranslatableString(getKey(@"rating"), @"Rating");

        /// <summary>
        /// "Ranking"
        /// </summary>
        public static LocalisableString Ranking => new TranslatableString(getKey(@"ranking"), @"Ranking");

        /// <summary>
        /// "#{0} for its year"
        /// </summary>
        public static LocalisableString YearRank(int rank) => new TranslatableString(getKey(@"year_rank"), @"#{0} for its year", rank);

        /// <summary>
        /// "#{0} overall"
        /// </summary>
        public static LocalisableString OverallRank(int rank) => new TranslatableString(getKey(@"overall_rank"), @"#{0} overall", rank);

        /// <summary>
        /// "Descriptors"
        /// </summary>
        public static LocalisableString Descriptors => new TranslatableString(getKey(@"descriptors"), @"Descriptors");

        /// <summary>
        /// "Nominators"
        /// </summary>
        public static LocalisableString Nominators => new TranslatableString(getKey(@"nominators"), @"Nominators");

        /// <summary>
        /// "{0} stars, {1} votes"
        /// </summary>
        public static LocalisableString HistogramTooltip(string score, int count) => new TranslatableString(getKey(@"histogram_tooltip"), @"{0} stars, {1} votes", score, count);

        /// <summary>
        /// "Enter your OMDB API key to see and submit ratings. It is shown on the OMDB settings page and can also be changed in the settings under Online."
        /// </summary>
        public static LocalisableString ApiKeyPrompt => new TranslatableString(getKey(@"api_key_prompt"),
            @"Enter your OMDB API key to see and submit ratings. It is shown on the OMDB settings page and can also be changed in the settings under Online.");

        /// <summary>
        /// "Save API key"
        /// </summary>
        public static LocalisableString SaveApiKey => new TranslatableString(getKey(@"save_api_key"), @"Save API key");

        private static string getKey(string key) => $"{prefix}:{key}";
    }
}
