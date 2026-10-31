// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.UI;
using osuTK;
using Realms;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Collects the slider paths of all local osu! beatmaps, so that they can be fitted between the head and tail of another slider.
    /// The paths are cached on disk per beatmap file and kept up to date when beatmaps are imported or deleted.
    /// </summary>
    public static class SliderLibrary
    {
        /// <summary>
        /// The maximum number of paths kept per beatmap, to bound memory on large libraries.
        /// </summary>
        private const int max_paths_per_beatmap = 30;

        /// <summary>
        /// Paths whose head and tail are closer than this can't be fitted, since they would have to be scaled up without bound.
        /// </summary>
        private const float min_chord_length = 10;

        private const int max_attempts = 30;

        /// <summary>
        /// The grid size which control points of paths going from (0, 0) to (1, 0) are snapped to, to tell whether two paths look alike.
        /// </summary>
        private const float shape_grid_size = 0.25f;

        private const string cache_filename = @"slider-library.cache";

        private const int cache_version = 1;

        // LegacyHitObjectType.Slider, which is internal to osu.Game.
        private const int slider_type_bit = 1 << 1;

        private const string osu_beatmaps_filter = $@"{nameof(BeatmapInfo.Ruleset)}.{nameof(RulesetInfo.ShortName)} == $0 AND {nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false";

        private static RealmAccess realm = null!;
        private static Storage storage = null!;
        private static IDisposable? subscription;

        private static volatile Library? library;

        private static readonly object build_lock = new object();
        private static bool buildPending;
        private static bool building;

        // only accessed by the build task.
        private static Dictionary<string, LibraryPath[]>? pathsByHash;
        private static HashSet<(string Hash, Guid SetId)>? lastBeatmaps;

        /// <summary>
        /// Starts collecting the paths in the background and keeps them up to date with the beatmap library, if that hasn't happened yet.
        /// </summary>
        public static void EnsureLoading(RealmAccess realm, Storage storage)
        {
            if (subscription != null)
                return;

            SliderLibrary.realm = realm;
            SliderLibrary.storage = storage;

            // fires once immediately, then whenever an osu! beatmap is imported, changed or deleted.
            subscription = realm.RegisterForNotifications(r => r.All<BeatmapInfo>().Filter(osu_beatmaps_filter, @"osu"), (_, _) => requestBuild());
        }

        /// <summary>
        /// Picks a random path from another beatmap set with as many control points as <paramref name="controlPoints"/>, or else as many segments.
        /// Paths which look alike are picked as one, so that common shapes like plain arcs don't crowd out the rest.
        /// The path is rotated and scaled so that it goes from <paramref name="head"/> to <paramref name="tail"/>.
        /// </summary>
        /// <returns>The control points of the fitted path in playfield space, or <c>null</c> if the library isn't loaded yet or no path fits.</returns>
        public static List<PathControlPoint>? Find(IReadOnlyList<PathControlPoint> controlPoints, Guid? excludedSetId, Vector2 head, Vector2 tail, Random rng)
        {
            var current = library;

            if (current == null)
                return null;

            var types = controlPoints.Select(p => p.Type).ToArray();
            string? currentShape = shapeKey(controlPoints.Select(p => p.Position).ToArray(), types);

            return fitFrom(current.ByPointCount.GetValueOrDefault(types.Length), currentShape, excludedSetId, head, tail, rng)
                   ?? fitFrom(current.BySegmentCount.GetValueOrDefault(segmentCount(types)), currentShape, excludedSetId, head, tail, rng);
        }

        private static List<PathControlPoint>? fitFrom(Bucket? bucket, string? currentShape, Guid? excludedSetId, Vector2 head, Vector2 tail, Random rng)
        {
            if (bucket == null)
                return null;

            for (int attempt = 0; attempt < max_attempts; attempt++)
            {
                var group = bucket.Groups[rng.Next(bucket.Groups.Count)];
                var entry = group[rng.Next(group.Count)];

                // pressing the hotkey again should change the shape, not just its size.
                if (entry.SetId == excludedSetId || (entry.Path.ShapeKey == currentShape && bucket.Groups.Count > 1))
                    continue;

                var fitted = fit(entry.Path, head, tail, rng.Next(2) == 0);

                if (insidePlayfield(fitted))
                    return fitted;
            }

            return null;
        }

        private static bool insidePlayfield(List<PathControlPoint> points)
            => points.All(p => p.Position.X >= 0 && p.Position.Y >= 0 && p.Position.X <= OsuPlayfield.BASE_SIZE.X && p.Position.Y <= OsuPlayfield.BASE_SIZE.Y);

        /// <summary>
        /// Rotates and scales <paramref name="positions"/> so that they go from (0, 0) to (1, 0).
        /// </summary>
        private static Vector2[]? normalisedShape(Vector2[] positions)
        {
            Vector2 chord = positions[^1] - positions[0];

            if (chord.Length < min_chord_length)
                return null;

            return positions.Select(p => divide(p - positions[0], chord)).ToArray();
        }

        /// <summary>
        /// Snaps the control points of the normalised path to a coarse grid, together with its segment kinds,
        /// so that paths which look alike (also when mirrored) get the same key.
        /// </summary>
        private static string? shapeKey(Vector2[] positions, PathType?[] types)
        {
            var shape = normalisedShape(positions);

            if (shape == null)
                return null;

            string key = keyFor(1);
            string mirroredKey = keyFor(-1);

            return string.CompareOrdinal(key, mirroredKey) <= 0 ? key : mirroredKey;

            string keyFor(int ySign) => signature(types) + string.Concat(shape.Select(p => $"{MathF.Round(p.X / shape_grid_size)},{MathF.Round(ySign * p.Y / shape_grid_size)};"));
        }

        /// <summary>
        /// Maps <paramref name="path"/> onto the line from <paramref name="head"/> to <paramref name="tail"/> with a rotation and uniform scale,
        /// treating positions as complex numbers so that the transform is a single multiplication.
        /// </summary>
        private static List<PathControlPoint> fit(LibraryPath path, Vector2 head, Vector2 tail, bool mirror)
        {
            Vector2 sourceChord = transformSource(path.Positions[^1]);
            Vector2 factor = divide(tail - head, sourceChord);

            var points = new List<PathControlPoint>(path.Positions.Length);

            for (int i = 0; i < path.Positions.Length; i++)
                points.Add(new PathControlPoint(head + multiply(transformSource(path.Positions[i]), factor), path.Types[i]));

            points[^1].Position = tail;
            return points;

            Vector2 transformSource(Vector2 position)
            {
                position -= path.Positions[0];
                return mirror ? new Vector2(position.X, -position.Y) : position;
            }
        }

        private static Vector2 multiply(Vector2 a, Vector2 b) => new Vector2(a.X * b.X - a.Y * b.Y, a.X * b.Y + a.Y * b.X);

        private static Vector2 divide(Vector2 a, Vector2 b) => multiply(a, new Vector2(b.X, -b.Y)) / b.LengthSquared;

        /// <summary>
        /// Describes every control point as the start of a curved segment (C), the start of a straight segment (L), or neither (-),
        /// so that a round and an angular path through the same points don't look alike.
        /// </summary>
        private static string signature(PathType?[] types)
        {
            char[] chars = new char[types.Length];

            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] is not PathType type || i == types.Length - 1)
                {
                    chars[i] = '-';
                    continue;
                }

                int end = i + 1;

                while (end < types.Length - 1 && types[end] == null)
                    end++;

                chars[i] = type.Type == SplineType.Linear || end - i == 1 ? 'L' : 'C';
            }

            return new string(chars);
        }

        private static int segmentCount(PathType?[] types) => types.Take(types.Length - 1).Count(t => t != null);

        private static void requestBuild()
        {
            lock (build_lock)
            {
                buildPending = true;

                if (building)
                    return;

                building = true;
            }

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    lock (build_lock)
                    {
                        if (!buildPending)
                        {
                            building = false;
                            return;
                        }

                        buildPending = false;
                    }

                    try
                    {
                        build();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, @"Failed to collect sliders for random slider shapes.");
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private static void build()
        {
            var beatmaps = realm.Run(r => r.All<BeatmapInfo>()
                                           .Filter(osu_beatmaps_filter, @"osu")
                                           .AsEnumerable()
                                           .Select(b => (b.Hash, SetId: b.BeatmapSet!.ID))
                                           .ToHashSet());

            // most notifications are about unrelated changes like star ratings.
            if (lastBeatmaps != null && lastBeatmaps.SetEquals(beatmaps))
                return;

            var stopwatch = Stopwatch.StartNew();

            pathsByHash ??= readCache();

            var hashes = beatmaps.Select(b => b.Hash).ToHashSet();
            int removed = pathsByHash.Keys.Where(h => !hashes.Contains(h)).ToList().Count(pathsByHash.Remove);
            int added = 0;

            var filesStorage = storage.GetStorageForDirectory(@"files");
            var rng = new Random();

            foreach (string hash in hashes)
            {
                if (pathsByHash.ContainsKey(hash))
                    continue;

                pathsByHash[hash] = readBeatmap(filesStorage, hash, rng);
                added++;
            }

            if (added > 0 || removed > 0)
                writeCache(pathsByHash);

            var newLibrary = new Library();

            foreach (var (hash, setId) in beatmaps)
            {
                foreach (var path in pathsByHash[hash])
                {
                    var entry = new LibraryEntry(setId, path);

                    bucketFor(newLibrary.ByPointCount, path.Positions.Length).Add(entry);
                    bucketFor(newLibrary.BySegmentCount, path.SegmentCount).Add(entry);
                }
            }

            library = newLibrary;
            lastBeatmaps = beatmaps;

            Logger.Log($"Collected sliders from {beatmaps.Count} beatmaps for random slider shapes ({added} read, {removed} removed, {stopwatch.ElapsedMilliseconds}ms).");
        }

        private static Bucket bucketFor(Dictionary<int, Bucket> buckets, int key)
        {
            if (!buckets.TryGetValue(key, out var bucket))
                buckets[key] = bucket = new Bucket();

            return bucket;
        }

        private static LibraryPath[] readBeatmap(Storage filesStorage, string hash, Random rng)
        {
            try
            {
                using (var stream = filesStorage.GetStream(new RealmFile { Hash = hash }.GetStoragePath()))
                {
                    if (stream == null)
                        return Array.Empty<LibraryPath>();

                    var paths = readPaths(new StreamReader(stream));

                    return paths.Count <= max_paths_per_beatmap
                        ? paths.ToArray()
                        : paths.OrderBy(_ => rng.Next()).Take(max_paths_per_beatmap).ToArray();
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Could not read the sliders of beatmap {hash}: {e.Message}");
                return Array.Empty<LibraryPath>();
            }
        }

        private static List<LibraryPath> readPaths(StreamReader reader)
        {
            int formatVersion = LegacyDecoder<Beatmap>.LATEST_VERSION;
            bool inHitObjects = false;
            ConvertHitObjectParser? parser = null;

            var paths = new List<LibraryPath>();
            var seenShapes = new HashSet<string>();

            while (reader.ReadLine() is string line)
            {
                if (line.StartsWith(@"osu file format v", StringComparison.Ordinal))
                {
                    if (!int.TryParse(line.AsSpan(17).Trim(), out formatVersion))
                        formatVersion = LegacyDecoder<Beatmap>.LATEST_VERSION;
                    continue;
                }

                if (line.StartsWith('['))
                {
                    inHitObjects = line.Trim() == @"[HitObjects]";
                    continue;
                }

                if (!inHitObjects)
                    continue;

                // only the position, type, path and repeat count are needed, so the samples are left out to keep parsing cheap.
                string[] split = line.Split(',', 8);

                if (split.Length < 7 || !int.TryParse(split[3], out int type) || (type & slider_type_bit) == 0)
                    continue;

                // copy-pasted sliders would otherwise be picked more often.
                if (!seenShapes.Add(split[5]))
                    continue;

                parser ??= new ConvertHitObjectParser(0, formatVersion);

                IHasPath? slider;

                try
                {
                    slider = parser.Parse(string.Join(',', split, 0, 7)) as IHasPath;
                }
                catch
                {
                    // a broken object shouldn't stop the rest of the beatmap from being read.
                    continue;
                }

                var controlPoints = slider?.Path.ControlPoints;

                if (controlPoints == null || controlPoints.Count < 2 || Vector2.Distance(controlPoints[0].Position, controlPoints[^1].Position) < min_chord_length)
                    continue;

                paths.Add(new LibraryPath(controlPoints.Select(p => p.Position).ToArray(), controlPoints.Select(p => p.Type).ToArray()));
            }

            return paths;
        }

        private static Dictionary<string, LibraryPath[]> readCache()
        {
            var cache = new Dictionary<string, LibraryPath[]>();

            try
            {
                using (var stream = storage.GetStream(cache_filename))
                {
                    if (stream == null)
                        return cache;

                    using (var reader = new BinaryReader(stream))
                    {
                        if (reader.ReadInt32() != cache_version)
                            return cache;

                        int beatmapCount = reader.ReadInt32();

                        for (int b = 0; b < beatmapCount; b++)
                        {
                            string hash = reader.ReadString();
                            var paths = new LibraryPath[reader.ReadInt32()];

                            for (int p = 0; p < paths.Length; p++)
                            {
                                int pointCount = reader.ReadInt32();
                                var positions = new Vector2[pointCount];
                                var types = new PathType?[pointCount];

                                for (int i = 0; i < pointCount; i++)
                                {
                                    positions[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());

                                    byte splineType = reader.ReadByte();
                                    int degree = reader.ReadInt32();

                                    if (splineType > 0)
                                        types[i] = new PathType { Type = (SplineType)(splineType - 1), Degree = degree < 0 ? null : degree };
                                }

                                paths[p] = new LibraryPath(positions, types);
                            }

                            cache[hash] = paths;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // the cache is only an optimisation, so a broken one is read again from the beatmaps.
                Logger.Log($"Could not read the slider library cache: {e.Message}");
                cache.Clear();
            }

            return cache;
        }

        private static void writeCache(Dictionary<string, LibraryPath[]> cache)
        {
            try
            {
                using (var stream = storage.CreateFileSafely(cache_filename))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(cache_version);
                    writer.Write(cache.Count);

                    foreach (var (hash, paths) in cache)
                    {
                        writer.Write(hash);
                        writer.Write(paths.Length);

                        foreach (var path in paths)
                        {
                            writer.Write(path.Positions.Length);

                            for (int i = 0; i < path.Positions.Length; i++)
                            {
                                writer.Write(path.Positions[i].X);
                                writer.Write(path.Positions[i].Y);
                                writer.Write((byte)(path.Types[i] is PathType type ? (int)type.Type + 1 : 0));
                                writer.Write(path.Types[i]?.Degree ?? -1);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Could not write the slider library cache: {e.Message}");
            }
        }

        private class LibraryPath
        {
            public readonly Vector2[] Positions;
            public readonly PathType?[] Types;
            public readonly string? ShapeKey;
            public readonly int SegmentCount;

            public LibraryPath(Vector2[] positions, PathType?[] types)
            {
                Positions = positions;
                Types = types;
                ShapeKey = shapeKey(positions, types);
                SegmentCount = segmentCount(types);
            }
        }

        private record LibraryEntry(Guid SetId, LibraryPath Path);

        /// <summary>
        /// Paths grouped by <see cref="LibraryPath.ShapeKey"/>, so that every shape is picked equally often however many beatmaps use it.
        /// </summary>
        private class Bucket
        {
            public readonly List<List<LibraryEntry>> Groups = new List<List<LibraryEntry>>();

            private readonly Dictionary<string, List<LibraryEntry>> groupsByShape = new Dictionary<string, List<LibraryEntry>>();

            public void Add(LibraryEntry entry)
            {
                string key = entry.Path.ShapeKey ?? string.Empty;

                if (!groupsByShape.TryGetValue(key, out var group))
                {
                    groupsByShape[key] = group = new List<LibraryEntry>();
                    Groups.Add(group);
                }

                group.Add(entry);
            }
        }

        private class Library
        {
            public readonly Dictionary<int, Bucket> ByPointCount = new Dictionary<int, Bucket>();
            public readonly Dictionary<int, Bucket> BySegmentCount = new Dictionary<int, Bucket>();
        }
    }
}
