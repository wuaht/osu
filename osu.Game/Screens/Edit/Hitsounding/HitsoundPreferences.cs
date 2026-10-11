// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Configuration;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Preferences of the hitsound editor which belong to difficulties or beatmap sets, stored in the config as lists of "id=value" entries.
    /// </summary>
    public static class HitsoundPreferences
    {
        /// <summary>
        /// Whether a difficulty is the hitsound difficulty of its beatmap set: if hitsound difficulty mode was turned on or off for it in the hitsound editor,
        /// or otherwise if it's named like one (see <see cref="HitsoundEditor.IsHitsoundDifficultyName"/>).
        /// </summary>
        public static bool IsHitsoundDifficulty(OsuConfigManager? config, BeatmapInfo difficulty)
        {
            if (config != null && get(config, OsuSetting.SlopHitsoundDifficultyModes, difficulty.ID) is string mode)
                return mode == @"1";

            return HitsoundEditor.IsHitsoundDifficultyName(difficulty.DifficultyName);
        }

        public static void SetHitsoundDifficulty(OsuConfigManager config, BeatmapInfo difficulty, bool value)
            => set(config, OsuSetting.SlopHitsoundDifficultyModes, difficulty.ID, value == HitsoundEditor.IsHitsoundDifficultyName(difficulty.DifficultyName) ? null : value ? @"1" : @"0");

        /// <summary>
        /// The hitsound difficulty of a beatmap set (see <see cref="IsHitsoundDifficulty"/>), if it has one.
        /// </summary>
        public static BeatmapInfo? FindHitsoundDifficulty(OsuConfigManager? config, IEnumerable<BeatmapInfo> difficulties)
            => difficulties.Where(b => IsHitsoundDifficulty(config, b)).OrderBy(b => b.DifficultyName).FirstOrDefault();

        /// <summary>
        /// Whether a difficulty receives the hitsounds of the hitsound difficulty when copying them (see <see cref="HitsoundDifficultySync"/>).
        /// </summary>
        public static bool ReceivesHitsounds(OsuConfigManager? config, BeatmapInfo difficulty)
        {
            if (config != null && get(config, OsuSetting.SlopHitsoundSyncExcludedDifficulties, difficulty.ID) != null)
                return false;

            return !IsHitsoundDifficulty(config, difficulty);
        }

        public static void SetReceivesHitsounds(OsuConfigManager config, BeatmapInfo difficulty, bool value)
            => set(config, OsuSetting.SlopHitsoundSyncExcludedDifficulties, difficulty.ID, value ? null : @"1");

        /// <summary>
        /// The bank of the hitnormals which are played along with new additions in a beatmap set, or <c>null</c> to use the bank which most hitsounds use.
        /// </summary>
        public static string? GetDefaultNormalBank(OsuConfigManager? config, BeatmapSetInfo? beatmapSet)
            => config == null || beatmapSet == null ? null : get(config, OsuSetting.SlopHitsoundDefaultNormalBanks, beatmapSet.ID);

        public static void SetDefaultNormalBank(OsuConfigManager config, BeatmapSetInfo beatmapSet, string? bank)
            => set(config, OsuSetting.SlopHitsoundDefaultNormalBanks, beatmapSet.ID, bank);

        private static string? get(OsuConfigManager config, OsuSetting setting, Guid id) => parse(config.Get<string>(setting)).GetValueOrDefault(id.ToString());

        private static void set(OsuConfigManager config, OsuSetting setting, Guid id, string? value)
        {
            var entries = parse(config.Get<string>(setting));

            if (value == null)
                entries.Remove(id.ToString());
            else
                entries[id.ToString()] = value;

            config.SetValue(setting, string.Join(';', entries.Select(e => $@"{e.Key}={e.Value}")));
        }

        private static Dictionary<string, string> parse(string value)
        {
            var entries = new Dictionary<string, string>();

            foreach (string entry in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = entry.IndexOf('=');

                if (separator > 0)
                    entries[entry[..separator]] = entry[(separator + 1)..];
            }

            return entries;
        }
    }
}
