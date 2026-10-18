// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Graphics.ColourEffects
{
    public enum ColourEffectMode
    {
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.ColourEffectModeRainbow))]
        Rainbow,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.ColourEffectModeGradient))]
        Gradient,
    }
}
