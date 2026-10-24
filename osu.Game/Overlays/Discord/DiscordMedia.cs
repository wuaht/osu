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
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
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
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
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
            // Long enough for videos, which are downloaded completely before they play.
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(DiscordFullClient.USER_AGENT);
            return client;
        }

        private static readonly string[] discord_hosts =
        {
            @"discordapp.com",
            @"discordapp.net",
            @"discord.com",
        };

        /// <summary>
        /// Whether media may be loaded from a URL.
        /// Only Discord (and the emoji images) is contacted automatically, so the IP address of the user isn't revealed to other sites.
        /// </summary>
        public static bool IsAllowedUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;

            if (discord_hosts.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith('.' + h, StringComparison.OrdinalIgnoreCase)))
                return true;

            if (uri.AbsoluteUri.StartsWith(DiscordEmoji.TWEMOJI_BASE_URL, StringComparison.Ordinal))
                return true;

            // Media of a link preview which the user chose to load.
            return DiscordLinkPreviews.IsPreviewMedia(url);
        }

        public static Task<byte[]> Download(string url, CancellationToken cancellation)
        {
            if (!IsAllowedUrl(url))
                return Task.FromException<byte[]>(new InvalidOperationException($@"Media is only loaded from Discord, not from {url}."));

            return http_client.GetByteArrayAsync(url, cancellation);
        }

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
                    dominantColours[url] = task = throttled(() => computeDominantColour(url));

                return task;
            }
        }

        /// <summary>
        /// Limits how many lookups run at once. A lookup performs its request synchronously and blocks a thread pool thread,
        /// so many at once (like the icons of all guilds) starve the thread pool which the requests need to complete.
        /// </summary>
        private static readonly SemaphoreSlim lookup_throttle = new SemaphoreSlim(4);

        private static async Task<T> throttled<T>(Func<T> lookup)
        {
            await lookup_throttle.WaitAsync().ConfigureAwait(false);

            try
            {
                return await Task.Run(lookup).ConfigureAwait(false);
            }
            finally
            {
                lookup_throttle.Release();
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

                var task = throttled(() => (Texture?)Get(url));
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
            protected override string GetLookupUrl(string url) => DiscordMedia.IsAllowedUrl(url) ? url : string.Empty;
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
    /// A video of a message, which shows a thumbnail until it is clicked and then plays inline with its sound and playback controls.
    /// </summary>
    public partial class DiscordMediaVideo : CompositeDrawable
    {
        private const float controls_height = 32;

        private readonly string videoUrl;
        private readonly bool autoPlay;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        private readonly BindableDouble muteAdjustment = new BindableDouble(1);

        private Container thumbnail = null!;
        private LoadingSpinner spinner = null!;
        private VideoControls controls = null!;

        private Video? video;
        private ITrackStore? trackStore;
        private Track? track;

        /// <summary>
        /// Whether the playback position is taken from the sound of the video, which only exists once it was decoded.
        /// </summary>
        private bool audioAttached;

        private bool started;
        private bool playing;
        private bool controlsShown;

        private IBindable<Visibility>? overlayState;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private DiscordOverlay? overlay { get; set; }

        /// <param name="videoUrl">The URL of the video.</param>
        /// <param name="thumbnailUrl">The URL of an image shown until the video is started.</param>
        /// <param name="size">The displayed size.</param>
        /// <param name="autoPlay">Whether the video starts by itself and loops without sound or controls, like a GIF.</param>
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
                controls = new VideoControls
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = controls_height,
                    Alpha = 0,
                    TogglePlayback = togglePlayback,
                    ToggleMute = () => muteAdjustment.Value = muteAdjustment.Value > 0 ? 0 : 1,
                    OpenInBrowser = () => host.OpenUrlExternally(videoUrl),
                    SeekTo = fraction => seekTo(fraction * duration),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (overlay != null)
            {
                overlayState = overlay.State.GetBoundCopy();

                // Drawables of a hidden overlay stop updating, but the sound would keep playing.
                overlayState.BindValueChanged(s =>
                {
                    if (s.NewValue == Visibility.Hidden)
                        pause();
                });
            }

            if (autoPlay)
                start();
        }

        private double duration => audioAttached ? track!.Length : video?.Duration ?? 0;

        private double position => audioAttached ? track!.CurrentTime : video?.PlaybackPosition ?? 0;

        protected override void Update()
        {
            base.Update();

            if (video == null)
                return;

            if (!audioAttached && track?.IsLoaded == true)
            {
                // The sound is decoded separately and may only become available after the video started, so it continues from where the video is.
                audioAttached = true;
                track.Seek(video.PlaybackPosition);

                if (playing)
                    track.Start();
            }

            if (audioAttached)
            {
                video.IsPlaying = false;
                video.PlaybackPosition = track!.CurrentTime;
            }
            else
            {
                video.IsPlaying = playing;

                if (playing && !video.Loop && video.Duration > 0 && video.PlaybackPosition >= video.Duration)
                    playing = false;
            }

            bool showControls = !autoPlay && (IsHovered || !playing);

            if (showControls != controlsShown)
            {
                controlsShown = showControls;
                controls.FadeTo(showControls ? 1 : 0, 200, Easing.OutQuint);
            }

            controls.UpdateState(position, duration, playing, audioAttached, muteAdjustment.Value == 0);
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (autoPlay)
                return false;

            if (!started)
                start();
            else
                togglePlayback();

            return true;
        }

        private void togglePlayback()
        {
            if (playing)
                pause();
            else
                play();
        }

        private void play()
        {
            if (video == null)
                return;

            // Restart a video which played until its end.
            if (duration > 0 && position >= duration - 1)
                seekTo(0);

            playing = true;

            if (audioAttached)
                track!.Start();
        }

        private void pause()
        {
            playing = false;
            track?.Stop();
        }

        private void seekTo(double time)
        {
            if (video == null)
                return;

            time = Math.Clamp(time, 0, Math.Max(0, duration));

            if (audioAttached)
                track!.Seek(time);

            video.PlaybackPosition = time;
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

                    Schedule(() => load(data));
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

        private void load(byte[] data)
        {
            // The video starts from the moment it is loaded, rather than from the time the game started.
            var loaded = new Video(new MemoryStream(data))
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Loop = autoPlay,
                Alpha = 0,
            };

            LoadComponentAsync(loaded, v =>
            {
                video = v;
                playing = true;

                AddInternal(v);
                ChangeInternalChildDepth(controls, float.MinValue);

                v.FadeIn(200, Easing.OutQuint);
                thumbnail.FadeOut(200);
                spinner.Hide();

                if (autoPlay)
                    return;

                // The sound is played by the audio engine, which fails to load it if the video has no sound.
                trackStore = audio.GetTrackStore(new VideoSoundStore(data));
                track = trackStore.Get(@"sound");

                if (track != null)
                {
                    track.AddAdjustment(AdjustableProperty.Volume, muteAdjustment);
                    track.Completed += () => Schedule(() => playing = false);
                }
            }, cancellation.Token);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();

            track?.Stop();
            track?.Dispose();
            trackStore?.Dispose();

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Provides the downloaded video to the audio engine to play its sound.
        /// </summary>
        private class VideoSoundStore : IResourceStore<byte[]>
        {
            private readonly byte[] data;

            public VideoSoundStore(byte[] data)
            {
                this.data = data;
            }

            public byte[] Get(string name) => data;

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(data);

            public Stream GetStream(string name) => new MemoryStream(data, false);

            public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();

            public void Dispose()
            {
            }
        }

        private partial class VideoControls : CompositeDrawable
        {
            public Action? TogglePlayback;
            public Action? ToggleMute;
            public Action? OpenInBrowser;
            public Action<double>? SeekTo;

            private readonly ControlButton playButton;
            private readonly ControlButton muteButton;
            private readonly OsuSpriteText timeText;
            private readonly SeekBar seekBar;

            public VideoControls()
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0), Color4.Black.Opacity(0.7f)),
                    },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 4 },
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(),
                            new Dimension(GridSizeMode.AutoSize),
                            new Dimension(GridSizeMode.AutoSize),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                playButton = new ControlButton(FontAwesome.Solid.Play, () => TogglePlayback?.Invoke()),
                                timeText = new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Font = OsuFont.Default.With(size: 12, fixedWidth: true),
                                    Margin = new MarginPadding { Horizontal = 4 },
                                },
                                seekBar = new SeekBar
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    RelativeSizeAxes = Axes.X,
                                    Height = 16,
                                    Padding = new MarginPadding { Horizontal = 6 },
                                    SeekTo = f => SeekTo?.Invoke(f),
                                },
                                muteButton = new ControlButton(FontAwesome.Solid.VolumeUp, () => ToggleMute?.Invoke()),
                                new ControlButton(FontAwesome.Solid.ExternalLinkAlt, () => OpenInBrowser?.Invoke())
                                {
                                    TooltipText = SlopDiscordStrings.OpenInBrowser,
                                },
                            },
                        },
                    },
                };
            }

            public void UpdateState(double position, double duration, bool playing, bool hasSound, bool muted)
            {
                playButton.Icon = playing ? FontAwesome.Solid.Pause : FontAwesome.Solid.Play;
                playButton.TooltipText = playing ? SlopDiscordStrings.PauseVideo : SlopDiscordStrings.PlayVideo;

                muteButton.Alpha = hasSound ? 1 : 0;
                muteButton.Icon = muted ? FontAwesome.Solid.VolumeMute : FontAwesome.Solid.VolumeUp;
                muteButton.TooltipText = muted ? SlopDiscordStrings.UnmuteVideo : SlopDiscordStrings.MuteVideo;

                timeText.Text = $@"{format(position)} / {format(duration)}";
                seekBar.Progress = duration > 0 ? (float)Math.Clamp(position / duration, 0, 1) : 0;
            }

            private static string format(double time)
            {
                var span = TimeSpan.FromMilliseconds(Math.Max(0, time));
                return $@"{(int)span.TotalMinutes}:{span.Seconds:00}";
            }

            private partial class ControlButton : OsuClickableContainer
            {
                private readonly SpriteIcon icon;

                public IconUsage Icon
                {
                    set => icon.Icon = value;
                }

                public ControlButton(IconUsage icon, Action action)
                {
                    Anchor = Anchor.CentreLeft;
                    Origin = Anchor.CentreLeft;
                    Size = new Vector2(24);
                    Action = action;

                    Child = this.icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Icon = icon,
                        Size = new Vector2(12),
                        Shadow = true,
                    };
                }

                protected override bool OnHover(HoverEvent e)
                {
                    icon.ScaleTo(1.2f, 200, Easing.OutQuint);
                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    icon.ScaleTo(1, 200, Easing.OutQuint);
                    base.OnHoverLost(e);
                }
            }

            private partial class SeekBar : Container
            {
                public Action<double>? SeekTo;

                private readonly Container bar;
                private readonly Box fill;

                public float Progress
                {
                    set => fill.Width = value;
                }

                public SeekBar()
                {
                    Child = bar = new CircularContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 4,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White,
                                Alpha = 0.3f,
                            },
                            fill = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White,
                                Width = 0,
                            },
                        },
                    };
                }

                protected override bool OnHover(HoverEvent e)
                {
                    bar.ResizeHeightTo(6, 200, Easing.OutQuint);
                    return base.OnHover(e);
                }

                protected override void OnHoverLost(HoverLostEvent e)
                {
                    bar.ResizeHeightTo(4, 200, Easing.OutQuint);
                    base.OnHoverLost(e);
                }

                protected override bool OnMouseDown(MouseDownEvent e)
                {
                    seek(e.ScreenSpaceMousePosition);
                    return true;
                }

                protected override bool OnClick(ClickEvent e) => true;

                protected override bool OnDragStart(DragStartEvent e) => true;

                protected override void OnDrag(DragEvent e) => seek(e.ScreenSpaceMousePosition);

                private void seek(Vector2 screenSpacePosition)
                {
                    float x = bar.ToLocalSpace(screenSpacePosition).X;
                    SeekTo?.Invoke(Math.Clamp(x / bar.DrawWidth, 0, 1));
                }
            }
        }
    }
}
