// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Security.Cryptography;
using System.Text;
using osu.Framework.Platform;
using osu.Game.Beatmaps.Formats;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Hitsounding;

namespace osu.Game.MapsetVerifier
{
    /// <summary>
    /// The files of a beatmap set as Mapset Verifier sees them, which is a song folder like the one of osu!stable.
    /// </summary>
    public class SetMaterializer
    {
        /// <summary>
        /// A hash of all files, which changes whenever any file of the beatmap set (including unsaved changes of the current difficulty) changes.
        /// </summary>
        public readonly string Hash;

        private readonly (string Filename, string StoragePath)[] files;
        private readonly string? currentDifficultyFilename;
        private readonly byte[]? currentDifficulty;

        private SetMaterializer((string Filename, string StoragePath)[] files, string? currentDifficultyFilename, byte[]? currentDifficulty)
        {
            this.files = files;
            this.currentDifficultyFilename = currentDifficultyFilename;
            this.currentDifficulty = currentDifficulty;

            var hashInput = new StringBuilder();

            foreach (var (filename, storagePath) in files)
                hashInput.Append(filename).Append('|').Append(storagePath).Append('\n');

            if (currentDifficultyFilename != null && currentDifficulty != null)
                hashInput.Append(currentDifficultyFilename).Append('|').Append(Convert.ToHexString(SHA1.HashData(currentDifficulty)));

            Hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(hashInput.ToString())));
        }

        /// <summary>
        /// Captures the current state of the beatmap set being edited. Has to be called on the update thread, as it encodes the current difficulty.
        /// </summary>
        /// <param name="editorBeatmap">The difficulty being edited.</param>
        /// <param name="hasUnsavedChanges">
        /// Whether the difficulty has unsaved changes. Without any, the saved file is used as is,
        /// such that the hash stays the same when switching between difficulties and the checks don't run again.
        /// </param>
        /// <param name="config">The config, which may mark difficulties as hitsound difficulties (see <see cref="HitsoundPreferences.IsHitsoundDifficulty"/>).</param>
        public static SetMaterializer Capture(EditorBeatmap editorBeatmap, bool hasUnsavedChanges, OsuConfigManager? config)
        {
            var beatmapInfo = editorBeatmap.BeatmapInfo;
            string currentFilename = beatmapInfo.File?.Filename ?? getFilenameForNewDifficulty(editorBeatmap);
            var excludedFilenames = getExcludedHitsoundDifficulties(beatmapInfo, config);

            if (!hasUnsavedChanges && beatmapInfo.File != null)
                return new SetMaterializer(getSetFiles(beatmapInfo, excludedFilenames), null, null);

            excludedFilenames.Add(currentFilename);

            byte[] encoded;

            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
                    new LegacyBeatmapEncoder(editorBeatmap.PlayableBeatmap, editorBeatmap.BeatmapSkin, editorBeatmap.Storyboard).Encode(writer);

                encoded = stream.ToArray();
            }

            return new SetMaterializer(getSetFiles(beatmapInfo, excludedFilenames), currentFilename, encoded);
        }

        /// <summary>
        /// The files of the hitsound difficulties, which aren't uploaded on submission and therefore aren't checked or shown.
        /// They are still included while one of them is being edited, or if the set has no other difficulty.
        /// </summary>
        private static HashSet<string> getExcludedHitsoundDifficulties(Beatmaps.BeatmapInfo beatmapInfo, OsuConfigManager? config)
        {
            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var difficulties = beatmapInfo.BeatmapSet?.Beatmaps.ToList();

            if (difficulties == null || HitsoundPreferences.IsHitsoundDifficulty(config, beatmapInfo))
                return excluded;

            var hitsoundDifficulties = difficulties.Where(b => HitsoundPreferences.IsHitsoundDifficulty(config, b)).ToList();

            if (hitsoundDifficulties.Count == difficulties.Count)
                return excluded;

            foreach (var difficulty in hitsoundDifficulties)
            {
                if (difficulty.File != null)
                    excluded.Add(difficulty.File.Filename);
            }

            return excluded;
        }

        private static (string, string)[] getSetFiles(Beatmaps.BeatmapInfo beatmapInfo, HashSet<string> excludedFilenames) =>
            beatmapInfo.BeatmapSet?.Files
                       .Where(f => !excludedFilenames.Contains(f.Filename))
                       .Select(f => (f.Filename, f.File.GetStoragePath()))
                       .OrderBy(f => f.Filename, StringComparer.OrdinalIgnoreCase)
                       .ToArray() ?? Array.Empty<(string, string)>();

        /// <summary>
        /// Writes all files into a folder, and removes files which are no longer part of the beatmap set.
        /// </summary>
        /// <param name="storage">The storage of the game, which contains the files of all beatmap sets.</param>
        /// <param name="folder">The folder to write the beatmap set to.</param>
        public void WriteTo(Storage storage, string folder)
        {
            Directory.CreateDirectory(folder);

            var fileStorage = storage.GetStorageForDirectory(@"files");
            var expectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (filename, storagePath) in files)
            {
                string? destination = getSafePath(folder, filename);

                if (destination == null)
                    continue;

                expectedPaths.Add(destination);

                using (var source = fileStorage.GetStream(storagePath))
                {
                    if (source == null)
                        continue;

                    // files are immutable in the game's storage, so a file of the same size is already the correct one.
                    if (File.Exists(destination) && new FileInfo(destination).Length == source.Length && !filename.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                    using (var target = File.Create(destination))
                        source.CopyTo(target);
                }
            }

            string? currentPath = currentDifficultyFilename == null ? null : getSafePath(folder, currentDifficultyFilename);

            if (currentPath != null && currentDifficulty != null)
            {
                expectedPaths.Add(currentPath);
                File.WriteAllBytes(currentPath, currentDifficulty);
            }

            foreach (string existing in Directory.EnumerateFiles(folder, @"*", SearchOption.AllDirectories))
            {
                if (!expectedPaths.Contains(Path.GetFullPath(existing)))
                    File.Delete(existing);
            }
        }

        /// <summary>
        /// Resolves the path of a file inside the folder, or null if the filename would lead outside of it.
        /// </summary>
        private static string? getSafePath(string folder, string filename)
        {
            string root = Path.GetFullPath(folder);
            string path = Path.GetFullPath(Path.Combine(root, filename));

            return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? path : null;
        }

        private static string getFilenameForNewDifficulty(EditorBeatmap editorBeatmap)
        {
            var metadata = editorBeatmap.BeatmapInfo.Metadata;
            string name = $@"{metadata.Artist} - {metadata.Title} ({metadata.Author.Username}) [{editorBeatmap.BeatmapInfo.DifficultyName}].osu";

            return string.Concat(name.Split(Path.GetInvalidFileNameChars()));
        }
    }
}
