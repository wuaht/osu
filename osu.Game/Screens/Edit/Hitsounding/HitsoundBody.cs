// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The body of an osu! slider, which plays "sliderslide" from the bank of its hitnormal, and "sliderwhistle" if it has a whistle.
    /// </summary>
    /// <remarks>
    /// This is a snapshot of the hitsounds at the time the containing <see cref="HitsoundMap"/> was created.
    /// </remarks>
    public sealed class HitsoundBody
    {
        public readonly HitObject HitObject;

        public readonly double StartTime;

        public readonly double EndTime;

        public readonly int Key;

        public readonly string? NormalBank;

        /// <summary>
        /// The bank of the whistle, or <c>null</c> if the body has no whistle.
        /// </summary>
        public readonly string? WhistleBank;

        public readonly int Volume;

        public HitsoundBody(HitObject hitObject, double endTime)
        {
            HitObject = hitObject;
            StartTime = hitObject.StartTime;
            EndTime = endTime;
            Key = (int)Math.Floor(StartTime + 0.5);

            var samples = hitObject.Samples;
            NormalBank = HitsoundSamples.GetNormalBank(samples);
            WhistleBank = samples.FirstOrDefault(s => s.Name == HitSampleInfo.HIT_WHISTLE)?.Bank;
            Volume = HitsoundSamples.GetVolume(samples);
        }

        /// <summary>
        /// Creates a snapshot of the hitsounds, which stays valid after the hit object changes.
        /// </summary>
        public HitsoundBodyState GetState() => new HitsoundBodyState(StartTime, NormalBank, WhistleBank, Volume, HitsoundSamples.GetCustomIndex(Samples));

        public IList<HitSampleInfo> Samples => HitObject.Samples;

        public void SetSamples(IEnumerable<HitSampleInfo> samples) => HitObject.Samples = samples.ToList();

        /// <summary>
        /// Whether the body plays the sample of the given lane.
        /// Only hitnormal lanes ("sliderslide") and whistle lanes ("sliderwhistle") can be played by a body.
        /// </summary>
        public bool Has(HitsoundLane lane)
        {
            if (lane.CustomIndex != null && HitsoundSamples.GetCustomIndex(Samples) != lane.CustomIndex)
                return false;

            switch (lane.Sample)
            {
                case HitSampleInfo.HIT_NORMAL:
                    return lane.Bank == NormalBank;

                case HitSampleInfo.HIT_WHISTLE:
                    return lane.Bank == WhistleBank;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether a body can play the sample of the given lane.
        /// </summary>
        public static bool SupportsLane(HitsoundLane lane) => lane.Sample == HitSampleInfo.HIT_NORMAL || lane.Sample == HitSampleInfo.HIT_WHISTLE;
    }
}
