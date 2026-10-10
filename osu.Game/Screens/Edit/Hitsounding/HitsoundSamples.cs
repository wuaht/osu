// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Reads and modifies the hitsounds of a sample list (the samples of a hit object, or of one node of a slider).
    /// </summary>
    /// <remarks>
    /// As in osu!stable, a sample list has one hitnormal, and all of its additions (whistle, finish and clap) use the same bank.
    /// The modifying methods keep it that way, and never modify the given list, but return a new one.
    /// </remarks>
    public static class HitsoundSamples
    {
        /// <summary>
        /// Whether the sample plays a file which is specified directly (keysounds in osu!mania), instead of a bank sample.
        /// The bank, additions and custom index of such samples can't be changed.
        /// </summary>
        public static bool IsFileSample(HitSampleInfo sample) => sample is ConvertHitObjectParser.FileHitSampleInfo;

        public static bool IsNormal(HitSampleInfo sample) => sample.Name == HitSampleInfo.HIT_NORMAL && !IsFileSample(sample);

        public static bool IsAddition(HitSampleInfo sample) => HitSampleInfo.ALL_ADDITIONS.Contains(sample.Name);

        /// <summary>
        /// Whether the samples play the sample of the given lane. For sub-lanes, the samples also have to use the custom sample index of the lane.
        /// </summary>
        public static bool Has(IEnumerable<HitSampleInfo> samples, HitsoundLane lane)
        {
            var list = samples as IList<HitSampleInfo> ?? samples.ToList();

            bool has = lane.IsAddition
                ? list.Any(s => s.Name == lane.Sample && s.Bank == lane.Bank)
                : list.Any(s => IsNormal(s) && s.Bank == lane.Bank);

            return has && (lane.CustomIndex == null || GetCustomIndex(list) == lane.CustomIndex);
        }

        public static string? GetNormalBank(IEnumerable<HitSampleInfo> samples) => samples.FirstOrDefault(IsNormal)?.Bank;

        public static string? GetAdditionBank(IEnumerable<HitSampleInfo> samples) => samples.FirstOrDefault(IsAddition)?.Bank;

        /// <summary>
        /// The volume of the samples, which is what the sample control point written for them in the beatmap file uses.
        /// </summary>
        public static int GetVolume(IList<HitSampleInfo> samples) => samples.Count == 0 ? 100 : samples.Max(s => s.Volume);

        /// <summary>
        /// The custom sample index of the samples: 0 for the skin's samples, 1 for the beatmap's default samples, or the suffix of the beatmap's samples.
        /// </summary>
        public static int GetCustomIndex(IEnumerable<HitSampleInfo> samples)
        {
            var sample = samples.FirstOrDefault(s => !IsFileSample(s));

            if (sample == null)
                return 0;

            if (sample.Suffix != null && int.TryParse(sample.Suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                return index;

            return sample.UseBeatmapSamples ? 1 : 0;
        }

        /// <summary>
        /// Sets the bank of the hitnormal.
        /// </summary>
        /// <param name="samples">The samples.</param>
        /// <param name="bank">The new bank.</param>
        /// <param name="autoAdditionsFollow">
        /// Whether additions whose bank follows the hitnormal's (<see cref="HitSampleInfo.EditorAutoBank"/>) should change their bank as well, like in osu!stable.
        /// If <c>false</c>, they keep their current bank and stop following the hitnormal's bank.
        /// </param>
        public static List<HitSampleInfo> WithNormalBank(IEnumerable<HitSampleInfo> samples, string bank, bool autoAdditionsFollow)
        {
            var list = samples.ToList();

            for (int i = 0; i < list.Count; i++)
            {
                if (IsNormal(list[i]))
                    list[i] = list[i].With(newBank: bank);
                else if (IsAddition(list[i]) && list[i].EditorAutoBank)
                    list[i] = autoAdditionsFollow ? list[i].With(newBank: bank) : list[i].With(newEditorAutoBank: false);
            }

            if (list.All(s => s.Name != HitSampleInfo.HIT_NORMAL))
                list.Insert(0, createSample(HitSampleInfo.HIT_NORMAL, bank, list, true));

            return list;
        }

        /// <summary>
        /// Adds an addition. As all additions use the same bank, the bank of other additions is changed to the given bank as well.
        /// </summary>
        public static List<HitSampleInfo> WithAddition(IEnumerable<HitSampleInfo> samples, string name, string bank)
        {
            var list = samples.Where(s => s.Name != name).ToList();
            bool autoBank = bank == GetNormalBank(list);

            for (int i = 0; i < list.Count; i++)
            {
                if (IsAddition(list[i]))
                    list[i] = list[i].With(newBank: bank, newEditorAutoBank: autoBank);
            }

            list.Add(createSample(name, bank, list, autoBank));
            return list;
        }

        public static List<HitSampleInfo> WithoutAddition(IEnumerable<HitSampleInfo> samples, string name) => samples.Where(s => s.Name != name).ToList();

        public static List<HitSampleInfo> WithoutAdditions(IEnumerable<HitSampleInfo> samples) => samples.Where(s => !IsAddition(s)).ToList();

        /// <summary>
        /// Replaces all additions with the given ones.
        /// </summary>
        public static List<HitSampleInfo> WithAdditions(IEnumerable<HitSampleInfo> samples, string bank, IEnumerable<string> names)
        {
            var list = WithoutAdditions(samples);

            foreach (string name in names.Distinct())
                list = WithAddition(list, name, bank);

            return list;
        }

        /// <summary>
        /// Sets the bank of all additions, like in osu!stable.
        /// </summary>
        /// <param name="samples">The samples.</param>
        /// <param name="bank">The new bank, or <c>null</c> to make the additions use the bank of the hitnormal.</param>
        public static List<HitSampleInfo> WithAdditionBank(IEnumerable<HitSampleInfo> samples, string? bank)
        {
            var list = samples.ToList();
            string normalBank = GetNormalBank(list) ?? HitSampleInfo.BANK_NORMAL;

            for (int i = 0; i < list.Count; i++)
            {
                if (!IsAddition(list[i]))
                    continue;

                list[i] = bank == null
                    ? list[i].With(newBank: normalBank, newEditorAutoBank: true)
                    : list[i].With(newBank: bank, newEditorAutoBank: false);
            }

            return list;
        }

        public static List<HitSampleInfo> WithVolume(IEnumerable<HitSampleInfo> samples, int volume)
            => samples.Select(s => s.Volume == volume ? s : s.With(newVolume: volume)).ToList();

        public static List<HitSampleInfo> WithCustomIndex(IEnumerable<HitSampleInfo> samples, int index)
            => samples.Select(s => IsFileSample(s)
                ? s
                : s.With(newSuffix: index >= 2 ? index.ToString(CultureInfo.InvariantCulture) : null, newUseBeatmapSamples: index >= 1)).ToList();

        /// <summary>
        /// Whether the two sample lists sound the same and are written to the beatmap file in the same way.
        /// </summary>
        public static bool AreEquivalent(IList<HitSampleInfo> first, IList<HitSampleInfo> second)
        {
            if (first.Count != second.Count)
                return false;

            return first.All(a => second.Any(b => a.Equals(b) && a.Volume == b.Volume && a.EditorAutoBank == b.EditorAutoBank));
        }

        private static HitSampleInfo createSample(string name, string bank, IList<HitSampleInfo> existing, bool autoBank)
        {
            int volume = GetVolume(existing);
            var template = existing.FirstOrDefault(s => !IsFileSample(s));

            switch (template)
            {
                // layered samples are hitnormals which are only played alongside additions, which a new sample isn't.
                case ConvertHitObjectParser.LegacyHitSampleInfo legacy:
                    return legacy.With(newName: name, newBank: bank, newVolume: volume, newEditorAutoBank: autoBank, newIsLayered: false);

                case not null:
                    return template.With(newName: name, newBank: bank, newVolume: volume, newEditorAutoBank: autoBank);

                default:
                    return new HitSampleInfo(name, bank, volume: volume, editorAutoBank: autoBank);
            }
        }
    }
}
