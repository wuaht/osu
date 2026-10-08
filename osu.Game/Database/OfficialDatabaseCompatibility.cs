// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Database
{
    /// <summary>
    /// Keeps the realm database usable by an official osu!(lazer) installation which shares the same data folder.
    /// </summary>
    /// <remarks>
    /// This client follows upstream master, which may use a newer realm schema version than the latest official release.
    /// Migrating the shared database would make it unusable for the official release, so in that case a separate copy is used instead,
    /// until the official release supports the same schema version.
    /// </remarks>
    public static class OfficialDatabaseCompatibility
    {
        /// <summary>
        /// The key of the <see cref="AssemblyMetadataAttribute"/> containing the realm schema version of the latest official release.
        /// Embedded at build time via the <c>OfficialRealmSchemaVersion</c> MSBuild property.
        /// </summary>
        public const string METADATA_KEY = @"OfficialRealmSchemaVersion";

        private const string realm_extension = @".realm";

        /// <summary>
        /// The realm schema version of the latest official release, or <see langword="null"/> if it was not embedded at build time
        /// (e.g. local development builds), in which case the shared database is used as normal.
        /// </summary>
        public static int? OfficialSchemaVersion { get; } = readOfficialSchemaVersion();

        /// <summary>
        /// Determines the database file to use for the given schema version.
        /// </summary>
        /// <param name="storage">The storage containing the database.</param>
        /// <param name="filename">The filename of the shared database (e.g. <c>client.realm</c>).</param>
        /// <param name="schemaVersion">The schema version of this client.</param>
        /// <param name="officialSchemaVersion">The schema version of the latest official release, or <see langword="null"/> if unknown.</param>
        /// <param name="usesSeparateDatabase">Whether a separate copy of the shared database is used.</param>
        /// <returns>
        /// <paramref name="filename"/> if the official release supports <paramref name="schemaVersion"/>.
        /// Otherwise, the filename of a separate copy (e.g. <c>client_53.realm</c>), which is created from the shared database if it doesn't exist yet.
        /// The shared database is never modified.
        /// </returns>
        public static string GetDatabaseFilename(Storage storage, string filename, int schemaVersion, int? officialSchemaVersion, out bool usesSeparateDatabase)
        {
            if (!filename.EndsWith(realm_extension, StringComparison.Ordinal))
                throw new ArgumentException($@"Filename must end with {realm_extension}.", nameof(filename));

            usesSeparateDatabase = officialSchemaVersion < schemaVersion;

            if (!usesSeparateDatabase)
                return filename;

            string separateFilename = $"{filename[..^realm_extension.Length]}_{schemaVersion}{realm_extension}";

            if (storage.Exists(separateFilename))
                return separateFilename;

            if (storage.Exists(filename))
            {
                Logger.Log($@"Copying {filename} to {separateFilename}, as the latest official release only supports schema version {officialSchemaVersion} (this client uses {schemaVersion}).",
                    LoggingTarget.Database);

                using (var shared = storage.GetStream(filename))
                using (var separate = storage.CreateFileSafely(separateFilename))
                    shared.CopyTo(separate);
            }

            return separateFilename;
        }

        private static int? readOfficialSchemaVersion()
        {
            string? value = typeof(OfficialDatabaseCompatibility).Assembly
                                                                 .GetCustomAttributes<AssemblyMetadataAttribute>()
                                                                 .FirstOrDefault(a => a.Key == METADATA_KEY)?.Value;

            return int.TryParse(value, out int version) ? version : null;
        }
    }

    /// <summary>
    /// Thrown when the database was migrated to a newer schema version than this client supports, e.g. by a newer official release.
    /// </summary>
    public class DatabaseTooNewException : Exception
    {
        public DatabaseTooNewException(string filename, Exception innerException)
            : base($@"The database {filename} was created by a newer version of osu! and can't be opened by this client. Please update this client first. Your data has not been modified.",
                innerException)
        {
        }
    }
}
