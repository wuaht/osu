// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The hitsound patterns of a difficulty: hitsounds which are used at several places of the song (like the patterns of FL Studio).
    /// Changing the hitsounds of one place changes all others (see <see cref="HitsoundEditor"/>).
    /// Stored in a file for each difficulty next to the other files of the game, as the beatmap format has no place for them.
    /// </summary>
    public class HitsoundPatterns
    {
        /// <summary>
        /// The directory containing a file for each difficulty, named by the ID of the difficulty.
        /// </summary>
        public const string DIRECTORY = @"hitsound-patterns";

        public static readonly Colour4[] COLOURS =
        [
            Colour4.FromHex(@"7ea8ff"),
            Colour4.FromHex(@"ff9a6b"),
            Colour4.FromHex(@"8fe388"),
            Colour4.FromHex(@"e08fff"),
            Colour4.FromHex(@"ffd36b"),
            Colour4.FromHex(@"6be0d6"),
            Colour4.FromHex(@"ff7aa8"),
            Colour4.FromHex(@"b9a6ff"),
        ];

        private readonly Storage storage;
        private readonly string beatmapTitle;
        private readonly string path;

        private readonly List<HitsoundPattern> patterns;
        private readonly List<HitsoundPatternInstance> instances;

        /// <summary>
        /// Invoked when a pattern or an instance was added, removed or changed.
        /// </summary>
        public event Action? Changed;

        /// <param name="storage">The storage of the game.</param>
        /// <param name="beatmapId">The ID of the difficulty whose patterns are read and written.</param>
        /// <param name="beatmapTitle">The title of the difficulty, which is written to the file such that it can be recognised.</param>
        public HitsoundPatterns(Storage storage, Guid beatmapId, string beatmapTitle)
        {
            this.storage = storage;
            this.beatmapTitle = beatmapTitle;

            path = Path.Combine(DIRECTORY, $@"{beatmapId}.json");

            (patterns, instances) = read();
        }

        public IReadOnlyList<HitsoundPattern> Patterns => patterns;

        /// <summary>
        /// The places where patterns are used, ordered by time.
        /// </summary>
        public IReadOnlyList<HitsoundPatternInstance> Instances => instances;

        public HitsoundPattern? GetPattern(Guid id) => patterns.FirstOrDefault(p => p.ID == id);

        public IEnumerable<HitsoundPatternInstance> GetInstances(HitsoundPattern pattern) => instances.Where(i => i.PatternID == pattern.ID);

        public double GetEndTime(HitsoundPatternInstance instance) => instance.StartTime + (GetPattern(instance.PatternID)?.Length ?? 0);

        /// <summary>
        /// Whether a time range overlaps a place where a pattern is used.
        /// </summary>
        public bool Overlaps(double startTime, double endTime)
            => instances.Any(i => i.StartTime < endTime - HitsoundEditor.PASTE_LENIENCY && GetEndTime(i) > startTime + HitsoundEditor.PASTE_LENIENCY);

        /// <summary>
        /// Creates a pattern.
        /// </summary>
        /// <param name="length">The length of the pattern in milliseconds.</param>
        /// <param name="states">The hitsounds of the pattern, with times relative to its start.</param>
        public HitsoundPattern Create(double length, IReadOnlyList<HitsoundColumnState> states)
        {
            int number = 1;

            while (patterns.Any(p => p.Name == defaultName(number)))
                number++;

            // the least used colour, such that patterns are distinguishable.
            int colourIndex = Enumerable.Range(0, COLOURS.Length).MinBy(c => patterns.Count(p => p.ColourIndex == c));

            var pattern = new HitsoundPattern(Guid.NewGuid(), defaultName(number), colourIndex, length, states);
            patterns.Add(pattern);

            onChanged();
            return pattern;

            static string defaultName(int number) => $@"Pattern {number}";
        }

        /// <summary>
        /// Deletes a pattern. Its hitsounds are kept where it was used.
        /// </summary>
        public void Delete(HitsoundPattern pattern)
        {
            patterns.Remove(pattern);
            instances.RemoveAll(i => i.PatternID == pattern.ID);

            onChanged();
        }

        public void Rename(HitsoundPattern pattern, string name)
        {
            name = name.Trim();

            if (string.IsNullOrEmpty(name) || name == pattern.Name)
                return;

            pattern.Name = name;
            onChanged();
        }

        /// <summary>
        /// Remembers the hitsounds of a pattern, which are used when it's placed while it isn't used anywhere.
        /// </summary>
        public void SetStates(HitsoundPattern pattern, IReadOnlyList<HitsoundColumnState> states)
        {
            if (HitsoundPatternStates.AreEqual(pattern.States, states))
                return;

            pattern.States = states;
            // not a visible change, so only written.
            write();
        }

        public HitsoundPatternInstance AddInstance(HitsoundPattern pattern, double startTime)
        {
            var instance = new HitsoundPatternInstance(Guid.NewGuid(), pattern.ID, startTime);

            instances.Add(instance);
            instances.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            onChanged();
            return instance;
        }

        /// <summary>
        /// Stops using a pattern at a place. Its hitsounds are kept.
        /// </summary>
        public void RemoveInstance(HitsoundPatternInstance instance)
        {
            if (instances.Remove(instance))
                onChanged();
        }

        private void onChanged()
        {
            write();
            Changed?.Invoke();
        }

        private (List<HitsoundPattern>, List<HitsoundPatternInstance>) read()
        {
            try
            {
                if (storage.Exists(path))
                {
                    PatternsFile? file;

                    using (var stream = storage.GetStream(path))
                    using (var reader = new StreamReader(stream))
                        file = JsonConvert.DeserializeObject<PatternsFile>(reader.ReadToEnd());

                    if (file != null)
                    {
                        var readPatterns = file.Patterns.Select(p => p.ToPattern()).ToList();
                        var readInstances = file.Instances.Where(i => readPatterns.Any(p => p.ID == i.PatternID))
                                                .OrderBy(i => i.StartTime)
                                                .ToList();

                        return (readPatterns, readInstances);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to read the hitsound patterns");
            }

            return (new List<HitsoundPattern>(), new List<HitsoundPatternInstance>());
        }

        private void write()
        {
            try
            {
                if (patterns.Count == 0)
                {
                    storage.Delete(path);
                    return;
                }

                var file = new PatternsFile
                {
                    Beatmap = beatmapTitle,
                    Patterns = patterns.Select(PatternEntry.FromPattern).ToList(),
                    Instances = instances.ToList(),
                };

                using (var stream = storage.CreateFileSafely(path))
                using (var writer = new StreamWriter(stream))
                    writer.Write(JsonConvert.SerializeObject(file, Formatting.Indented));
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to save the hitsound patterns");
            }
        }

        private class PatternsFile
        {
            /// <summary>
            /// The title of the difficulty, such that the file can be recognised. Not read.
            /// </summary>
            [JsonProperty(@"beatmap")]
            public string? Beatmap { get; set; }

            [JsonProperty(@"patterns")]
            public List<PatternEntry> Patterns { get; set; } = new List<PatternEntry>();

            [JsonProperty(@"instances")]
            public List<HitsoundPatternInstance> Instances { get; set; } = new List<HitsoundPatternInstance>();
        }

        private class PatternEntry
        {
            [JsonProperty(@"id")]
            public Guid ID { get; set; }

            [JsonProperty(@"name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty(@"colour")]
            public int ColourIndex { get; set; }

            [JsonProperty(@"length")]
            public double Length { get; set; }

            [JsonProperty(@"hitsounds")]
            public List<StateEntry> States { get; set; } = new List<StateEntry>();

            public static PatternEntry FromPattern(HitsoundPattern pattern) => new PatternEntry
            {
                ID = pattern.ID,
                Name = pattern.Name,
                ColourIndex = pattern.ColourIndex,
                Length = pattern.Length,
                States = pattern.States.Select(StateEntry.FromState).ToList(),
            };

            public HitsoundPattern ToPattern() => new HitsoundPattern(ID, Name, ColourIndex, Length, States.Select(s => s.ToState()).ToList());
        }

        private class StateEntry
        {
            [JsonProperty(@"time")]
            public double Time { get; set; }

            [JsonProperty(@"normal_banks")]
            public List<string> NormalBanks { get; set; } = new List<string>();

            [JsonProperty(@"additions")]
            public List<AdditionEntry> Additions { get; set; } = new List<AdditionEntry>();

            [JsonProperty(@"volume")]
            public int Volume { get; set; }

            [JsonProperty(@"custom_index")]
            public int CustomIndex { get; set; }

            public static StateEntry FromState(HitsoundColumnState state) => new StateEntry
            {
                Time = state.Time,
                NormalBanks = state.NormalBanks.ToList(),
                Additions = state.AdditionGroups.Select(g => new AdditionEntry { Bank = g.Bank, Names = g.Names.ToList() }).ToList(),
                Volume = state.Volume,
                CustomIndex = state.CustomIndex,
            };

            public HitsoundColumnState ToState()
                => new HitsoundColumnState(Time, NormalBanks, Additions.Select(a => new HitsoundAdditionGroup(a.Bank, a.Names)).ToList(), Volume, CustomIndex);
        }

        private class AdditionEntry
        {
            [JsonProperty(@"bank")]
            public string Bank { get; set; } = string.Empty;

            [JsonProperty(@"names")]
            public List<string> Names { get; set; } = new List<string>();
        }
    }

    /// <summary>
    /// Hitsounds which are used at several places of the song.
    /// </summary>
    public sealed class HitsoundPattern
    {
        public readonly Guid ID;

        public string Name { get; set; }

        /// <summary>
        /// The index of the colour in <see cref="HitsoundPatterns.COLOURS"/>.
        /// </summary>
        public readonly int ColourIndex;

        /// <summary>
        /// The length in milliseconds.
        /// </summary>
        public readonly double Length;

        /// <summary>
        /// The last known hitsounds of the pattern, with times relative to its start.
        /// The places where the pattern is used are the actual source of its hitsounds, as they are changed by undoing.
        /// </summary>
        public IReadOnlyList<HitsoundColumnState> States { get; set; }

        public Colour4 Colour => HitsoundPatterns.COLOURS[ColourIndex % HitsoundPatterns.COLOURS.Length];

        public HitsoundPattern(Guid id, string name, int colourIndex, double length, IReadOnlyList<HitsoundColumnState> states)
        {
            ID = id;
            Name = name;
            ColourIndex = colourIndex;
            Length = length;
            States = states;
        }
    }

    /// <summary>
    /// A place where a pattern is used.
    /// </summary>
    public sealed record HitsoundPatternInstance(
        [property: JsonProperty(@"id")] Guid ID,
        [property: JsonProperty(@"pattern")] Guid PatternID,
        [property: JsonProperty(@"start_time")]
        double StartTime);

    public static class HitsoundPatternStates
    {
        /// <summary>
        /// Whether two lists of hitsounds sound the same.
        /// </summary>
        public static bool AreEqual(IReadOnlyList<HitsoundColumnState> a, IReadOnlyList<HitsoundColumnState> b)
        {
            if (a.Count != b.Count)
                return false;

            for (int i = 0; i < a.Count; i++)
            {
                if (!AreEqual(a[i], b[i]))
                    return false;
            }

            return true;
        }

        public static bool AreEqual(HitsoundColumnState a, HitsoundColumnState b)
            => Math.Abs(a.Time - b.Time) <= HitsoundEditor.PASTE_LENIENCY
               && a.Volume == b.Volume
               && a.CustomIndex == b.CustomIndex
               && a.NormalBanks.SequenceEqual(b.NormalBanks)
               && a.AdditionGroups.Count == b.AdditionGroups.Count
               && a.AdditionGroups.Zip(b.AdditionGroups).All(g => g.First.Bank == g.Second.Bank && g.First.Names.OrderBy(n => n).SequenceEqual(g.Second.Names.OrderBy(n => n)));
    }
}
