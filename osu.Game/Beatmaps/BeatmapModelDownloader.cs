// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Logging;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.BeatmapMirrors;
using osu.Game.Overlays.Notifications;
using osu.Game.Utils;

namespace osu.Game.Beatmaps
{
    public class BeatmapModelDownloader : ModelDownloader<BeatmapSetInfo, IBeatmapSetInfo>
    {
        private readonly IModelImporter<BeatmapSetInfo> beatmapImporter;
        private readonly BeatmapMirrorProvider? mirrors;

        protected override ArchiveDownloadRequest<IBeatmapSetInfo> CreateDownloadRequest(IBeatmapSetInfo set, bool minimiseDownloadSize)
        {
            if (mirrors?.IsActive == true)
                return mirrors.CreateDownloadRequest(set, minimiseDownloadSize);

            return new DownloadBeatmapSetRequest(set, minimiseDownloadSize);
        }

        public override ArchiveDownloadRequest<IBeatmapSetInfo>? GetExistingDownload(IBeatmapSetInfo model)
            => CurrentDownloads.Find(r => r.Model.OnlineID == model.OnlineID);

        public bool Download(IBeatmapSetInfo model, bool withoutVideo) => Download(model, withoutVideo, null);

        public void DownloadAsUpdate(BeatmapSetInfo originalModel, bool withoutVideo) => Download(originalModel, withoutVideo, originalModel);

        /// <summary>
        /// Downloads the set of <paramref name="beatmap"/>, but only takes over the new version of that one difficulty.
        /// Every other difficulty of the local set is kept as it is.
        /// </summary>
        /// <param name="originalSet">A detached copy of the local set containing <paramref name="beatmap"/>, including its files.</param>
        /// <param name="beatmap">The difficulty to update.</param>
        /// <param name="withoutVideo">Whether to download the set without its video.</param>
        public bool DownloadAsDifficultyUpdate(BeatmapSetInfo originalSet, BeatmapInfo beatmap, bool withoutVideo)
            => DownloadAndImport(originalSet, withoutVideo, (notification, download) => importDifficultyUpdate(notification, download, originalSet, beatmap));

        private async Task<bool> importDifficultyUpdate(ProgressNotification notification, ImportTask download, BeatmapSetInfo originalSet, BeatmapInfo beatmap)
        {
            string? mountedPath = null;

            try
            {
                var original = originalSet.Beatmaps.FirstOrDefault(b => b.ID == beatmap.ID);
                string? originalFilename = original == null ? null : originalSet.Files.FirstOrDefault(f => f.File.Hash == original.Hash)?.Filename;

                if (original == null || originalFilename == null)
                    return fail($"Couldn't update \"{beatmap}\" as it is no longer part of its local beatmap set.");

                mountedPath = (await beatmapImporter.BeginExternalEditing(originalSet).ConfigureAwait(false)).MountedPath;

                using (ArchiveReader reader = download.GetReader())
                {
                    string? updatedFilename = findDifficulty(reader, original);

                    if (updatedFilename == null)
                        return fail($"Couldn't update \"{beatmap}\" as it is no longer part of the online beatmap set.");

                    // the new version is written over the local file, so every difficulty keeps its local filename (which the online lookup relies on).
                    await copyFile(reader, updatedFilename, getMountedPath(mountedPath, originalFilename)).ConfigureAwait(false);

                    string prefix = reader.Filenames.GetCommonPrefix();
                    if (!(prefix.EndsWith('/') || prefix.EndsWith('\\')))
                        prefix = string.Empty;

                    var localFilenames = new HashSet<string>(originalSet.Files.Select(f => f.Filename), StringComparer.OrdinalIgnoreCase);

                    foreach (string file in reader.Filenames)
                    {
                        string filename = file.Substring(prefix.Length).ToStandardisedPath();
                        string extension = Path.GetExtension(filename).ToLowerInvariant();

                        // shortcut: files the set already has are kept in their local version, as the other difficulties may rely on them.
                        // if the updated difficulty changed such a file under the same name (e.g. a re-timed audio file), it uses the local one. a full set update covers that case.
                        if (extension == @".osu" || extension == @".osb" || localFilenames.Contains(filename) || FilesystemSanityCheckHelpers.IncursPathTraversalRisk(filename))
                            continue;

                        await copyFile(reader, file, getMountedPath(mountedPath, filename)).ConfigureAwait(false);
                    }
                }

                var imported = await beatmapImporter.ImportAsUpdate(notification, new ImportTask(mountedPath), originalSet).ConfigureAwait(false);

                if (imported == null)
                    return false;

                imported.PerformWrite(updated => restoreKeptDifficulties(updated, originalSet));
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Couldn't update \"{beatmap}\"");
                notification.State = ProgressNotificationState.Cancelled;
                return false;
            }
            finally
            {
                tryDelete(() => File.Delete(download.Path));

                if (mountedPath != null)
                    tryDelete(() => Directory.Delete(mountedPath, true));
            }

            bool fail(string message)
            {
                Logger.Log(message, level: LogLevel.Important);
                notification.State = ProgressNotificationState.Cancelled;
                return false;
            }
        }

        /// <summary>
        /// Finds the file of <paramref name="original"/> in a downloaded archive of its set, by online ID or (if the files don't have IDs) by difficulty name.
        /// </summary>
        private static string? findDifficulty(ArchiveReader reader, BeatmapInfo original)
        {
            var difficulties = reader.Filenames
                                     .Where(f => f.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase))
                                     .Select(f =>
                                     {
                                         using (var stream = new LineBufferedReader(reader.GetStream(f)))
                                             return (filename: f, info: Decoder.GetDecoder<Beatmap>(stream).Decode(stream).BeatmapInfo);
                                     })
                                     .ToList();

            if (original.OnlineID > 0 && difficulties.FirstOrDefault(d => d.info.OnlineID == original.OnlineID).filename is string byId)
                return byId;

            return difficulties.FirstOrDefault(d => d.info.DifficultyName == original.DifficultyName).filename;
        }

        /// <summary>
        /// Transfers the local state of the difficulties which were kept as they were onto their re-imported copies.
        /// </summary>
        private static void restoreKeptDifficulties(BeatmapSetInfo updated, BeatmapSetInfo originalSet)
        {
            foreach (var beatmap in updated.Beatmaps)
            {
                var original = originalSet.Beatmaps.FirstOrDefault(b => b.Hash == beatmap.Hash);

                // the updated difficulty.
                if (original == null)
                    continue;

                beatmap.LastPlayed = original.LastPlayed;
                beatmap.Hidden = original.Hidden;
                beatmap.UserSettings.Offset = original.UserSettings.Offset;

                // the online lookup may not have found the outdated difficulty again.
                if (beatmap.OnlineID <= 0)
                {
                    beatmap.OnlineID = original.OnlineID;
                    beatmap.OnlineMD5Hash = original.OnlineMD5Hash;
                    beatmap.LastOnlineUpdate = original.LastOnlineUpdate;
                }

                // the online lookup only applies these to difficulties matching their online version, which outdated ones don't.
                if (beatmap.Status == BeatmapOnlineStatus.None)
                {
                    beatmap.Status = original.Status;
                    beatmap.Metadata.Author.OnlineID = original.Metadata.Author.OnlineID;

                    if (beatmap.Metadata.UserTags.Count == 0)
                    {
                        foreach (string tag in original.Metadata.UserTags)
                            beatmap.Metadata.UserTags.Add(tag);
                    }
                }
            }

            // likewise only applied when all difficulties match their online versions.
            if (updated.Status == BeatmapOnlineStatus.None)
            {
                updated.Status = originalSet.Status;
                updated.DateRanked ??= originalSet.DateRanked;
                updated.DateSubmitted ??= originalSet.DateSubmitted;
            }
        }

        private static string getMountedPath(string mountedPath, string filename)
            => Path.Combine(filename.Split('/').Select(p => p.GetValidFilename()).Prepend(mountedPath).ToArray());

        private static async Task copyFile(ArchiveReader reader, string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            using (var inStream = reader.GetStream(source))
            using (var outStream = File.Create(destination))
                await inStream.CopyToAsync(outStream).ConfigureAwait(false);
        }

        private static void tryDelete(Action delete)
        {
            try
            {
                delete();
            }
            catch
            {
            }
        }

        /// <param name="beatmapImporter">The importer for downloaded beatmap sets.</param>
        /// <param name="api">The API to perform the downloads with.</param>
        /// <param name="mirrors">Provides the beatmap mirror to download from while the official servers can't be used. If <c>null</c>, the official servers are always used.</param>
        public BeatmapModelDownloader(IModelImporter<BeatmapSetInfo> beatmapImporter, IAPIProvider api, BeatmapMirrorProvider? mirrors = null)
            : base(beatmapImporter, api)
        {
            this.beatmapImporter = beatmapImporter;
            this.mirrors = mirrors;
        }
    }
}
