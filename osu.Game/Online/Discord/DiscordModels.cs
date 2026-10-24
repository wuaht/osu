// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
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
        /// A full avatar URL, which takes precedence over <see cref="Avatar"/> (the Social SDK only provides full URLs).
        /// </summary>
        [JsonIgnore]
        public string? AvatarUrlOverride { get; set; }

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
        /// Whether this channel is a conversation outside of a guild (a direct message or a group).
        /// </summary>
        [JsonIgnore]
        public bool IsPrivate => Type == DiscordChannelType.DirectMessage || Type == DiscordChannelType.GroupDirectMessage;

        /// <summary>
        /// Whether messages can be read and written in this channel as a list of messages.
        /// </summary>
        [JsonIgnore]
        public bool IsTextBased => IsPrivate || Type == DiscordChannelType.GuildText || Type == DiscordChannelType.GuildAnnouncement || Type == DiscordChannelType.GuildVoice
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

        [JsonProperty("channels")]
        public List<DiscordChannel> Channels { get; set; } = new List<DiscordChannel>();

        [JsonProperty("roles")]
        public List<DiscordRole> Roles { get; set; } = new List<DiscordRole>();

        /// <summary>
        /// The role IDs of the local user in this guild, if known.
        /// </summary>
        [JsonIgnore]
        public List<ulong>? LocalUserRoles { get; set; }

        public string? GetIconUrl(int size = 96) => string.IsNullOrEmpty(Icon) ? null : $@"https://cdn.discordapp.com/icons/{Id}/{Icon}.png?size={size}";

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

        [JsonProperty("referenced_message")]
        public DiscordMessage? ReferencedMessage { get; set; }

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
        /// Whether this is a reply (type 19) to another message.
        /// </summary>
        [JsonIgnore]
        public bool IsReply => Type == 19;
    }
}
