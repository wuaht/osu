// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The part of a hit object which plays a hitsound at a single point in time.
    /// </summary>
    public sealed class HitsoundTarget
    {
        public readonly HitObject HitObject;

        /// <summary>
        /// The index into <see cref="IHasRepeats.NodeSamples"/>, or <c>null</c> if the hitsound is defined by <see cref="Rulesets.Objects.HitObject.Samples"/>.
        /// </summary>
        public readonly int? NodeIndex;

        /// <summary>
        /// The time at which the hitsound plays.
        /// </summary>
        public readonly double Time;

        public readonly HitsoundTargetKind Kind;

        public HitsoundTarget(HitObject hitObject, int? nodeIndex, double time, HitsoundTargetKind kind)
        {
            HitObject = hitObject;
            NodeIndex = nodeIndex;
            Time = time;
            Kind = kind;
        }

        public IList<HitSampleInfo> Samples
        {
            get
            {
                if (NodeIndex is int index)
                {
                    var nodeSamples = ((IHasRepeats)HitObject).NodeSamples;
                    return index < nodeSamples.Count ? nodeSamples[index] : HitObject.Samples;
                }

                return HitObject.Samples;
            }
        }

        /// <summary>
        /// Replaces the samples.
        /// </summary>
        /// <remarks>
        /// The samples are always replaced with a new list, as sample lists may be shared between nodes or with <see cref="Rulesets.Objects.HitObject.Samples"/>.
        /// </remarks>
        public void SetSamples(IEnumerable<HitSampleInfo> samples)
        {
            var list = samples.ToList();

            if (NodeIndex is int index)
            {
                var nodeSamples = ((IHasRepeats)HitObject).NodeSamples;

                // nodes without samples of their own use the samples of the hit object.
                while (nodeSamples.Count <= index)
                    nodeSamples.Add(HitObject.Samples.ToList());

                nodeSamples[index] = list;
            }
            else
                HitObject.Samples = list;
        }
    }

    public enum HitsoundTargetKind
    {
        /// <summary>
        /// A hit object without duration.
        /// </summary>
        Object,

        /// <summary>
        /// The start of an object with repeats (e.g. a slider head).
        /// </summary>
        Head,

        Repeat,

        /// <summary>
        /// The end of an object with repeats (e.g. a slider end).
        /// </summary>
        Tail,

        /// <summary>
        /// An object with duration which plays its hitsound at its start (e.g. an osu!mania hold note).
        /// </summary>
        HoldStart,

        /// <summary>
        /// An object with duration which plays its hitsound at its end (e.g. a spinner).
        /// </summary>
        End,
    }
}
