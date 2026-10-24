// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Screens.Select.Filter
{
    public enum SongSelectSortDirection
    {
        /// <summary>
        /// Beatmaps are listed in the natural order of the selected <see cref="SortMode"/>.
        /// </summary>
        [Description("Top to bottom")]
        TopToBottom,

        /// <summary>
        /// The order of the selected <see cref="SortMode"/> is reversed.
        /// </summary>
        [Description("Bottom to top")]
        BottomToTop,
    }
}
