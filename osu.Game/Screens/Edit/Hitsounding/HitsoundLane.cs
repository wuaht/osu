// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Audio;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// A lane of the hitsound editor, representing one sample (e.g. "soft-hitwhistle").
    /// </summary>
    /// <param name="Bank">The bank of the sample.</param>
    /// <param name="Sample">The name of the sample.</param>
    /// <param name="CustomIndex">
    /// The custom sample index, if this is a sub-lane which only represents the sample of one custom sample set (e.g. "soft-hitwhistle2"),
    /// or <c>null</c> if it represents the sample of all custom sample sets.
    /// </param>
    public readonly record struct HitsoundLane(string Bank, string Sample, int? CustomIndex = null)
    {
        /// <summary>
        /// The banks in the order they are displayed.
        /// </summary>
        public static readonly string[] BANKS = [HitSampleInfo.BANK_NORMAL, HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_DRUM];

        /// <summary>
        /// The samples in the order they are displayed within a bank.
        /// </summary>
        public static readonly string[] SAMPLES = [HitSampleInfo.HIT_NORMAL, HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_FINISH, HitSampleInfo.HIT_CLAP];

        /// <summary>
        /// All lanes in the order they are displayed.
        /// </summary>
        public static readonly HitsoundLane[] ALL = BANKS.SelectMany(bank => SAMPLES.Select(sample => new HitsoundLane(bank, sample))).ToArray();

        /// <summary>
        /// Whether this lane is an addition (whistle, finish or clap), as opposed to the hitnormal which every hitsound has.
        /// </summary>
        public bool IsAddition => Sample != HitSampleInfo.HIT_NORMAL;

        /// <summary>
        /// Whether this lane only represents the sample of one custom sample set.
        /// </summary>
        public bool IsSubLane => CustomIndex != null;

        /// <summary>
        /// The lane which represents the sample of all custom sample sets.
        /// </summary>
        public HitsoundLane Parent => this with { CustomIndex = null };

        /// <summary>
        /// The sub-lane which only represents the sample of the given custom sample set.
        /// </summary>
        public HitsoundLane WithCustomIndex(int customIndex) => this with { CustomIndex = customIndex };

        public override string ToString() => CustomIndex is int index ? $@"{Bank}-{Sample} ({index})" : $@"{Bank}-{Sample}";

        public static Colour4 GetBankColour(string bank)
        {
            switch (bank)
            {
                case HitSampleInfo.BANK_SOFT:
                    return Colour4.FromHex(@"4fd6c8");

                case HitSampleInfo.BANK_DRUM:
                    return Colour4.FromHex(@"f7c744");

                default:
                    return Colour4.FromHex(@"ff6fa8");
            }
        }
    }
}
