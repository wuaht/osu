// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// All hitsounds which play at the same time, e.g. the hitsounds of a slider end and of a circle stacked on it, or of an osu!mania chord.
    /// </summary>
    /// <remarks>
    /// This is a snapshot of the hitsounds at the time the containing <see cref="HitsoundMap"/> was created.
    /// </remarks>
    public sealed class HitsoundColumn
    {
        /// <summary>
        /// Identifies the column by its time rounded to whole milliseconds. Unique within a <see cref="HitsoundMap"/>.
        /// </summary>
        public readonly int Key;

        /// <summary>
        /// The time of the earliest target.
        /// </summary>
        public readonly double Time;

        public readonly IReadOnlyList<HitsoundTarget> Targets;

        /// <summary>
        /// The volume, which is the highest volume of all targets.
        /// </summary>
        public readonly int Volume;

        /// <summary>
        /// The custom sample index of the first target.
        /// </summary>
        public readonly int CustomIndex;

        /// <summary>
        /// Whether any target plays a sample file directly, whose hitsounds can't be edited.
        /// </summary>
        public readonly bool HasFileSamples;

        /// <summary>
        /// A bit mask of the lanes the column plays, indexed like <see cref="HitsoundLane.ALL"/>.
        /// </summary>
        public readonly int LaneMask;

        public HitsoundColumn(IReadOnlyList<HitsoundTarget> targets)
        {
            if (targets.Count == 0)
                throw new ArgumentException("A column needs at least one target.", nameof(targets));

            Targets = targets;
            Time = targets.Min(t => t.Time);
            Key = (int)Math.Floor(Time + 0.5);

            Volume = targets.Max(t => HitsoundSamples.GetVolume(t.Samples));
            CustomIndex = HitsoundSamples.GetCustomIndex(targets[0].Samples);
            HasFileSamples = targets.Any(t => t.Samples.Any(HitsoundSamples.IsFileSample));
            LaneMask = GetLaneMask(targets.SelectMany(t => t.Samples));
        }

        public bool Has(HitsoundLane lane)
        {
            // the custom sample index is per target, so sub-lanes are checked on the targets.
            if (lane.IsSubLane)
                return (LaneMask & GetLaneBit(lane.Parent)) != 0 && Targets.Any(t => HitsoundSamples.Has(t.Samples, lane));

            return (LaneMask & GetLaneBit(lane)) != 0;
        }

        /// <summary>
        /// The bit of a lane in <see cref="LaneMask"/>, or 0 for sub-lanes.
        /// </summary>
        public static int GetLaneBit(HitsoundLane lane)
        {
            int index = Array.IndexOf(HitsoundLane.ALL, lane);
            return index >= 0 ? 1 << index : 0;
        }

        /// <summary>
        /// The bank of the first target's hitnormal.
        /// </summary>
        public string? NormalBank => Targets.Select(t => HitsoundSamples.GetNormalBank(t.Samples)).FirstOrDefault(b => b != null);

        /// <summary>
        /// Creates a snapshot of the hitsounds, which stays valid after the hit objects change.
        /// </summary>
        public HitsoundColumnState GetState() => HitsoundColumnState.Create(Time, Targets.Select(t => t.Samples));

        public static int GetLaneMask(IEnumerable<HitSampleInfo> samples)
        {
            int mask = 0;
            var list = samples as IList<HitSampleInfo> ?? samples.ToList();

            for (int i = 0; i < HitsoundLane.ALL.Length; i++)
            {
                if (HitsoundSamples.Has(list, HitsoundLane.ALL[i]))
                    mask |= 1 << i;
            }

            return mask;
        }
    }
}
