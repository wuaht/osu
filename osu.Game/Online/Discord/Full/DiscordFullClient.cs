// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Configuration;

namespace osu.Game.Online.Discord.Full
{
    /// <summary>
    /// A Discord client logged in with a user token, which provides guilds and the full message history.
    /// </summary>
    public partial class DiscordFullClient : DiscordClient
    {
        public const string USER_AGENT = @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        private const string api_url = @"https://discord.com/api/v9/";
        private const string gateway_url = @"wss://gateway.discord.gg/?v=9&encoding=json";

        private const ulong permission_administrator = 1 << 3;
        private const ulong permission_view_channel = 1 << 10;

        public override bool SupportsGuilds => true;

        public override bool SupportsHistory => true;

        /// <summary>
        /// The URL to show as a QR code during a login, or null while it is being requested.
        /// </summary>
        public readonly Bindable<string?> QrCodeUrl = new Bindable<string?>();

        /// <summary>
        /// The user who scanned the QR code and still has to confirm the login on their phone.
        /// </summary>
        public readonly Bindable<DiscordUser?> ScannedUser = new Bindable<DiscordUser?>();

        /// <summary>
        /// Whether the shown QR code expired and has to be refreshed with <see cref="DiscordClient.Login"/>.
        /// </summary>
        public readonly BindableBool QrCodeExpired = new BindableBool();

        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private static readonly JObject super_properties = createSuperProperties();
        private static readonly string encoded_super_properties = Convert.ToBase64String(Encoding.UTF8.GetBytes(super_properties.ToString(Formatting.None)));

        private Bindable<string> userToken = null!;

        private DiscordRemoteAuth? remoteAuth;
        private CancellationTokenSource? connectionCancellation;
        private ClientWebSocket? gatewaySocket;
        private readonly SemaphoreSlim gatewaySendLock = new SemaphoreSlim(1, 1);

        // Only accessed by the gateway tasks.
        private string? sessionId;
        private string? resumeGatewayUrl;
        private long sequence = -1;
        private volatile bool heartbeatAcknowledged;

        /// <summary>
        /// All channels of each guild, before filtering by permissions.
        /// </summary>
        private readonly Dictionary<ulong, List<DiscordChannel>> allGuildChannels = new Dictionary<ulong, List<DiscordChannel>>();

        private readonly HashSet<ulong> requestedGuildMembers = new HashSet<ulong>();
        private readonly HashSet<ulong> subscribedGuilds = new HashSet<ulong>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            userToken = config.GetBindable<string>(OsuSetting.SlopDiscordUserToken);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            userToken.BindValueChanged(_ => connect(), true);
        }

        public override void Login()
        {
            disposeRemoteAuth();

            Error.Value = null;
            QrCodeUrl.Value = null;
            ScannedUser.Value = null;
            QrCodeExpired.Value = false;
            State.Value = DiscordConnectionState.LoggingIn;

            var auth = remoteAuth = new DiscordRemoteAuth();

            auth.QrCodeReceived += url => Schedule(() =>
            {
                if (auth == remoteAuth)
                    QrCodeUrl.Value = url;
            });

            auth.UserScanned += user => Schedule(() =>
            {
                if (auth == remoteAuth)
                    ScannedUser.Value = user;
            });

            auth.TokenReceived += token => Schedule(() =>
            {
                if (auth != remoteAuth)
                    return;

                disposeRemoteAuth();
                userToken.Value = token;
            });

            auth.Expired += () => Schedule(() =>
            {
                if (auth != remoteAuth)
                    return;

                ScannedUser.Value = null;
                QrCodeExpired.Value = true;
            });

            auth.Failed += error => Schedule(() =>
            {
                if (auth != remoteAuth)
                    return;

                disposeRemoteAuth();
                Error.Value = error;
                State.Value = DiscordConnectionState.LoggedOut;
            });

            auth.Start();
        }

        public override void CancelLogin()
        {
            if (remoteAuth == null)
                return;

            disposeRemoteAuth();
            State.Value = string.IsNullOrEmpty(userToken.Value) ? DiscordConnectionState.LoggedOut : DiscordConnectionState.Connecting;
        }

        public override void Logout()
        {
            string token = userToken.Value;

            if (!string.IsNullOrEmpty(token))
            {
                // Invalidate the session on Discord's side as well, since the token is forgotten anyway.
                _ = SendRequest(token, HttpMethod.Post, @"auth/logout", new JObject { [@"provider"] = null, [@"voip_provider"] = null }, CancellationToken.None)
                    .ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
            }

            Error.Value = null;
            userToken.Value = string.Empty;
        }

        public override void SelectGuild(DiscordGuild guild)
        {
            subscribeToGuild(guild.Id);

            if (guild.LocalUserRoles != null || LocalUser.Value == null || !requestedGuildMembers.Add(guild.Id))
                return;

            string token = userToken.Value;

            Task.Run(async () =>
            {
                var member = await SendRequest(token, HttpMethod.Get, $@"users/@me/guilds/{guild.Id}/member", null, CancellationToken.None).ConfigureAwait(false);
                var roles = member[@"roles"]?.ToObject<List<ulong>>() ?? new List<ulong>();

                Schedule(() =>
                {
                    if (GetGuild(guild.Id) != guild)
                        return;

                    guild.LocalUserRoles = roles;
                    refreshGuildChannels(guild);
                });
            }).ContinueWith(t => Logger.Log($@"Discord guild member could not be fetched: {t.Exception?.InnerException?.Message}", LoggingTarget.Network),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        public override void SelectChannel(DiscordChannel channel)
        {
            if (channel.GuildId is ulong guildId)
                subscribeToGuild(guildId);
        }

        public override async Task<IReadOnlyList<DiscordMessage>> GetMessages(DiscordChannel channel, ulong? before = null)
        {
            string path = $@"channels/{channel.Id}/messages?limit=50";

            if (before != null)
                path += $@"&before={before}";

            var response = await request(HttpMethod.Get, path, null).ConfigureAwait(false);
            var messages = response.ToObject<List<DiscordMessage>>() ?? new List<DiscordMessage>();

            messages.Reverse();

            Schedule(() =>
            {
                foreach (var message in messages)
                    StoreUser(message.Author);
            });

            return messages;
        }

        public override async Task<DiscordMessage> SendMessage(DiscordChannel channel, string content, string nonce)
        {
            var response = await request(HttpMethod.Post, $@"channels/{channel.Id}/messages", new JObject
            {
                [@"content"] = content,
                [@"nonce"] = nonce,
                [@"tts"] = false,
                [@"flags"] = 0,
            }).ConfigureAwait(false);

            return response.ToObject<DiscordMessage>() ?? throw new DiscordRequestException(@"Discord returned an invalid message.");
        }

        private async Task<JToken> request(HttpMethod method, string path, JToken? body)
        {
            string token = userToken.Value;

            try
            {
                return await SendRequest(token, method, path, body, CancellationToken.None).ConfigureAwait(false);
            }
            catch (DiscordUnauthorizedException)
            {
                Schedule(() => onUnauthorized(token));
                throw;
            }
        }

        #region Connection

        private void connect()
        {
            disconnect();
            clearData();

            if (string.IsNullOrEmpty(userToken.Value))
            {
                if (remoteAuth == null)
                    State.Value = DiscordConnectionState.LoggedOut;

                return;
            }

            State.Value = DiscordConnectionState.Connecting;

            var cancellation = connectionCancellation = new CancellationTokenSource();
            string token = userToken.Value;

            Task.Run(() => runGateway(token, cancellation.Token));
        }

        private void disconnect()
        {
            connectionCancellation?.Cancel();
            connectionCancellation?.Dispose();
            connectionCancellation = null;

            gatewaySocket?.Abort();
            gatewaySocket = null;

            sessionId = null;
            resumeGatewayUrl = null;
            Interlocked.Exchange(ref sequence, -1);
        }

        private void clearData()
        {
            Guilds.Clear();
            PrivateChannels.Clear();
            LocalUser.Value = null;
            Users.Clear();
            allGuildChannels.Clear();
            requestedGuildMembers.Clear();
            subscribedGuilds.Clear();
        }

        private void onUnauthorized(string token)
        {
            if (userToken.Value != token)
                return;

            Error.Value = @"Your Discord login expired. Please log in again.";
            userToken.Value = string.Empty;
        }

        private async Task runGateway(string token, CancellationToken cancellation)
        {
            int failures = 0;

            while (!cancellation.IsCancellationRequested)
            {
                bool ready = false;

                try
                {
                    using var socket = new ClientWebSocket();
                    socket.Options.SetRequestHeader(@"User-Agent", USER_AGENT);
                    socket.Options.SetRequestHeader(@"Origin", @"https://discord.com");

                    string url = resumeGatewayUrl != null && sessionId != null ? $@"{resumeGatewayUrl}/?v=9&encoding=json" : gateway_url;

                    await socket.ConnectAsync(new Uri(url), cancellation).ConfigureAwait(false);
                    gatewaySocket = socket;

                    using var sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

                    try
                    {
                        var result = await receiveGateway(socket, token, () => ready = true, sessionCancellation.Token).ConfigureAwait(false);

                        if (result == GatewayResult.Fatal)
                            return;
                    }
                    finally
                    {
                        sessionCancellation.Cancel();
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    Logger.Log($@"Discord gateway connection failed: {e.Message}", LoggingTarget.Network);
                }

                if (cancellation.IsCancellationRequested)
                    return;

                failures = ready ? 0 : failures + 1;

                Schedule(() =>
                {
                    if (!cancellation.IsCancellationRequested)
                        State.Value = DiscordConnectionState.Connecting;
                });

                try
                {
                    int delay = failures == 0 ? 1000 : Math.Min(60000, 1000 * (1 << Math.Min(failures, 6)));
                    await Task.Delay(delay + Random.Shared.Next(1000), cancellation).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private enum GatewayResult
        {
            Reconnect,
            Fatal,
        }

        private async Task<GatewayResult> receiveGateway(ClientWebSocket socket, string token, Action onReady, CancellationToken cancellation)
        {
            while (socket.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
            {
                string? text = await ReceiveText(socket, cancellation).ConfigureAwait(false);

                if (text == null)
                    break;

                var payload = JObject.Parse(text);
                var data = payload[@"d"];

                if (payload[@"s"]?.Type == JTokenType.Integer)
                    Interlocked.Exchange(ref sequence, payload.Value<long>(@"s"));

                switch (payload.Value<int>(@"op"))
                {
                    // Dispatch
                    case 0:
                        string? type = payload.Value<string>(@"t");

                        if (type == @"READY" || type == @"RESUMED")
                            onReady();

                        handleDispatch(type, data);
                        break;

                    // Heartbeat request
                    case 1:
                        await sendHeartbeat(socket, cancellation).ConfigureAwait(false);
                        break;

                    // Reconnect
                    case 7:
                        return GatewayResult.Reconnect;

                    // Invalid session
                    case 9:
                        if (data?.Type != JTokenType.Boolean || !data.Value<bool>())
                        {
                            sessionId = null;
                            resumeGatewayUrl = null;
                        }

                        await Task.Delay(1000 + Random.Shared.Next(4000), cancellation).ConfigureAwait(false);
                        return GatewayResult.Reconnect;

                    // Hello
                    case 10:
                        int interval = data?.Value<int?>(@"heartbeat_interval") ?? 41250;
                        heartbeatAcknowledged = true;
                        _ = runHeartbeat(socket, interval, cancellation);

                        await sendGateway(socket, sessionId != null && Interlocked.Read(ref sequence) >= 0 ? createResume(token) : createIdentify(token), cancellation)
                            .ConfigureAwait(false);
                        break;

                    // Heartbeat acknowledgement
                    case 11:
                        heartbeatAcknowledged = true;
                        break;
                }
            }

            switch ((int?)socket.CloseStatus)
            {
                // Authentication failed
                case 4004:
                    Schedule(() => onUnauthorized(token));
                    return GatewayResult.Fatal;

                // Invalid sequence or session timed out
                case 4007:
                case 4009:
                    sessionId = null;
                    resumeGatewayUrl = null;
                    break;

                // Invalid configuration on our side, which won't be fixed by reconnecting.
                case >= 4010 and <= 4014:
                    Schedule(() => Error.Value = $@"Discord closed the connection ({socket.CloseStatusDescription}).");
                    return GatewayResult.Fatal;
            }

            return GatewayResult.Reconnect;
        }

        private async Task runHeartbeat(ClientWebSocket socket, int interval, CancellationToken cancellation)
        {
            try
            {
                await Task.Delay((int)(interval * Random.Shared.NextDouble()), cancellation).ConfigureAwait(false);

                while (socket.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
                {
                    if (!heartbeatAcknowledged)
                    {
                        // The connection is dead without being closed, so force a reconnect.
                        Logger.Log(@"Discord gateway did not acknowledge a heartbeat, reconnecting.", LoggingTarget.Network);
                        socket.Abort();
                        return;
                    }

                    heartbeatAcknowledged = false;
                    await sendHeartbeat(socket, cancellation).ConfigureAwait(false);
                    await Task.Delay(interval, cancellation).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The receive loop reconnects.
            }
        }

        private Task sendHeartbeat(ClientWebSocket socket, CancellationToken cancellation)
        {
            long seq = Interlocked.Read(ref sequence);
            return sendGateway(socket, new JObject { [@"op"] = 1, [@"d"] = seq < 0 ? null : seq }, cancellation);
        }

        private async Task sendGateway(ClientWebSocket socket, JObject payload, CancellationToken cancellation)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(payload.ToString(Formatting.None));

            await gatewaySendLock.WaitAsync(cancellation).ConfigureAwait(false);

            try
            {
                if (socket.State == WebSocketState.Open)
                    await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellation).ConfigureAwait(false);
            }
            finally
            {
                gatewaySendLock.Release();
            }
        }

        private static JObject createIdentify(string token) => new JObject
        {
            [@"op"] = 2,
            [@"d"] = new JObject
            {
                [@"token"] = token,
                [@"capabilities"] = 0,
                [@"properties"] = super_properties,
                [@"presence"] = new JObject
                {
                    [@"status"] = @"unknown",
                    [@"since"] = 0,
                    [@"activities"] = new JArray(),
                    [@"afk"] = false,
                },
                [@"compress"] = false,
                [@"client_state"] = new JObject { [@"guild_versions"] = new JObject() },
            },
        };

        private JObject createResume(string token) => new JObject
        {
            [@"op"] = 6,
            [@"d"] = new JObject
            {
                [@"token"] = token,
                [@"session_id"] = sessionId,
                [@"seq"] = Interlocked.Read(ref sequence),
            },
        };

        private void subscribeToGuild(ulong guildId)
        {
            var socket = gatewaySocket;

            if (socket == null || State.Value != DiscordConnectionState.Ready || !subscribedGuilds.Add(guildId))
                return;

            // User accounts don't receive the messages of large guilds until they subscribe to them.
            var payload = new JObject
            {
                [@"op"] = 37,
                [@"d"] = new JObject
                {
                    [@"subscriptions"] = new JObject
                    {
                        [guildId.ToString()] = new JObject
                        {
                            [@"typing"] = true,
                            [@"activities"] = false,
                            [@"threads"] = false,
                        },
                    },
                },
            };

            _ = sendGateway(socket, payload, connectionCancellation?.Token ?? CancellationToken.None)
                .ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);
        }

        #endregion

        #region Events

        /// <summary>
        /// Parses a gateway event on the gateway thread and applies it on the update thread.
        /// </summary>
        private void handleDispatch(string? type, JToken? data)
        {
            if (data is not JObject obj)
                return;

            switch (type)
            {
                case @"READY":
                    sessionId = obj.Value<string>(@"session_id");
                    resumeGatewayUrl = obj.Value<string>(@"resume_gateway_url")?.TrimEnd('/');

                    var ready = parseReady(obj);
                    Schedule(() => applyReady(ready));
                    break;

                case @"RESUMED":
                    Schedule(() => State.Value = DiscordConnectionState.Ready);
                    break;

                case @"GUILD_CREATE":
                    if (obj.Value<bool?>(@"unavailable") == true)
                        break;

                    var createdGuild = parseGuild(obj);
                    var members = obj[@"members"] as JArray;

                    Schedule(() =>
                    {
                        if (LocalUser.Value != null)
                            createdGuild.Guild.LocalUserRoles ??= findMemberRoles(members, LocalUser.Value.Id);

                        addGuild(createdGuild);
                    });
                    break;

                case @"GUILD_UPDATE":
                    ulong updatedGuildId = obj.Value<ulong>(@"id");
                    string? name = obj.Value<string>(@"name");
                    string? icon = obj.Value<string>(@"icon");
                    string? banner = obj.Value<string>(@"banner");

                    Schedule(() =>
                    {
                        var guild = GetGuild(updatedGuildId);

                        if (guild == null)
                            return;

                        // Replace the guild so that bound lists update.
                        int index = Guilds.IndexOf(guild);
                        guild.Name = name ?? guild.Name;
                        guild.Icon = icon;
                        guild.Banner = banner;
                        Guilds[index] = guild;
                    });
                    break;

                case @"GUILD_DELETE":
                    if (obj.Value<bool?>(@"unavailable") == true)
                        break;

                    ulong deletedGuildId = obj.Value<ulong>(@"id");

                    Schedule(() =>
                    {
                        var guild = GetGuild(deletedGuildId);

                        if (guild != null)
                            Guilds.Remove(guild);

                        allGuildChannels.Remove(deletedGuildId);
                    });
                    break;

                case @"GUILD_MEMBER_UPDATE":
                    ulong memberGuildId = obj.Value<ulong>(@"guild_id");
                    ulong memberId = obj[@"user"]?.Value<ulong>(@"id") ?? 0;
                    var memberRoles = obj[@"roles"]?.ToObject<List<ulong>>();

                    Schedule(() =>
                    {
                        var guild = GetGuild(memberGuildId);

                        if (guild == null || memberRoles == null || memberId != LocalUser.Value?.Id)
                            return;

                        guild.LocalUserRoles = memberRoles;
                        refreshGuildChannels(guild);
                    });
                    break;

                case @"GUILD_ROLE_CREATE":
                case @"GUILD_ROLE_UPDATE":
                    ulong roleGuildId = obj.Value<ulong>(@"guild_id");
                    var role = obj[@"role"]?.ToObject<DiscordRole>();

                    Schedule(() =>
                    {
                        var guild = GetGuild(roleGuildId);

                        if (guild == null || role == null)
                            return;

                        guild.Roles.RemoveAll(r => r.Id == role.Id);
                        guild.Roles.Add(role);
                        refreshGuildChannels(guild);
                    });
                    break;

                case @"GUILD_ROLE_DELETE":
                    ulong deletedRoleGuildId = obj.Value<ulong>(@"guild_id");
                    ulong deletedRoleId = obj.Value<ulong>(@"role_id");

                    Schedule(() =>
                    {
                        var guild = GetGuild(deletedRoleGuildId);

                        if (guild == null)
                            return;

                        guild.Roles.RemoveAll(r => r.Id == deletedRoleId);
                        refreshGuildChannels(guild);
                    });
                    break;

                case @"CHANNEL_CREATE":
                case @"CHANNEL_UPDATE":
                    var channel = obj.ToObject<DiscordChannel>();

                    if (channel != null)
                        Schedule(() => addOrUpdateChannel(channel));
                    break;

                case @"CHANNEL_DELETE":
                    ulong deletedChannelId = obj.Value<ulong>(@"id");
                    ulong? deletedChannelGuildId = obj.Value<ulong?>(@"guild_id");

                    Schedule(() => removeChannel(deletedChannelId, deletedChannelGuildId));
                    break;

                case @"MESSAGE_CREATE":
                    var message = obj.ToObject<DiscordMessage>();

                    if (message != null)
                        Schedule(() => onMessageCreated(message));
                    break;

                case @"MESSAGE_UPDATE":
                    var updated = obj.ToObject<DiscordMessage>();

                    if (updated == null)
                        break;

                    // Updates for embeds that finished loading only contain the changed fields.
                    updated.IsPartial = obj[@"author"] == null || obj[@"content"] == null;

                    Schedule(() =>
                    {
                        if (!updated.IsPartial)
                            StoreUser(updated.Author);

                        TriggerMessageUpdated(updated);
                    });
                    break;

                case @"MESSAGE_DELETE":
                    ulong messageChannelId = obj.Value<ulong>(@"channel_id");
                    ulong messageId = obj.Value<ulong>(@"id");

                    Schedule(() => TriggerMessageDeleted(messageChannelId, messageId));
                    break;

                case @"MESSAGE_DELETE_BULK":
                    ulong bulkChannelId = obj.Value<ulong>(@"channel_id");
                    var messageIds = obj[@"ids"]?.ToObject<List<ulong>>() ?? new List<ulong>();

                    Schedule(() =>
                    {
                        foreach (ulong id in messageIds)
                            TriggerMessageDeleted(bulkChannelId, id);
                    });
                    break;
            }
        }

        private record ParsedGuild(DiscordGuild Guild, List<DiscordChannel> Channels);

        private record ParsedReady(DiscordUser User, List<ParsedGuild> Guilds, List<DiscordChannel> PrivateChannels, List<DiscordUser> Users);

        private static ParsedReady parseReady(JObject data)
        {
            var user = data[@"user"]?.ToObject<DiscordUser>() ?? new DiscordUser();
            var users = data[@"users"]?.ToObject<List<DiscordUser>>() ?? new List<DiscordUser>();
            var usersById = users.ToDictionary(u => u.Id);

            var guildObjects = (data[@"guilds"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
            var mergedMembers = data[@"merged_members"] as JArray;

            var guilds = new List<ParsedGuild>();

            for (int i = 0; i < guildObjects.Count; i++)
            {
                var guildObject = guildObjects[i];

                if (guildObject.Value<bool?>(@"unavailable") == true)
                    continue;

                var parsed = parseGuild(guildObject);

                // The merged members are in the same order as the guilds and only contain the local user.
                if (mergedMembers != null && i < mergedMembers.Count && mergedMembers[i] is JArray merged)
                    parsed.Guild.LocalUserRoles = findMemberRoles(merged, user.Id);

                parsed.Guild.LocalUserRoles ??= findMemberRoles(guildObject[@"members"] as JArray, user.Id);

                guilds.Add(parsed);
            }

            sortGuilds(guilds, data[@"user_settings"] as JObject);

            var privateChannels = data[@"private_channels"]?.ToObject<List<DiscordChannel>>() ?? new List<DiscordChannel>();

            foreach (var channel in privateChannels)
            {
                if (channel.Recipients.Count == 0 && channel.RecipientIds != null)
                {
                    foreach (ulong id in channel.RecipientIds)
                    {
                        if (usersById.TryGetValue(id, out var recipient))
                            channel.Recipients.Add(recipient);
                    }
                }

                users.AddRange(channel.Recipients);
            }

            privateChannels.Sort((a, b) => (b.LastMessageId ?? b.Id).CompareTo(a.LastMessageId ?? a.Id));

            return new ParsedReady(user, guilds, privateChannels, users);
        }

        private static ParsedGuild parseGuild(JObject data)
        {
            // Newer payloads move most fields of the guild into "properties".
            if (data[@"properties"] is JObject properties)
            {
                data = (JObject)data.DeepClone();

                foreach (var property in properties.Properties())
                    data[property.Name] ??= property.Value;
            }

            var guild = data.ToObject<DiscordGuild>() ?? new DiscordGuild();
            var channels = guild.Channels;

            foreach (var channel in channels)
                channel.GuildId = guild.Id;

            guild.Channels = new List<DiscordChannel>();

            return new ParsedGuild(guild, channels);
        }

        private static List<ulong>? findMemberRoles(JArray? members, ulong userId)
        {
            if (members == null)
                return null;

            foreach (var member in members.OfType<JObject>())
            {
                ulong id = member.Value<ulong?>(@"user_id") ?? member[@"user"]?.Value<ulong?>(@"id") ?? 0;

                if (id == userId)
                    return member[@"roles"]?.ToObject<List<ulong>>() ?? new List<ulong>();
            }

            return null;
        }

        private static void sortGuilds(List<ParsedGuild> guilds, JObject? settings)
        {
            var order = new List<ulong>();

            if (settings?[@"guild_folders"] is JArray folders)
            {
                foreach (var folder in folders)
                    order.AddRange(folder[@"guild_ids"]?.ToObject<List<ulong>>() ?? new List<ulong>());
            }

            if (order.Count == 0)
                order = settings?[@"guild_positions"]?.ToObject<List<ulong>>() ?? new List<ulong>();

            guilds.Sort((a, b) =>
            {
                int indexA = order.IndexOf(a.Guild.Id);
                int indexB = order.IndexOf(b.Guild.Id);

                // Guilds missing from the settings are the most recently joined ones, which Discord shows at the top.
                if (indexA < 0 || indexB < 0)
                    return indexA < 0 && indexB < 0 ? b.Guild.Id.CompareTo(a.Guild.Id) : indexA < 0 ? -1 : 1;

                return indexA.CompareTo(indexB);
            });
        }

        private void applyReady(ParsedReady ready)
        {
            clearData();

            StoreUser(ready.User);
            foreach (var user in ready.Users)
                StoreUser(user);

            LocalUser.Value = ready.User;

            foreach (var guild in ready.Guilds)
            {
                allGuildChannels[guild.Guild.Id] = guild.Channels;
                guild.Guild.Channels = filterChannels(guild.Guild, guild.Channels);
            }

            Guilds.AddRange(ready.Guilds.Select(g => g.Guild));
            PrivateChannels.AddRange(ready.PrivateChannels);

            Error.Value = null;
            State.Value = DiscordConnectionState.Ready;
        }

        private void addGuild(ParsedGuild parsed)
        {
            var guild = parsed.Guild;

            allGuildChannels[guild.Id] = parsed.Channels;
            guild.Channels = filterChannels(guild, parsed.Channels);

            var existing = GetGuild(guild.Id);

            if (existing != null)
                Guilds[Guilds.IndexOf(existing)] = guild;
            else
                Guilds.Insert(0, guild);
        }

        private void addOrUpdateChannel(DiscordChannel channel)
        {
            if (channel.IsPrivate)
            {
                foreach (var recipient in channel.Recipients)
                    StoreUser(recipient);

                var existing = PrivateChannels.FirstOrDefault(c => c.Id == channel.Id);

                if (existing != null)
                    PrivateChannels[PrivateChannels.IndexOf(existing)] = channel;
                else
                    PrivateChannels.Insert(0, channel);

                return;
            }

            if (channel.GuildId is not ulong guildId || !allGuildChannels.TryGetValue(guildId, out var channels))
                return;

            channels.RemoveAll(c => c.Id == channel.Id);
            channels.Add(channel);

            var guild = GetGuild(guildId);

            if (guild != null)
                refreshGuildChannels(guild);
        }

        private void removeChannel(ulong channelId, ulong? guildId)
        {
            if (guildId == null)
            {
                var existing = PrivateChannels.FirstOrDefault(c => c.Id == channelId);

                if (existing != null)
                    PrivateChannels.Remove(existing);

                return;
            }

            if (!allGuildChannels.TryGetValue(guildId.Value, out var channels))
                return;

            channels.RemoveAll(c => c.Id == channelId);

            var guild = GetGuild(guildId.Value);

            if (guild != null)
                refreshGuildChannels(guild);
        }

        private void refreshGuildChannels(DiscordGuild guild)
        {
            if (!allGuildChannels.TryGetValue(guild.Id, out var channels))
                return;

            guild.Channels = filterChannels(guild, channels);
            TriggerGuildChannelsChanged(guild);
        }

        private void onMessageCreated(DiscordMessage message)
        {
            StoreUser(message.Author);

            var channel = GetChannel(message.ChannelId);

            if (channel == null && message.GuildId == null)
            {
                // A direct message from someone who wasn't messaged before. The channel is created by a separate event, which may arrive later.
                return;
            }

            if (channel?.IsPrivate == true)
                BumpPrivateChannel(channel.Id, message.Id);
            else if (channel != null)
                channel.LastMessageId = message.Id;

            TriggerMessageCreated(message);
        }

        #endregion

        #region Permissions

        private List<DiscordChannel> filterChannels(DiscordGuild guild, List<DiscordChannel> channels)
        {
            ulong userId = LocalUser.Value?.Id ?? 0;

            var visible = channels.Where(c => c.Type != DiscordChannelType.GuildCategory && c.IsTextBased && canView(guild, c, userId)).ToList();
            var categories = channels.Where(c => c.Type == DiscordChannelType.GuildCategory && visible.Any(v => v.ParentId == c.Id));

            return visible.Concat(categories).ToList();
        }

        private static bool canView(DiscordGuild guild, DiscordChannel channel, ulong userId)
        {
            // Without the roles of the user, the permissions are unknown. Show everything until they are fetched.
            if (guild.OwnerId == userId || guild.LocalUserRoles == null)
                return true;

            ulong permissions = parsePermissions(guild.Roles.FirstOrDefault(r => r.Id == guild.Id)?.Permissions);

            foreach (var role in guild.Roles)
            {
                if (guild.LocalUserRoles.Contains(role.Id))
                    permissions |= parsePermissions(role.Permissions);
            }

            if ((permissions & permission_administrator) != 0)
                return true;

            var everyone = channel.PermissionOverwrites.FirstOrDefault(o => o.Id == guild.Id);

            if (everyone != null)
                permissions = (permissions & ~parsePermissions(everyone.Deny)) | parsePermissions(everyone.Allow);

            ulong allow = 0;
            ulong deny = 0;

            foreach (var overwrite in channel.PermissionOverwrites)
            {
                if (overwrite.Type == 0 && overwrite.Id != guild.Id && guild.LocalUserRoles.Contains(overwrite.Id))
                {
                    allow |= parsePermissions(overwrite.Allow);
                    deny |= parsePermissions(overwrite.Deny);
                }
            }

            permissions = (permissions & ~deny) | allow;

            var member = channel.PermissionOverwrites.FirstOrDefault(o => o.Type == 1 && o.Id == userId);

            if (member != null)
                permissions = (permissions & ~parsePermissions(member.Deny)) | parsePermissions(member.Allow);

            return (permissions & permission_view_channel) != 0;
        }

        private static ulong parsePermissions(string? value) => ulong.TryParse(value, out ulong permissions) ? permissions : 0;

        #endregion

        #region Shared helpers

        /// <summary>
        /// Sends a request to the Discord API, retrying when rate limited.
        /// </summary>
        /// <param name="token">The user token, or null for unauthenticated requests.</param>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">The path relative to the API root.</param>
        /// <param name="body">The JSON body.</param>
        /// <param name="cancellation">A token to cancel the request.</param>
        public static async Task<JToken> SendRequest(string? token, HttpMethod method, string path, JToken? body, CancellationToken cancellation)
        {
            for (int attempt = 0;; attempt++)
            {
                using var request = new HttpRequestMessage(method, api_url + path);

                if (!string.IsNullOrEmpty(token))
                    request.Headers.TryAddWithoutValidation(@"Authorization", token);

                request.Headers.TryAddWithoutValidation(@"User-Agent", USER_AGENT);
                request.Headers.TryAddWithoutValidation(@"X-Super-Properties", encoded_super_properties);
                request.Headers.TryAddWithoutValidation(@"X-Discord-Locale", @"en-US");

                if (body != null)
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, @"application/json");

                using var response = await http.SendAsync(request, cancellation).ConfigureAwait(false);
                string text = await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 3)
                {
                    double retryAfter = tryParse(text)?.Value<double?>(@"retry_after") ?? 1;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(retryAfter, 0.1, 30)), cancellation).ConfigureAwait(false);
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new DiscordUnauthorizedException();

                if (!response.IsSuccessStatusCode)
                {
                    var error = tryParse(text);

                    if (error?[@"captcha_key"] != null)
                        throw new DiscordRequestException(@"Discord requires a captcha for this. Please do it once in the official Discord client.");

                    throw new DiscordRequestException(error?.Value<string>(@"message") ?? $@"{(int)response.StatusCode} {response.ReasonPhrase}");
                }

                return string.IsNullOrWhiteSpace(text) ? new JObject() : JToken.Parse(text);
            }
        }

        private static JObject? tryParse(string text)
        {
            try
            {
                return JObject.Parse(text);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Receives a whole text message from a web socket, or null if the socket was closed.
        /// </summary>
        public static async Task<string?> ReceiveText(ClientWebSocket socket, CancellationToken cancellation)
        {
            byte[] buffer = new byte[16384];
            using var stream = new MemoryStream();

            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, cancellation).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                    return null;

                stream.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                    return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }
        }

        private static JObject createSuperProperties() => new JObject
        {
            [@"os"] = @"Windows",
            [@"browser"] = @"Chrome",
            [@"device"] = string.Empty,
            [@"system_locale"] = @"en-US",
            [@"browser_user_agent"] = USER_AGENT,
            [@"browser_version"] = @"131.0.0.0",
            [@"os_version"] = @"10",
            [@"referrer"] = string.Empty,
            [@"referring_domain"] = string.Empty,
            [@"referrer_current"] = string.Empty,
            [@"referring_domain_current"] = string.Empty,
            [@"release_channel"] = @"stable",
            [@"client_build_number"] = 420000,
            [@"client_event_source"] = null,
        };

        #endregion

        private void disposeRemoteAuth()
        {
            remoteAuth?.Dispose();
            remoteAuth = null;
            QrCodeUrl.Value = null;
            ScannedUser.Value = null;
            QrCodeExpired.Value = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            remoteAuth?.Dispose();
            disconnect();
        }
    }

    public class DiscordUnauthorizedException : DiscordRequestException
    {
        public DiscordUnauthorizedException()
            : base(@"Your Discord login expired. Please log in again.")
        {
        }
    }
}
