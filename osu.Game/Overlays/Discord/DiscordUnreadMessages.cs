// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Counts the unread direct messages of each conversation.
    /// Discord can't be told that a message was read through the Social SDK, so this is only known to the client.
    /// </summary>
    public class DiscordUnreadMessages
    {
        private readonly Dictionary<ulong, int> counts = new Dictionary<ulong, int>();

        public event Action? Changed;

        public int Get(ulong channelId) => counts.GetValueOrDefault(channelId);

        public void Add(ulong channelId)
        {
            counts[channelId] = Get(channelId) + 1;
            Changed?.Invoke();
        }

        public void MarkRead(ulong channelId)
        {
            if (counts.Remove(channelId))
                Changed?.Invoke();
        }

        public void Clear()
        {
            if (counts.Count == 0)
                return;

            counts.Clear();
            Changed?.Invoke();
        }
    }
}
