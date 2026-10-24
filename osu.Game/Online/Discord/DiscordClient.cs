// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Online.Discord
{
    public enum DiscordClientMode
    {
        /// <summary>
        /// The official Social SDK, which only provides direct messages with friends.
        /// </summary>
        [LocalisableDescription(typeof(SlopDiscordStrings), nameof(SlopDiscordStrings.ModeOfficialSdk))]
        Official,

        /// <summary>
        /// A full client logged in as the user, which provides servers as well.
        /// </summary>
        [LocalisableDescription(typeof(SlopDiscordStrings), nameof(SlopDiscordStrings.ModeFullClient))]
        FullClient,
    }

    public enum DiscordConnectionState
    {
        /// <summary>
        /// The client can't be used until something is set up (such as the application ID or the SDK library).
        /// </summary>
        NotConfigured,

        LoggedOut,

        /// <summary>
        /// A login was started and is waiting for the user (for example in the browser or on their phone).
        /// </summary>
        LoggingIn,

        Connecting,

        Ready,
    }

    /// <summary>
    /// A backend of the Discord overlay. All bindables and events are updated on the update thread.
    /// </summary>
    public abstract partial class DiscordClient : Component
    {
        public readonly Bindable<DiscordConnectionState> State = new Bindable<DiscordConnectionState>(DiscordConnectionState.LoggedOut);

        /// <summary>
        /// A message describing why the client can't be used or the last login failed.
        /// </summary>
        public readonly Bindable<string?> Error = new Bindable<string?>();

        public readonly Bindable<DiscordUser?> LocalUser = new Bindable<DiscordUser?>();

        /// <summary>
        /// The guilds of the local user, in the order they are shown in Discord.
        /// </summary>
        public readonly BindableList<DiscordGuild> Guilds = new BindableList<DiscordGuild>();

        /// <summary>
        /// The direct messages and groups of the local user, most recently active first.
        /// </summary>
        public readonly BindableList<DiscordChannel> PrivateChannels = new BindableList<DiscordChannel>();

        public event Action<DiscordMessage>? MessageCreated;

        /// <summary>
        /// Invoked when the status, name or avatar of a user changes.
        /// </summary>
        public event Action<DiscordUser>? UserUpdated;

        public event Action<DiscordMessage>? MessageUpdated;

        /// <summary>
        /// Invoked with the channel ID and the message ID of a deleted message.
        /// </summary>
        public event Action<ulong, ulong>? MessageDeleted;

        /// <summary>
        /// Invoked when the channels of a guild change.
        /// </summary>
        public event Action<DiscordGuild>? GuildChannelsChanged;

        /// <summary>
        /// Whether this client provides guilds. If not, only direct messages are shown.
        /// </summary>
        public abstract bool SupportsGuilds { get; }

        /// <summary>
        /// Whether older messages can be fetched beyond the first page.
        /// </summary>
        public abstract bool SupportsHistory { get; }

        protected readonly Dictionary<ulong, DiscordUser> Users = new Dictionary<ulong, DiscordUser>();

        /// <summary>
        /// Starts an interactive login.
        /// </summary>
        public abstract void Login();

        /// <summary>
        /// Cancels a pending login.
        /// </summary>
        public virtual void CancelLogin()
        {
        }

        /// <summary>
        /// Invoked when a guild is shown, so that its data can be completed if needed.
        /// </summary>
        public virtual void SelectGuild(DiscordGuild guild)
        {
        }

        /// <summary>
        /// Invoked when a channel is opened, so that its events can be subscribed to if needed.
        /// </summary>
        public virtual void SelectChannel(DiscordChannel channel)
        {
        }

        /// <summary>
        /// Logs out and forgets the stored credentials.
        /// </summary>
        public abstract void Logout();

        /// <summary>
        /// Fetches messages of a channel, oldest first.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="before">Only fetch messages older than this message.</param>
        public abstract Task<IReadOnlyList<DiscordMessage>> GetMessages(DiscordChannel channel, ulong? before = null);

        /// <summary>
        /// Sends a message. The returned task completes with the message as Discord sees it.
        /// </summary>
        public abstract Task<DiscordMessage> SendMessage(DiscordChannel channel, string content, string nonce);

        public DiscordUser? GetUser(ulong id) => Users.GetValueOrDefault(id);

        public DiscordChannel? GetChannel(ulong id)
        {
            foreach (var channel in PrivateChannels)
            {
                if (channel.Id == id)
                    return channel;
            }

            foreach (var guild in Guilds)
            {
                foreach (var channel in guild.Channels)
                {
                    if (channel.Id == id)
                        return channel;
                }
            }

            return null;
        }

        public DiscordGuild? GetGuild(ulong id) => Guilds.FirstOrDefault(g => g.Id == id);

        public DiscordRole? GetRole(ulong id)
        {
            foreach (var guild in Guilds)
            {
                foreach (var role in guild.Roles)
                {
                    if (role.Id == id)
                        return role;
                }
            }

            return null;
        }

        protected void StoreUser(DiscordUser user)
        {
            if (user.Id == 0)
                return;

            Users[user.Id] = user;
        }

        protected void TriggerMessageCreated(DiscordMessage message) => MessageCreated?.Invoke(message);

        protected void TriggerUserUpdated(DiscordUser user) => UserUpdated?.Invoke(user);

        protected void TriggerMessageUpdated(DiscordMessage message) => MessageUpdated?.Invoke(message);

        protected void TriggerMessageDeleted(ulong channelId, ulong messageId) => MessageDeleted?.Invoke(channelId, messageId);

        /// <summary>
        /// Creates the preview of a link in a message, which is only done when the user asks for it.
        /// </summary>
        public void LoadLinkPreview(DiscordMessage message, string url)
        {
            DiscordLinkPreviews.Resolve(url).ContinueWith(t => Schedule(() =>
            {
                var embed = t.GetResultSafely();

                TriggerMessageUpdated(new DiscordMessage
                {
                    Id = message.Id,
                    ChannelId = message.ChannelId,
                    Embeds = embed != null ? message.Embeds.Append(embed).ToList() : message.Embeds,
                    PreviewLinks = message.PreviewLinks.Where(l => l != url).ToList(),
                    IsPartial = true,
                });
            }));
        }

        protected void TriggerGuildChannelsChanged(DiscordGuild guild) => GuildChannelsChanged?.Invoke(guild);

        /// <summary>
        /// Moves a private channel to the top of the list because a message was received in it.
        /// </summary>
        protected void BumpPrivateChannel(ulong channelId, ulong messageId)
        {
            int index = -1;

            for (int i = 0; i < PrivateChannels.Count; i++)
            {
                if (PrivateChannels[i].Id == channelId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return;

            var channel = PrivateChannels[index];
            channel.LastMessageId = messageId;

            if (index > 0)
                PrivateChannels.Move(index, 0);
        }
    }

    public class DiscordRequestException : Exception
    {
        public DiscordRequestException(string message)
            : base(message)
        {
        }
    }
}
