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
using osu.Game.Audio;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The hitsound patterns of a difficulty: hitsounds which are used at several places of the song (like the patterns of FL Studio).
    /// Changing the hitsounds of one place changes all others (see <see cref="HitsoundEditor"/>).
    /// Patterns can contain other patterns (sub-patterns), which are then used at the same place in every place where the containing pattern is used.
    /// Stored in a file for each difficulty next to the other files of the game, as the beatmap format has no place for them.
    /// </summary>
    public class HitsoundPatterns : IEditorStateExtension
    {
        /// <summary>
        /// The directory containing a file for each difficulty, named by the ID of the difficulty.
        /// </summary>
        public const string DIRECTORY = @"hitsound-patterns";

        /// <summary>
        /// The maximum depth of sub-patterns, as a safety net against patterns containing themselves.
        /// </summary>
        private const int max_depth = 8;

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

        private List<HitsoundPattern> patterns;
        private List<HitsoundPatternInstance> instances;

        private List<HitsoundPatternOccurrence>? occurrences;

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

        /// <summary>
        /// The patterns of the difficulty which is being edited.
        /// </summary>
        public static HitsoundPatterns Create(Storage storage, EditorBeatmap editorBeatmap)
        {
            var metadata = editorBeatmap.BeatmapInfo.Metadata;
            return new HitsoundPatterns(storage, editorBeatmap.BeatmapInfo.ID, $@"{metadata.Artist} - {metadata.Title} [{editorBeatmap.BeatmapInfo.DifficultyName}]");
        }

        public IReadOnlyList<HitsoundPattern> Patterns => patterns;

        /// <summary>
        /// The places where patterns are used directly in the song (not as a sub-pattern), ordered by time.
        /// </summary>
        public IReadOnlyList<HitsoundPatternInstance> Instances => instances;

        /// <summary>
        /// All places where patterns are used, including sub-patterns inside every place of the pattern containing them, ordered by time and then depth.
        /// </summary>
        public IReadOnlyList<HitsoundPatternOccurrence> Occurrences => occurrences ??= computeOccurrences();

        public HitsoundPattern? GetPattern(Guid id) => patterns.FirstOrDefault(p => p.ID == id);

        public IEnumerable<HitsoundPatternInstance> GetInstances(HitsoundPattern pattern) => instances.Where(i => i.PatternID == pattern.ID);

        /// <summary>
        /// All places where a pattern is used, including places where it's used as a sub-pattern.
        /// </summary>
        public IEnumerable<HitsoundPatternOccurrence> GetOccurrences(HitsoundPattern pattern) => Occurrences.Where(o => o.Pattern == pattern);

        public double GetEndTime(HitsoundPatternInstance instance) => instance.StartTime + (GetPattern(instance.PatternID)?.Length ?? 0);

        /// <summary>
        /// Whether a time range overlaps a place where a pattern is used directly in the song.
        /// </summary>
        /// <param name="startTime">The start of the time range.</param>
        /// <param name="endTime">The end of the time range.</param>
        /// <param name="except">A place which isn't considered, e.g. the one being moved.</param>
        public bool Overlaps(double startTime, double endTime, HitsoundPatternInstance? except = null)
            => instances.Any(i => i != except && i.StartTime < endTime - HitsoundEditor.PASTE_LENIENCY && GetEndTime(i) > startTime + HitsoundEditor.PASTE_LENIENCY);

        /// <summary>
        /// The innermost place where a pattern is used which contains a whole time range, in which a sub-pattern could be used for the range.
        /// </summary>
        public HitsoundPatternOccurrence? FindContainingOccurrence(double startTime, double endTime)
            => Occurrences.Where(o => o.StartTime <= startTime + HitsoundEditor.PASTE_LENIENCY && o.EndTime >= endTime - HitsoundEditor.PASTE_LENIENCY)
                          .MaxBy(o => o.Depth);

        /// <summary>
        /// Whether a pattern can be used as a sub-pattern of another one, which isn't possible if the other pattern is (or is contained in) the pattern.
        /// </summary>
        public bool CanContain(HitsoundPattern parent, HitsoundPattern child) => parent != child && !contains(child, parent, 0);

        private bool contains(HitsoundPattern pattern, HitsoundPattern searched, int depth)
        {
            if (depth > max_depth)
                return true;

            foreach (var sub in pattern.SubPatterns)
            {
                if (GetPattern(sub.PatternID) is not HitsoundPattern child)
                    continue;

                if (child == searched || contains(child, searched, depth + 1))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a sub-pattern can be placed in a pattern at an offset: it has to fit into the pattern, and not overlap the other sub-patterns there.
        /// </summary>
        public bool CanPlaceSubPattern(HitsoundPattern parent, HitsoundPattern child, double offset)
            => CanContain(parent, child) && FitsSubPattern(parent, offset, child.Length);

        /// <summary>
        /// Whether a sub-pattern of a length fits into a pattern at an offset, without overlapping the other sub-patterns there.
        /// </summary>
        /// <param name="parent">The pattern which would contain the sub-pattern.</param>
        /// <param name="offset">The time from the start of <paramref name="parent"/> at which the sub-pattern would start.</param>
        /// <param name="length">The length of the sub-pattern.</param>
        /// <param name="except">A sub-pattern which isn't considered, e.g. the one being moved.</param>
        public bool FitsSubPattern(HitsoundPattern parent, double offset, double length, HitsoundSubPattern? except = null)
        {
            double end = offset + length;

            if (offset < -HitsoundEditor.PASTE_LENIENCY || end > parent.Length + HitsoundEditor.PASTE_LENIENCY)
                return false;

            return !parent.SubPatterns.Any(s => s != except
                                                && s.Offset < end - HitsoundEditor.PASTE_LENIENCY
                                                && s.Offset + (GetPattern(s.PatternID)?.Length ?? 0) > offset + HitsoundEditor.PASTE_LENIENCY);
        }

        /// <summary>
        /// Creates a pattern.
        /// </summary>
        /// <param name="length">The length of the pattern in milliseconds.</param>
        /// <param name="states">The hitsounds of the pattern, with times relative to its start.</param>
        /// <param name="name">The name, or <c>null</c> to number the pattern.</param>
        public HitsoundPattern Create(double length, IReadOnlyList<HitsoundColumnState> states, string? name = null)
        {
            int number = 1;

            while (patterns.Any(p => p.Name == defaultName(number)))
                number++;

            // the least used colour, such that patterns are distinguishable.
            int colourIndex = Enumerable.Range(0, COLOURS.Length).MinBy(c => patterns.Count(p => p.ColourIndex == c));

            var pattern = new HitsoundPattern(Guid.NewGuid(), name ?? defaultName(number), colourIndex, length, states);
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

            foreach (var other in patterns)
                other.SubPatterns.RemoveAll(s => s.PatternID == pattern.ID);

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
            sortInstances();

            onChanged();
            return instance;
        }

        /// <summary>
        /// Uses a pattern as a sub-pattern of another one, such that it is used in every place where the other pattern is used.
        /// </summary>
        /// <param name="parent">The pattern containing the sub-pattern.</param>
        /// <param name="child">The sub-pattern.</param>
        /// <param name="offset">The time from the start of <paramref name="parent"/> at which <paramref name="child"/> starts.</param>
        public HitsoundSubPattern AddSubPattern(HitsoundPattern parent, HitsoundPattern child, double offset)
        {
            var sub = new HitsoundSubPattern(Guid.NewGuid(), child.ID, Math.Max(0, offset));

            parent.SubPatterns.Add(sub);
            parent.SubPatterns.Sort((a, b) => a.Offset.CompareTo(b.Offset));

            onChanged();
            return sub;
        }

        /// <summary>
        /// Moves a place where a pattern is used to another time. Only the place is moved, its hitsounds have to be moved separately.
        /// For a sub-pattern, this moves it within the pattern containing it, so in all places where that pattern is used.
        /// </summary>
        /// <param name="occurrence">The place.</param>
        /// <param name="startTime">The new start time.</param>
        public void Move(HitsoundPatternOccurrence occurrence, double startTime)
        {
            if (occurrence.Instance != null)
            {
                int index = instances.IndexOf(occurrence.Instance);

                if (index < 0)
                    return;

                instances[index] = occurrence.Instance with { StartTime = startTime };
                sortInstances();
            }
            else if (occurrence.Parent != null && occurrence.SubPattern != null)
            {
                int index = occurrence.Parent.SubPatterns.IndexOf(occurrence.SubPattern);

                if (index < 0)
                    return;

                double parentStart = occurrence.StartTime - occurrence.SubPattern.Offset;

                occurrence.Parent.SubPatterns[index] = occurrence.SubPattern with { Offset = Math.Max(0, startTime - parentStart) };
                occurrence.Parent.SubPatterns.Sort((a, b) => a.Offset.CompareTo(b.Offset));
            }

            onChanged();
        }

        /// <summary>
        /// Whether a place where a pattern is used can be moved to another time without overlapping other places (or leaving the pattern containing it).
        /// </summary>
        public bool CanMove(HitsoundPatternOccurrence occurrence, double startTime)
        {
            if (occurrence.Instance != null)
                return !Overlaps(startTime, startTime + occurrence.Pattern.Length, occurrence.Instance);

            if (occurrence.Parent != null && occurrence.SubPattern != null)
            {
                double parentStart = occurrence.StartTime - occurrence.SubPattern.Offset;
                return FitsSubPattern(occurrence.Parent, startTime - parentStart, occurrence.Pattern.Length, occurrence.SubPattern);
            }

            return false;
        }

        /// <summary>
        /// Stops using a pattern at a place. Its hitsounds have to be removed separately (see <see cref="HitsoundEditor.RemovePatternOccurrence"/>).
        /// For a sub-pattern, this removes it from all places where the pattern containing it is used.
        /// </summary>
        public void Remove(HitsoundPatternOccurrence occurrence)
        {
            if (occurrence.Instance != null)
                instances.Remove(occurrence.Instance);
            else if (occurrence.Parent != null && occurrence.SubPattern != null)
                occurrence.Parent.SubPatterns.Remove(occurrence.SubPattern);

            onChanged();
        }

        /// <summary>
        /// Makes a place use a copy of its pattern (like "Make unique" in FL Studio), such that it can be changed without changing the other places.
        /// For a sub-pattern, the copy is used in all places where the pattern containing it is used.
        /// </summary>
        /// <param name="occurrence">The place.</param>
        /// <param name="states">The current hitsounds of the place, with times relative to its start.</param>
        /// <returns>The copy.</returns>
        public HitsoundPattern MakeUnique(HitsoundPatternOccurrence occurrence, IReadOnlyList<HitsoundColumnState> states)
        {
            var original = occurrence.Pattern;

            int number = 2;

            while (patterns.Any(p => p.Name == copyName(number)))
                number++;

            // created without notifying, as the place is switched to the copy below.
            int colourIndex = Enumerable.Range(0, COLOURS.Length).MinBy(c => patterns.Count(p => p.ColourIndex == c));
            var copy = new HitsoundPattern(Guid.NewGuid(), copyName(number), colourIndex, original.Length, states);
            copy.SubPatterns.AddRange(original.SubPatterns.Select(s => s with { ID = Guid.NewGuid() }));
            patterns.Add(copy);

            if (occurrence.Instance != null)
            {
                int index = instances.IndexOf(occurrence.Instance);

                if (index >= 0)
                    instances[index] = occurrence.Instance with { PatternID = copy.ID };
            }
            else if (occurrence.Parent != null && occurrence.SubPattern != null)
            {
                int index = occurrence.Parent.SubPatterns.IndexOf(occurrence.SubPattern);

                if (index >= 0)
                    occurrence.Parent.SubPatterns[index] = occurrence.SubPattern with { PatternID = copy.ID };
            }

            onChanged();
            return copy;

            string copyName(int n) => $@"{original.Name} #{n}";
        }

        private void sortInstances() => instances.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        private List<HitsoundPatternOccurrence> computeOccurrences()
        {
            var result = new List<HitsoundPatternOccurrence>();

            foreach (var instance in instances)
            {
                if (GetPattern(instance.PatternID) is HitsoundPattern pattern)
                    add(new HitsoundPatternOccurrence(pattern, instance.StartTime, 0, instance, null, null));
            }

            return result.OrderBy(o => o.StartTime).ThenBy(o => o.Depth).ToList();

            void add(HitsoundPatternOccurrence occurrence)
            {
                result.Add(occurrence);

                if (occurrence.Depth >= max_depth)
                    return;

                foreach (var sub in occurrence.Pattern.SubPatterns)
                {
                    if (GetPattern(sub.PatternID) is HitsoundPattern child)
                        add(new HitsoundPatternOccurrence(child, occurrence.StartTime + sub.Offset, occurrence.Depth + 1, null, occurrence.Pattern, sub));
                }
            }
        }

        /// <summary>
        /// Increased whenever patterns or the places where they are used change (but not their hitsounds).
        /// </summary>
        public int StructureVersion { get; private set; }

        private void onChanged()
        {
            StructureVersion++;
            occurrences = null;
            write();
            Changed?.Invoke();
        }

        private (List<HitsoundPattern>, List<HitsoundPatternInstance>) read()
        {
            try
            {
                if (storage.Exists(path))
                {
                    using (var stream = storage.GetStream(path))
                    using (var reader = new StreamReader(stream))
                        return deserialise(reader.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to read the hitsound patterns");
            }

            return (new List<HitsoundPattern>(), new List<HitsoundPatternInstance>());
        }

        private static (List<HitsoundPattern>, List<HitsoundPatternInstance>) deserialise(string json)
        {
            var file = JsonConvert.DeserializeObject<PatternsFile>(json);

            if (file == null)
                return (new List<HitsoundPattern>(), new List<HitsoundPatternInstance>());

            var readPatterns = file.Patterns.Select(p => p.ToPattern()).ToList();

            foreach (var pattern in readPatterns)
                pattern.SubPatterns.RemoveAll(s => readPatterns.All(p => p.ID != s.PatternID));

            var readInstances = file.Instances.Where(i => readPatterns.Any(p => p.ID == i.PatternID))
                                    .OrderBy(i => i.StartTime)
                                    .ToList();

            return (readPatterns, readInstances);
        }

        private string serialise() => JsonConvert.SerializeObject(new PatternsFile
        {
            Beatmap = beatmapTitle,
            Patterns = patterns.Select(PatternEntry.FromPattern).ToList(),
            Instances = instances.ToList(),
        }, Formatting.Indented);

        /// <summary>
        /// The patterns and where they are used, such that creating, placing and removing patterns can be undone along with the hitsounds.
        /// </summary>
        public string GetState() => serialise();

        public void ApplyState(string state)
        {
            try
            {
                (patterns, instances) = deserialise(state);
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to restore the hitsound patterns");
                return;
            }

            onChanged();
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

                using (var stream = storage.CreateFileSafely(path))
                using (var writer = new StreamWriter(stream))
                    writer.Write(serialise());
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

            [JsonProperty(@"sub_patterns")]
            public List<HitsoundSubPattern> SubPatterns { get; set; } = new List<HitsoundSubPattern>();

            public static PatternEntry FromPattern(HitsoundPattern pattern) => new PatternEntry
            {
                ID = pattern.ID,
                Name = pattern.Name,
                ColourIndex = pattern.ColourIndex,
                Length = pattern.Length,
                States = pattern.States.Select(StateEntry.FromState).ToList(),
                SubPatterns = pattern.SubPatterns.ToList(),
            };

            public HitsoundPattern ToPattern()
            {
                var pattern = new HitsoundPattern(ID, Name, ColourIndex, Length, States.Select(s => s.ToState()).ToList());
                pattern.SubPatterns.AddRange(SubPatterns);
                return pattern;
            }
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

            /// <summary>
            /// The exact samples of each target (see <see cref="HitsoundColumnState.TargetSamples"/>). Missing in files written by earlier versions.
            /// </summary>
            [JsonProperty(@"targets")]
            public List<List<SampleEntry>>? Targets { get; set; }

            public static StateEntry FromState(HitsoundColumnState state) => new StateEntry
            {
                Time = state.Time,
                NormalBanks = state.NormalBanks.ToList(),
                Additions = state.AdditionGroups.Select(g => new AdditionEntry { Bank = g.Bank, Names = g.Names.ToList() }).ToList(),
                Volume = state.Volume,
                CustomIndex = state.CustomIndex,
                // samples playing files directly (keysounds) can't be stored, so the exact samples are only stored without them.
                Targets = state.TargetSamples?.Any(t => t.Any(HitsoundSamples.IsFileSample)) == false
                    ? state.TargetSamples.Select(t => t.Select(SampleEntry.FromSample).ToList()).ToList()
                    : null,
            };

            public HitsoundColumnState ToState()
                => new HitsoundColumnState(Time, NormalBanks, Additions.Select(a => new HitsoundAdditionGroup(a.Bank, a.Names)).ToList(), Volume, CustomIndex)
                {
                    TargetSamples = Targets?.Select(t => (IReadOnlyList<HitSampleInfo>)t.Select(s => s.ToSample()).ToList()).ToList(),
                };
        }

        private class SampleEntry
        {
            [JsonProperty(@"name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty(@"bank")]
            public string Bank { get; set; } = string.Empty;

            [JsonProperty(@"suffix")]
            public string? Suffix { get; set; }

            [JsonProperty(@"volume")]
            public int Volume { get; set; }

            [JsonProperty(@"auto_bank")]
            public bool EditorAutoBank { get; set; }

            [JsonProperty(@"beatmap_samples")]
            public bool UseBeatmapSamples { get; set; }

            public static SampleEntry FromSample(HitSampleInfo sample) => new SampleEntry
            {
                Name = sample.Name,
                Bank = sample.Bank,
                Suffix = sample.Suffix,
                Volume = sample.Volume,
                EditorAutoBank = sample.EditorAutoBank,
                UseBeatmapSamples = sample.UseBeatmapSamples,
            };

            public HitSampleInfo ToSample() => new HitSampleInfo(Name, Bank, Suffix, Volume, EditorAutoBank, UseBeatmapSamples);
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

        /// <summary>
        /// The patterns used inside this pattern, ordered by their offset.
        /// </summary>
        public readonly List<HitsoundSubPattern> SubPatterns = new List<HitsoundSubPattern>();

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
    /// A place where a pattern is used directly in the song.
    /// </summary>
    public sealed record HitsoundPatternInstance(
        [property: JsonProperty(@"id")] Guid ID,
        [property: JsonProperty(@"pattern")] Guid PatternID,
        [property: JsonProperty(@"start_time")]
        double StartTime);

    /// <summary>
    /// A pattern used inside another pattern.
    /// </summary>
    public sealed record HitsoundSubPattern(
        [property: JsonProperty(@"id")] Guid ID,
        [property: JsonProperty(@"pattern")] Guid PatternID,
        [property: JsonProperty(@"offset")] double Offset);

    /// <summary>
    /// A place in the song where a pattern is used: directly (<see cref="Instance"/>), or as a sub-pattern inside a place where another pattern is used.
    /// </summary>
    /// <param name="Pattern">The pattern.</param>
    /// <param name="StartTime">The time at which it starts.</param>
    /// <param name="Depth">0 if the pattern is used directly, otherwise the number of patterns it's nested in.</param>
    /// <param name="Instance">The instance, if the pattern is used directly.</param>
    /// <param name="Parent">The pattern containing it, if it's a sub-pattern.</param>
    /// <param name="SubPattern">The sub-pattern entry in <paramref name="Parent"/>, if it's a sub-pattern.</param>
    public sealed record HitsoundPatternOccurrence(
        HitsoundPattern Pattern,
        double StartTime,
        int Depth,
        HitsoundPatternInstance? Instance,
        HitsoundPattern? Parent,
        HitsoundSubPattern? SubPattern)
    {
        public double EndTime => StartTime + Pattern.Length;
    }

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
               && a.AdditionGroups.Zip(b.AdditionGroups).All(g => g.First.Bank == g.Second.Bank && g.First.Names.OrderBy(n => n).SequenceEqual(g.Second.Names.OrderBy(n => n)))
               && targetSamplesEqual(a.TargetSamples, b.TargetSamples);

        private static bool targetSamplesEqual(IReadOnlyList<IReadOnlyList<HitSampleInfo>>? a, IReadOnlyList<IReadOnlyList<HitSampleInfo>>? b)
        {
            // hitsounds without exact samples (from files of earlier versions) are only compared by their summary.
            if (a == null || b == null)
                return true;

            return a.Count == b.Count && a.Zip(b).All(t => HitsoundSamples.AreEquivalent(t.First.ToList(), t.Second.ToList()));
        }
    }
}
