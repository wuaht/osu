// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Screens.Edit.Components.Menus
{
    public class DifficultyMenuItem : StatefulMenuItem<bool>
    {
        public BeatmapInfo BeatmapInfo { get; }

        public DifficultyMenuItem(BeatmapInfo beatmapInfo, bool selected, Action<BeatmapInfo> difficultyChangeFunc, bool showStars = false)
            : base(GetDisplayText(beatmapInfo, showStars), null)
        {
            BeatmapInfo = beatmapInfo;
            State.Value = selected;

            if (!selected)
                Action.Value = () => difficultyChangeFunc.Invoke(beatmapInfo);
        }

        public override IconUsage? GetIconForState(bool state) => state ? FontAwesome.Solid.Check : null;

        /// <summary>
        /// Returns the text displayed for a difficulty, optionally prefixed with its star rating and ruleset.
        /// </summary>
        public static string GetDisplayText(BeatmapInfo beatmapInfo, bool showStars)
        {
            string name = string.IsNullOrEmpty(beatmapInfo.DifficultyName) ? "(unnamed)" : beatmapInfo.DifficultyName;

            if (!showStars)
                return name;

            string mode;

            switch (beatmapInfo.Ruleset.OnlineID)
            {
                case 0:
                    mode = "O";
                    break;

                case 1:
                    mode = "T";
                    break;

                case 2:
                    mode = "C";
                    break;

                case 3:
                    mode = "M";
                    break;

                default:
                    mode = "?";
                    break;
            }

            return FormattableString.Invariant($"({Math.Max(0, beatmapInfo.StarRating):F2}* {mode}) {name}");
        }
    }
}
