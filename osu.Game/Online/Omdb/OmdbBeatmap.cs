// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace osu.Game.Online.Omdb
{
    /// <summary>
    /// A beatmap on OMDB, with its ratings.
    /// </summary>
    public class OmdbBeatmap
    {
        public int SetID { get; set; }

        public int BeatmapID { get; set; }

        public string Artist { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Difficulty { get; set; } = string.Empty;

        public int? ChartRank { get; set; }

        public int? ChartYearRank { get; set; }

        public int? RatingCount { get; set; }

        public double? WeightedAvg { get; set; }

        /// <summary>
        /// The rating of the user the API key belongs to, or <c>null</c> if they haven't rated this beatmap.
        /// </summary>
        public double? OwnRating { get; set; }

        /// <summary>
        /// How many ratings each score (0 to 5 in steps of 0.5) has.
        /// </summary>
        [JsonConverter(typeof(OmdbRatingCountsConverter))]
        public Dictionary<double, int> Ratings { get; set; } = new Dictionary<double, int>();

        public OmdbDescriptor[] Descriptors { get; set; } = Array.Empty<OmdbDescriptor>();

        public OmdbNominator[] Nominators { get; set; } = Array.Empty<OmdbNominator>();
    }

    public class OmdbDescriptor
    {
        [JsonProperty(@"id")]
        public int Id { get; set; }

        [JsonProperty(@"name")]
        public string Name { get; set; } = string.Empty;
    }

    public class OmdbNominator
    {
        public int UserID { get; set; }

        public string Username { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reads the rating counts, which are sent as an object with the scores as keys and counts which may be strings,
    /// or as an empty array if there are no ratings.
    /// </summary>
    internal class OmdbRatingCountsConverter : JsonConverter<Dictionary<double, int>>
    {
        public override Dictionary<double, int> ReadJson(JsonReader reader, Type objectType, Dictionary<double, int>? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var counts = new Dictionary<double, int>();

            if (JToken.Load(reader) is not JObject obj)
                return counts;

            foreach (var property in obj.Properties())
            {
                if (!double.TryParse(property.Name, NumberStyles.Float, CultureInfo.InvariantCulture, out double score))
                    continue;

                if (int.TryParse(property.Value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                    counts[score] = count;
            }

            return counts;
        }

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, Dictionary<double, int>? value, JsonSerializer serializer) => throw new NotSupportedException();
    }
}
