// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Which hitsounds are commonly used for which sounds of a song, following common hitsounding guides.
    /// </summary>
    public static class HitsoundSoundGuide
    {
        /// <param name="Sound">The sound of the song.</param>
        /// <param name="Lanes">The hitsounds which are commonly used for it, which are often layered.</param>
        /// <param name="Alternatives">Hitsounds which are used for it instead by some hitsounding styles.</param>
        public sealed record Entry(LocalisableString Sound, IReadOnlyList<HitsoundLane> Lanes, IReadOnlyList<HitsoundLane> Alternatives);

        public static readonly IReadOnlyList<Entry> ENTRIES = new[]
        {
            entry(SlopHitsoundEditorStrings.SoundCymbalCrash, new[] { lane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_FINISH), lane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_FINISH) }),
            entry(SlopHitsoundEditorStrings.SoundSnare, new[] { lane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL), lane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_CLAP) }),
            entry(SlopHitsoundEditorStrings.SoundKick, new[] { lane(HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_NORMAL) }, new[] { lane(HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_FINISH) }),
            entry(SlopHitsoundEditorStrings.SoundMelody, new[] { lane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_WHISTLE), lane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_WHISTLE) }),
            entry(SlopHitsoundEditorStrings.SoundClaps, new[] { lane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP) }),
            entry(SlopHitsoundEditorStrings.SoundHiHat, new[] { lane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_NORMAL), lane(HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_WHISTLE) }),
            entry(SlopHitsoundEditorStrings.SoundCymbalRide, new[] { lane(HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_WHISTLE) }),
            entry(SlopHitsoundEditorStrings.SoundTom, new[] { lane(HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_CLAP), lane(HitSampleInfo.BANK_DRUM, HitSampleInfo.HIT_FINISH) }),
            entry(SlopHitsoundEditorStrings.SoundRim, new[] { lane(HitSampleInfo.BANK_SOFT, HitSampleInfo.HIT_CLAP) }),
            entry(SlopHitsoundEditorStrings.SoundDrumsticks, new[] { lane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL) }),
            entry(SlopHitsoundEditorStrings.SoundCowbell, new[] { lane(HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_NORMAL) }),
        };

        /// <summary>
        /// The sounds for which a hitsound is commonly used.
        /// </summary>
        public static IEnumerable<LocalisableString> GetSoundsFor(HitsoundLane lane)
            => ENTRIES.Where(e => e.Lanes.Contains(lane) || e.Alternatives.Contains(lane)).Select(e => e.Sound);

        /// <summary>
        /// The name of a hitsound, like "Soft Finish".
        /// </summary>
        public static string GetLaneName(HitsoundLane lane)
            => $@"{HitsoundLaneHeaders.GetBankName(lane.Bank)} {HitsoundLaneHeaders.GetSampleName(lane.Sample)}";

        /// <summary>
        /// The whole guide as text, with a line for each sound.
        /// </summary>
        public static string Describe()
        {
            var lines = ENTRIES.Select(e =>
            {
                string line = $@"{e.Sound}: {string.Join(@" + ", e.Lanes.Select(GetLaneName))}";

                if (e.Alternatives.Count > 0)
                    line += $@" ({SlopHitsoundEditorStrings.SoundGuideAlternatively(string.Join(@", ", e.Alternatives.Select(GetLaneName)))})";

                return line;
            });

            return $@"{SlopHitsoundEditorStrings.SoundGuideHeader}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
        }

        private static Entry entry(LocalisableString sound, HitsoundLane[] lanes, HitsoundLane[]? alternatives = null)
            => new Entry(sound, lanes, alternatives ?? Array.Empty<HitsoundLane>());

        private static HitsoundLane lane(string bank, string sample) => new HitsoundLane(bank, sample);
    }
}
