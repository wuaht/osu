// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Models;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Writes compressed backups of beatmap files before they are overwritten by an editor save.
    /// </summary>
    public static class EditorBeatmapBackup
    {
        /// <summary>
        /// The folder (relative to the user's data storage) which backups are written to.
        /// </summary>
        public const string BACKUPS_DIRECTORY = @"Backups";

        /// <summary>
        /// Writes a compressed copy of the currently stored file of <paramref name="beatmapInfo"/> to the backups folder.
        /// Does nothing if the beatmap has no stored file yet.
        /// </summary>
        /// <remarks>
        /// The previous file is read synchronously (so that it is guaranteed to be read before it gets replaced),
        /// while compression happens in the background. Failures are logged and never thrown, so that saving is never blocked by a failed backup.
        /// </remarks>
        /// <param name="storage">The user's data storage.</param>
        /// <param name="beatmapInfo">The beatmap which is about to be saved.</param>
        public static void CreateBackup(Storage storage, BeatmapInfo beatmapInfo)
        {
            try
            {
                string? filename = beatmapInfo.Path;
                RealmNamedFileUsage? file = filename == null ? null : beatmapInfo.BeatmapSet?.GetFile(filename);

                if (filename == null || file == null)
                    return;

                byte[] previousContent;

                using (var stream = storage.GetStorageForDirectory(@"files").GetStream(file.File.GetStoragePath()))
                {
                    if (stream == null)
                        return;

                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        previousContent = memoryStream.ToArray();
                    }
                }

                var backupStorage = storage.GetStorageForDirectory(BACKUPS_DIRECTORY);
                string backupFilename = getAvailableBackupFilename(backupStorage, filename);

                // create the file immediately (rather than in the background task) so that multiple backups within the same second can't pick the same name.
                var output = backupStorage.GetStream(backupFilename, FileAccess.Write, FileMode.CreateNew);

                Task.Run(() =>
                {
                    try
                    {
                        using (output)
                            XzCompressor.Compress(previousContent, output);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to write beatmap backup \"{backupFilename}\".");
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to create beatmap backup.");
            }
        }

        /// <summary>
        /// Creates a backup filename in the format "2026-10-07 21-16-41_AUTO__Artist - Title (Creator) [Difficulty].osu.xz".
        /// </summary>
        private static string getAvailableBackupFilename(Storage backupStorage, string beatmapFilename)
        {
            string timestamp = DateTime.Now.ToString(@"yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture);
            string name = beatmapFilename.GetValidFilename();

            string backupFilename = $@"{timestamp}_AUTO__{name}.xz";

            // in the rare case of multiple saves within the same second, keep all backups.
            for (int i = 2; backupStorage.Exists(backupFilename); i++)
                backupFilename = $@"{timestamp}-{i}_AUTO__{name}.xz";

            return backupFilename;
        }
    }
}
