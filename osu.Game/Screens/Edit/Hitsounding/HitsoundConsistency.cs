// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Finds hitsounds which differ between difficulties, at the times at which both have hitsounds (like the hitsound consistency checks of Mapset Verifier).
    /// </summary>
    public static class HitsoundConsistency
    {
        /// <summary>
        /// How many milliseconds apart hitsounds may be to be compared.
        /// </summary>
        public const double LENIENCY = 5;

        /// <summary>
        /// Compares the hitsounds of another difficulty to the reference.
        /// Hitsounds without a counterpart aren't compared, as difficulties usually have different rhythms.
        /// </summary>
        /// <param name="reference">The hitsounds to compare to, ordered by time.</param>
        /// <param name="other">The hitsounds of the other difficulty, ordered by time.</param>
        /// <returns>The differences, ordered by time.</returns>
        public static List<HitsoundDifference> Compare(IReadOnlyList<HitsoundColumnState> reference, IReadOnlyList<HitsoundColumnState> other)
        {
            var differences = new List<HitsoundDifference>();
            int index = 0;

            foreach (var state in other)
            {
                while (index < reference.Count && reference[index].Time < state.Time - LENIENCY)
                    index++;

                if (index >= reference.Count || Math.Abs(reference[index].Time - state.Time) > LENIENCY)
                    continue;

                var expected = reference[index];
                var details = describe(expected, state);

                if (details.Count > 0)
                    differences.Add(new HitsoundDifference(state.Time, details));
            }

            return differences;
        }

        private static List<string> describe(HitsoundColumnState expected, HitsoundColumnState actual)
        {
            var details = new List<string>();

            if (!expected.NormalBanks.ToHashSet().SetEquals(actual.NormalBanks))
                details.Add($@"{string.Join('+', actual.NormalBanks)} hitnormal instead of {string.Join('+', expected.NormalBanks)}");

            var expectedAdditions = additions(expected);
            var actualAdditions = additions(actual);

            var missing = expectedAdditions.Except(actualAdditions).ToList();
            var extra = actualAdditions.Except(expectedAdditions).ToList();

            if (missing.Count > 0)
                details.Add($@"missing {string.Join(", ", missing)}");

            if (extra.Count > 0)
                details.Add($@"extra {string.Join(", ", extra)}");

            // muted hitsounds (e.g. slider ends) are usually muted on purpose.
            if (expected.Volume != actual.Volume && expected.Volume > DrawableHitObject.MINIMUM_SAMPLE_VOLUME && actual.Volume > DrawableHitObject.MINIMUM_SAMPLE_VOLUME)
                details.Add($@"{actual.Volume}% instead of {expected.Volume}%");

            if (expected.CustomIndex != actual.CustomIndex)
                details.Add($@"custom index {actual.CustomIndex} instead of {expected.CustomIndex}");

            return details;
        }

        private static HashSet<string> additions(HitsoundColumnState state)
            => state.AdditionGroups.SelectMany(g => g.Names.Select(n => $@"{g.Bank}-{n.Replace(@"hit", string.Empty)}")).ToHashSet();
    }

    /// <param name="Time">The time of the hitsound.</param>
    /// <param name="Details">How the hitsound differs, e.g. "missing soft-clap".</param>
    public sealed record HitsoundDifference(double Time, IReadOnlyList<string> Details);
}
