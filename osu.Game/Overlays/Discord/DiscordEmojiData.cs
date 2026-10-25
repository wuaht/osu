// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// A standard unicode emoji.
    /// </summary>
    /// <param name="Unicode">The emoji itself.</param>
    /// <param name="Label">The name of the emoji, like "grinning face".</param>
    /// <param name="Shortcodes">The names the emoji is written with in chats, like "grinning".</param>
    /// <param name="Tags">Further words the emoji is found with.</param>
    public record DiscordStandardEmoji(string Unicode, string Label, IReadOnlyList<string> Shortcodes, IReadOnlyList<string> Tags)
    {
        public bool Matches(string search) =>
            Label.Contains(search, StringComparison.OrdinalIgnoreCase)
            || Shortcodes.Any(s => s.Contains(search, StringComparison.OrdinalIgnoreCase))
            || Tags.Any(t => t.StartsWith(search, StringComparison.OrdinalIgnoreCase));
    }

    public record DiscordStandardEmojiCategory(LocalisableString Name, IReadOnlyList<DiscordStandardEmoji> Emojis);

    /// <summary>
    /// The list of standard emojis, which is loaded from the emojibase data set that matches the version of the Twemoji images.
    /// </summary>
    public static class DiscordEmojiData
    {
        public const string BASE_URL = @"https://cdn.jsdelivr.net/npm/emojibase-data@15.3.0/en/";

        private static readonly object load_lock = new object();
        private static Task<IReadOnlyList<DiscordStandardEmojiCategory>>? loadTask;

        /// <summary>
        /// Loads the emojis once, retrying on the next call if loading failed.
        /// </summary>
        public static Task<IReadOnlyList<DiscordStandardEmojiCategory>> Load()
        {
            lock (load_lock)
            {
                if (loadTask == null || loadTask.IsFaulted || loadTask.IsCanceled)
                    loadTask = Task.Run(load);

                return loadTask;
            }
        }

        private static async Task<IReadOnlyList<DiscordStandardEmojiCategory>> load()
        {
            var emojiData = DiscordMedia.Download(BASE_URL + @"compact.json", CancellationToken.None);
            var shortcodeData = DiscordMedia.Download(BASE_URL + @"shortcodes/github.json", CancellationToken.None);

            var emojis = JArray.Parse(Encoding.UTF8.GetString(await emojiData.ConfigureAwait(false)));
            var shortcodes = JObject.Parse(Encoding.UTF8.GetString(await shortcodeData.ConfigureAwait(false)));

            var groups = new SortedDictionary<int, List<(int order, DiscordStandardEmoji emoji)>>();

            foreach (var entry in emojis.OfType<JObject>())
            {
                int? group = entry.Value<int?>(@"group");
                string? unicode = entry.Value<string>(@"unicode");
                string? hexcode = entry.Value<string>(@"hexcode");

                // Entries without a group are the regional indicators, and group 2 are the skin tone components.
                if (group == null || group == 2 || unicode == null || hexcode == null)
                    continue;

                string[] codes = shortcodes[hexcode] switch
                {
                    JArray array => array.Values<string>().OfType<string>().ToArray(),
                    JValue value when value.Value is string code => new[] { code },
                    _ => Array.Empty<string>(),
                };

                var emoji = new DiscordStandardEmoji(
                    unicode,
                    entry.Value<string>(@"label") ?? string.Empty,
                    codes,
                    entry[@"tags"]?.Values<string>().OfType<string>().ToArray() ?? Array.Empty<string>());

                if (!groups.TryGetValue(group.Value, out var list))
                    groups[group.Value] = list = new List<(int, DiscordStandardEmoji)>();

                list.Add((entry.Value<int?>(@"order") ?? int.MaxValue, emoji));
            }

            return groups.Select(g => new DiscordStandardEmojiCategory(getCategoryName(g.Key), g.Value.OrderBy(e => e.order).Select(e => e.emoji).ToList())).ToList();
        }

        private static LocalisableString getCategoryName(int group) => group switch
        {
            0 => SlopDiscordStrings.EmojiCategorySmileys,
            1 => SlopDiscordStrings.EmojiCategoryPeople,
            3 => SlopDiscordStrings.EmojiCategoryNature,
            4 => SlopDiscordStrings.EmojiCategoryFood,
            5 => SlopDiscordStrings.EmojiCategoryTravel,
            6 => SlopDiscordStrings.EmojiCategoryActivities,
            7 => SlopDiscordStrings.EmojiCategoryObjects,
            8 => SlopDiscordStrings.EmojiCategorySymbols,
            9 => SlopDiscordStrings.EmojiCategoryFlags,
            _ => SlopDiscordStrings.EmojiCategoryOther,
        };
    }
}
