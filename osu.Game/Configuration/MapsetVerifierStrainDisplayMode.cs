// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// What the star rating chart of the mapset verifier's difficulty overview shows.
    /// </summary>
    public enum MapsetVerifierStrainDisplayMode
    {
        /// <summary>
        /// The strain of each skill.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MapsetVerifierStrainOnly))]
        Strain,

        /// <summary>
        /// The cumulative star rating.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MapsetVerifierStarRatingOnly))]
        StarRating,

        /// <summary>
        /// Both the cumulative star rating and the strain of each skill.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MapsetVerifierStrainAndStarRating))]
        Both,
    }
}
