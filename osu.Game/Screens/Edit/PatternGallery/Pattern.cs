// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace osu.Game.Screens.Edit.PatternGallery
{
    /// <summary>
    /// A saved group of hitobjects which can be inserted into any beatmap (see <see cref="PatternStore"/>).
    /// </summary>
    public class Pattern
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(@"N");

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The online ID of the ruleset the pattern was made in.
        /// </summary>
        public int RulesetId { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;

        public int UseCount { get; set; }

        public int CircleCount { get; set; }

        public int SliderCount { get; set; }

        public int SpinnerCount { get; set; }

        public double DurationMs { get; set; }

        public double DurationBeats { get; set; }

        public double SourceBeatLength { get; set; }

        public float SourceCircleSize { get; set; }

        [JsonIgnore]
        public int ObjectCount => CircleCount + SliderCount + SpinnerCount;

        [JsonIgnore]
        public double SourceBpm => SourceBeatLength > 0 ? 60000 / SourceBeatLength : 0;

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Name) ? @"(unnamed)" : Name;

        /// <summary>
        /// The object counts with the names of the objects of the pattern's ruleset, e.g. "3 notes, 1 hold note".
        /// </summary>
        [JsonIgnore]
        public string Breakdown
        {
            get
            {
                var (circle, slider, spinner) = RulesetId switch
                {
                    1 => ("note", "drumroll", "spinner"),
                    2 => ("fruit", "slider", "banana"),
                    3 => ("note", "hold note", "spinner"),
                    _ => ("circle", "slider", "spinner"),
                };

                var parts = new[] { (CircleCount, circle), (SliderCount, slider), (SpinnerCount, spinner) }
                            .Where(p => p.Item1 > 0)
                            .Select(p => $@"{p.Item1} {p.Item2}{(p.Item1 == 1 ? string.Empty : @"s")}");

                return string.Join(@", ", parts);
            }
        }

        private static readonly Regex comparison = new Regex(@"^(\w+)(<=|>=|!=|==|=|<|>)(.+)$", RegexOptions.Compiled);

        /// <summary>
        /// Whether the pattern matches every word of a search query.
        /// A word is either part of the name, or a filter like <c>c>3</c>, <c>b=4</c> or <c>name:jump</c>
        /// (keys: name, c, s, sp, o, b, bpm, cs, u).
        /// </summary>
        public bool Matches(string query) => query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(matchesWord);

        private bool matchesWord(string word)
        {
            int colon = word.IndexOf(':');
            var match = comparison.Match(colon > 0 ? word[..colon] + "=" + word[(colon + 1)..] : word);

            if (!match.Success)
                return Name.Contains(word, StringComparison.OrdinalIgnoreCase);

            string key = match.Groups[1].Value.ToLowerInvariant();
            string op = match.Groups[2].Value;
            string operand = match.Groups[3].Value;

            if (key == @"name")
                return Name.Contains(operand, StringComparison.OrdinalIgnoreCase) == (op != @"!=");

            if (!double.TryParse(operand, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                return Name.Contains(word, StringComparison.OrdinalIgnoreCase);

            double? actual = key switch
            {
                @"c" or @"circle" or @"circles" => CircleCount,
                @"s" or @"slider" or @"sliders" => SliderCount,
                @"sp" or @"spin" or @"spinner" or @"spinners" => SpinnerCount,
                @"o" or @"obj" or @"object" or @"objects" => ObjectCount,
                @"b" or @"beat" or @"beats" => Math.Round(DurationBeats, 2),
                @"bpm" => Math.Round(SourceBpm),
                @"cs" => Math.Round(SourceCircleSize, 1),
                @"u" or @"used" or @"uses" => UseCount,
                _ => null,
            };

            if (actual == null)
                return Name.Contains(word, StringComparison.OrdinalIgnoreCase);

            return op switch
            {
                @"<" => actual < value,
                @">" => actual > value,
                @"<=" => actual <= value,
                @">=" => actual >= value,
                @"!=" => actual != value,
                _ => actual == value,
            };
        }
    }
}
