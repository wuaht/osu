// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FileSelectorStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.FileSelector";

        /// <summary>
        /// "Enter a path"
        /// </summary>
        public static LocalisableString EnterPath => new TranslatableString(getKey(@"enter_path"), @"Enter a path");

        /// <summary>
        /// "Paste or type the path of a folder and press Enter"
        /// </summary>
        public static LocalisableString EnterPathPlaceholder => new TranslatableString(getKey(@"enter_path_placeholder"), @"Paste or type the path of a folder and press Enter");

        /// <summary>
        /// "Quick access"
        /// </summary>
        public static LocalisableString QuickAccess => new TranslatableString(getKey(@"quick_access"), @"Quick access");

        /// <summary>
        /// "This PC"
        /// </summary>
        public static LocalisableString ThisPC => new TranslatableString(getKey(@"this_pc"), @"This PC");

        /// <summary>
        /// "Recently opened"
        /// </summary>
        public static LocalisableString RecentlyOpened => new TranslatableString(getKey(@"recently_opened"), @"Recently opened");

        /// <summary>
        /// "Desktop"
        /// </summary>
        public static LocalisableString Desktop => new TranslatableString(getKey(@"desktop"), @"Desktop");

        /// <summary>
        /// "Downloads"
        /// </summary>
        public static LocalisableString Downloads => new TranslatableString(getKey(@"downloads"), @"Downloads");

        /// <summary>
        /// "Documents"
        /// </summary>
        public static LocalisableString Documents => new TranslatableString(getKey(@"documents"), @"Documents");

        /// <summary>
        /// "Pictures"
        /// </summary>
        public static LocalisableString Pictures => new TranslatableString(getKey(@"pictures"), @"Pictures");

        /// <summary>
        /// "Music"
        /// </summary>
        public static LocalisableString Music => new TranslatableString(getKey(@"music"), @"Music");

        /// <summary>
        /// "Videos"
        /// </summary>
        public static LocalisableString Videos => new TranslatableString(getKey(@"videos"), @"Videos");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
