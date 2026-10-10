// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The hitsounds of a beatmap, as <see cref="HitsoundColumn"/>s ordered by time.
    /// </summary>
    /// <remarks>
    /// This is a snapshot of the hit objects at the time of creation, and has to be recreated after they change.
    /// </remarks>
    public sealed class HitsoundMap
    {
        /// <summary>
        /// Hitsounds which are less than this many milliseconds apart are combined into the same <see cref="HitsoundColumn"/>.
        /// </summary>
        public const double COLUMN_MERGE_DISTANCE = 1;

        public readonly IReadOnlyList<HitsoundColumn> Columns;

        /// <summary>
        /// The slider bodies, ordered by start time. Only osu! sliders have bodies with hitsounds.
        /// </summary>
        public readonly IReadOnlyList<HitsoundBody> Bodies;

        /// <summary>
        /// A bit mask of the lanes played anywhere in the beatmap, indexed like <see cref="HitsoundLane.ALL"/>.
        /// </summary>
        public readonly int UsedLaneMask;

        private readonly Dictionary<int, HitsoundColumn> columnsByKey;

        private HitsoundMap(IReadOnlyList<HitsoundColumn> columns, IReadOnlyList<HitsoundBody> bodies)
        {
            Columns = columns;
            Bodies = bodies;
            columnsByKey = columns.ToDictionary(c => c.Key);

            foreach (var column in columns)
                UsedLaneMask |= column.LaneMask;

            for (int i = 0; i < HitsoundLane.ALL.Length; i++)
            {
                if (bodies.Any(b => b.Has(HitsoundLane.ALL[i])))
                    UsedLaneMask |= 1 << i;
            }
        }

        public static readonly HitsoundMap EMPTY = new HitsoundMap(Array.Empty<HitsoundColumn>(), Array.Empty<HitsoundBody>());

        /// <param name="hitObjects">The top-level hit objects of the beatmap.</param>
        /// <param name="rulesetOnlineId">The online ID of the beatmap's ruleset, which decides when objects with duration play their hitsound.</param>
        public static HitsoundMap Create(IEnumerable<HitObject> hitObjects, int rulesetOnlineId)
        {
            var targets = new List<HitsoundTarget>();
            var bodies = new List<HitsoundBody>();

            foreach (var hitObject in hitObjects)
            {
                if (hitObject is IHasRepeats repeats && repeats.NodeSamples != null)
                {
                    int spanCount = repeats.SpanCount();
                    double spanDuration = repeats.Duration / spanCount;

                    for (int i = 0; i <= spanCount; i++)
                    {
                        var kind = i == 0 ? HitsoundTargetKind.Head : i == spanCount ? HitsoundTargetKind.Tail : HitsoundTargetKind.Repeat;
                        targets.Add(new HitsoundTarget(hitObject, i, hitObject.StartTime + i * spanDuration, kind));
                    }

                    // only osu! plays hitsounds for slider bodies.
                    if (rulesetOnlineId == 0)
                        bodies.Add(new HitsoundBody(hitObject, hitObject.GetEndTime()));
                }
                else if (hitObject is IHasDuration duration)
                {
                    // spinners in osu! (and banana showers in osu!catch) play their hitsound at the end, objects in other rulesets at the start.
                    if (rulesetOnlineId == 0 || rulesetOnlineId == 2)
                        targets.Add(new HitsoundTarget(hitObject, null, duration.EndTime, HitsoundTargetKind.End));
                    else
                        targets.Add(new HitsoundTarget(hitObject, null, hitObject.StartTime, HitsoundTargetKind.HoldStart));
                }
                else
                    targets.Add(new HitsoundTarget(hitObject, null, hitObject.StartTime, HitsoundTargetKind.Object));
            }

            // keep the order of objects for targets at the same time, such that the first target of a column is stable.
            var sorted = targets.Select((t, i) => (target: t, index: i))
                                .OrderBy(t => t.target.Time)
                                .ThenBy(t => t.index)
                                .Select(t => t.target)
                                .ToList();

            var columns = new List<HitsoundColumn>();
            var group = new List<HitsoundTarget>();

            foreach (var target in sorted)
            {
                if (group.Count > 0 && target.Time - group[0].Time >= COLUMN_MERGE_DISTANCE)
                {
                    columns.Add(new HitsoundColumn(group));
                    group = new List<HitsoundTarget>();
                }

                group.Add(target);
            }

            if (group.Count > 0)
                columns.Add(new HitsoundColumn(group));

            return new HitsoundMap(columns, bodies.OrderBy(b => b.StartTime).ToList());
        }

        public HitsoundColumn? GetColumn(int key) => columnsByKey.GetValueOrDefault(key);

        /// <summary>
        /// Returns the index of the first column at or after the given time, or <see cref="Columns"/>.Count if there is none.
        /// </summary>
        public int IndexOfFirstColumnAtOrAfter(double time)
        {
            int min = 0;
            int max = Columns.Count;

            while (min < max)
            {
                int mid = (min + max) / 2;

                if (Columns[mid].Time < time)
                    min = mid + 1;
                else
                    max = mid;
            }

            return min;
        }

        /// <summary>
        /// Returns the columns from <paramref name="startTime"/> to <paramref name="endTime"/> (both inclusive).
        /// </summary>
        public IEnumerable<HitsoundColumn> GetColumnsInRange(double startTime, double endTime)
        {
            for (int i = IndexOfFirstColumnAtOrAfter(startTime); i < Columns.Count && Columns[i].Time <= endTime; i++)
                yield return Columns[i];
        }

        /// <summary>
        /// Returns the column closest to the given time, if it is at most <paramref name="leniency"/> milliseconds away.
        /// </summary>
        public HitsoundColumn? FindClosestColumn(double time, double leniency)
        {
            int index = IndexOfFirstColumnAtOrAfter(time);

            HitsoundColumn? closest = null;

            for (int i = Math.Max(0, index - 1); i <= Math.Min(Columns.Count - 1, index); i++)
            {
                double distance = Math.Abs(Columns[i].Time - time);

                if (distance <= leniency && (closest == null || distance < Math.Abs(closest.Time - time)))
                    closest = Columns[i];
            }

            return closest;
        }

        /// <summary>
        /// Returns the body which spans the given time.
        /// </summary>
        public HitsoundBody? FindBody(double time) => Bodies.LastOrDefault(b => b.StartTime <= time && time <= b.EndTime);

        /// <summary>
        /// Returns the body starting closest to the given time, if it is at most <paramref name="leniency"/> milliseconds away.
        /// </summary>
        public HitsoundBody? FindBodyStartingAt(double time, double leniency)
            => Bodies.Where(b => Math.Abs(b.StartTime - time) <= leniency).MinBy(b => Math.Abs(b.StartTime - time));

        public bool UsesLane(HitsoundLane lane) => (UsedLaneMask & HitsoundColumn.GetLaneBit(lane.Parent)) != 0;

        private readonly Dictionary<HitsoundLane, SortedSet<int>> usedCustomIndices = new Dictionary<HitsoundLane, SortedSet<int>>();

        /// <summary>
        /// The custom sample indices with which the sample of a lane is played anywhere in the beatmap.
        /// </summary>
        public IReadOnlySet<int> GetUsedCustomIndices(HitsoundLane lane)
        {
            lane = lane.Parent;

            if (usedCustomIndices.TryGetValue(lane, out var indices))
                return indices;

            indices = new SortedSet<int>();

            foreach (var column in Columns)
            {
                if (!column.Has(lane))
                    continue;

                foreach (var target in column.Targets)
                {
                    if (HitsoundSamples.Has(target.Samples, lane))
                        indices.Add(HitsoundSamples.GetCustomIndex(target.Samples));
                }
            }

            foreach (var body in Bodies)
            {
                if (body.Has(lane))
                    indices.Add(HitsoundSamples.GetCustomIndex(body.Samples));
            }

            return usedCustomIndices[lane] = indices;
        }
    }
}
