// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The names which the user gave to sub-lanes (e.g. "Kick" for "drum-hitnormal2") of a beatmap set, such that they are shared by all of its difficulties.
    /// Stored in a file for each beatmap set next to the other files of the game, as the beatmap format has no place for them.
    /// </summary>
    public class HitsoundLaneNames
    {
        /// <summary>
        /// The directory containing a file for each beatmap set, named by the ID of the beatmap set.
        /// </summary>
        public const string DIRECTORY = @"hitsound-lane-names";

        /// <summary>
        /// The file which contained the names of all beatmap sets, before they were split into a file for each beatmap set.
        /// </summary>
        private const string legacy_filename = @"hitsound-lane-names.json";

        private readonly Storage storage;
        private readonly string beatmapSetTitle;
        private readonly string path;

        /// <summary>
        /// The names by lane key.
        /// </summary>
        private readonly Dictionary<string, string> names;

        /// <summary>
        /// Invoked when a name changed.
        /// </summary>
        public event Action? NamesChanged;

        /// <param name="storage">The storage of the game.</param>
        /// <param name="beatmapSetId">The ID of the beatmap set whose names are read and written.</param>
        /// <param name="beatmapSetTitle">The title of the beatmap set, which is written to the file such that it can be recognised.</param>
        public HitsoundLaneNames(Storage storage, Guid beatmapSetId, string beatmapSetTitle)
        {
            this.storage = storage;
            this.beatmapSetTitle = beatmapSetTitle;

            path = GetPath(beatmapSetId);

            migrateLegacyFile();
            names = read();
        }

        /// <summary>
        /// The path of the file containing the names of a beatmap set, relative to the storage of the game.
        /// </summary>
        public static string GetPath(Guid beatmapSetId) => Path.Combine(DIRECTORY, $@"{beatmapSetId}.json");

        /// <summary>
        /// The name which the user gave to a lane, or <c>null</c> if it has none.
        /// </summary>
        public string? Get(HitsoundLane lane) => names.TryGetValue(getKey(lane), out string? name) ? name : null;

        /// <summary>
        /// Sets the name of a lane.
        /// </summary>
        /// <param name="lane">The lane.</param>
        /// <param name="name">The name, or <c>null</c> (or whitespace) to remove it.</param>
        public void Set(HitsoundLane lane, string? name)
        {
            name = name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                if (!names.Remove(getKey(lane)))
                    return;
            }
            else
            {
                if (names.TryGetValue(getKey(lane), out string? existing) && existing == name)
                    return;

                names[getKey(lane)] = name;
            }

            write();
            NamesChanged?.Invoke();
        }

        /// <summary>
        /// Identifies a lane like the file of its sample, e.g. "drum-hitnormal2".
        /// </summary>
        private static string getKey(HitsoundLane lane) => $@"{lane.Bank}-{lane.Sample}{lane.CustomIndex}";

        private Dictionary<string, string> read()
        {
            try
            {
                if (storage.Exists(path))
                {
                    using (var stream = storage.GetStream(path))
                    using (var reader = new StreamReader(stream))
                        return JsonConvert.DeserializeObject<LaneNamesFile>(reader.ReadToEnd())?.Names ?? new Dictionary<string, string>();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to read the names of hitsound editor lanes");
            }

            return new Dictionary<string, string>();
        }

        private void write()
        {
            try
            {
                if (names.Count == 0)
                {
                    storage.Delete(path);
                    return;
                }

                writeFile(path, new LaneNamesFile { BeatmapSet = beatmapSetTitle, Names = names });
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to save the names of hitsound editor lanes");
            }
        }

        private void writeFile(string filePath, LaneNamesFile file)
        {
            using (var stream = storage.CreateFileSafely(filePath))
            using (var writer = new StreamWriter(stream))
                writer.Write(JsonConvert.SerializeObject(file, Formatting.Indented));
        }

        /// <summary>
        /// Splits the file which contained the names of all beatmap sets into a file for each beatmap set.
        /// </summary>
        private void migrateLegacyFile()
        {
            try
            {
                if (!storage.Exists(legacy_filename))
                    return;

                Dictionary<Guid, Dictionary<string, string>>? legacy;

                using (var stream = storage.GetStream(legacy_filename))
                using (var reader = new StreamReader(stream))
                    legacy = JsonConvert.DeserializeObject<Dictionary<Guid, Dictionary<string, string>>>(reader.ReadToEnd());

                foreach (var (id, setNames) in legacy ?? new Dictionary<Guid, Dictionary<string, string>>())
                {
                    // the title of other beatmap sets isn't known here, so it is only written once their names change.
                    if (setNames.Count > 0 && !storage.Exists(GetPath(id)))
                        writeFile(GetPath(id), new LaneNamesFile { Names = setNames });
                }

                storage.Delete(legacy_filename);
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to migrate the names of hitsound editor lanes");
            }
        }

        private class LaneNamesFile
        {
            /// <summary>
            /// The title of the beatmap set, such that the file can be recognised. Not read.
            /// </summary>
            [JsonProperty(@"beatmap_set")]
            public string? BeatmapSet { get; set; }

            [JsonProperty(@"names")]
            public Dictionary<string, string> Names { get; set; } = new Dictionary<string, string>();
        }
    }
}
