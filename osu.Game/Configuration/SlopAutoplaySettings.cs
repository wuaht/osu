// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Extensions;

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

        public static SlopAutoplayEngine EditorEngine => Config?.Get<SlopAutoplayEngine>(OsuSetting.SlopEditorAutoplayEngine) ?? SlopAutoplayEngine.Realistic;

        public static bool Get(OsuSetting setting, bool fallback) => Config?.Get<bool>(setting) ?? fallback;

        public static double Get(OsuSetting setting, double fallback) => Config?.Get<double>(setting) ?? fallback;

        public static int Get(OsuSetting setting, int fallback) => Config?.Get<int>(setting) ?? fallback;

        /// <summary>
        /// The name shown for the <see cref="SlopAutoplayEngine.Realistic"/> engine, which is also the username of its replays.
        /// </summary>
        public const string MIKU_NAME = "Miku";

        /// <summary>
        /// The username of replays created by the given engine.
        /// </summary>
        public static string GetUsername(SlopAutoplayEngine engine) => engine == SlopAutoplayEngine.Default ? "Autoplay" : engine.GetDescription();
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
        [Description(SlopAutoplaySettings.MIKU_NAME)]
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
