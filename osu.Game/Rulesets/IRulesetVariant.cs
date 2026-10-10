// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Concurrent;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets
{
    /// <summary>
    /// A ruleset which plays the beatmaps of another ruleset as they are, e.g. from a different perspective.
    /// </summary>
    /// <remarks>
    /// The beatmaps of the base ruleset aren't considered converted, and the configuration (and settings) of the base ruleset are used.
    /// </remarks>
    public interface IRulesetVariant
    {
        /// <summary>
        /// The short name of the ruleset whose beatmaps are played.
        /// </summary>
        string BaseRulesetShortName { get; }
    }

    public static class RulesetVariantExtensions
    {
        // creating ruleset instances is relatively expensive, and beatmaps are e.g. filtered in bulk.
        private static readonly ConcurrentDictionary<string, string?> base_ruleset_short_names = new ConcurrentDictionary<string, string?>();

        /// <summary>
        /// Returns the short name of the ruleset whose beatmaps the given ruleset plays as they are (see <see cref="IRulesetVariant"/>), or <c>null</c> if it isn't a variant.
        /// </summary>
        public static string? GetBaseRulesetShortName(this RulesetInfo ruleset)
            => base_ruleset_short_names.GetOrAdd(ruleset.ShortName, _ =>
            {
                try
                {
                    return (ruleset.CreateInstance() as IRulesetVariant)?.BaseRulesetShortName;
                }
                catch
                {
                    // e.g. an unavailable ruleset.
                    return null;
                }
            });

        /// <summary>
        /// Whether a beatmap belongs to the given ruleset, or to the ruleset it is a variant of (see <see cref="IRulesetVariant"/>).
        /// </summary>
        public static bool IsNativeTo(this IBeatmapInfo beatmap, RulesetInfo ruleset)
            => beatmap.Ruleset.ShortName == ruleset.ShortName || beatmap.Ruleset.ShortName == ruleset.GetBaseRulesetShortName();
    }
}
