// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// Removes kiai time from gameplay, which disables all kiai effects.
    /// </summary>
    public static class GameplayKiaiRemoval
    {
        /// <summary>
        /// Disables kiai on all effect points of the given (gameplay-local) control points.
        /// </summary>
        public static void RemoveKiai(ControlPointInfo controlPoints)
        {
            foreach (var effect in controlPoints.EffectPoints)
                effect.KiaiMode = false;
        }

        /// <summary>
        /// Disables kiai on the given hit objects and all of their nested hit objects.
        /// </summary>
        public static void RemoveKiai(IEnumerable<HitObject> hitObjects)
        {
            foreach (var hitObject in hitObjects)
            {
                hitObject.Kiai = false;
                RemoveKiai(hitObject.NestedHitObjects);
            }
        }
    }
}
