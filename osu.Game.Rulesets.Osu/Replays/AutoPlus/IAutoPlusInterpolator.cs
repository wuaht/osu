// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// Generates the cursor positions of Auto+ between two or more key frames.
    /// </summary>
    public interface IAutoPlusInterpolator
    {
        int StartTime { get; }

        int EndTime { get; }

        /// <summary>
        /// Whether no frames should be generated for the time range of this interpolator.
        /// </summary>
        bool Ignored { get; }

        Vector2 PositionAtTime(int time);

        Vector2 DerivativeAtStartTime();

        Vector2 DerivativeAtEndTime();
    }

    public readonly struct PointWithTime : IComparable<PointWithTime>
    {
        public readonly int Time;
        public readonly Vector2 Position;

        public PointWithTime(int time, Vector2 position)
        {
            Time = time;
            Position = position;
        }

        public int CompareTo(PointWithTime other) => Time.CompareTo(other.Time);
    }

    public class AutoPlusInterpolatorComparer : IComparer<IAutoPlusInterpolator>
    {
        public static readonly AutoPlusInterpolatorComparer DEFAULT = new AutoPlusInterpolatorComparer();

        public int Compare(IAutoPlusInterpolator? x, IAutoPlusInterpolator? y) => x!.StartTime.CompareTo(y!.StartTime);
    }
}
