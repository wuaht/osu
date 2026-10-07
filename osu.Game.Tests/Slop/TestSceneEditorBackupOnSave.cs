// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Screens.Edit;
using osu.Game.Tests.Visual;
using SharpCompress.Compressors.Xz;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneEditorBackupOnSave : EditorSavingTestScene
    {
        private static readonly Regex backup_filename = new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2}(-\d+)?_AUTO__(?<name>.+\.osu)\.xz$");

        private Storage storage => (Storage)Game.Dependencies.Get(typeof(Storage));

        private Storage backupStorage => storage.GetStorageForDirectory(EditorBeatmapBackup.BACKUPS_DIRECTORY);

        [Test]
        public void TestBackupContainsPreviousFile()
        {
            string[] existingBackups = null!;
            byte[] previousContent = null!;
            string previousFilename = null!;

            AddStep("enable backups", () => Game.LocalConfig.SetValue(OsuSetting.SlopEditorBackupOnSave, true));

            AddStep("set metadata", () =>
            {
                EditorBeatmap.BeatmapInfo.Metadata.Artist = "artist";
                EditorBeatmap.BeatmapInfo.Metadata.Title = "title";
                EditorBeatmap.BeatmapInfo.DifficultyName = "first";
            });
            SaveEditor();

            AddStep("store previous file", () =>
            {
                existingBackups = backupStorage.GetFiles(string.Empty).ToArray();
                previousFilename = EditorBeatmap.BeatmapInfo.Path!;
                previousContent = readStoredFile(EditorBeatmap.BeatmapInfo);
            });

            AddStep("change difficulty name", () => EditorBeatmap.BeatmapInfo.DifficultyName = "second");
            SaveEditor();

            AddUntilStep("backup written", () => getNewBackups(existingBackups).Length == 1 && tryDecompress(getNewBackups(existingBackups).Single()) != null);

            AddAssert("backup named after previous file", () =>
            {
                var match = backup_filename.Match(getNewBackups(existingBackups).Single());
                return match.Success && match.Groups["name"].Value == previousFilename;
            });

            AddAssert("backup contains previous file", () => tryDecompress(getNewBackups(existingBackups).Single()), () => Is.EqualTo(previousContent));
        }

        [Test]
        public void TestNoBackupWhenDisabled()
        {
            string[] existingBackups = null!;

            AddStep("disable backups", () => Game.LocalConfig.SetValue(OsuSetting.SlopEditorBackupOnSave, false));
            SaveEditor();

            AddStep("store existing backups", () => existingBackups = backupStorage.GetFiles(string.Empty).ToArray());
            AddStep("change difficulty name", () => EditorBeatmap.BeatmapInfo.DifficultyName = "changed");
            SaveEditor();

            AddWaitStep("wait for potential backup", 5);
            AddAssert("no backup written", () => getNewBackups(existingBackups), () => Is.Empty);

            AddStep("restore default", () => Game.LocalConfig.SetValue(OsuSetting.SlopEditorBackupOnSave, true));
        }

        private string[] getNewBackups(string[] existingBackups) => backupStorage.GetFiles(string.Empty).Except(existingBackups).ToArray();

        private byte[] readStoredFile(BeatmapInfo beatmapInfo)
        {
            var file = beatmapInfo.BeatmapSet!.GetFile(beatmapInfo.Path!)!;

            using (var stream = storage.GetStorageForDirectory(@"files").GetStream(file.File.GetStoragePath()))
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }

        private byte[]? tryDecompress(string backupFilename)
        {
            try
            {
                using (var stream = backupStorage.GetStream(backupFilename))
                using (var xz = new XZStream(stream))
                using (var memoryStream = new MemoryStream())
                {
                    xz.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
            catch
            {
                // the backup may still be in the process of being written.
                return null;
            }
        }
    }
}
