// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Creates the simplest hit object of a ruleset (e.g. a circle in osu!), which hitsound difficulties consist of.
    /// </summary>
    /// <remarks>
    /// The objects are created by converting legacy circles with the ruleset's beatmap converter, which works for any ruleset that can convert beatmaps.
    /// </remarks>
    public static class HitsoundObjectFactory
    {
        public const int OSU_MANIA_RULESET_ID = 3;

        /// <summary>
        /// Creates one object at each of the given times.
        /// </summary>
        /// <param name="ruleset">The ruleset of the beatmap.</param>
        /// <param name="difficulty">The difficulty of the beatmap, which decides the key count in osu!mania.</param>
        /// <param name="placements">
        /// The times of the objects, and the number of objects already placed at that time.
        /// The latter is used to place objects at the same time in different osu!mania columns.
        /// </param>
        /// <returns>The objects, or an empty list if the ruleset can't create objects this way.</returns>
        public static IReadOnlyList<HitObject> Create(RulesetInfo ruleset, IBeatmapDifficultyInfo difficulty, IReadOnlyList<(double time, int stackIndex)> placements)
        {
            if (placements.Count == 0)
                return Array.Empty<HitObject>();

            int keyCount = Math.Max(1, (int)Math.Round(difficulty.CircleSize));

            var text = new StringBuilder();

            text.AppendLine(@"osu file format v14");
            text.AppendLine();
            text.AppendLine(@"[Difficulty]");
            text.AppendLine(FormattableString.Invariant($@"HPDrainRate:{difficulty.DrainRate}"));
            text.AppendLine(FormattableString.Invariant($@"CircleSize:{difficulty.CircleSize}"));
            text.AppendLine(FormattableString.Invariant($@"OverallDifficulty:{difficulty.OverallDifficulty}"));
            text.AppendLine(FormattableString.Invariant($@"ApproachRate:{difficulty.ApproachRate}"));
            text.AppendLine(FormattableString.Invariant($@"SliderMultiplier:{difficulty.SliderMultiplier}"));
            text.AppendLine(FormattableString.Invariant($@"SliderTickRate:{difficulty.SliderTickRate}"));
            text.AppendLine();
            text.AppendLine(@"[HitObjects]");

            foreach (var (time, stackIndex) in placements)
            {
                // objects at the same time are placed in different columns in osu!mania, and on top of each other otherwise.
                int x = ruleset.OnlineID == OSU_MANIA_RULESET_ID
                    ? (int)((stackIndex % keyCount + 0.5) * 512 / keyCount)
                    : 256;

                text.AppendLine(FormattableString.Invariant($@"{x},192,{time.ToString("R", CultureInfo.InvariantCulture)},1,0,0:0:0:0:"));
            }

            Beatmap decoded;

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString())))
            using (var reader = new LineBufferedReader(stream))
                decoded = new LegacyBeatmapDecoder().Decode(reader);

            decoded.BeatmapInfo.Ruleset = ruleset;

            var converter = ruleset.CreateInstance().CreateBeatmapConverter(decoded);

            if (!converter.CanConvert())
                return Array.Empty<HitObject>();

            return converter.Convert().HitObjects.ToList();
        }
    }
}
