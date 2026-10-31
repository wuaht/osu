// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Objects.Types
{
    /// <summary>
    /// A hit object whose volume can change while it is active (e.g. the spinning sound of a spinner).
    /// </summary>
    /// <remarks>
    /// In the legacy format these are the sample control points within the object.
    /// </remarks>
    public interface IHasVolumeChanges : IHasDuration
    {
        /// <summary>
        /// The volume changes during this object, ordered by offset and starting at offset 0.
        /// Empty if the volume of the object's samples applies throughout.
        /// </summary>
        IList<VolumeChange> VolumeChanges { get; set; }
    }

    /// <summary>
    /// A volume which applies from an offset (relative to the start time of a <see cref="IHasVolumeChanges"/>) onwards.
    /// </summary>
    public record struct VolumeChange(double Offset, int Volume);

    public static class HasVolumeChangesExtensions
    {
        /// <summary>
        /// Returns the volume of a hit object at the given time, or null if the volume of its samples applies.
        /// </summary>
        public static int? GetVolumeAt(this HitObject hitObject, double time)
        {
            if (hitObject is not IHasVolumeChanges hasVolumeChanges || time >= hasVolumeChanges.EndTime)
                return null;

            int? volume = null;

            foreach (var change in hasVolumeChanges.VolumeChanges)
            {
                if (hitObject.StartTime + change.Offset > time)
                    break;

                volume = change.Volume;
            }

            return volume;
        }
    }
}
