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
        /// "The linked site will see your IP address"
        /// </summary>
        public static LocalisableString LinkPreviewHint => new TranslatableString(getKey(@"link_preview_hint"), @"The linked site will see your IP address");

        /// <summary>
        /// "Show or hide the servers"
        /// </summary>
        public static LocalisableString ToggleServers => new TranslatableString(getKey(@"toggle_servers"), @"Show or hide the servers");

        /// <summary>
        /// "Show or hide the channels"
        /// </summary>
        public static LocalisableString ToggleChannels => new TranslatableString(getKey(@"toggle_channels"), @"Show or hide the channels");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
