// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Counts the unread direct messages of each conversation, and the unread mentions of the local user in guild channels.
    /// Discord can't be told that a message was read through the Social SDK, so this is only known to the client.
    /// </summary>
    public class DiscordUnreadMessages
    {
        private readonly Dictionary<ulong, int> counts = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, ulong> channelGuilds = new Dictionary<ulong, ulong>();

        public event Action? Changed;

        public int Get(ulong channelId) => counts.GetValueOrDefault(channelId);

        /// <summary>
        /// The number of unread mentions in all channels of a guild.
        /// </summary>
        public int GetGuild(ulong guildId) => counts.Where(c => channelGuilds.TryGetValue(c.Key, out ulong g) && g == guildId).Sum(c => c.Value);

        public void Add(ulong channelId, ulong? guildId = null)
        {
            counts[channelId] = Get(channelId) + 1;

            if (guildId is ulong id)
                channelGuilds[channelId] = id;

            Changed?.Invoke();
        }

        public void MarkRead(ulong channelId)
        {
            channelGuilds.Remove(channelId);

            if (counts.Remove(channelId))
                Changed?.Invoke();
        }

        public void Clear()
        {
            if (counts.Count == 0)
                return;

            counts.Clear();
            channelGuilds.Clear();
            Changed?.Invoke();
        }
    }
}
