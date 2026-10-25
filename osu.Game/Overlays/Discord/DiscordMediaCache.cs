// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// A cache on disk for all downloaded Discord media, so images, emoji, GIFs, videos and audio aren't downloaded again every time they are shown.
    /// Media which wasn't used for a while is deleted.
    /// </summary>
    public static class DiscordMediaCache
    {
        /// <summary>
        /// How long cached media is kept after it was last used.
        /// </summary>
        private static readonly TimeSpan expiry = TimeSpan.FromDays(7);

        /// <summary>
        /// The query parameters of Discord's signed attachment URLs, which change every time a message is fetched while the file stays the same.
        /// </summary>
        private static readonly string[] volatile_parameters = { @"ex", @"is", @"hm" };

        private static string? directory;

        private static readonly object initialise_lock = new object();

        /// <summary>
        /// Sets up the cache in the cache directory of the game and deletes media which wasn't used for a while in the background.
        /// </summary>
        public static void Initialise(GameHost host)
        {
            lock (initialise_lock)
            {
                if (directory != null || host.CacheStorage == null)
                    return;

                try
                {
                    directory = host.CacheStorage.GetStorageForDirectory(@"discord").GetFullPath(string.Empty, true);
                }
                catch (Exception e)
                {
                    Logger.Log($@"Failed to set up the Discord media cache: {e.Message}");
                    return;
                }
            }

            Task.Run(deleteExpired);
        }

        /// <summary>
        /// Retrieves cached media, or null if it isn't cached. Marks the media as used, so it isn't deleted.
        /// </summary>
        public static byte[]? Get(string url)
        {
            string? path = pathFor(url);

            if (path == null || !File.Exists(path))
                return null;

            try
            {
                byte[] data = File.ReadAllBytes(path);

                // The access time isn't reliably updated by the file system, so the write time is used to track when the media was last used.
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                return data;
            }
            catch (IOException)
            {
                // The file is being written by another lookup of the same media.
                return null;
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to read cached Discord media: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Stores downloaded media in the cache.
        /// </summary>
        public static void Store(string url, byte[] data)
        {
            string? path = pathFor(url);

            if (path == null || data.Length == 0)
                return;

            string temporaryPath = $@"{path}.{Guid.NewGuid():N}.tmp";

            try
            {
                // Written to a temporary file first, so a lookup never reads a partially written file.
                File.WriteAllBytes(temporaryPath, data);
                File.Move(temporaryPath, path, true);
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to cache Discord media: {e.Message}");

                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                }
            }
        }

        private static string? pathFor(string url)
        {
            if (directory == null)
                return null;

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalise(url)));
            return Path.Combine(directory, Convert.ToHexString(hash).ToLowerInvariant());
        }

        private static string normalise(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri.Query))
                return url;

            var parameters = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                                .Where(p => !volatile_parameters.Contains(p.Split('=')[0], StringComparer.OrdinalIgnoreCase));

            return uri.GetLeftPart(UriPartial.Path) + '?' + string.Join('&', parameters);
        }

        private static void deleteExpired()
        {
            if (directory == null)
                return;

            int deleted = 0;

            try
            {
                DateTime threshold = DateTime.UtcNow - expiry;

                foreach (var file in new DirectoryInfo(directory).EnumerateFiles())
                {
                    try
                    {
                        // Temporary files which are left over from a crash are deleted after a day.
                        bool expired = file.Extension == @".tmp" ? file.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-1) : file.LastWriteTimeUtc < threshold;

                        if (!expired)
                            continue;

                        file.Delete();
                        deleted++;
                    }
                    catch
                    {
                        // The file is in use and is deleted the next time.
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to clean up the Discord media cache: {e.Message}");
            }

            if (deleted > 0)
                Logger.Log($@"Deleted {deleted} unused files from the Discord media cache.");
        }
    }
}
