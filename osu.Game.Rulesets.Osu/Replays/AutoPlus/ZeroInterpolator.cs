// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// Keeps the cursor in the top-left corner of the playfield.
    /// </summary>
    public class ZeroInterpolator : IAutoPlusInterpolator
    {
        public int StartTime { get; }

        public int EndTime { get; }

        public bool Ignored => false;

        public ZeroInterpolator(List<PointWithTime> points)
        {
            if (points.Count < 2)
                throw new ArgumentException("Cannot interpolate with less than 2 points.", nameof(points));

            StartTime = points[0].Time;
            EndTime = points[^1].Time;
        }

        public Vector2 PositionAtTime(int time) => Vector2.Zero;

        public Vector2 DerivativeAtStartTime() => Vector2.Zero;

        public Vector2 DerivativeAtEndTime() => Vector2.Zero;
    }
}
