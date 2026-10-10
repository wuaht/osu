// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Creates the hit objects of hitsound difficulties, which play the hitsounds of all difficulties.
    /// </summary>
    public static class HitsoundDifficultyBuilder
    {
        /// <summary>
        /// How many milliseconds apart hitsounds of different difficulties may be to be considered the same.
        /// </summary>
        public const double MERGE_LENIENCY = 5;

        /// <summary>
        /// Combines the hitsounds of multiple difficulties.
        /// Hitsounds of <paramref name="primary"/> are always kept, and hitsounds of the other difficulties are only added at times without a hitsound.
        /// </summary>
        /// <param name="primary">The hitsounds of the most important difficulty, ordered by time.</param>
        /// <param name="others">The hitsounds of the other difficulties, ordered by importance.</param>
        /// <param name="leniency">How many milliseconds apart hitsounds may be to be considered the same.</param>
        /// <returns>The combined hitsounds, ordered by time.</returns>
        public static List<HitsoundColumnState> Merge(IReadOnlyList<HitsoundColumnState> primary, IEnumerable<IReadOnlyList<HitsoundColumnState>> others, double leniency = MERGE_LENIENCY)
        {
            var merged = primary.OrderBy(s => s.Time).ToList();

            foreach (var other in others)
            {
                foreach (var state in other)
                {
                    int index = indexOfFirstAtOrAfter(merged, state.Time);

                    bool covered = (index < merged.Count && merged[index].Time - state.Time <= leniency)
                                   || (index > 0 && state.Time - merged[index - 1].Time <= leniency);

                    if (!covered)
                        merged.Insert(index, state);
                }
            }

            return merged;
        }

        /// <summary>
        /// Replaces the hitnormal bank and volume which most hitsounds use with other ones, keeping hitsounds which differ from them as they are.
        /// </summary>
        /// <param name="states">The hitsounds.</param>
        /// <param name="oldBank">The hitnormal bank which is replaced.</param>
        /// <param name="oldVolume">The volume which is replaced.</param>
        /// <param name="newBank">The new hitnormal bank.</param>
        /// <param name="newVolume">The new volume.</param>
        public static List<HitsoundColumnState> WithBaseHitsounds(IReadOnlyList<HitsoundColumnState> states, string oldBank, int oldVolume, string newBank, int newVolume)
            => states.Select(s => s with
            {
                NormalBanks = s.NormalBanks.Select(b => b == oldBank ? newBank : b).Distinct().ToList(),
                Volume = s.Volume == oldVolume ? newVolume : s.Volume,
            }).ToList();

        /// <summary>
        /// Creates hit objects which play the given hitsounds, stacking objects where a single object can't play all hitsounds.
        /// </summary>
        /// <param name="states">The hitsounds, ordered by time.</param>
        /// <param name="ruleset">The ruleset of the beatmap.</param>
        /// <param name="difficulty">The difficulty of the beatmap.</param>
        /// <param name="controlPoints">The control points of the beatmap.</param>
        /// <returns>The hit objects, with defaults applied.</returns>
        public static List<HitObject> CreateHitObjects(IReadOnlyList<HitsoundColumnState> states, RulesetInfo ruleset, BeatmapDifficulty difficulty, ControlPointInfo controlPoints)
        {
            var placements = states.SelectMany(s => Enumerable.Range(0, s.RequiredTargetCount).Select(i => (s.Time, i))).ToList();
            var hitObjects = HitsoundObjectFactory.Create(ruleset, difficulty, placements).ToList();

            foreach (var h in hitObjects)
                h.ApplyDefaults(controlPoints, difficulty);

            HitsoundCopier.Copy(states, Array.Empty<HitsoundBodyState>(), HitsoundMap.Create(hitObjects, ruleset.OnlineID), new HitsoundCopyOptions
            {
                Leniency = HitsoundMap.COLUMN_MERGE_DISTANCE,
            });

            // the samples of nested objects are created from the samples of their parents.
            foreach (var h in hitObjects)
                h.ApplyDefaults(controlPoints, difficulty);

            return hitObjects;
        }

        private static int indexOfFirstAtOrAfter(List<HitsoundColumnState> states, double time)
        {
            int min = 0;
            int max = states.Count;

            while (min < max)
            {
                int mid = (min + max) / 2;

                if (states[mid].Time < time)
                    min = mid + 1;
                else
                    max = mid;
            }

            return min;
        }
    }
}
