// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// A snapshot of the hitsounds of a <see cref="HitsoundColumn"/>, independent of any hit objects.
    /// Used to copy hitsounds between difficulties, and as the content of the hitsound clipboard.
    /// </summary>
    /// <param name="Time">The time of the hitsounds.</param>
    /// <param name="NormalBanks">The distinct banks of the hitnormals, in the order of the targets.</param>
    /// <param name="AdditionGroups">The additions grouped by bank, with the largest group first.</param>
    /// <param name="Volume">The volume.</param>
    /// <param name="CustomIndex">The custom sample index.</param>
    public sealed record HitsoundColumnState(double Time, IReadOnlyList<string> NormalBanks, IReadOnlyList<HitsoundAdditionGroup> AdditionGroups, int Volume, int CustomIndex)
    {
        public HitsoundColumnState WithTime(double time) => this with { Time = time };

        /// <summary>
        /// The number of hit objects needed to play all hitsounds, as each hit object only plays one hitnormal and one bank of additions.
        /// </summary>
        public int RequiredTargetCount => Math.Max(1, Math.Max(NormalBanks.Count, AdditionGroups.Count));

        public static HitsoundColumnState Create(double time, IEnumerable<IList<HitSampleInfo>> sampleLists)
        {
            var lists = sampleLists.ToList();

            var normalBanks = lists.Select(HitsoundSamples.GetNormalBank).OfType<string>().Distinct().ToList();

            var additionGroups = lists.SelectMany(l => l.Where(HitsoundSamples.IsAddition))
                                      .GroupBy(s => s.Bank)
                                      .Select(g => new HitsoundAdditionGroup(g.Key, g.Select(s => s.Name).Distinct().ToList()))
                                      // order by size, keeping the order of appearance for groups of the same size.
                                      .OrderByDescending(g => g.Names.Count)
                                      .ToList();

            return new HitsoundColumnState(
                time,
                normalBanks,
                additionGroups,
                lists.Count == 0 ? 100 : lists.Max(HitsoundSamples.GetVolume),
                lists.Count == 0 ? 0 : HitsoundSamples.GetCustomIndex(lists[0]));
        }
    }

    /// <summary>
    /// Additions which play from the same bank.
    /// </summary>
    public sealed record HitsoundAdditionGroup(string Bank, IReadOnlyList<string> Names);
}
