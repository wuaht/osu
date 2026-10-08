// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using SQLitePCL;

namespace osu.Game.Database
{
    /// <summary>
    /// Persists the owners (mappers) of beatmap difficulties retrieved from the API,
    /// so they can be displayed immediately and don't have to be requested again in every session.
    /// </summary>
    /// <remarks>
    /// Stored in a separate SQLite database rather than realm to keep the realm schema compatible with the official client.
    /// Entries are read and written individually, so this scales to installations with a large number of beatmaps.
    /// </remarks>
    public partial class BeatmapOwnerStore : Component
    {
        /// <summary>
        /// The duration after which stored owners are requested again, to pick up username changes and owner changes.
        /// </summary>
        public static readonly TimeSpan REFRESH_INTERVAL = TimeSpan.FromDays(7);

        private const string database_name = @"beatmap-owners.db";

        private readonly Storage storage;

        /// <summary>
        /// Entries which have already been read from (or written to) the database.
        /// A <see langword="null"/> value denotes that no entry is stored for the beatmap.
        /// </summary>
        private readonly ConcurrentDictionary<int, Entry?> memoryCache = new ConcurrentDictionary<int, Entry?>();

        private readonly Task initialiseTask;

        /// <summary>
        /// Guards all usages of <see cref="connection"/>, as SQLite connections must not be used concurrently.
        /// </summary>
        private readonly Lock connectionLock = new Lock();

        private SqliteConnection? connection;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private BeatmapLookupCache beatmapLookupCache { get; set; } = null!;

        /// <summary>
        /// Whether the database has been opened, i.e. <see cref="GetStored"/> is able to return stored entries.
        /// </summary>
        public bool HasLoadedFromDisk => initialiseTask.IsCompleted;

        public BeatmapOwnerStore(Storage storage)
        {
            this.storage = storage;

            initialiseTask = Task.Run(initialise);
        }

        /// <summary>
        /// Returns the stored owners of the given beatmap difficulty without performing any online lookups.
        /// </summary>
        /// <returns>The stored entry, or <see langword="null"/> if none is stored or the database has not been opened yet.</returns>
        public Entry? GetStored(int beatmapId)
        {
            if (!HasLoadedFromDisk)
                return null;

            if (memoryCache.TryGetValue(beatmapId, out var cached))
                return cached;

            var entry = read(beatmapId);
            memoryCache.TryAdd(beatmapId, entry);
            return entry;
        }

        /// <summary>
        /// Retrieves the owners of the given beatmap difficulty, requesting them online if none are stored or the stored ones are stale.
        /// </summary>
        /// <returns>
        /// The up-to-date entry if it could be retrieved, otherwise the stored (possibly stale) entry,
        /// or <see langword="null"/> if neither is available.
        /// </returns>
        public async Task<Entry?> GetAsync(int beatmapId, CancellationToken token = default)
        {
            await initialiseTask.ConfigureAwait(false);

            var stored = GetStored(beatmapId);

            if (stored?.IsStale == false || api.State.Value != APIState.Online)
                return stored;

            var beatmap = await beatmapLookupCache.GetBeatmapAsync(beatmapId, token).ConfigureAwait(false);

            if (beatmap == null || beatmap.OnlineID != beatmapId)
                return stored;

            var entry = new Entry
            {
                Owners = beatmap.BeatmapOwners,
                LastUpdated = DateTimeOffset.UtcNow,
            };

            memoryCache[beatmapId] = entry;
            write(beatmapId, entry);

            return entry;
        }

        private void initialise()
        {
            try
            {
                // required to initialise native SQLite libraries on some platforms.
                Batteries_V2.Init();
            }
            catch
            {
                // may fail if platform not supported.
            }

            try
            {
                setConnection(openDatabase());
            }
            catch (SqliteException e) when (e.SqliteErrorCode == 26 || e.SqliteErrorCode == 11) // SQLITE_NOTADB, SQLITE_CORRUPT
            {
                Logger.Log($"{database_name} is corrupt and will be recreated ({e.Message}).");

                try
                {
                    storage.Delete(database_name);
                    setConnection(openDatabase());
                }
                catch (Exception retryException)
                {
                    Logger.Error(retryException, $"Failed to recreate {database_name}. Beatmap owners will not be persisted.");
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to open {database_name}. Beatmap owners will not be persisted.");
            }
        }

        private void setConnection(SqliteConnection db)
        {
            lock (connectionLock)
            {
                // the game may have exited while the database was being opened.
                if (IsDisposed)
                    db.Dispose();
                else
                    connection = db;
            }
        }

        private SqliteConnection openDatabase()
        {
            var db = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = storage.GetFullPath(database_name, true),
                // ensures the database file is closed when the connection is disposed.
                Pooling = false,
            }.ToString());

            try
            {
                db.Open();

                using (var cmd = db.CreateCommand())
                {
                    cmd.CommandText = @"PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
CREATE TABLE IF NOT EXISTS `beatmap_owners` (
    `beatmap_id` INTEGER PRIMARY KEY NOT NULL,
    `owners` TEXT NOT NULL,
    `last_updated` INTEGER NOT NULL
);";
                    cmd.ExecuteNonQuery();
                }

                return db;
            }
            catch
            {
                db.Dispose();
                throw;
            }
        }

        private Entry? read(int beatmapId)
        {
            lock (connectionLock)
            {
                if (connection == null)
                    return null;

                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"SELECT `owners`, `last_updated` FROM `beatmap_owners` WHERE `beatmap_id` = @BeatmapID";
                        cmd.Parameters.Add(new SqliteParameter(@"@BeatmapID", beatmapId));

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                                return null;

                            return new Entry
                            {
                                Owners = JsonConvert.DeserializeObject<APIBeatmap.BeatmapOwner[]>(reader.GetString(0)) ?? Array.Empty<APIBeatmap.BeatmapOwner>(),
                                LastUpdated = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(1)),
                            };
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Failed to read owners of beatmap {beatmapId} from {database_name}.");
                    return null;
                }
            }
        }

        private void write(int beatmapId, Entry entry)
        {
            lock (connectionLock)
            {
                if (connection == null)
                    return;

                try
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = @"INSERT OR REPLACE INTO `beatmap_owners` (`beatmap_id`, `owners`, `last_updated`) VALUES (@BeatmapID, @Owners, @LastUpdated)";
                        cmd.Parameters.Add(new SqliteParameter(@"@BeatmapID", beatmapId));
                        cmd.Parameters.Add(new SqliteParameter(@"@Owners", JsonConvert.SerializeObject(entry.Owners)));
                        cmd.Parameters.Add(new SqliteParameter(@"@LastUpdated", entry.LastUpdated.ToUnixTimeSeconds()));
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Failed to write owners of beatmap {beatmapId} to {database_name}.");
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            lock (connectionLock)
            {
                connection?.Dispose();
                connection = null;
            }
        }

        public class Entry
        {
            public APIBeatmap.BeatmapOwner[] Owners { get; init; } = Array.Empty<APIBeatmap.BeatmapOwner>();

            public DateTimeOffset LastUpdated { get; init; }

            public bool IsStale => DateTimeOffset.UtcNow - LastUpdated > REFRESH_INTERVAL;
        }
    }
}
