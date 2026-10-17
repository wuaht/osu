// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// How slider paths are treated when scaling a selection.
    /// </summary>
    public enum SliderScaleMode
    {
        /// <summary>
        /// Slider paths are scaled along with object positions, and their lengths are snapped to the beat divisor.
        /// </summary>
        [Description("Default")]
        Default,

        /// <summary>
        /// The paths of all selected sliders are scaled, adjusting their slider velocity so that their durations stay unchanged.
        /// </summary>
        [Description("Adjust SV")]
        AdjustSV,

        /// <summary>
        /// Slider paths are never scaled; only object positions are.
        /// </summary>
        [Description("Ignore paths")]
        IgnorePaths,
    }
}
