// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Extensions;
using osu.Game.Localisation;
using osu.Game.Rulesets.Objects.Legacy;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Describes hitsounds in text, for tooltips.
    /// </summary>
    public static class HitsoundColumnDescription
    {
        public static LocalisableString Describe(HitsoundColumn column)
        {
            var description = LocalisableString.Interpolate($"{column.Time.ToEditorFormattedString()}");

            foreach (var target in column.Targets)
            {
                string samples = string.Join(@", ", target.Samples.Select(describeSample));
                description = LocalisableString.Interpolate($"{description}\n{getKindName(target.Kind)}: {samples}");
            }

            return LocalisableString.Interpolate(
                $"{description}\n{SlopHitsoundEditorStrings.VolumeValue(column.Volume)} · {SlopHitsoundEditorStrings.CustomIndexValue(column.CustomIndex)}");
        }

        private static string describeSample(HitSampleInfo sample)
        {
            if (sample is ConvertHitObjectParser.FileHitSampleInfo fileSample)
                return fileSample.Filename;

            return $@"{sample.Bank}-{sample.Name}";
        }

        private static LocalisableString getKindName(HitsoundTargetKind kind)
        {
            switch (kind)
            {
                case HitsoundTargetKind.Object:
                    return SlopHitsoundEditorStrings.KindObject;

                case HitsoundTargetKind.Head:
                    return SlopHitsoundEditorStrings.KindHead;

                case HitsoundTargetKind.Repeat:
                    return SlopHitsoundEditorStrings.KindRepeat;

                case HitsoundTargetKind.Tail:
                    return SlopHitsoundEditorStrings.KindTail;

                case HitsoundTargetKind.HoldStart:
                    return SlopHitsoundEditorStrings.KindHoldStart;

                case HitsoundTargetKind.End:
                    return SlopHitsoundEditorStrings.KindEnd;

                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
