// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using osu.Framework;

namespace osu.Game.Online.Discord.Sdk
{
    /// <summary>
    /// A minimal binding to the C ABI of the Discord Social SDK (discord_partner_sdk).
    /// The library is loaded at runtime so that the game works without it.
    /// </summary>
    internal static unsafe class DiscordSdkNative
    {
        public const string LIBRARY_NAME = @"discord_partner_sdk";

        [StructLayout(LayoutKind.Sequential)]
        public struct DiscordString
        {
            public byte* Ptr;
            public nuint Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HandleSpan
        {
            public nint* Ptr;
            public nuint Size;
        }

        public enum Status
        {
            Disconnected = 0,
            Connecting = 1,
            Connected = 2,
            Ready = 3,
            Reconnecting = 4,
            Disconnecting = 5,
            HttpWait = 6,
        }

        public enum AvatarType
        {
            Gif = 0,
            Webp = 1,
            Png = 2,
            Jpeg = 3,
        }

        public const int TOKEN_TYPE_BEARER = 1;
        public const int RELATIONSHIP_FRIEND = 1;

        private static nint library;

        public static bool IsLoaded => library != 0;

        // Client
        public static delegate* unmanaged[Cdecl]<nint*, void> ClientInit;
        public static delegate* unmanaged[Cdecl]<nint*, void> ClientDrop;
        public static delegate* unmanaged[Cdecl]<nint*, ulong, void> ClientSetApplicationId;
        public static delegate* unmanaged[Cdecl]<nint*, nint*, void> ClientCreateAuthorizationCodeVerifier;
        public static delegate* unmanaged[Cdecl]<nint*, nint*, void*, void*, void*, void> ClientAuthorize;
        public static delegate* unmanaged[Cdecl]<nint*, ulong, DiscordString, DiscordString, DiscordString, void*, void*, void*, void> ClientGetToken;
        public static delegate* unmanaged[Cdecl]<nint*, ulong, DiscordString, void*, void*, void*, void> ClientRefreshToken;
        public static delegate* unmanaged[Cdecl]<nint*, int, DiscordString, void*, void*, void*, void> ClientUpdateToken;
        public static delegate* unmanaged[Cdecl]<nint*, void> ClientConnect;
        public static delegate* unmanaged[Cdecl]<nint*, void> ClientDisconnect;
        public static delegate* unmanaged[Cdecl]<nint*, void*, void*, void*, void> ClientSetStatusChangedCallback;
        public static delegate* unmanaged[Cdecl]<nint*, HandleSpan*, void> ClientGetRelationships;
        public static delegate* unmanaged[Cdecl]<nint*, nint*, byte> ClientGetCurrentUserV2;
        public static delegate* unmanaged[Cdecl]<nint*, ulong, nint*, byte> ClientGetUser;
        public static delegate* unmanaged[Cdecl]<nint*, ulong, int, void*, void*, void*, void> ClientGetUserMessagesWithLimit;
        public static delegate* unmanaged[Cdecl]<nint*, ulong, DiscordString, void*, void*, void*, void> ClientSendUserMessage;
        public static delegate* unmanaged[Cdecl]<nint*, void*, void*, void*, void> ClientSetMessageCreatedCallback;
        public static delegate* unmanaged[Cdecl]<nint*, void*, void*, void*, void> ClientSetRelationshipCreatedCallback;
        public static delegate* unmanaged[Cdecl]<nint*, void*, void*, void*, void> ClientSetRelationshipDeletedCallback;
        public static delegate* unmanaged[Cdecl]<nint*, ulong, nint*, byte> ClientGetMessageHandle;
        public static delegate* unmanaged[Cdecl]<DiscordString*, void> ClientGetDefaultCommunicationScopes;

        // Authorization
        public static delegate* unmanaged[Cdecl]<nint*, void> AuthorizationArgsInit;
        public static delegate* unmanaged[Cdecl]<nint*, void> AuthorizationArgsDrop;
        public static delegate* unmanaged[Cdecl]<nint*, ulong, void> AuthorizationArgsSetClientId;
        public static delegate* unmanaged[Cdecl]<nint*, DiscordString, void> AuthorizationArgsSetScopes;
        public static delegate* unmanaged[Cdecl]<nint*, nint*, void> AuthorizationArgsSetCodeChallenge;
        public static delegate* unmanaged[Cdecl]<nint*, void> VerifierDrop;
        public static delegate* unmanaged[Cdecl]<nint*, nint*, void> VerifierChallenge;
        public static delegate* unmanaged[Cdecl]<nint*, DiscordString*, void> VerifierVerifier;
        public static delegate* unmanaged[Cdecl]<nint*, void> ChallengeDrop;

        // Results
        public static delegate* unmanaged[Cdecl]<nint*, void> ClientResultDrop;
        public static delegate* unmanaged[Cdecl]<nint*, byte> ClientResultSuccessful;
        public static delegate* unmanaged[Cdecl]<nint*, DiscordString*, void> ClientResultError;

        // Relationships and users
        public static delegate* unmanaged[Cdecl]<nint*, void> RelationshipDrop;
        public static delegate* unmanaged[Cdecl]<nint*, int> RelationshipDiscordRelationshipType;
        public static delegate* unmanaged[Cdecl]<nint*, nint*, byte> RelationshipUser;
        public static delegate* unmanaged[Cdecl]<nint*, void> UserDrop;
        public static delegate* unmanaged[Cdecl]<nint*, ulong> UserId;
        public static delegate* unmanaged[Cdecl]<nint*, DiscordString*, void> UserUsername;
        public static delegate* unmanaged[Cdecl]<nint*, DiscordString*, void> UserDisplayName;
        public static delegate* unmanaged[Cdecl]<nint*, AvatarType, AvatarType, DiscordString*, void> UserAvatarUrl;

        // Messages
        public static delegate* unmanaged[Cdecl]<nint*, void> MessageDrop;
        public static delegate* unmanaged[Cdecl]<nint*, ulong> MessageId;
        public static delegate* unmanaged[Cdecl]<nint*, ulong> MessageAuthorId;
        public static delegate* unmanaged[Cdecl]<nint*, ulong> MessageRecipientId;
        public static delegate* unmanaged[Cdecl]<nint*, ulong> MessageSentTimestamp;
        public static delegate* unmanaged[Cdecl]<nint*, DiscordString*, void> MessageContent;
        public static delegate* unmanaged[Cdecl]<nint*, nint*, byte> MessageAuthor;

        // Global
        public static delegate* unmanaged[Cdecl]<void*, void> Free;
        public static delegate* unmanaged[Cdecl]<void> RunCallbacks;

        /// <summary>
        /// Attempts to load the library from the given directories.
        /// </summary>
        /// <returns>Whether the library is available.</returns>
        public static bool TryLoad(IEnumerable<string> directories)
        {
            if (library != 0)
                return true;

            string fileName = RuntimeInfo.OS switch
            {
                RuntimeInfo.Platform.Windows => $@"{LIBRARY_NAME}.dll",
                RuntimeInfo.Platform.macOS => $@"lib{LIBRARY_NAME}.dylib",
                _ => $@"lib{LIBRARY_NAME}.so",
            };

            nint handle = 0;

            foreach (string directory in directories)
            {
                string path = Path.Combine(directory, fileName);

                if (File.Exists(path) && NativeLibrary.TryLoad(path, out handle))
                    break;
            }

            if (handle == 0)
                return false;

            try
            {
                bind(handle);
            }
            catch (EntryPointNotFoundException)
            {
                NativeLibrary.Free(handle);
                return false;
            }

            library = handle;
            return true;
        }

        private static void bind(nint handle)
        {
            nint get(string name) => NativeLibrary.GetExport(handle, name);

            ClientInit = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_Client_Init");
            ClientDrop = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_Client_Drop");
            ClientSetApplicationId = (delegate* unmanaged[Cdecl]<nint*, ulong, void>)get(@"Discord_Client_SetApplicationId");
            ClientCreateAuthorizationCodeVerifier = (delegate* unmanaged[Cdecl]<nint*, nint*, void>)get(@"Discord_Client_CreateAuthorizationCodeVerifier");
            ClientAuthorize = (delegate* unmanaged[Cdecl]<nint*, nint*, void*, void*, void*, void>)get(@"Discord_Client_Authorize");
            ClientGetToken = (delegate* unmanaged[Cdecl]<nint*, ulong, DiscordString, DiscordString, DiscordString, void*, void*, void*, void>)get(@"Discord_Client_GetToken");
            ClientRefreshToken = (delegate* unmanaged[Cdecl]<nint*, ulong, DiscordString, void*, void*, void*, void>)get(@"Discord_Client_RefreshToken");
            ClientUpdateToken = (delegate* unmanaged[Cdecl]<nint*, int, DiscordString, void*, void*, void*, void>)get(@"Discord_Client_UpdateToken");
            ClientConnect = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_Client_Connect");
            ClientDisconnect = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_Client_Disconnect");
            ClientSetStatusChangedCallback = (delegate* unmanaged[Cdecl]<nint*, void*, void*, void*, void>)get(@"Discord_Client_SetStatusChangedCallback");
            ClientGetRelationships = (delegate* unmanaged[Cdecl]<nint*, HandleSpan*, void>)get(@"Discord_Client_GetRelationships");
            ClientGetCurrentUserV2 = (delegate* unmanaged[Cdecl]<nint*, nint*, byte>)get(@"Discord_Client_GetCurrentUserV2");
            ClientGetUser = (delegate* unmanaged[Cdecl]<nint*, ulong, nint*, byte>)get(@"Discord_Client_GetUser");
            ClientGetUserMessagesWithLimit = (delegate* unmanaged[Cdecl]<nint*, ulong, int, void*, void*, void*, void>)get(@"Discord_Client_GetUserMessagesWithLimit");
            ClientSendUserMessage = (delegate* unmanaged[Cdecl]<nint*, ulong, DiscordString, void*, void*, void*, void>)get(@"Discord_Client_SendUserMessage");
            ClientSetMessageCreatedCallback = (delegate* unmanaged[Cdecl]<nint*, void*, void*, void*, void>)get(@"Discord_Client_SetMessageCreatedCallback");
            ClientSetRelationshipCreatedCallback = (delegate* unmanaged[Cdecl]<nint*, void*, void*, void*, void>)get(@"Discord_Client_SetRelationshipCreatedCallback");
            ClientSetRelationshipDeletedCallback = (delegate* unmanaged[Cdecl]<nint*, void*, void*, void*, void>)get(@"Discord_Client_SetRelationshipDeletedCallback");
            ClientGetMessageHandle = (delegate* unmanaged[Cdecl]<nint*, ulong, nint*, byte>)get(@"Discord_Client_GetMessageHandle");
            ClientGetDefaultCommunicationScopes = (delegate* unmanaged[Cdecl]<DiscordString*, void>)get(@"Discord_Client_GetDefaultCommunicationScopes");

            AuthorizationArgsInit = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_AuthorizationArgs_Init");
            AuthorizationArgsDrop = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_AuthorizationArgs_Drop");
            AuthorizationArgsSetClientId = (delegate* unmanaged[Cdecl]<nint*, ulong, void>)get(@"Discord_AuthorizationArgs_SetClientId");
            AuthorizationArgsSetScopes = (delegate* unmanaged[Cdecl]<nint*, DiscordString, void>)get(@"Discord_AuthorizationArgs_SetScopes");
            AuthorizationArgsSetCodeChallenge = (delegate* unmanaged[Cdecl]<nint*, nint*, void>)get(@"Discord_AuthorizationArgs_SetCodeChallenge");
            VerifierDrop = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_AuthorizationCodeVerifier_Drop");
            VerifierChallenge = (delegate* unmanaged[Cdecl]<nint*, nint*, void>)get(@"Discord_AuthorizationCodeVerifier_Challenge");
            VerifierVerifier = (delegate* unmanaged[Cdecl]<nint*, DiscordString*, void>)get(@"Discord_AuthorizationCodeVerifier_Verifier");
            ChallengeDrop = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_AuthorizationCodeChallenge_Drop");

            ClientResultDrop = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_ClientResult_Drop");
            ClientResultSuccessful = (delegate* unmanaged[Cdecl]<nint*, byte>)get(@"Discord_ClientResult_Successful");
            ClientResultError = (delegate* unmanaged[Cdecl]<nint*, DiscordString*, void>)get(@"Discord_ClientResult_Error");

            RelationshipDrop = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_RelationshipHandle_Drop");
            RelationshipDiscordRelationshipType = (delegate* unmanaged[Cdecl]<nint*, int>)get(@"Discord_RelationshipHandle_DiscordRelationshipType");
            RelationshipUser = (delegate* unmanaged[Cdecl]<nint*, nint*, byte>)get(@"Discord_RelationshipHandle_User");
            UserDrop = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_UserHandle_Drop");
            UserId = (delegate* unmanaged[Cdecl]<nint*, ulong>)get(@"Discord_UserHandle_Id");
            UserUsername = (delegate* unmanaged[Cdecl]<nint*, DiscordString*, void>)get(@"Discord_UserHandle_Username");
            UserDisplayName = (delegate* unmanaged[Cdecl]<nint*, DiscordString*, void>)get(@"Discord_UserHandle_DisplayName");
            UserAvatarUrl = (delegate* unmanaged[Cdecl]<nint*, AvatarType, AvatarType, DiscordString*, void>)get(@"Discord_UserHandle_AvatarUrl");

            MessageDrop = (delegate* unmanaged[Cdecl]<nint*, void>)get(@"Discord_MessageHandle_Drop");
            MessageId = (delegate* unmanaged[Cdecl]<nint*, ulong>)get(@"Discord_MessageHandle_Id");
            MessageAuthorId = (delegate* unmanaged[Cdecl]<nint*, ulong>)get(@"Discord_MessageHandle_AuthorId");
            MessageRecipientId = (delegate* unmanaged[Cdecl]<nint*, ulong>)get(@"Discord_MessageHandle_RecipientId");
            MessageSentTimestamp = (delegate* unmanaged[Cdecl]<nint*, ulong>)get(@"Discord_MessageHandle_SentTimestamp");
            MessageContent = (delegate* unmanaged[Cdecl]<nint*, DiscordString*, void>)get(@"Discord_MessageHandle_Content");
            MessageAuthor = (delegate* unmanaged[Cdecl]<nint*, nint*, byte>)get(@"Discord_MessageHandle_Author");

            Free = (delegate* unmanaged[Cdecl]<void*, void>)get(@"Discord_Free");
            RunCallbacks = (delegate* unmanaged[Cdecl]<void>)get(@"Discord_RunCallbacks");
        }

        /// <summary>
        /// Reads a string returned by the SDK and frees its memory.
        /// </summary>
        public static string TakeString(DiscordString value)
        {
            if (value.Ptr == null)
                return string.Empty;

            string result = Encoding.UTF8.GetString(value.Ptr, (int)value.Size);
            Free(value.Ptr);
            return result;
        }

        /// <summary>
        /// A temporary UTF-8 copy of a string that can be passed to the SDK.
        /// </summary>
        public readonly struct NativeString : IDisposable
        {
            public readonly DiscordString Value;

            public NativeString(string value)
            {
                Value = new DiscordString
                {
                    Ptr = (byte*)Marshal.StringToCoTaskMemUTF8(value),
                    Size = (nuint)Encoding.UTF8.GetByteCount(value),
                };
            }

            public void Dispose() => Marshal.FreeCoTaskMem((nint)Value.Ptr);
        }

        /// <summary>
        /// Reads the error of an SDK result, or null if it succeeded. Drops the result.
        /// </summary>
        public static string? TakeResultError(nint* result)
        {
            try
            {
                if (ClientResultSuccessful(result) != 0)
                    return null;

                DiscordString error;
                ClientResultError(result, &error);
                string message = TakeString(error);
                return string.IsNullOrEmpty(message) ? @"Unknown error" : message;
            }
            finally
            {
                ClientResultDrop(result);
            }
        }

        /// <summary>
        /// Converts a user handle to a model. Drops the handle.
        /// </summary>
        public static DiscordUser TakeUser(nint* user)
        {
            try
            {
                DiscordString username, displayName, avatar;
                UserUsername(user, &username);
                UserDisplayName(user, &displayName);
                UserAvatarUrl(user, AvatarType.Png, AvatarType.Png, &avatar);

                return new DiscordUser
                {
                    Id = UserId(user),
                    Username = TakeString(username),
                    GlobalName = TakeString(displayName),
                    AvatarUrlOverride = TakeString(avatar),
                };
            }
            finally
            {
                UserDrop(user);
            }
        }
    }
}
