// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using static osu.Game.Online.Discord.Sdk.DiscordSdkNative;

namespace osu.Game.Online.Discord.Sdk
{
    /// <summary>
    /// A client using the official Discord Social SDK. It provides direct messages with friends of the user.
    /// </summary>
    public unsafe partial class DiscordSocialSdkClient : DiscordClient
    {
        /// <summary>
        /// The directory (inside the game data) in which the SDK library is looked up.
        /// </summary>
        public const string LIBRARY_DIRECTORY = @"discord";

        public override bool SupportsGuilds => false;

        public override bool SupportsHistory => false;

        private Bindable<string> applicationIdSetting = null!;
        private Bindable<string> refreshToken = null!;

        private nint* client;
        private ulong applicationId;
        private bool libraryLoaded;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Storage storage)
        {
            applicationIdSetting = config.GetBindable<string>(OsuSetting.SlopDiscordApplicationId);
            refreshToken = config.GetBindable<string>(OsuSetting.SlopDiscordRefreshToken);

            var libraryStorage = storage.GetStorageForDirectory(LIBRARY_DIRECTORY);
            string libraryDirectory = libraryStorage.GetFullPath(string.Empty);

            if (!TryLoad(new[] { libraryDirectory, AppContext.BaseDirectory }))
            {
                State.Value = DiscordConnectionState.NotConfigured;
                Error.Value = $@"The Discord Social SDK library ({LIBRARY_NAME}) was not found. Download the Social SDK from the Discord Developer Portal and copy the library into ""{libraryDirectory}"".";
                return;
            }

            libraryLoaded = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            applicationIdSetting.BindValueChanged(_ => recreateClient(), true);
        }

        private void recreateClient()
        {
            // Without the library, none of the native functions can be called (and the error about the missing library has to stay visible).
            if (!libraryLoaded)
                return;

            destroyClient();

            if (!ulong.TryParse(applicationIdSetting.Value.Trim(), out applicationId) || applicationId == 0)
            {
                State.Value = DiscordConnectionState.NotConfigured;
                Error.Value = @"Enter the application ID of your Discord application in the settings to log in.";
                return;
            }

            Error.Value = null;
            client = (nint*)NativeMemory.AllocZeroed((nuint)sizeof(nint));

            ClientInit(client);
            ClientSetApplicationId(client, applicationId);

            void* self = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(this));
            ClientSetStatusChangedCallback(client, (void*)(delegate* unmanaged[Cdecl]<int, int, int, void*, void>)&onStatusChanged, freeHandlePointer, self);

            self = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(this));
            ClientSetMessageCreatedCallback(client, (void*)(delegate* unmanaged[Cdecl]<ulong, void*, void>)&onMessageCreated, freeHandlePointer, self);

            self = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(this));
            ClientSetRelationshipCreatedCallback(client, (void*)(delegate* unmanaged[Cdecl]<ulong, byte, void*, void>)&onRelationshipChanged, freeHandlePointer, self);

            self = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(this));
            ClientSetRelationshipDeletedCallback(client, (void*)(delegate* unmanaged[Cdecl]<ulong, byte, void*, void>)&onRelationshipChanged, freeHandlePointer, self);

            if (!string.IsNullOrEmpty(refreshToken.Value))
            {
                State.Value = DiscordConnectionState.Connecting;
                using var token = new NativeString(refreshToken.Value);
                ClientRefreshToken(client, applicationId, token.Value, (void*)(delegate* unmanaged[Cdecl]<nint*, DiscordString, DiscordString, int, int, DiscordString, void*, void>)&onTokenExchanged,
                    freeHandlePointer, allocate(new TokenRequest(this, true)));
            }
            else
                State.Value = DiscordConnectionState.LoggedOut;
        }

        protected override void Update()
        {
            base.Update();

            if (client != null)
                RunCallbacks();
        }

        public override void Login()
        {
            if (client == null || State.Value != DiscordConnectionState.LoggedOut)
                return;

            Error.Value = null;
            State.Value = DiscordConnectionState.LoggingIn;

            nint verifier = 0, challenge = 0, args = 0;

            ClientCreateAuthorizationCodeVerifier(client, &verifier);
            VerifierChallenge(&verifier, &challenge);

            DiscordString verifierString;
            VerifierVerifier(&verifier, &verifierString);
            string codeVerifier = TakeString(verifierString);

            DiscordString scopesString;
            ClientGetDefaultCommunicationScopes(&scopesString);
            string scopes = TakeString(scopesString);

            AuthorizationArgsInit(&args);
            AuthorizationArgsSetClientId(&args, applicationId);

            using (var scopesValue = new NativeString(scopes))
                AuthorizationArgsSetScopes(&args, scopesValue.Value);

            AuthorizationArgsSetCodeChallenge(&args, &challenge);

            ClientAuthorize(client, &args, (void*)(delegate* unmanaged[Cdecl]<nint*, DiscordString, DiscordString, void*, void>)&onAuthorized,
                freeHandlePointer, allocate(new AuthorizeRequest(this, codeVerifier)));

            AuthorizationArgsDrop(&args);
            ChallengeDrop(&challenge);
            VerifierDrop(&verifier);
        }

        public override void Logout()
        {
            refreshToken.Value = string.Empty;
            recreateClient();
        }

        public override Task<IReadOnlyList<DiscordMessage>> GetMessages(DiscordChannel channel, ulong? before = null)
        {
            var source = new TaskCompletionSource<IReadOnlyList<DiscordMessage>>();

            if (before != null)
            {
                source.SetResult(Array.Empty<DiscordMessage>());
                return source.Task;
            }

            if (client == null)
            {
                source.SetException(new DiscordRequestException(@"Not connected."));
                return source.Task;
            }

            ClientGetUserMessagesWithLimit(client, channel.Id, 100, (void*)(delegate* unmanaged[Cdecl]<nint*, HandleSpan, void*, void>)&onMessagesReceived,
                freeHandlePointer, allocate(new MessagesRequest(this, source)));

            return source.Task;
        }

        public override Task<DiscordMessage> SendMessage(DiscordChannel channel, string content, string nonce)
        {
            var source = new TaskCompletionSource<DiscordMessage>();

            if (client == null)
            {
                source.SetException(new DiscordRequestException(@"Not connected."));
                return source.Task;
            }

            using var contentValue = new NativeString(content);
            ClientSendUserMessage(client, channel.Id, contentValue.Value, (void*)(delegate* unmanaged[Cdecl]<nint*, ulong, void*, void>)&onMessageSent,
                freeHandlePointer, allocate(new SendRequest(this, source, nonce)));

            return source.Task;
        }

        private void onReady()
        {
            nint user = 0;

            if (ClientGetCurrentUserV2(client, &user) != 0)
            {
                var localUser = TakeUser(&user);
                StoreUser(localUser);
                LocalUser.Value = localUser;
            }

            refreshFriends();
            State.Value = DiscordConnectionState.Ready;
        }

        private void refreshFriends()
        {
            HandleSpan span;
            ClientGetRelationships(client, &span);

            var friends = new List<DiscordUser>();

            for (int i = 0; i < (int)span.Size; i++)
            {
                nint* relationship = span.Ptr + i;

                try
                {
                    if (RelationshipDiscordRelationshipType(relationship) != RELATIONSHIP_FRIEND)
                        continue;

                    nint user = 0;

                    if (RelationshipUser(relationship, &user) == 0)
                        continue;

                    var friend = TakeUser(&user);
                    StoreUser(friend);
                    friends.Add(friend);
                }
                finally
                {
                    RelationshipDrop(relationship);
                }
            }

            if (span.Ptr != null)
                Free(span.Ptr);

            // Keep the channels that already exist so that the order by activity is kept.
            var channels = PrivateChannels.Where(c => friends.Any(f => f.Id == c.Id)).ToList();

            foreach (var friend in friends.OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase))
            {
                var existing = channels.FirstOrDefault(c => c.Id == friend.Id);

                if (existing != null)
                    existing.Recipients = new List<DiscordUser> { friend };
                else
                {
                    channels.Add(new DiscordChannel
                    {
                        Id = friend.Id,
                        Type = DiscordChannelType.DirectMessage,
                        Recipients = new List<DiscordUser> { friend },
                    });
                }
            }

            PrivateChannels.Clear();
            PrivateChannels.AddRange(channels);
        }

        private DiscordMessage? getMessage(ulong id)
        {
            nint message = 0;

            if (client == null || ClientGetMessageHandle(client, id, &message) == 0)
                return null;

            return takeMessage(&message);
        }

        private DiscordMessage takeMessage(nint* message)
        {
            try
            {
                ulong authorId = MessageAuthorId(message);
                ulong recipientId = MessageRecipientId(message);

                DiscordString content;
                MessageContent(message, &content);

                var author = GetUser(authorId);

                if (author == null)
                {
                    nint user = 0;

                    if (MessageAuthor(message, &user) != 0)
                    {
                        author = TakeUser(&user);
                        StoreUser(author);
                    }
                }

                ulong timestamp = MessageSentTimestamp(message);

                return new DiscordMessage
                {
                    Id = MessageId(message),
                    ChannelId = authorId == LocalUser.Value?.Id ? recipientId : authorId,
                    Author = author ?? new DiscordUser { Id = authorId, Username = authorId.ToString() },
                    Content = TakeString(content),
                    // The SDK may return either seconds or milliseconds depending on the version.
                    Timestamp = timestamp < 100_000_000_000
                        ? DateTimeOffset.FromUnixTimeSeconds((long)timestamp)
                        : DateTimeOffset.FromUnixTimeMilliseconds((long)timestamp),
                };
            }
            finally
            {
                MessageDrop(message);
            }
        }

        private void handleTokenExchanged(TokenRequest request, string? error, string accessToken, string newRefreshToken)
        {
            if (error != null)
            {
                Logger.Log($@"Discord token exchange failed: {error}", LoggingTarget.Network);

                refreshToken.Value = string.Empty;
                Error.Value = request.IsRefresh ? @"The Discord login expired. Please log in again." : $@"Discord login failed: {error}";
                State.Value = DiscordConnectionState.LoggedOut;
                return;
            }

            refreshToken.Value = newRefreshToken;

            using var token = new NativeString(accessToken);
            ClientUpdateToken(client, TOKEN_TYPE_BEARER, token.Value, (void*)(delegate* unmanaged[Cdecl]<nint*, void*, void>)&onTokenUpdated, freeHandlePointer, allocate(this));
        }

        private void destroyClient()
        {
            if (client == null)
                return;

            ClientDisconnect(client);
            ClientDrop(client);
            NativeMemory.Free(client);
            client = null;

            LocalUser.Value = null;
            PrivateChannels.Clear();
            Users.Clear();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            destroyClient();
        }

        #region Native callbacks

        private static void* freeHandlePointer => (void*)(delegate* unmanaged[Cdecl]<void*, void>)&freeHandle;

        private static void* allocate(object target) => (void*)GCHandle.ToIntPtr(GCHandle.Alloc(target));

        private static T? get<T>(void* userData) where T : class => GCHandle.FromIntPtr((nint)userData).Target as T;

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void freeHandle(void* userData)
        {
            if (userData != null)
                GCHandle.FromIntPtr((nint)userData).Free();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onStatusChanged(int status, int error, int errorDetail, void* userData)
        {
            var self = get<DiscordSocialSdkClient>(userData);

            if (self?.client == null)
                return;

            try
            {
                switch ((Status)status)
                {
                    case Status.Ready:
                        self.onReady();
                        break;

                    case Status.Disconnected:
                        if (error != 0)
                            self.Error.Value = $@"Disconnected from Discord (error {error}, {errorDetail}).";

                        if (self.State.Value != DiscordConnectionState.LoggedOut)
                            self.State.Value = string.IsNullOrEmpty(self.refreshToken.Value) ? DiscordConnectionState.LoggedOut : DiscordConnectionState.Connecting;
                        break;

                    default:
                        if (self.State.Value == DiscordConnectionState.Ready)
                            self.State.Value = DiscordConnectionState.Connecting;
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Discord status callback failed");
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onMessageCreated(ulong messageId, void* userData)
        {
            var self = get<DiscordSocialSdkClient>(userData);

            try
            {
                var message = self?.getMessage(messageId);

                if (self == null || message == null)
                    return;

                self.BumpPrivateChannel(message.ChannelId, message.Id);
                self.TriggerMessageCreated(message);
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Discord message callback failed");
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onRelationshipChanged(ulong userId, byte isDiscordRelationship, void* userData)
        {
            var self = get<DiscordSocialSdkClient>(userData);

            try
            {
                if (self?.client != null && self.State.Value == DiscordConnectionState.Ready)
                    self.refreshFriends();
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Discord relationship callback failed");
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onAuthorized(nint* result, DiscordString code, DiscordString redirectUri, void* userData)
        {
            string? error = TakeResultError(result);
            string codeValue = TakeString(code);
            string redirectValue = TakeString(redirectUri);

            var request = get<AuthorizeRequest>(userData);
            var self = request?.Client;

            if (self?.client == null)
                return;

            try
            {
                if (error != null)
                {
                    self.Error.Value = $@"Discord login failed: {error}";
                    self.State.Value = DiscordConnectionState.LoggedOut;
                    return;
                }

                self.State.Value = DiscordConnectionState.Connecting;

                using var codeString = new NativeString(codeValue);
                using var verifierString = new NativeString(request!.CodeVerifier);
                using var redirectString = new NativeString(redirectValue);

                ClientGetToken(self.client, self.applicationId, codeString.Value, verifierString.Value, redirectString.Value,
                    (void*)(delegate* unmanaged[Cdecl]<nint*, DiscordString, DiscordString, int, int, DiscordString, void*, void>)&onTokenExchanged,
                    freeHandlePointer, allocate(new TokenRequest(self, false)));
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Discord authorization callback failed");
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onTokenExchanged(nint* result, DiscordString accessToken, DiscordString refreshToken, int tokenType, int expiresIn, DiscordString scopes, void* userData)
        {
            string? error = TakeResultError(result);
            string access = TakeString(accessToken);
            string refresh = TakeString(refreshToken);
            TakeString(scopes);

            var request = get<TokenRequest>(userData);

            if (request?.Client.client == null)
                return;

            try
            {
                request.Client.handleTokenExchanged(request, error, access, refresh);
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Discord token callback failed");
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onTokenUpdated(nint* result, void* userData)
        {
            string? error = TakeResultError(result);
            var self = get<DiscordSocialSdkClient>(userData);

            if (self?.client == null)
                return;

            if (error != null)
            {
                self.Error.Value = $@"Discord login failed: {error}";
                self.State.Value = DiscordConnectionState.LoggedOut;
                return;
            }

            ClientConnect(self.client);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onMessagesReceived(nint* result, HandleSpan messages, void* userData)
        {
            string? error = TakeResultError(result);
            var request = get<MessagesRequest>(userData);

            var list = new List<DiscordMessage>();

            for (int i = 0; i < (int)messages.Size; i++)
            {
                try
                {
                    if (request != null && error == null)
                        list.Add(request.Client.takeMessage(messages.Ptr + i));
                    else
                        MessageDrop(messages.Ptr + i);
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"Reading a Discord message failed");
                }
            }

            if (messages.Ptr != null)
                Free(messages.Ptr);

            if (request == null)
                return;

            if (error != null)
                request.Source.TrySetException(new DiscordRequestException(error));
            else
                request.Source.TrySetResult(list.OrderBy(m => m.Timestamp).ThenBy(m => m.Id).ToList());
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onMessageSent(nint* result, ulong messageId, void* userData)
        {
            string? error = TakeResultError(result);
            var request = get<SendRequest>(userData);

            if (request == null)
                return;

            if (error != null)
            {
                request.Source.TrySetException(new DiscordRequestException(error));
                return;
            }

            try
            {
                var message = request.Client.getMessage(messageId);

                if (message == null)
                {
                    request.Source.TrySetException(new DiscordRequestException(@"The sent message could not be read."));
                    return;
                }

                message.Nonce = request.Nonce;
                request.Source.TrySetResult(message);
            }
            catch (Exception e)
            {
                request.Source.TrySetException(e);
            }
        }

        private record AuthorizeRequest(DiscordSocialSdkClient Client, string CodeVerifier);

        private record TokenRequest(DiscordSocialSdkClient Client, bool IsRefresh);

        private record MessagesRequest(DiscordSocialSdkClient Client, TaskCompletionSource<IReadOnlyList<DiscordMessage>> Source);

        private record SendRequest(DiscordSocialSdkClient Client, TaskCompletionSource<DiscordMessage> Source, string Nonce);

        #endregion
    }
}
