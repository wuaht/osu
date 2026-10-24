// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using osu.Framework.Logging;

namespace osu.Game.Online.Discord.Full
{
    /// <summary>
    /// The QR code login of Discord ("remote auth"). The QR code is scanned with the Discord app on a phone, which then hands a user token to this client.
    /// Events are invoked on a background thread.
    /// </summary>
    public class DiscordRemoteAuth : IDisposable
    {
        private const string gateway_url = @"wss://remote-auth-gateway.discord.gg/?v=2";

        /// <summary>
        /// Invoked with the URL that is shown as a QR code.
        /// </summary>
        public event Action<string>? QrCodeReceived;

        /// <summary>
        /// Invoked with a preview of the user once the QR code was scanned.
        /// </summary>
        public event Action<DiscordUser>? UserScanned;

        /// <summary>
        /// Invoked with the user token once the login was confirmed on the phone.
        /// </summary>
        public event Action<string>? TokenReceived;

        /// <summary>
        /// Invoked when the login was cancelled on the phone or the QR code expired.
        /// </summary>
        public event Action? Expired;

        public event Action<string>? Failed;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly RSA rsa = RSA.Create(2048);
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

        private ClientWebSocket? socket;
        private bool finished;

        public void Start() => Task.Run(run);

        private async Task run()
        {
            var token = cancellation.Token;

            try
            {
                using var ws = socket = new ClientWebSocket();
                ws.Options.SetRequestHeader(@"Origin", @"https://discord.com");
                ws.Options.SetRequestHeader(@"User-Agent", DiscordFullClient.USER_AGENT);

                await ws.ConnectAsync(new Uri(gateway_url), token).ConfigureAwait(false);

                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    string? text = await DiscordFullClient.ReceiveText(ws, token).ConfigureAwait(false);

                    if (text == null)
                        break;

                    await handle(JObject.Parse(text), token).ConfigureAwait(false);

                    if (finished)
                        return;
                }

                if (!token.IsCancellationRequested && !finished)
                    Expired?.Invoke();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                if (token.IsCancellationRequested)
                    return;

                Logger.Log($@"Discord QR login failed: {e.Message}", LoggingTarget.Network);
                Failed?.Invoke(e.Message);
            }
        }

        private async Task handle(JObject message, CancellationToken token)
        {
            switch (message.Value<string>(@"op"))
            {
                case @"hello":
                    int interval = message.Value<int?>(@"heartbeat_interval") ?? 41250;
                    _ = heartbeat(interval, token);

                    await send(new JObject
                    {
                        [@"op"] = @"init",
                        [@"encoded_public_key"] = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()),
                    }, token).ConfigureAwait(false);
                    break;

                case @"nonce_proof":
                    byte[] nonce = decrypt(message.Value<string>(@"encrypted_nonce")!);

                    await send(new JObject
                    {
                        [@"op"] = @"nonce_proof",
                        [@"proof"] = toBase64Url(SHA256.HashData(nonce)),
                    }, token).ConfigureAwait(false);
                    break;

                case @"pending_remote_init":
                    QrCodeReceived?.Invoke($@"https://discord.com/ra/{message.Value<string>(@"fingerprint")}");
                    break;

                case @"pending_ticket":
                    string payload = Encoding.UTF8.GetString(decrypt(message.Value<string>(@"encrypted_user_payload")!));
                    string[] parts = payload.Split(':', 4);

                    if (parts.Length == 4)
                    {
                        UserScanned?.Invoke(new DiscordUser
                        {
                            Id = ulong.TryParse(parts[0], out ulong id) ? id : 0,
                            Avatar = parts[2] == @"0" ? null : parts[2],
                            Username = parts[3],
                        });
                    }

                    break;

                case @"pending_login":
                    string userToken = await exchangeTicket(message.Value<string>(@"ticket")!, token).ConfigureAwait(false);
                    finish(userToken);
                    break;

                // Older versions of the protocol send the token directly.
                case @"finish":
                    finish(Encoding.UTF8.GetString(decrypt(message.Value<string>(@"encrypted_token")!)));
                    break;

                case @"cancel":
                    finished = true;
                    Expired?.Invoke();
                    break;
            }
        }

        private void finish(string userToken)
        {
            finished = true;
            TokenReceived?.Invoke(userToken);
        }

        private async Task<string> exchangeTicket(string ticket, CancellationToken token)
        {
            var response = await DiscordFullClient.SendRequest(null, System.Net.Http.HttpMethod.Post, @"users/@me/remote-auth/login", new JObject { [@"ticket"] = ticket }, token)
                                                  .ConfigureAwait(false);

            string? encrypted = response.Value<string>(@"encrypted_token");

            if (encrypted == null)
                throw new DiscordRequestException(@"Discord did not return a token. You may have to log in through the Discord website once.");

            return Encoding.UTF8.GetString(decrypt(encrypted));
        }

        private async Task heartbeat(int interval, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && socket?.State == WebSocketState.Open)
                {
                    await Task.Delay(interval, token).ConfigureAwait(false);
                    await send(new JObject { [@"op"] = @"heartbeat" }, token).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The receive loop reports the failure.
            }
        }

        private async Task send(JObject message, CancellationToken token)
        {
            if (socket == null)
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None));

            await sendLock.WaitAsync(token).ConfigureAwait(false);

            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private byte[] decrypt(string base64) => rsa.Decrypt(Convert.FromBase64String(base64), RSAEncryptionPadding.OaepSHA256);

        private static string toBase64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public void Dispose()
        {
            cancellation.Cancel();
            rsa.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
