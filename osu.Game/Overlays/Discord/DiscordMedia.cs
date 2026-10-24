// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Discord.Full;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Helpers for loading Discord media.
    /// </summary>
    public static class DiscordMedia
    {
        public const float MAX_WIDTH = 400;
        public const float MAX_HEIGHT = 300;

        /// <summary>
        /// The largest dimension animated images are decoded at, to keep their memory usage reasonable.
        /// </summary>
        private const int max_animation_dimension = 400;

        private const int max_animation_frames = 300;

        private static readonly HttpClient http_client = createHttpClient();

        private static HttpClient createHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(DiscordFullClient.USER_AGENT);
            return client;
        }

        public static Task<byte[]> Download(string url, CancellationToken cancellation) => http_client.GetByteArrayAsync(url, cancellation);

        /// <summary>
        /// Fits media of a known size into the given bounds, keeping its aspect ratio and never scaling it up.
        /// </summary>
        public static Vector2 FitSize(int? width, int? height, float maxWidth = MAX_WIDTH, float maxHeight = MAX_HEIGHT)
        {
            if (width == null || height == null || width <= 0 || height <= 0)
                return new Vector2(maxWidth, maxHeight * 0.75f);

            float scale = Math.Min(1, Math.Min(maxWidth / width.Value, maxHeight / height.Value));
            return new Vector2(Math.Max(1, width.Value * scale), Math.Max(1, height.Value * scale));
        }

        /// <summary>
        /// Requests a downscaled version of an image from the Discord media proxy, which saves bandwidth and memory.
        /// </summary>
        public static string Downscale(string url, Vector2 size, string? format = null)
        {
            if (!url.Contains(@"discordapp.net", StringComparison.Ordinal) && !url.Contains(@"discordapp.com/attachments", StringComparison.Ordinal))
                return url;

            // Request twice the displayed size for high DPI screens.
            int width = (int)Math.Ceiling(size.X * 2);
            int height = (int)Math.Ceiling(size.Y * 2);

            string query = $@"width={width}&height={height}";

            if (format != null)
                query += $@"&format={format}";

            return url + (url.Contains('?') ? '&' : '?') + query;
        }

        /// <summary>
        /// Decodes an animated image into frames. Returns null if the image could not be decoded.
        /// </summary>
        public static (Texture texture, double duration)[]? DecodeAnimation(IRenderer renderer, byte[] data)
        {
            try
            {
                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(data);

                int largest = Math.Max(image.Width, image.Height);

                if (largest > max_animation_dimension)
                {
                    float scale = max_animation_dimension / (float)largest;
                    image.Mutate(i => i.Resize(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale))));
                }

                int frameCount = Math.Min(image.Frames.Count, max_animation_frames);
                var frames = new (Texture, double)[frameCount];

                for (int i = 0; i < frameCount; i++)
                {
                    // The delay is in hundredths of a second. Browsers treat very short delays as 100ms, which GIFs rely on.
                    int delay = image.Frames[i].Metadata.GetGifMetadata().FrameDelay;
                    double duration = delay <= 1 ? 100 : delay * 10;

                    var frame = image.Frames.CloneFrame(i);
                    var texture = renderer.CreateTexture(frame.Width, frame.Height);
                    texture.SetData(new TextureUpload(frame));

                    frames[i] = (texture, duration);
                }

                return frames;
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to decode a Discord animation: {e.Message}", LoggingTarget.Network);
                return null;
            }
        }
    }

    /// <summary>
    /// A texture store for Discord media, which only allows lookups from Discord's CDN and the emoji CDN.
    /// The game's own texture store only allows lookups from osu! domains.
    /// </summary>
    public class DiscordTextureStore : LargeTextureStore
    {
        private readonly DiscordOnlineStore onlineStore;

        public DiscordTextureStore(GameHost host)
            : this(host, new DiscordOnlineStore())
        {
        }

        private DiscordTextureStore(GameHost host, DiscordOnlineStore onlineStore)
            : base(host.Renderer, host.CreateTextureLoaderStore(onlineStore))
        {
            this.onlineStore = onlineStore;
        }

        private readonly Dictionary<string, Task<Texture?>> pendingLookups = new Dictionary<string, Task<Texture?>>();

        private readonly Dictionary<string, Task<Color4?>> dominantColours = new Dictionary<string, Task<Color4?>>();

        /// <summary>
        /// Retrieves the dominant colour of an image in the background, which Discord uses as the profile colour of users without one.
        /// </summary>
        public Task<Color4?> GetDominantColourAsync(string url)
        {
            lock (dominantColours)
            {
                if (!dominantColours.TryGetValue(url, out var task))
                    dominantColours[url] = task = Task.Run(() => computeDominantColour(url));

                return task;
            }
        }

        private Color4? computeDominantColour(string url)
        {
            try
            {
                byte[]? data = onlineStore.Get(url);

                if (data == null)
                    return null;

                using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(data);
                image.Mutate(i => i.Resize(32, 32));

                // Group similar colours and use the average of the largest group.
                var groups = new Dictionary<int, (int count, Vector3 sum)>();

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        var pixel = image[x, y];

                        if (pixel.A < 128)
                            continue;

                        int key = (pixel.R >> 5) << 6 | (pixel.G >> 5) << 3 | pixel.B >> 5;
                        groups.TryGetValue(key, out var group);
                        groups[key] = (group.count + 1, group.sum + new Vector3(pixel.R, pixel.G, pixel.B));
                    }
                }

                if (groups.Count == 0)
                    return null;

                var largest = groups.Values.MaxBy(g => g.count);
                var average = largest.sum / largest.count / 255;

                return new Color4(average.X, average.Y, average.Z, 1);
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to compute the colour of {url}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Retrieves a texture in the background. Unlike the base store's GetAsync, concurrent lookups of the same URL
        /// share one retrieval, as the base store fails when a thread pool thread has to wait for a retrieval which is already running.
        /// </summary>
        public Task<Texture?> GetTextureAsync(string url)
        {
            lock (pendingLookups)
            {
                if (pendingLookups.TryGetValue(url, out var pending))
                    return pending;

                var task = Task.Run(() => (Texture?)Get(url));
                pendingLookups[url] = task;

                task.ContinueWith(_ =>
                {
                    lock (pendingLookups)
                        pendingLookups.Remove(url);
                }, TaskContinuationOptions.ExecuteSynchronously);

                return task;
            }
        }

        private class DiscordOnlineStore : OnlineStore
        {
            private static readonly string[] trusted_hosts =
            {
                @"discordapp.com",
                @"discordapp.net",
                @"discord.com",
                @"cdn.jsdelivr.net",
            };

            protected override string GetLookupUrl(string url)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps
                                                                       || !trusted_hosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase)
                                                                                                 || uri.Host.EndsWith('.' + h, StringComparison.OrdinalIgnoreCase)))
                    return string.Empty;

                return url;
            }
        }
    }

    /// <summary>
    /// An image which is loaded from a URL and faded in once loaded.
    /// </summary>
    public partial class DiscordRemoteImage : CompositeDrawable
    {
        private readonly string? url;
        private readonly bool animated;

        public FillMode ImageFillMode { get; init; } = FillMode.Fill;

        /// <summary>
        /// Whether to show a placeholder until the image is loaded.
        /// </summary>
        public bool ShowPlaceholder { get; init; } = true;

        [Resolved]
        private DiscordTextureStore textures { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private Box placeholder = null!;

        /// <param name="url">The URL of the image.</param>
        /// <param name="animated">Whether the image is an animated GIF, which is decoded with all of its frames.</param>
        public DiscordRemoteImage(string? url, bool animated = false)
        {
            this.url = url;
            this.animated = animated;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider? colourProvider)
        {
            AddInternal(placeholder = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colourProvider?.Background4 ?? Color4.Gray,
                Alpha = ShowPlaceholder ? 0.5f : 0,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (string.IsNullOrEmpty(url))
                return;

            if (animated)
                Task.Run(loadAnimation);
            else
                textures.GetTextureAsync(url).ContinueWith(t => Schedule(() => onTextureLoaded(t)), TaskContinuationOptions.ExecuteSynchronously);
        }

        private void onTextureLoaded(Task<Texture?> task)
        {
            if (!task.IsCompletedSuccessfully || task.GetResultSafely() == null)
                return;

            addContent(new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = ImageFillMode,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Texture = task.GetResultSafely(),
            });
        }

        private async Task loadAnimation()
        {
            try
            {
                byte[] data = await DiscordMedia.Download(url!, cancellation.Token).ConfigureAwait(false);

                if (cancellation.IsCancellationRequested)
                    return;

                var frames = DiscordMedia.DecodeAnimation(renderer, data);

                if (frames == null || frames.Length == 0)
                    return;

                Schedule(() =>
                {
                    var animation = new TextureAnimation
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = ImageFillMode,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Loop = true,
                    };

                    foreach (var (texture, duration) in frames)
                        animation.AddFrame(texture, duration);

                    addContent(animation);
                });
            }
            catch (Exception e)
            {
                if (!cancellation.IsCancellationRequested)
                    Logger.Log($@"Failed to load a Discord animation: {e.Message}", LoggingTarget.Network);
            }
        }

        private void addContent(Drawable content)
        {
            content.Alpha = 0;
            AddInternal(content);
            content.FadeIn(200, Easing.OutQuint);
            placeholder.FadeOut(200);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
        }
    }

    /// <summary>
    /// A circular avatar of a Discord user.
    /// </summary>
    public partial class DiscordAvatar : CircularContainer
    {
        public DiscordAvatar(string? url, float size = DiscordStyle.AVATAR_SIZE)
        {
            Size = new Vector2(size);
            Masking = true;

            Child = new DiscordRemoteImage(url)
            {
                RelativeSizeAxes = Axes.Both,
            };
        }
    }

    /// <summary>
    /// An image of a message, which opens in the browser when clicked.
    /// </summary>
    public partial class DiscordMediaImage : OsuClickableContainer
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        public DiscordMediaImage(string url, string? displayUrl, Vector2 size, bool animated)
        {
            Size = size;
            Masking = true;
            CornerRadius = 6;

            TooltipText = SlopDiscordStrings.OpenInBrowser;
            Action = () => host.OpenUrlExternally(url);

            Child = new DiscordRemoteImage(animated ? displayUrl ?? url : DiscordMedia.Downscale(displayUrl ?? url, size))
            {
                RelativeSizeAxes = Axes.Both,
            };
        }
    }

    /// <summary>
    /// A video of a message, which shows a thumbnail until it is clicked and then plays inline.
    /// </summary>
    public partial class DiscordMediaVideo : CompositeDrawable
    {
        private readonly string videoUrl;
        private readonly bool autoPlay;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private Container thumbnail = null!;
        private LoadingSpinner spinner = null!;

        private bool started;

        /// <param name="videoUrl">The URL of the video.</param>
        /// <param name="thumbnailUrl">The URL of an image shown until the video is started.</param>
        /// <param name="size">The displayed size.</param>
        /// <param name="autoPlay">Whether the video starts by itself and loops without sound, like a GIF.</param>
        public DiscordMediaVideo(string videoUrl, string? thumbnailUrl, Vector2 size, bool autoPlay)
        {
            this.videoUrl = videoUrl;
            this.autoPlay = autoPlay;

            Size = size;
            Masking = true;
            CornerRadius = 6;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.6f,
                },
                thumbnail = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new DiscordRemoteImage(thumbnailUrl == null ? null : DiscordMedia.Downscale(thumbnailUrl, size, @"jpeg"))
                        {
                            RelativeSizeAxes = Axes.Both,
                            ShowPlaceholder = false,
                        },
                        new CircularContainer
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(48),
                            Masking = true,
                            Alpha = autoPlay ? 0 : 1,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black,
                                    Alpha = 0.6f,
                                },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Icon = FontAwesome.Solid.Play,
                                    Size = new Vector2(18),
                                    X = 2,
                                },
                            },
                        },
                    },
                },
                spinner = new LoadingSpinner
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(32),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (autoPlay)
                start();
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (started)
                return false;

            start();
            return true;
        }

        private void start()
        {
            started = true;
            spinner.Show();

            Task.Run(async () =>
            {
                try
                {
                    byte[] data = await DiscordMedia.Download(videoUrl, cancellation.Token).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        var video = new Video(new MemoryStream(data), false)
                        {
                            RelativeSizeAxes = Axes.Both,
                            FillMode = FillMode.Fit,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Loop = true,
                            Alpha = 0,
                        };

                        AddInternal(video);
                        video.FadeIn(200, Easing.OutQuint);
                        thumbnail.FadeOut(200);
                        spinner.Hide();
                    });
                }
                catch (Exception e)
                {
                    if (cancellation.IsCancellationRequested)
                        return;

                    Logger.Log($@"Failed to load a Discord video: {e.Message}", LoggingTarget.Network);

                    Schedule(() =>
                    {
                        spinner.Hide();
                        started = false;
                    });
                }
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
