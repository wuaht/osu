// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace osu.Game.Online.Discord
{
    public enum DiscordChannelType
    {
        GuildText = 0,
        DirectMessage = 1,
        GuildVoice = 2,
        GroupDirectMessage = 3,
        GuildCategory = 4,
        GuildAnnouncement = 5,
        AnnouncementThread = 10,
        PublicThread = 11,
        PrivateThread = 12,
        GuildStageVoice = 13,
        GuildDirectory = 14,
        GuildForum = 15,
        GuildMedia = 16,
    }

    public enum DiscordUserStatus
    {
        /// <summary>
        /// The status is not provided by the client.
        /// </summary>
        Unknown,

        Online,
        Idle,
        DoNotDisturb,
        Offline,
    }

    public class DiscordUser
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("global_name")]
        public string? GlobalName { get; set; }

        [JsonProperty("avatar")]
        public string? Avatar { get; set; }

        [JsonProperty("bot")]
        public bool Bot { get; set; }

        /// <summary>
        /// The Nitro subscription of the user, which is only provided for the local user.
        /// </summary>
        [JsonProperty("premium_type")]
        public int PremiumType { get; set; }

        [JsonIgnore]
        public bool HasNitro => PremiumType > 0;

        /// <summary>
        /// The profile banner, which is only provided by some API endpoints.
        /// </summary>
        [JsonProperty("banner")]
        public string? Banner { get; set; }

        /// <summary>
        /// The profile colour, which is only provided by some API endpoints.
        /// </summary>
        [JsonProperty("accent_color")]
        public int? AccentColour { get; set; }

        /// <summary>
        /// A full avatar URL, which takes precedence over <see cref="Avatar"/> (the Social SDK only provides full URLs).
        /// </summary>
        [JsonIgnore]
        public string? AvatarUrlOverride { get; set; }

        [JsonIgnore]
        public DiscordUserStatus Status { get; set; }

        [JsonIgnore]
        public string DisplayName => string.IsNullOrEmpty(GlobalName) ? Username : GlobalName;

        public string GetAvatarUrl(int size = 64)
        {
            if (!string.IsNullOrEmpty(AvatarUrlOverride))
                return AvatarUrlOverride;

            if (string.IsNullOrEmpty(Avatar))
                return $@"https://cdn.discordapp.com/embed/avatars/{(Id >> 22) % 6}.png";

            return $@"https://cdn.discordapp.com/avatars/{Id}/{Avatar}.png?size={size}";
        }

        public string? GetBannerUrl(int size = 600) => string.IsNullOrEmpty(Banner) ? null : $@"https://cdn.discordapp.com/banners/{Id}/{Banner}.png?size={size}";
    }

    public class DiscordRole
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("color")]
        public int Colour { get; set; }

        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("permissions")]
        public string Permissions { get; set; } = "0";
    }

    public class DiscordCustomEmoji
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("animated")]
        public bool Animated { get; set; }

        /// <summary>
        /// Whether the emoji can be used, which it can't once the guild lost the boost level it was added with.
        /// </summary>
        [JsonProperty("available")]
        public bool Available { get; set; } = true;
    }

    /// <summary>
    /// The emoji of a reaction, which is either a unicode emoji (without ID) or a custom emoji.
    /// </summary>
    public class DiscordReactionEmoji : IEquatable<DiscordReactionEmoji>
    {
        [JsonProperty("id")]
        public ulong? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("animated")]
        public bool Animated { get; set; }

        /// <summary>
        /// The emoji as it is written in the URL of a reaction.
        /// </summary>
        [JsonIgnore]
        public string UrlName => Id == null ? Uri.EscapeDataString(Name ?? string.Empty) : $@"{Uri.EscapeDataString(Name ?? @"_")}:{Id}";

        public bool Equals(DiscordReactionEmoji? other)
        {
            if (other == null)
                return false;

            return Id != null || other.Id != null ? Id == other.Id : Name == other.Name;
        }

        public override bool Equals(object? obj) => obj is DiscordReactionEmoji other && Equals(other);

        public override int GetHashCode() => Id?.GetHashCode() ?? Name?.GetHashCode() ?? 0;
    }

    public enum DiscordReactionUpdateType
    {
        Add,
        Remove,

        /// <summary>
        /// All reactions of the message were removed.
        /// </summary>
        RemoveAll,

        /// <summary>
        /// All reactions with one emoji were removed.
        /// </summary>
        RemoveEmoji,
    }

    /// <param name="Burst">Whether the reaction is a super reaction.</param>
    /// <param name="BurstColours">The colours of a super reaction which was added, as hex strings.</param>
    public record DiscordReactionUpdate(ulong ChannelId, ulong MessageId, DiscordReactionEmoji? Emoji, ulong UserId, DiscordReactionUpdateType Type,
                                        bool Burst = false, IReadOnlyList<string>? BurstColours = null);

    /// <summary>
    /// A file which is sent with a message.
    /// </summary>
    public record DiscordUpload(string Filename, byte[] Data);

    public class DiscordReaction
    {
        [JsonProperty("emoji")]
        public DiscordReactionEmoji Emoji { get; set; } = new DiscordReactionEmoji();

        /// <summary>
        /// The number of normal reactions with this emoji.
        /// </summary>
        [JsonIgnore]
        public int Count { get; set; }

        /// <summary>
        /// The number of super reactions with this emoji.
        /// </summary>
        [JsonIgnore]
        public int BurstCount { get; set; }

        /// <summary>
        /// Whether the local user reacted with this emoji.
        /// </summary>
        [JsonProperty("me")]
        public bool Me { get; set; }

        /// <summary>
        /// Whether the local user super reacted with this emoji.
        /// </summary>
        [JsonProperty("me_burst")]
        public bool MeBurst { get; set; }

        /// <summary>
        /// The colours of the super reactions, as hex strings.
        /// </summary>
        [JsonProperty("burst_colors")]
        public List<string> BurstColours { get; set; } = new List<string>();

        // Discord counts both kinds of reactions in "count", and splits them in "count_details".
        [JsonProperty("count")]
        private int totalCount { get; set; }

        [JsonProperty("burst_count")]
        private int burstCount { get; set; }

        [JsonProperty("count_details")]
        private CountDetails? countDetails { get; set; }

        [OnDeserialized]
        private void onDeserialized(StreamingContext context)
        {
            BurstCount = countDetails?.Burst ?? burstCount;
            Count = countDetails?.Normal ?? Math.Max(0, totalCount - BurstCount);
        }

        private class CountDetails
        {
            [JsonProperty("normal")]
            public int Normal { get; set; }

            [JsonProperty("burst")]
            public int Burst { get; set; }
        }
    }

    public class DiscordMessageReference
    {
        [JsonProperty("channel_id")]
        public ulong? ChannelId { get; set; }

        [JsonProperty("message_id")]
        public ulong? MessageId { get; set; }
    }

    public class DiscordThreadMetadata
    {
        [JsonProperty("archived")]
        public bool Archived { get; set; }

        [JsonProperty("locked")]
        public bool Locked { get; set; }
    }

    public class DiscordThreadMember
    {
        [JsonProperty("join_timestamp")]
        public DateTimeOffset? JoinTimestamp { get; set; }
    }

    public class DiscordForumTag
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("emoji_name")]
        public string? EmojiName { get; set; }
    }

    /// <summary>
    /// The posts of a forum, as found by <see cref="DiscordClient.SearchThreads"/>.
    /// </summary>
    public class DiscordThreadSearchResult
    {
        public List<DiscordChannel> Threads { get; init; } = new List<DiscordChannel>();

        /// <summary>
        /// The first message of each post, by the ID of the post.
        /// </summary>
        public Dictionary<ulong, DiscordMessage> FirstMessages { get; init; } = new Dictionary<ulong, DiscordMessage>();
    }

    public class DiscordPermissionOverwrite
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        /// <summary>
        /// 0 for a role, 1 for a member.
        /// </summary>
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("allow")]
        public string Allow { get; set; } = "0";

        [JsonProperty("deny")]
        public string Deny { get; set; } = "0";
    }

    public class DiscordChannel
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("type")]
        public DiscordChannelType Type { get; set; }

        [JsonProperty("guild_id")]
        public ulong? GuildId { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("topic")]
        public string? Topic { get; set; }

        [JsonProperty("position")]
        public int Position { get; set; }

        [JsonProperty("parent_id")]
        public ulong? ParentId { get; set; }

        [JsonProperty("last_message_id")]
        public ulong? LastMessageId { get; set; }

        [JsonProperty("recipients")]
        public List<DiscordUser> Recipients { get; set; } = new List<DiscordUser>();

        [JsonProperty("recipient_ids")]
        public List<ulong>? RecipientIds { get; set; }

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("permission_overwrites")]
        public List<DiscordPermissionOverwrite> PermissionOverwrites { get; set; } = new List<DiscordPermissionOverwrite>();

        /// <summary>
        /// The user who created a thread.
        /// </summary>
        [JsonProperty("owner_id")]
        public ulong? OwnerId { get; set; }

        /// <summary>
        /// The number of messages in a thread, without its first message.
        /// </summary>
        [JsonProperty("message_count")]
        public int? MessageCount { get; set; }

        [JsonProperty("thread_metadata")]
        public DiscordThreadMetadata? ThreadMetadata { get; set; }

        /// <summary>
        /// The membership of the local user in a thread, if they joined it.
        /// </summary>
        [JsonProperty("member")]
        public DiscordThreadMember? Member { get; set; }

        /// <summary>
        /// The tags which can be applied to the posts of a forum.
        /// </summary>
        [JsonProperty("available_tags")]
        public List<DiscordForumTag> AvailableTags { get; set; } = new List<DiscordForumTag>();

        /// <summary>
        /// The tags of a forum post.
        /// </summary>
        [JsonProperty("applied_tags")]
        public List<ulong> AppliedTags { get; set; } = new List<ulong>();

        /// <summary>
        /// Whether this channel is a conversation outside of a guild (a direct message or a group).
        /// </summary>
        [JsonIgnore]
        public bool IsPrivate => Type == DiscordChannelType.DirectMessage || Type == DiscordChannelType.GroupDirectMessage;

        /// <summary>
        /// Whether this channel is a thread of a text channel or a post of a forum.
        /// </summary>
        [JsonIgnore]
        public bool IsThread => Type == DiscordChannelType.AnnouncementThread || Type == DiscordChannelType.PublicThread || Type == DiscordChannelType.PrivateThread;

        /// <summary>
        /// Whether this channel is a forum, which consists of posts instead of messages.
        /// </summary>
        [JsonIgnore]
        public bool IsForum => Type == DiscordChannelType.GuildForum || Type == DiscordChannelType.GuildMedia;

        /// <summary>
        /// Whether this thread is archived, which hides it from the channel list.
        /// </summary>
        [JsonIgnore]
        public bool IsArchived => ThreadMetadata?.Archived == true;

        /// <summary>
        /// Whether messages can be read and written in this channel as a list of messages.
        /// </summary>
        [JsonIgnore]
        public bool IsTextBased => IsPrivate || IsThread || Type == DiscordChannelType.GuildText || Type == DiscordChannelType.GuildAnnouncement || Type == DiscordChannelType.GuildVoice
                                   || Type == DiscordChannelType.GuildStageVoice;

        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Name))
                    return Name;

                if (Recipients.Count > 0)
                    return string.Join(@", ", Recipients.ConvertAll(r => r.DisplayName));

                return @"Unnamed";
            }
        }
    }

    public class DiscordGuild
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("icon")]
        public string? Icon { get; set; }

        [JsonProperty("owner_id")]
        public ulong OwnerId { get; set; }

        [JsonProperty("banner")]
        public string? Banner { get; set; }

        [JsonProperty("channels")]
        public List<DiscordChannel> Channels { get; set; } = new List<DiscordChannel>();

        /// <summary>
        /// The known threads of the guild, which are not part of <see cref="Channels"/>.
        /// </summary>
        [JsonProperty("threads")]
        public List<DiscordChannel> Threads { get; set; } = new List<DiscordChannel>();

        [JsonProperty("roles")]
        public List<DiscordRole> Roles { get; set; } = new List<DiscordRole>();

        [JsonProperty("emojis")]
        public List<DiscordCustomEmoji> Emojis { get; set; } = new List<DiscordCustomEmoji>();

        [JsonProperty("stickers")]
        public List<DiscordSticker> Stickers { get; set; } = new List<DiscordSticker>();

        /// <summary>
        /// The role IDs of the local user in this guild, if known.
        /// </summary>
        [JsonIgnore]
        public List<ulong>? LocalUserRoles { get; set; }

        public string? GetIconUrl(int size = 96) => string.IsNullOrEmpty(Icon) ? null : $@"https://cdn.discordapp.com/icons/{Id}/{Icon}.png?size={size}";

        public string? GetBannerUrl(int size = 480) => string.IsNullOrEmpty(Banner) ? null : $@"https://cdn.discordapp.com/banners/{Id}/{Banner}.png?size={size}";

        /// <summary>
        /// The initials of the guild name, shown in place of a missing icon.
        /// </summary>
        [JsonIgnore]
        public string Acronym
        {
            get
            {
                string acronym = string.Empty;

                foreach (string word in Name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    acronym += char.IsLetterOrDigit(word[0]) ? word[0] : string.Empty;

                    if (acronym.Length >= 3)
                        break;
                }

                return acronym.Length == 0 && Name.Length > 0 ? Name[..1] : acronym;
            }
        }
    }

    public class DiscordAttachment
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        [JsonProperty("proxy_url")]
        public string? ProxyUrl { get; set; }

        [JsonProperty("content_type")]
        public string? ContentType { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("width")]
        public int? Width { get; set; }

        [JsonProperty("height")]
        public int? Height { get; set; }

        [JsonIgnore]
        public bool IsImage => ContentType?.StartsWith(@"image/", StringComparison.Ordinal) == true || hasExtension(@".png", @".jpg", @".jpeg", @".gif", @".webp");

        [JsonIgnore]
        public bool IsAnimatedImage => ContentType == @"image/gif" || hasExtension(@".gif");

        [JsonIgnore]
        public bool IsVideo => ContentType?.StartsWith(@"video/", StringComparison.Ordinal) == true || hasExtension(@".mp4", @".webm", @".mov");

        [JsonIgnore]
        public bool IsAudio => ContentType?.StartsWith(@"audio/", StringComparison.Ordinal) == true || hasExtension(@".mp3", @".ogg", @".wav", @".flac", @".m4a", @".opus");

        private bool hasExtension(params string[] extensions)
        {
            foreach (string extension in extensions)
            {
                if (Filename.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    public class DiscordEmbedMedia
    {
        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("proxy_url")]
        public string? ProxyUrl { get; set; }

        [JsonProperty("width")]
        public int? Width { get; set; }

        [JsonProperty("height")]
        public int? Height { get; set; }
    }

    public class DiscordEmbedAuthor
    {
        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }
    }

    public class DiscordEmbedField
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("value")]
        public string Value { get; set; } = string.Empty;
    }

    public class DiscordEmbed
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("color")]
        public int? Colour { get; set; }

        [JsonProperty("author")]
        public DiscordEmbedAuthor? Author { get; set; }

        [JsonProperty("provider")]
        public DiscordEmbedAuthor? Provider { get; set; }

        [JsonProperty("fields")]
        public List<DiscordEmbedField> Fields { get; set; } = new List<DiscordEmbedField>();

        [JsonProperty("image")]
        public DiscordEmbedMedia? Image { get; set; }

        [JsonProperty("thumbnail")]
        public DiscordEmbedMedia? Thumbnail { get; set; }

        [JsonProperty("video")]
        public DiscordEmbedMedia? Video { get; set; }

        /// <summary>
        /// Whether this embed is only media (such as a linked image or GIF), which is shown without a frame.
        /// </summary>
        [JsonIgnore]
        public bool IsMediaOnly => Type == @"image" || Type == @"gifv";
    }

    public class DiscordSticker
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Whether a sticker of a guild can be used, which it can't when the guild lost the boosts it needs.
        /// </summary>
        [JsonProperty("available")]
        public bool Available { get; set; } = true;

        /// <summary>
        /// 1 = PNG, 2 = APNG, 3 = Lottie, 4 = GIF.
        /// </summary>
        [JsonProperty("format_type")]
        public int FormatType { get; set; }

        public string? GetUrl() => FormatType switch
        {
            1 or 2 => $@"https://media.discordapp.net/stickers/{Id}.png?size=160",
            4 => $@"https://media.discordapp.net/stickers/{Id}.gif?size=160",
            _ => null,
        };
    }

    /// <summary>
    /// A GIF of Discord's GIF picker, which is sent as its link.
    /// </summary>
    public class DiscordGif
    {
        /// <summary>
        /// The page of the GIF, which is sent as the message.
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string? Title { get; set; }

        /// <summary>
        /// A still image of the GIF, which some GIF providers give as a video instead.
        /// </summary>
        [JsonProperty("preview")]
        public string? Preview { get; set; }

        /// <summary>
        /// The GIF itself, which is a video when searching for videos.
        /// </summary>
        [JsonProperty("src")]
        public string? Src { get; set; }

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        /// <summary>
        /// Whether <see cref="Src"/> is a video instead of an image.
        /// </summary>
        [JsonIgnore]
        public bool IsVideo { get; set; }

        /// <summary>
        /// The position of a favourite GIF, where the newest one is the highest.
        /// </summary>
        [JsonIgnore]
        public int Order { get; set; }
    }

    /// <summary>
    /// The kind of content which the Social SDK only describes instead of providing it.
    /// </summary>
    public enum DiscordUnsupportedContentType
    {
        Other,
        Attachment,
        Poll,
        VoiceMessage,
        Thread,
        Embed,
        Sticker,
    }

    /// <summary>
    /// Content of a message which can only be viewed in Discord itself.
    /// </summary>
    public class DiscordUnsupportedContent
    {
        public DiscordUnsupportedContentType Type { get; set; }

        public string? Title { get; set; }

        public int Count { get; set; }
    }

    public class DiscordMessage
    {
        [JsonProperty("id")]
        public ulong Id { get; set; }

        [JsonProperty("channel_id")]
        public ulong ChannelId { get; set; }

        [JsonProperty("guild_id")]
        public ulong? GuildId { get; set; }

        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("author")]
        public DiscordUser Author { get; set; } = new DiscordUser();

        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty;

        [JsonProperty("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonProperty("edited_timestamp")]
        public DateTimeOffset? EditedTimestamp { get; set; }

        [JsonProperty("attachments")]
        public List<DiscordAttachment> Attachments { get; set; } = new List<DiscordAttachment>();

        [JsonProperty("embeds")]
        public List<DiscordEmbed> Embeds { get; set; } = new List<DiscordEmbed>();

        [JsonProperty("sticker_items")]
        public List<DiscordSticker> Stickers { get; set; } = new List<DiscordSticker>();

        [JsonProperty("mentions")]
        public List<DiscordUser> Mentions { get; set; } = new List<DiscordUser>();

        [JsonProperty("mention_everyone")]
        public bool MentionEveryone { get; set; }

        [JsonProperty("mention_roles")]
        public List<ulong> MentionRoles { get; set; } = new List<ulong>();

        [JsonProperty("reactions")]
        public List<DiscordReaction> Reactions { get; set; } = new List<DiscordReaction>();

        private DiscordMessage? referencedMessage;

        [JsonProperty("referenced_message")]
        public DiscordMessage? ReferencedMessage
        {
            get => referencedMessage;
            set
            {
                referencedMessage = value;
                ReferencedMessageProvided = true;
            }
        }

        /// <summary>
        /// Whether <see cref="ReferencedMessage"/> was provided. Discord only marks a deleted message with an explicit null,
        /// while some payloads (like messages of bots) leave the field out, in which case the message has to be fetched.
        /// </summary>
        [JsonIgnore]
        public bool ReferencedMessageProvided { get; private set; }

        [JsonProperty("message_reference")]
        public DiscordMessageReference? MessageReference { get; set; }

        /// <summary>
        /// The thread which was started from this message.
        /// </summary>
        [JsonProperty("thread")]
        public DiscordChannel? Thread { get; set; }

        /// <summary>
        /// Whether this is the notice that a thread was started, which links to the thread.
        /// </summary>
        [JsonIgnore]
        public bool IsThreadCreated => Type == 18;

        /// <summary>
        /// Whether this is the first message of a thread, which shows the message of the channel the thread was started from.
        /// </summary>
        [JsonIgnore]
        public bool IsThreadStarter => Type == 21;

        /// <summary>
        /// The nonce of a message sent by this client, used to replace the pending local echo once the message arrives.
        /// </summary>
        [JsonProperty("nonce")]
        public string? Nonce { get; set; }

        /// <summary>
        /// Whether this message was sent from this client and has not been confirmed by Discord yet.
        /// </summary>
        [JsonIgnore]
        public bool Pending { get; set; }

        /// <summary>
        /// Whether sending this message failed.
        /// </summary>
        [JsonIgnore]
        public bool Failed { get; set; }

        /// <summary>
        /// Whether this is an update which only contains some fields of the message (such as an embed that finished loading).
        /// </summary>
        [JsonIgnore]
        public bool IsPartial { get; set; }

        /// <summary>
        /// Content of the message which can't be shown, because the Social SDK doesn't provide it (like attachments).
        /// </summary>
        [JsonIgnore]
        public DiscordUnsupportedContent? UnsupportedContent { get; set; }

        /// <summary>
        /// Links whose preview is only created on the client when the user asks for it, as loading it reveals their IP address to the linked site.
        /// </summary>
        [JsonIgnore]
        public List<string> PreviewLinks { get; set; } = new List<string>();

        /// <summary>
        /// Whether this is a reply (type 19) to another message.
        /// </summary>
        [JsonIgnore]
        public bool IsReply => Type == 19;

        /// <summary>
        /// Applies a reaction which was added or removed.
        /// </summary>
        public void ApplyReaction(DiscordReactionUpdate update, ulong localUserId)
        {
            switch (update.Type)
            {
                case DiscordReactionUpdateType.RemoveAll:
                    Reactions.Clear();
                    return;

                case DiscordReactionUpdateType.RemoveEmoji:
                    Reactions.RemoveAll(r => r.Emoji.Equals(update.Emoji));
                    return;
            }

            if (update.Emoji == null)
                return;

            var reaction = Reactions.Find(r => r.Emoji.Equals(update.Emoji));
            bool byLocalUser = update.UserId == localUserId;

            if (update.Type == DiscordReactionUpdateType.Add)
            {
                if (reaction == null)
                    Reactions.Add(reaction = new DiscordReaction { Emoji = update.Emoji });

                // The local user can only react once with each emoji and kind.
                if (byLocalUser && (update.Burst ? reaction.MeBurst : reaction.Me))
                    return;

                if (update.Burst)
                {
                    reaction.BurstCount++;
                    reaction.MeBurst |= byLocalUser;

                    if (reaction.BurstColours.Count == 0 && update.BurstColours != null)
                        reaction.BurstColours.AddRange(update.BurstColours);
                }
                else
                {
                    reaction.Count++;
                    reaction.Me |= byLocalUser;
                }
            }
            else if (reaction != null)
            {
                if (byLocalUser && !(update.Burst ? reaction.MeBurst : reaction.Me))
                    return;

                if (update.Burst)
                {
                    reaction.BurstCount = Math.Max(0, reaction.BurstCount - 1);

                    if (byLocalUser)
                        reaction.MeBurst = false;
                }
                else
                {
                    reaction.Count = Math.Max(0, reaction.Count - 1);

                    if (byLocalUser)
                        reaction.Me = false;
                }

                if (reaction.Count == 0 && reaction.BurstCount == 0)
                    Reactions.Remove(reaction);
            }
        }
    }
}
