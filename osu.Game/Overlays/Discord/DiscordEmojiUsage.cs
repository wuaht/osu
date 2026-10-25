// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Online.Discord;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Remembers how often each emoji was used, for the quick reactions and the frequently used emojis of the picker.
    /// </summary>
    public partial class DiscordEmojiUsage : Component
    {
        private static readonly DiscordReactionEmoji[] default_emojis =
        {
            new DiscordReactionEmoji { Name = @"❓" },
            new DiscordReactionEmoji { Name = @"✅" },
            new DiscordReactionEmoji { Name = @"💀" },
        };

        private readonly Bindable<string> setting = new Bindable<string>();

        private readonly Dictionary<DiscordReactionEmoji, int> counts = new Dictionary<DiscordReactionEmoji, int>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopDiscordEmojiUsage, setting);

            foreach (string line in setting.Value.Split('\n'))
            {
                string[] parts = line.Split('\t');

                if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                    continue;

                var emoji = deserialise(parts[1]);

                if (emoji != null)
                    counts[emoji] = count;
            }
        }

        /// <summary>
        /// The most used emojis, filled up with defaults while few emojis were used.
        /// </summary>
        public IReadOnlyList<DiscordReactionEmoji> GetMostUsed(int amount)
        {
            var result = counts.OrderByDescending(c => c.Value).Select(c => c.Key).Take(amount).ToList();

            foreach (var emoji in default_emojis)
            {
                if (result.Count >= amount)
                    break;

                if (!result.Contains(emoji))
                    result.Add(emoji);
            }

            return result;
        }

        public void Record(DiscordReactionEmoji emoji)
        {
            counts[emoji] = counts.GetValueOrDefault(emoji) + 1;

            // Only the most used emojis are kept, so the setting doesn't grow forever.
            setting.Value = string.Join('\n', counts.OrderByDescending(c => c.Value)
                                                    .Take(50)
                                                    .Select(c => $@"{c.Value.ToString(CultureInfo.InvariantCulture)}{'\t'}{serialise(c.Key)}"));
        }

        private static string serialise(DiscordReactionEmoji emoji) =>
            emoji.Id == null ? $@"u:{emoji.Name}" : $@"c:{emoji.Id}:{(emoji.Animated ? 1 : 0)}:{emoji.Name}";

        private static DiscordReactionEmoji? deserialise(string value)
        {
            if (value.StartsWith(@"u:"))
                return value.Length > 2 ? new DiscordReactionEmoji { Name = value[2..] } : null;

            string[] parts = value.Split(':', 4);

            if (parts.Length != 4 || parts[0] != @"c" || !ulong.TryParse(parts[1], out ulong id))
                return null;

            return new DiscordReactionEmoji { Id = id, Animated = parts[2] == @"1", Name = parts[3] };
        }
    }
}
