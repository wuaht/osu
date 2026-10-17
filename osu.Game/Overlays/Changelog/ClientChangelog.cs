// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Overlays.Changelog
{
    /// <summary>
    /// The changelog of this client, built from its commits which are embedded at build time (see ClientChangelog.targets).
    /// The commits are shown as their own update stream, with one build per day.
    /// </summary>
    public static class ClientChangelog
    {
        public const string STREAM_NAME = @"client";

        /// <summary>
        /// The ids of the local builds and stream, which are negative so that they never clash with online ones.
        /// </summary>
        private const long stream_id = -1;

        private const string resource_name = @"osu.Game.ClientChangelog.txt";

        private static readonly Lazy<(APIUpdateStream? stream, List<APIChangelogBuild> builds)> changelog = new Lazy<(APIUpdateStream?, List<APIChangelogBuild>)>(load);

        /// <summary>
        /// The update stream of this client, or null if there are no commits.
        /// </summary>
        public static APIUpdateStream? Stream => changelog.Value.stream;

        /// <summary>
        /// The builds of this client, newest first.
        /// </summary>
        public static IReadOnlyList<APIChangelogBuild> Builds => changelog.Value.builds;

        public static bool IsLocal(APIChangelogBuild build) => build.UpdateStream?.Name == STREAM_NAME;

        private static (APIUpdateStream?, List<APIChangelogBuild>) load()
        {
            var builds = new List<APIChangelogBuild>();

            string log;

            using (var resource = typeof(ClientChangelog).Assembly.GetManifestResourceStream(resource_name))
            {
                if (resource == null)
                    return (null, builds);

                using (var reader = new StreamReader(resource))
                    log = reader.ReadToEnd();
            }

            var commits = parseCommits(log);

            if (commits.Count == 0)
                return (null, builds);

            var stream = new APIUpdateStream
            {
                Id = stream_id,
                Name = STREAM_NAME,
                DisplayName = @"slop!",
            };

            long nextEntryId = -1;

            foreach (var day in commits.GroupBy(c => c.Date.Date).OrderByDescending(d => d.Key))
            {
                builds.Add(new APIChangelogBuild
                {
                    Id = stream_id - builds.Count - 1,
                    Version = day.Key.ToString(@"yyyy.MMdd", CultureInfo.InvariantCulture),
                    DisplayVersion = day.Key.ToString(@"yyyy.Mdd", CultureInfo.InvariantCulture),
                    CreatedAt = day.Max(c => c.Date),
                    UpdateStream = stream,
                    ChangelogEntries = day.OrderBy(c => c.Date).Select(c => new APIChangelogEntry
                    {
                        Id = nextEntryId--,
                        Type = getType(c.Subject),
                        Category = getCategory(c.Files),
                        Title = c.Subject,
                        CreatedAt = c.Date,
                        GithubUser = new APIChangelogUser { DisplayName = c.Author },
                    }).ToList(),
                });
            }

            for (int i = 0; i < builds.Count; i++)
            {
                builds[i].Versions = new APIChangelogBuild.VersionNavigation
                {
                    Next = i > 0 ? builds[i - 1] : null,
                    Previous = i < builds.Count - 1 ? builds[i + 1] : null,
                };
            }

            stream.LatestBuild = builds[0];

            return (stream, builds);
        }

        private static List<Commit> parseCommits(string log)
        {
            var commits = new List<Commit>();

            // every commit starts with a record separator, followed by its unit separated fields and the names of its changed files.
            foreach (string record in log.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] lines = record.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (lines.Length == 0)
                    continue;

                string[] fields = lines[0].Split('\x1f');

                if (fields.Length < 4 || !DateTimeOffset.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    continue;

                commits.Add(new Commit(date, fields[2], fields[3], lines.Skip(1).ToArray()));
            }

            return commits;
        }

        private static ChangelogEntryType getType(string subject)
        {
            if (subject.StartsWith(@"Fix", StringComparison.OrdinalIgnoreCase))
                return ChangelogEntryType.Fix;

            if (subject.StartsWith(@"Add", StringComparison.OrdinalIgnoreCase))
                return ChangelogEntryType.Add;

            return ChangelogEntryType.Misc;
        }

        /// <summary>
        /// The category of a commit, which is the area most of its changed files are in.
        /// </summary>
        private static string getCategory(string[] files)
        {
            return files.Select(categoryOf)
                        .OfType<string>()
                        .GroupBy(c => c)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault() ?? @"Misc";
        }

        private static string? categoryOf(string file)
        {
            // tests and strings don't tell what a commit is about.
            if (file.Contains(@".Tests/", StringComparison.Ordinal) || file.StartsWith(@"osu.Game/Localisation/", StringComparison.Ordinal))
                return null;

            if (file.StartsWith(@"osu.Game.Rulesets.Fposu", StringComparison.Ordinal))
                return @"osu!fps";
            if (file.StartsWith(@"osu.Game.Rulesets.Osu", StringComparison.Ordinal))
                return @"osu!";
            if (file.StartsWith(@"osu.Game.Rulesets.Taiko", StringComparison.Ordinal))
                return @"osu!taiko";
            if (file.StartsWith(@"osu.Game.Rulesets.Catch", StringComparison.Ordinal))
                return @"osu!catch";
            if (file.StartsWith(@"osu.Game.Rulesets.Mania", StringComparison.Ordinal))
                return @"osu!mania";
            if (file.StartsWith(@"osu.Game/Screens/Edit/", StringComparison.Ordinal) || file.StartsWith(@"osu.Game/Rulesets/Edit/", StringComparison.Ordinal))
                return @"Editor";
            if (file.StartsWith(@"osu.Game/Overlays/Settings/", StringComparison.Ordinal) || file.StartsWith(@"osu.Game/Configuration/", StringComparison.Ordinal))
                return @"Settings";
            if (file.StartsWith(@"osu.Game/Screens/Play/", StringComparison.Ordinal) || file.StartsWith(@"osu.Game/Screens/Ranking/", StringComparison.Ordinal))
                return @"Gameplay";
            if (file.StartsWith(@"osu.Game/Screens/Select", StringComparison.Ordinal))
                return @"Song Select";
            if (file.StartsWith(@"osu.Game/Skinning/", StringComparison.Ordinal))
                return @"Skinning";
            if (file.StartsWith(@"osu.Game/Screens/Menu/", StringComparison.Ordinal))
                return @"Main Menu";
            if (file.StartsWith(@"osu.Game/Overlays/", StringComparison.Ordinal))
                return @"Overlays";

            return @"Misc";
        }

        private record Commit(DateTimeOffset Date, string Author, string Subject, string[] Files);
    }
}
