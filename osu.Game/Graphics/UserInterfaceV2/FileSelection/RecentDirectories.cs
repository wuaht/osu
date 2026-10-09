// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Configuration;

namespace osu.Game.Graphics.UserInterfaceV2.FileSelection
{
    /// <summary>
    /// The directories which files were recently selected in, which are offered by file selectors.
    /// </summary>
    public static class RecentDirectories
    {
        /// <summary>
        /// Separates the directories in the setting. Not allowed in paths on Windows, and very uncommon elsewhere.
        /// </summary>
        public const char SEPARATOR = '|';

        /// <summary>
        /// The maximum number of directories which are remembered.
        /// </summary>
        public const int MAX_COUNT = 6;

        /// <summary>
        /// Returns the recent directories which still exist, most recent first.
        /// </summary>
        public static IReadOnlyList<DirectoryInfo> Get(string setting) => Parse(setting).Where(d => d.Exists).ToList();

        /// <summary>
        /// Returns the setting with the given directory added as the most recent one.
        /// </summary>
        public static string Add(string setting, DirectoryInfo directory)
        {
            string path = Path.TrimEndingDirectorySeparator(directory.FullName);

            var paths = Parse(setting).Select(d => Path.TrimEndingDirectorySeparator(d.FullName))
                                      .Where(p => !string.Equals(p, path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                                      .Prepend(path)
                                      .Take(MAX_COUNT);

            return string.Join(SEPARATOR, paths);
        }

        /// <summary>
        /// Adds a directory to the setting of the given config.
        /// </summary>
        public static void Add(OsuConfigManager config, DirectoryInfo directory)
        {
            Bindable<string> setting = config.GetBindable<string>(OsuSetting.SlopFileSelectorRecentDirectories);
            setting.Value = Add(setting.Value, directory);
        }

        public static IEnumerable<DirectoryInfo> Parse(string setting)
        {
            foreach (string path in setting.Split(SEPARATOR, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                DirectoryInfo? directory;

                try
                {
                    directory = new DirectoryInfo(path);
                }
                catch (Exception e) when (e is ArgumentException or PathTooLongException or NotSupportedException or System.Security.SecurityException)
                {
                    continue;
                }

                yield return directory;
            }
        }
    }
}
