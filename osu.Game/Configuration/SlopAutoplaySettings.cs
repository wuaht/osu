// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Configuration
{
    /// <summary>
    /// Gives replay generators (which have no access to dependency injection) access to the autoplay settings.
    /// </summary>
    public static class SlopAutoplaySettings
    {
        /// <summary>
        /// The game's config manager, or null if none is available (e.g. in tests).
        /// </summary>
        public static OsuConfigManager? Config { get; set; }

        public static SlopAutoplayEngine Engine => Config?.Get<SlopAutoplayEngine>(OsuSetting.SlopAutoplayEngine) ?? SlopAutoplayEngine.Default;

        public static bool Get(OsuSetting setting, bool fallback) => Config?.Get<bool>(setting) ?? fallback;

        public static double Get(OsuSetting setting, double fallback) => Config?.Get<double>(setting) ?? fallback;

        public static int Get(OsuSetting setting, int fallback) => Config?.Get<int>(setting) ?? fallback;
    }

    public enum SlopAutoplayEngine
    {
        Default,
        Pippi,

        [Description("Auto+")]
        AutoPlus,

        /// <summary>
        /// Moves the cursor like a player would, following a spring towards each target and cutting corners on sliders.
        /// </summary>
        Realistic,
    }

    public enum AutoPlusTagSplitMode
    {
        Combo,

        [Description("Combo colour")]
        Colour,

        Object,
    }

    public enum AutoPlusTagColourSource
    {
        [Description("Map colours")]
        MapColours,

        [Description("Skin colours")]
        SkinColours,

        Rainbow,
    }
}
