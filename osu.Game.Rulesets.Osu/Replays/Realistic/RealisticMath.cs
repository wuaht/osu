// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.Realistic
{
    public static class RealisticMath
    {
        /// <summary>
        /// Moves <paramref name="position"/> onto the edge of the given circle if it is outside of it.
        /// </summary>
        public static Vector2 MoveIntoCircle(Vector2 position, Vector2 centre, float radius)
        {
            Vector2 offset = position - centre;
            float distance = offset.Length;

            return distance <= radius ? position : centre + offset / distance * radius;
        }

        /// <summary>
        /// Returns a pseudo-random value in the range [0, 1) which is always the same for the same <paramref name="seed"/>.
        /// </summary>
        public static float Random(Vector2 seed)
        {
            double value = Math.Sin(seed.X * 12.9898 + seed.Y * 78.233) * 43758.5453123;
            return (float)(value - Math.Floor(value));
        }
    }
}
