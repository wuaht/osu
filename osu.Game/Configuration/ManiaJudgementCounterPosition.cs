// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Where the osu!mania judgement counter is displayed.
    /// </summary>
    public enum ManiaJudgementCounterPosition
    {
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.ManiaJudgementCounterDisabled))]
        Disabled,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.ManiaJudgementCounterBottomLeft))]
        BottomLeft,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.ManiaJudgementCounterStageLeft))]
        StageLeft,
    }
}
