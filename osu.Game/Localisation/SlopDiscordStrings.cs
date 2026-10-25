// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopDiscordStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopDiscord";

        /// <summary>
        /// "Discord"
        /// </summary>
        public static LocalisableString Title => new TranslatableString(getKey(@"title"), @"Discord");

        /// <summary>
        /// "chat with your friends and servers"
        /// </summary>
        public static LocalisableString Description => new TranslatableString(getKey(@"description"), @"chat with your friends and servers");

        /// <summary>
        /// "Toggle Discord"
        /// </summary>
        public static LocalisableString ToggleDiscord => new TranslatableString(getKey(@"toggle_discord"), @"Toggle Discord");

        /// <summary>
        /// "Direct Messages"
        /// </summary>
        public static LocalisableString DirectMessages => new TranslatableString(getKey(@"direct_messages"), @"Direct Messages");

        /// <summary>
        /// "Friends"
        /// </summary>
        public static LocalisableString Friends => new TranslatableString(getKey(@"friends"), @"Friends");

        /// <summary>
        /// "Log in with Discord"
        /// </summary>
        public static LocalisableString LoginWithDiscord => new TranslatableString(getKey(@"login_with_discord"), @"Log in with Discord");

        /// <summary>
        /// "Your browser will open to authorise this client. Afterwards you can chat with your Discord friends."
        /// </summary>
        public static LocalisableString LoginBrowserDescription =>
            new TranslatableString(getKey(@"login_browser_description"), @"Your browser will open to authorise this client. Afterwards you can chat with your Discord friends.");

        /// <summary>
        /// "Waiting for the browser..."
        /// </summary>
        public static LocalisableString WaitingForBrowser => new TranslatableString(getKey(@"waiting_for_browser"), @"Waiting for the browser...");

        /// <summary>
        /// "Log in with QR code"
        /// </summary>
        public static LocalisableString LoginWithQrCode => new TranslatableString(getKey(@"login_with_qr_code"), @"Log in with QR code");

        /// <summary>
        /// "Scan this with the Discord app on your phone to log in."
        /// </summary>
        public static LocalisableString ScanQrCode => new TranslatableString(getKey(@"scan_qr_code"), @"Scan this with the Discord app on your phone to log in.");

        /// <summary>
        /// "Check your phone"
        /// </summary>
        public static LocalisableString CheckYourPhone => new TranslatableString(getKey(@"check_your_phone"), @"Check your phone");

        /// <summary>
        /// "Logging in as {0}"
        /// </summary>
        public static LocalisableString LoggingInAs(string name) => new TranslatableString(getKey(@"logging_in_as"), @"Logging in as {0}", name);

        /// <summary>
        /// "The full client logs in with your user account, which is against the Discord Terms of Service and can get your account banned. Use at your own risk."
        /// </summary>
        public static LocalisableString FullClientWarning => new TranslatableString(getKey(@"full_client_warning"),
            @"The full client logs in with your user account, which is against the Discord Terms of Service and can get your account banned. Use at your own risk.");

        /// <summary>
        /// "Connecting..."
        /// </summary>
        public static LocalisableString Connecting => new TranslatableString(getKey(@"connecting"), @"Connecting...");

        /// <summary>
        /// "Open settings"
        /// </summary>
        public static LocalisableString OpenSettings => new TranslatableString(getKey(@"open_settings"), @"Open settings");

        /// <summary>
        /// "Retry"
        /// </summary>
        public static LocalisableString Retry => new TranslatableString(getKey(@"retry"), @"Retry");

        /// <summary>
        /// "Cancel"
        /// </summary>
        public static LocalisableString Cancel => new TranslatableString(getKey(@"cancel"), @"Cancel");

        /// <summary>
        /// "Message {0}"
        /// </summary>
        public static LocalisableString MessagePlaceholder(string channel) => new TranslatableString(getKey(@"message_placeholder"), @"Message {0}", channel);

        /// <summary>
        /// "Select a conversation to start chatting."
        /// </summary>
        public static LocalisableString SelectConversation => new TranslatableString(getKey(@"select_conversation"), @"Select a conversation to start chatting.");

        /// <summary>
        /// "This is the beginning of {0}."
        /// </summary>
        public static LocalisableString ChannelStart(string channel) => new TranslatableString(getKey(@"channel_start"), @"This is the beginning of {0}.", channel);

        /// <summary>
        /// "Only the most recent messages are available with the official SDK."
        /// </summary>
        public static LocalisableString HistoryUnavailable =>
            new TranslatableString(getKey(@"history_unavailable"), @"Only the most recent messages are available with the official SDK.");

        /// <summary>
        /// "No conversations yet."
        /// </summary>
        public static LocalisableString NoConversations => new TranslatableString(getKey(@"no_conversations"), @"No conversations yet.");

        /// <summary>
        /// "Messages could not be loaded: {0}"
        /// </summary>
        public static LocalisableString LoadFailed(string error) => new TranslatableString(getKey(@"load_failed"), @"Messages could not be loaded: {0}", error);

        /// <summary>
        /// "Sending failed"
        /// </summary>
        public static LocalisableString SendFailed => new TranslatableString(getKey(@"send_failed"), @"Sending failed");

        /// <summary>
        /// "(edited)"
        /// </summary>
        public static LocalisableString Edited => new TranslatableString(getKey(@"edited"), @"(edited)");

        /// <summary>
        /// "Today at {0}"
        /// </summary>
        public static LocalisableString TodayAt(string time) => new TranslatableString(getKey(@"today_at"), @"Today at {0}", time);

        /// <summary>
        /// "Yesterday at {0}"
        /// </summary>
        public static LocalisableString YesterdayAt(string time) => new TranslatableString(getKey(@"yesterday_at"), @"Yesterday at {0}", time);

        /// <summary>
        /// "Log out"
        /// </summary>
        public static LocalisableString Logout => new TranslatableString(getKey(@"logout"), @"Log out");

        /// <summary>
        /// "Open in browser"
        /// </summary>
        public static LocalisableString OpenInBrowser => new TranslatableString(getKey(@"open_in_browser"), @"Open in browser");

        /// <summary>
        /// "Open in DMs"
        /// </summary>
        public static LocalisableString OpenInDirectMessages => new TranslatableString(getKey(@"open_in_direct_messages"), @"Open in DMs");

        /// <summary>
        /// "Close"
        /// </summary>
        public static LocalisableString CloseViewer => new TranslatableString(getKey(@"close_viewer"), @"Close");

        /// <summary>
        /// "Play"
        /// </summary>
        public static LocalisableString PlayVideo => new TranslatableString(getKey(@"play_video"), @"Play");

        /// <summary>
        /// "Pause"
        /// </summary>
        public static LocalisableString PauseVideo => new TranslatableString(getKey(@"pause_video"), @"Pause");

        /// <summary>
        /// "Mute"
        /// </summary>
        public static LocalisableString MuteVideo => new TranslatableString(getKey(@"mute_video"), @"Mute");

        /// <summary>
        /// "Unmute"
        /// </summary>
        public static LocalisableString UnmuteVideo => new TranslatableString(getKey(@"unmute_video"), @"Unmute");

        /// <summary>
        /// "Official SDK"
        /// </summary>
        public static LocalisableString ModeOfficialSdk => new TranslatableString(getKey(@"mode_official_sdk"), @"Official SDK");

        /// <summary>
        /// "Full client"
        /// </summary>
        public static LocalisableString ModeFullClient => new TranslatableString(getKey(@"mode_full_client"), @"Full client");

        /// <summary>
        /// "Search friends"
        /// </summary>
        public static LocalisableString SearchFriends => new TranslatableString(getKey(@"search_friends"), @"Search friends");

        /// <summary>
        /// "{0} members"
        /// </summary>
        public static LocalisableString Members(int count) => new TranslatableString(getKey(@"members"), @"{0} members", count);

        /// <summary>
        /// "You have no friends on Discord yet."
        /// </summary>
        public static LocalisableString NoFriends => new TranslatableString(getKey(@"no_friends"), @"You have no friends on Discord yet.");

        /// <summary>
        /// "No friends match your search."
        /// </summary>
        public static LocalisableString NoFriendsFound => new TranslatableString(getKey(@"no_friends_found"), @"No friends match your search.");

        /// <summary>
        /// "Online"
        /// </summary>
        public static LocalisableString StatusOnline => new TranslatableString(getKey(@"status_online"), @"Online");

        /// <summary>
        /// "Idle"
        /// </summary>
        public static LocalisableString StatusIdle => new TranslatableString(getKey(@"status_idle"), @"Idle");

        /// <summary>
        /// "Do Not Disturb"
        /// </summary>
        public static LocalisableString StatusDoNotDisturb => new TranslatableString(getKey(@"status_do_not_disturb"), @"Do Not Disturb");

        /// <summary>
        /// "Offline"
        /// </summary>
        public static LocalisableString StatusOffline => new TranslatableString(getKey(@"status_offline"), @"Offline");

        /// <summary>
        /// "{0}: {1}"
        /// </summary>
        public static LocalisableString MessageNotification(string author, string content) => new TranslatableString(getKey(@"message_notification"), @"{0}: {1}", author, content);

        /// <summary>
        /// "NEW"
        /// </summary>
        public static LocalisableString NewMentions => new TranslatableString(getKey(@"new_mentions"), @"NEW");

        /// <summary>
        /// "{0} in #{1}: {2}"
        /// </summary>
        public static LocalisableString GuildMessageNotification(string author, string channel, string content) => new TranslatableString(getKey(@"guild_message_notification"), @"{0} in #{1}: {2}", author, channel, content);

        /// <summary>
        /// "{0} mentioned you in #{1}"
        /// </summary>
        public static LocalisableString GuildAttachmentNotification(string author, string channel) => new TranslatableString(getKey(@"guild_attachment_notification"), @"{0} mentioned you in #{1}", author, channel);

        /// <summary>
        /// "{0} sent an attachment"
        /// </summary>
        public static LocalisableString AttachmentNotification(string author) => new TranslatableString(getKey(@"attachment_notification"), @"{0} sent an attachment", author);

        /// <summary>
        /// "Attachment"
        /// </summary>
        public static LocalisableString UnsupportedAttachment => new TranslatableString(getKey(@"unsupported_attachment"), @"Attachment");

        /// <summary>
        /// "{0} attachments"
        /// </summary>
        public static LocalisableString UnsupportedAttachments(int count) => new TranslatableString(getKey(@"unsupported_attachments"), @"{0} attachments", count);

        /// <summary>
        /// "Poll"
        /// </summary>
        public static LocalisableString UnsupportedPoll => new TranslatableString(getKey(@"unsupported_poll"), @"Poll");

        /// <summary>
        /// "Voice message"
        /// </summary>
        public static LocalisableString UnsupportedVoiceMessage => new TranslatableString(getKey(@"unsupported_voice_message"), @"Voice message");

        /// <summary>
        /// "Thread"
        /// </summary>
        public static LocalisableString UnsupportedThread => new TranslatableString(getKey(@"unsupported_thread"), @"Thread");

        /// <summary>
        /// "Embed"
        /// </summary>
        public static LocalisableString UnsupportedEmbed => new TranslatableString(getKey(@"unsupported_embed"), @"Embed");

        /// <summary>
        /// "Sticker"
        /// </summary>
        public static LocalisableString UnsupportedSticker => new TranslatableString(getKey(@"unsupported_sticker"), @"Sticker");

        /// <summary>
        /// "Unsupported content"
        /// </summary>
        public static LocalisableString UnsupportedOther => new TranslatableString(getKey(@"unsupported_other"), @"Unsupported content");

        /// <summary>
        /// "Can only be viewed in Discord"
        /// </summary>
        public static LocalisableString UnsupportedContentHint => new TranslatableString(getKey(@"unsupported_content_hint"), @"Can only be viewed in Discord");

        /// <summary>
        /// "Load preview"
        /// </summary>
        public static LocalisableString LoadLinkPreview => new TranslatableString(getKey(@"load_link_preview"), @"Load preview");

        /// <summary>
        /// "Loading preview..."
        /// </summary>
        public static LocalisableString LoadingLinkPreview => new TranslatableString(getKey(@"loading_link_preview"), @"Loading preview...");

        /// <summary>
        /// "Connects to this site to load the preview"
        /// </summary>
        public static LocalisableString LinkPreviewHint => new TranslatableString(getKey(@"link_preview_hint"), @"Connects to this site to load the preview");

        /// <summary>
        /// "Show or hide the servers"
        /// </summary>
        public static LocalisableString ToggleServers => new TranslatableString(getKey(@"toggle_servers"), @"Show or hide the servers");

        /// <summary>
        /// "Show or hide the channels"
        /// </summary>
        public static LocalisableString ToggleChannels => new TranslatableString(getKey(@"toggle_channels"), @"Show or hide the channels");

        /// <summary>
        /// "started a thread"
        /// </summary>
        public static LocalisableString StartedThread => new TranslatableString(getKey(@"started_thread"), @"started a thread");

        /// <summary>
        /// "{0} messages"
        /// </summary>
        public static LocalisableString ThreadMessages(int count) => new TranslatableString(getKey(@"thread_messages"), @"{0} messages", count);

        /// <summary>
        /// "Open thread"
        /// </summary>
        public static LocalisableString OpenThread => new TranslatableString(getKey(@"open_thread"), @"Open thread");

        /// <summary>
        /// "Archived"
        /// </summary>
        public static LocalisableString ArchivedThread => new TranslatableString(getKey(@"archived_thread"), @"Archived");

        /// <summary>
        /// "There are no posts in this forum yet."
        /// </summary>
        public static LocalisableString ForumEmpty => new TranslatableString(getKey(@"forum_empty"), @"There are no posts in this forum yet.");

        /// <summary>
        /// "Posts could not be loaded: {0}"
        /// </summary>
        public static LocalisableString ForumLoadFailed(string error) => new TranslatableString(getKey(@"forum_load_failed"), @"Posts could not be loaded: {0}", error);

        /// <summary>
        /// "Original message was deleted"
        /// </summary>
        public static LocalisableString ReplyOriginalDeleted => new TranslatableString(getKey(@"reply_original_deleted"), @"Original message was deleted");

        /// <summary>
        /// "Loading the original message…"
        /// </summary>
        public static LocalisableString ReplyOriginalLoading => new TranslatableString(getKey(@"reply_original_loading"), @"Loading the original message…");

        /// <summary>
        /// "The original message could not be loaded"
        /// </summary>
        public static LocalisableString ReplyOriginalUnavailable => new TranslatableString(getKey(@"reply_original_unavailable"), @"The original message could not be loaded");

        /// <summary>
        /// "Smileys & Emotion"
        /// </summary>
        public static LocalisableString EmojiCategorySmileys => new TranslatableString(getKey(@"emoji_category_smileys"), @"Smileys & Emotion");

        /// <summary>
        /// "People & Body"
        /// </summary>
        public static LocalisableString EmojiCategoryPeople => new TranslatableString(getKey(@"emoji_category_people"), @"People & Body");

        /// <summary>
        /// "Animals & Nature"
        /// </summary>
        public static LocalisableString EmojiCategoryNature => new TranslatableString(getKey(@"emoji_category_nature"), @"Animals & Nature");

        /// <summary>
        /// "Food & Drink"
        /// </summary>
        public static LocalisableString EmojiCategoryFood => new TranslatableString(getKey(@"emoji_category_food"), @"Food & Drink");

        /// <summary>
        /// "Travel & Places"
        /// </summary>
        public static LocalisableString EmojiCategoryTravel => new TranslatableString(getKey(@"emoji_category_travel"), @"Travel & Places");

        /// <summary>
        /// "Activities"
        /// </summary>
        public static LocalisableString EmojiCategoryActivities => new TranslatableString(getKey(@"emoji_category_activities"), @"Activities");

        /// <summary>
        /// "Objects"
        /// </summary>
        public static LocalisableString EmojiCategoryObjects => new TranslatableString(getKey(@"emoji_category_objects"), @"Objects");

        /// <summary>
        /// "Symbols"
        /// </summary>
        public static LocalisableString EmojiCategorySymbols => new TranslatableString(getKey(@"emoji_category_symbols"), @"Symbols");

        /// <summary>
        /// "Flags"
        /// </summary>
        public static LocalisableString EmojiCategoryFlags => new TranslatableString(getKey(@"emoji_category_flags"), @"Flags");

        /// <summary>
        /// "Other"
        /// </summary>
        public static LocalisableString EmojiCategoryOther => new TranslatableString(getKey(@"emoji_category_other"), @"Other");

        /// <summary>
        /// "Frequently Used"
        /// </summary>
        public static LocalisableString EmojiFrequentlyUsed => new TranslatableString(getKey(@"emoji_frequently_used"), @"Frequently Used");

        /// <summary>
        /// "Find the perfect emoji"
        /// </summary>
        public static LocalisableString EmojiSearchPlaceholder => new TranslatableString(getKey(@"emoji_search_placeholder"), @"Find the perfect emoji");

        /// <summary>
        /// "Emojis could not be loaded: {0}"
        /// </summary>
        public static LocalisableString EmojiLoadFailed(string error) => new TranslatableString(getKey(@"emoji_load_failed"), @"Emojis could not be loaded: {0}", error);

        /// <summary>
        /// "No emoji matches your search"
        /// </summary>
        public static LocalisableString EmojiNoResults => new TranslatableString(getKey(@"emoji_no_results"), @"No emoji matches your search");

        /// <summary>
        /// "Select emoji"
        /// </summary>
        public static LocalisableString SelectEmoji => new TranslatableString(getKey(@"select_emoji"), @"Select emoji");

        /// <summary>
        /// "Add Reaction"
        /// </summary>
        public static LocalisableString AddReaction => new TranslatableString(getKey(@"add_reaction"), @"Add Reaction");

        /// <summary>
        /// "Super Reaction"
        /// </summary>
        public static LocalisableString SuperReaction => new TranslatableString(getKey(@"super_reaction"), @"Super Reaction");

        /// <summary>
        /// "Send a GIF"
        /// </summary>
        public static LocalisableString SelectGif => new TranslatableString(getKey(@"select_gif"), @"Send a GIF");

        /// <summary>
        /// "Send a sticker"
        /// </summary>
        public static LocalisableString SelectSticker => new TranslatableString(getKey(@"select_sticker"), @"Send a sticker");

        /// <summary>
        /// "Search GIFs"
        /// </summary>
        public static LocalisableString GifSearchPlaceholder => new TranslatableString(getKey(@"gif_search_placeholder"), @"Search GIFs");

        /// <summary>
        /// "Search stickers"
        /// </summary>
        public static LocalisableString StickerSearchPlaceholder => new TranslatableString(getKey(@"sticker_search_placeholder"), @"Search stickers");

        /// <summary>
        /// "No GIFs found."
        /// </summary>
        public static LocalisableString GifNoResults => new TranslatableString(getKey(@"gif_no_results"), @"No GIFs found.");

        /// <summary>
        /// "Couldn't load the GIFs: {0}"
        /// </summary>
        public static LocalisableString GifLoadFailed(string error) => new TranslatableString(getKey(@"gif_load_failed"), @"Couldn't load the GIFs: {0}", error);

        /// <summary>
        /// "There are no stickers which can be sent here."
        /// </summary>
        public static LocalisableString NoStickers => new TranslatableString(getKey(@"no_stickers"), @"There are no stickers which can be sent here.");

        /// <summary>
        /// "Favourite GIFs"
        /// </summary>
        public static LocalisableString FavouriteGifs => new TranslatableString(getKey(@"favourite_gifs"), @"Favourite GIFs");

        /// <summary>
        /// "Add to favourites"
        /// </summary>
        public static LocalisableString AddGifToFavourites => new TranslatableString(getKey(@"add_gif_to_favourites"), @"Add to favourites");

        /// <summary>
        /// "Remove from favourites"
        /// </summary>
        public static LocalisableString RemoveGifFromFavourites => new TranslatableString(getKey(@"remove_gif_from_favourites"), @"Remove from favourites");

        /// <summary>
        /// "You have no favourite GIFs yet. Click the star on a GIF to add it."
        /// </summary>
        public static LocalisableString NoFavouriteGifs => new TranslatableString(getKey(@"no_favourite_gifs"), @"You have no favourite GIFs yet. Click the star on a GIF to add it.");

        /// <summary>
        /// "{0} reacted with {1}"
        /// </summary>
        public static LocalisableString ReactedWith(LocalisableString users, string emoji) => new TranslatableString(getKey(@"reacted_with"), @"{0} reacted with {1}", users, emoji);

        /// <summary>
        /// "{0} and {1} others"
        /// </summary>
        public static LocalisableString ReactionUsersAndOthers(string users, int others) => new TranslatableString(getKey(@"reaction_users_and_others"), @"{0} and {1} others", users, others);

        /// <summary>
        /// "Reply"
        /// </summary>
        public static LocalisableString ReplyToMessage => new TranslatableString(getKey(@"reply_to_message"), @"Reply");

        /// <summary>
        /// "Edit Message"
        /// </summary>
        public static LocalisableString EditMessage => new TranslatableString(getKey(@"edit_message"), @"Edit Message");

        /// <summary>
        /// "Delete Message"
        /// </summary>
        public static LocalisableString DeleteMessage => new TranslatableString(getKey(@"delete_message"), @"Delete Message");

        /// <summary>
        /// "Copy Text"
        /// </summary>
        public static LocalisableString CopyText => new TranslatableString(getKey(@"copy_text"), @"Copy Text");

        /// <summary>
        /// "Copy Message ID"
        /// </summary>
        public static LocalisableString CopyMessageId => new TranslatableString(getKey(@"copy_message_id"), @"Copy Message ID");

        /// <summary>
        /// "More"
        /// </summary>
        public static LocalisableString MoreActions => new TranslatableString(getKey(@"more_actions"), @"More");

        /// <summary>
        /// "Replying to {0}"
        /// </summary>
        public static LocalisableString ReplyingTo(string name) => new TranslatableString(getKey(@"replying_to"), @"Replying to {0}", name);

        /// <summary>
        /// "Editing message"
        /// </summary>
        public static LocalisableString EditingMessage => new TranslatableString(getKey(@"editing_message"), @"Editing message");

        /// <summary>
        /// "Upload a File"
        /// </summary>
        public static LocalisableString UploadFile => new TranslatableString(getKey(@"upload_file"), @"Upload a File");

        /// <summary>
        /// "Remove"
        /// </summary>
        public static LocalisableString RemoveAttachment => new TranslatableString(getKey(@"remove_attachment"), @"Remove");

        /// <summary>
        /// "That didn't work: {0}"
        /// </summary>
        public static LocalisableString ActionFailed(string error) => new TranslatableString(getKey(@"action_failed"), @"That didn't work: {0}", error);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
