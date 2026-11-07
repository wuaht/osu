// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.PatternGallery
{
    /// <summary>
    /// The saved patterns of the pattern gallery: an index file with the details of every pattern, and a <c>.osu</c> file per pattern holding its hitobjects and timing.
    /// The patterns are shared by all beatmaps.
    /// </summary>
    public class PatternStore
    {
        public const string DIRECTORY = @"pattern-gallery";

        private const string index_file = @"index.json";

        private readonly Storage storage;

        public readonly BindableList<Pattern> Patterns = new BindableList<Pattern>();

        public PatternStore(Storage storage)
        {
            this.storage = storage.GetStorageForDirectory(DIRECTORY);

            try
            {
                if (this.storage.Exists(index_file))
                {
                    using var stream = this.storage.GetStream(index_file);
                    using var reader = new StreamReader(stream);
                    Patterns.AddRange(JsonConvert.DeserializeObject<List<Pattern>>(reader.ReadToEnd()) ?? new List<Pattern>());
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to read the pattern gallery.");
                return;
            }

            // deleted patterns keep their file until the next start, such that the deletion can be undone.
            foreach (string file in this.storage.GetFiles(string.Empty, @"*.osu"))
            {
                if (Patterns.All(p => fileOf(p) != Path.GetFileName(file)))
                    this.storage.Delete(file);
            }
        }

        /// <summary>
        /// Writes the details of all patterns.
        /// </summary>
        public void SaveIndex()
        {
            using var stream = storage.CreateFileSafely(index_file);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(Patterns.ToList(), Formatting.Indented));
        }

        /// <summary>
        /// Adds a pattern holding the given hitobjects, with the timing, difficulty and ruleset of the given beatmap.
        /// </summary>
        public Pattern Add(string name, IBeatmap source, IEnumerable<HitObject> hitObjects)
        {
            var pattern = new Pattern { Name = name };
            write(pattern, source, hitObjects);

            Patterns.Add(pattern);
            SaveIndex();
            return pattern;
        }

        /// <summary>
        /// Replaces the hitobjects of a pattern, keeping its name and use statistics.
        /// </summary>
        public void Overwrite(Pattern pattern, IBeatmap source, IEnumerable<HitObject> hitObjects)
        {
            pattern.DurationMs = pattern.DurationBeats = 0;
            write(pattern, source, hitObjects);
            SaveIndex();
        }

        /// <summary>
        /// Removes a pattern. Its file is kept until the next start, so it can be brought back by <see cref="Restore"/>.
        /// </summary>
        public void Delete(Pattern pattern)
        {
            Patterns.Remove(pattern);
            SaveIndex();
        }

        /// <summary>
        /// Brings back a pattern removed by <see cref="Delete"/>.
        /// </summary>
        public void Restore(Pattern pattern)
        {
            if (Patterns.Contains(pattern))
                return;

            Patterns.Add(pattern);
            SaveIndex();
        }

        /// <summary>
        /// Reads the beatmap holding the hitobjects of a pattern, or null if it could not be read.
        /// </summary>
        public Beatmap? Open(Pattern pattern)
        {
            try
            {
                using var stream = storage.GetStream(fileOf(pattern));

                if (stream == null)
                    return null;

                using var reader = new LineBufferedReader(stream);
                return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Failed to read pattern ""{pattern.DisplayName}"".");
                return null;
            }
        }

        /// <summary>
        /// Marks a pattern as just inserted.
        /// </summary>
        public void MarkUsed(Pattern pattern)
        {
            pattern.UseCount++;
            pattern.LastUsedUtc = DateTime.UtcNow;
            SaveIndex();
        }

        private static string fileOf(Pattern pattern) => $@"{pattern.Id}.osu";

        private void write(Pattern pattern, IBeatmap source, IEnumerable<HitObject> hitObjects)
        {
            var sorted = hitObjects.OrderBy(h => h.StartTime).ToList();

            var beatmap = new Beatmap
            {
                BeatmapInfo = { Ruleset = source.BeatmapInfo.Ruleset },
                Difficulty = source.Difficulty.Clone(),
                ControlPointInfo = trimTiming(source.ControlPointInfo, sorted),
                HitObjects = sorted,
            };

            using (var stream = storage.CreateFileSafely(fileOf(pattern)))
            using (var writer = new StreamWriter(stream))
                new LegacyBeatmapEncoder(beatmap, null, null).Encode(writer);

            describe(pattern, beatmap);
        }

        /// <summary>
        /// The timing points which apply to the given hitobjects. Effect and sample points are left out, the hitobjects carry their own samples.
        /// </summary>
        private static ControlPointInfo trimTiming(ControlPointInfo source, IReadOnlyList<HitObject> hitObjects)
        {
            var result = new ControlPointInfo();

            if (hitObjects.Count == 0)
                return result;

            double start = hitObjects[0].StartTime;
            double end = hitObjects.Max(h => h.GetEndTime());

            var first = source.TimingPointAt(start);
            result.Add(first.Time, (TimingControlPoint)first.DeepClone());

            foreach (var t in source.TimingPoints.Where(t => t.Time > start && t.Time <= end))
                result.Add(t.Time, (TimingControlPoint)t.DeepClone());

            return result;
        }

        private static void describe(Pattern pattern, IBeatmap beatmap)
        {
            var objects = beatmap.HitObjects;

            pattern.RulesetId = beatmap.BeatmapInfo.Ruleset.OnlineID;
            pattern.SourceCircleSize = beatmap.Difficulty.CircleSize;
            pattern.CircleCount = pattern.SliderCount = pattern.SpinnerCount = 0;

            foreach (var h in objects)
            {
                if (h is IHasPath || (h is IHasColumn && h is IHasDuration))
                    pattern.SliderCount++;
                else if (h is IHasDuration)
                    pattern.SpinnerCount++;
                else
                    pattern.CircleCount++;
            }

            if (objects.Count == 0)
                return;

            double start = objects[0].StartTime;
            double end = objects.Max(h => h.GetEndTime());

            pattern.SourceBeatLength = beatmap.ControlPointInfo.TimingPointAt(start).BeatLength;

            if (pattern.DurationMs <= 0)
                pattern.DurationMs = end - start;
            if (pattern.DurationBeats <= 0)
                pattern.DurationBeats = PatternPlacement.BeatsBetween(beatmap.ControlPointInfo, start, end);
        }

        #region Mapping Tools import

        /// <summary>
        /// The folder of Mapping Tools, whose pattern gallery the patterns are imported from.
        /// </summary>
        public static string MappingToolsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Mapping Tools");

        /// <summary>
        /// Imports the patterns of the pattern gallery of Mapping Tools, skipping ones which were imported before.
        /// </summary>
        /// <returns>The number of imported patterns.</returns>
        public int ImportMappingTools()
        {
            int imported = 0;

            foreach (var (project, folder) in mappingToolsCollections())
            {
                foreach (var source in project.Patterns!)
                {
                    if (string.IsNullOrEmpty(source.FileName))
                        continue;

                    var created = source.CreationTime.ToUniversalTime();

                    if (Patterns.Any(p => p.Name == source.Name && p.CreatedUtc == created))
                        continue;

                    string path = Path.Combine(folder, source.FileName);

                    if (!File.Exists(path))
                        continue;

                    var pattern = new Pattern
                    {
                        Name = source.Name ?? string.Empty,
                        CreatedUtc = created,
                        LastUsedUtc = source.LastUsedTime.ToUniversalTime(),
                        UseCount = source.UseCount,
                        DurationMs = TimeSpan.TryParse(source.Duration, CultureInfo.InvariantCulture, out var duration) ? duration.TotalMilliseconds : 0,
                        DurationBeats = source.BeatLength,
                    };

                    try
                    {
                        using (var input = File.OpenRead(path))
                        using (var output = storage.CreateFileSafely(fileOf(pattern)))
                            input.CopyTo(output);

                        var beatmap = Open(pattern);

                        if (beatmap == null || beatmap.HitObjects.Count == 0)
                        {
                            storage.Delete(fileOf(pattern));
                            continue;
                        }

                        describe(pattern, beatmap);

                        if (pattern.DurationBeats > 0 && pattern.DurationMs > 0)
                            pattern.SourceBeatLength = pattern.DurationMs / pattern.DurationBeats;
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $@"Failed to import Mapping Tools pattern {source.FileName}.");
                        continue;
                    }

                    Patterns.Add(pattern);
                    imported++;
                }
            }

            if (imported > 0)
                SaveIndex();

            return imported;
        }

        private static IEnumerable<(MappingToolsProject Project, string PatternFolder)> mappingToolsCollections()
        {
            string projects = Path.Combine(MappingToolsDirectory, @"Pattern Gallery Projects");

            if (Directory.Exists(projects))
            {
                foreach (string directory in Directory.GetDirectories(projects))
                {
                    string? file = Path.Combine(directory, @"project.json");
                    if (!File.Exists(file))
                        file = Directory.GetFiles(directory, @"*.json").FirstOrDefault();

                    var project = readMappingToolsProject(file);
                    if (project?.Patterns != null)
                        yield return (project, Path.Combine(directory, project.FilesFolder));
                }
            }

            var legacy = readMappingToolsProject(Path.Combine(MappingToolsDirectory, @"patterngalleryproject.json"));

            if (legacy?.Patterns != null)
            {
                string? collection = legacy.FileHandler?.CollectionFolderName;
                yield return (legacy, string.IsNullOrEmpty(collection)
                    ? Path.Combine(MappingToolsDirectory, legacy.FilesFolder)
                    : Path.Combine(projects, collection, legacy.FilesFolder));
            }
        }

        private static MappingToolsProject? readMappingToolsProject(string? path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<MappingToolsProject>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Failed to read the Mapping Tools pattern collection at {path}.");
                return null;
            }
        }

        private class MappingToolsProject
        {
            public List<MappingToolsPattern>? Patterns { get; set; }

            public MappingToolsFileHandler? FileHandler { get; set; }

            public string FilesFolder => string.IsNullOrEmpty(FileHandler?.PatternFilesFolderName) ? @"Pattern Files" : FileHandler.PatternFilesFolderName;
        }

        private class MappingToolsFileHandler
        {
            public string? CollectionFolderName { get; set; }

            public string? PatternFilesFolderName { get; set; }
        }

        private class MappingToolsPattern
        {
            public string? Name { get; set; }

            public string? FileName { get; set; }

            public DateTime CreationTime { get; set; }

            public DateTime LastUsedTime { get; set; }

            public int UseCount { get; set; }

            public string? Duration { get; set; }

            public double BeatLength { get; set; }
        }

        #endregion
    }
}
