// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Logging;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Screens.Edit.MediaReencoding
{
    public enum MediaReencoderMediaType
    {
        Video,
        Audio,
    }

    public enum MediaReencoderVideoCodec
    {
        [Description("H.264 AVI")]
        H264,

        [Description("XVID AVI")]
        Xvid,

        [Description("WMV2 WMV")]
        Wmv2,
    }

    public enum MediaReencoderResize
    {
        Original,

        [Description("1280x720")]
        Height720,

        [Description("854x480")]
        Height480,

        [Description("640x360")]
        Height360,
    }

    public enum MediaReencoderAudioCodec
    {
        [Description("OGG Vorbis (208 kbps)")]
        OggVorbis,

        [Description("MP3 (192 kbps)")]
        Mp3,
    }

    public record MediaReencoderOptions(
        MediaReencoderMediaType MediaType,
        MediaReencoderVideoCodec VideoCodec = MediaReencoderVideoCodec.H264,
        MediaReencoderResize Resize = MediaReencoderResize.Height720,
        MediaReencoderAudioCodec AudioCodec = MediaReencoderAudioCodec.OggVorbis,
        int Quality = 23);

    /// <summary>
    /// Converts videos and audio tracks into formats accepted by the ranking criteria, using the ffmpeg executable on PATH.
    /// Ported from the stable client's media reencoder.
    /// </summary>
    public static class MediaReencoder
    {
        public const int MAX_QUALITY = 30;

        private static readonly Regex duration_regex = new Regex(@"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled);

        public static string GetOutputExtension(MediaReencoderOptions options)
        {
            if (options.MediaType == MediaReencoderMediaType.Audio)
                return options.AudioCodec == MediaReencoderAudioCodec.OggVorbis ? @".ogg" : @".mp3";

            return options.VideoCodec == MediaReencoderVideoCodec.Wmv2 ? @".wmv" : @".avi";
        }

        public static List<string> BuildArguments(MediaReencoderOptions options, string inputPath, string outputPath)
        {
            var args = new List<string> { @"-hide_banner", @"-nostdin", @"-y", @"-i", inputPath };

            if (options.MediaType == MediaReencoderMediaType.Audio)
            {
                bool ogg = options.AudioCodec == MediaReencoderAudioCodec.OggVorbis;
                // only the first audio stream is kept, as files often contain cover art as a video stream. tags are kept for the beatmap's metadata.
                args.AddRange([@"-map", @"0:a:0", @"-c:a", ogg ? @"libvorbis" : @"libmp3lame", @"-b:a", ogg ? @"208k" : @"192k"]);
            }
            else
            {
                int quality = Math.Clamp(options.Quality, 0, MAX_QUALITY);
                args.AddRange([@"-an", @"-sn", @"-vf", VideoFilter(options.VideoCodec, options.Resize), @"-pix_fmt", @"yuv420p"]);

                switch (options.VideoCodec)
                {
                    case MediaReencoderVideoCodec.Xvid:
                        // qscale has no 0, so the best quality maps onto 1.
                        args.AddRange([@"-c:v", @"mpeg4", @"-vtag", @"xvid", @"-q:v", Math.Max(1, quality).ToString(CultureInfo.InvariantCulture)]);
                        break;

                    case MediaReencoderVideoCodec.Wmv2:
                        args.AddRange([@"-c:v", @"wmv2", @"-b:v", $@"{WmvBitrateKbps(quality, options.Resize)}k"]);
                        break;

                    default:
                        args.AddRange([@"-c:v", @"libx264", @"-preset", @"veryfast", @"-crf", quality.ToString(CultureInfo.InvariantCulture)]);
                        break;
                }
            }

            args.AddRange([@"-progress", @"pipe:1", @"-nostats", outputPath]);
            return args;
        }

        public static string VideoFilter(MediaReencoderVideoCodec codec, MediaReencoderResize resize)
        {
            int height = resize switch
            {
                MediaReencoderResize.Height720 => 720,
                MediaReencoderResize.Height480 => 480,
                MediaReencoderResize.Height360 => 360,
                _ => 0,
            };

            // XVID needs the width to be a multiple of 16 and square pixels.
            bool xvid = codec == MediaReencoderVideoCodec.Xvid;

            if (height > 0)
            {
                int width = height == 480 ? 854 : height * 16 / 9;
                int widthMultiple = xvid ? 16 : 2;
                // only ever downscale, keeping the aspect ratio.
                string scale = $@"min(1,min({width}/iw,{height}/ih))";
                return $@"scale=w='trunc(iw*{scale}/{widthMultiple})*{widthMultiple}':h='trunc(ih*{scale}/2)*2'" + (xvid ? @",setsar=1" : string.Empty);
            }

            return xvid
                ? @"pad=ceil(iw/16)*16:ceil(ih/2)*2:(ow-iw)/2:(oh-ih)/2,setsar=1"
                : @"pad=ceil(iw/2)*2:ceil(ih/2)*2";
        }

        public static int WmvBitrateKbps(int quality, MediaReencoderResize resize)
        {
            quality = Math.Clamp(quality, 0, MAX_QUALITY);

            (int min, int max) = resize switch
            {
                MediaReencoderResize.Height360 => (500, 1400),
                MediaReencoderResize.Height480 => (800, 2200),
                MediaReencoderResize.Height720 => (1400, 3500),
                _ => (900, 2600),
            };

            double t = (double)quality / MAX_QUALITY;
            return (int)Math.Round(max - (max - min) * t * t);
        }

        /// <summary>
        /// Runs ffmpeg to convert <paramref name="inputPath"/> into <paramref name="outputPath"/>.
        /// <paramref name="progress"/> is invoked from a background thread with the progress in [0, 1].
        /// </summary>
        /// <exception cref="Win32Exception">ffmpeg isn't installed.</exception>
        public static async Task RunAsync(MediaReencoderOptions options, string inputPath, string outputPath, Action<float>? progress, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo(@"ffmpeg")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (string arg in BuildArguments(options, inputPath, outputPath))
                startInfo.ArgumentList.Add(arg);

            using var process = new Process();
            process.StartInfo = startInfo;
            process.Start();

            double durationMicroseconds = 0;
            var lastErrorLines = new Queue<string>();

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null)
                    return;

                if (durationMicroseconds == 0 && duration_regex.Match(e.Data) is { Success: true } match)
                {
                    durationMicroseconds = (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 3600
                                            + int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * 60
                                            + double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)) * 1_000_000;
                }

                lock (lastErrorLines)
                {
                    lastErrorLines.Enqueue(e.Data);
                    if (lastErrorLines.Count > 10)
                        lastErrorLines.Dequeue();
                }
            };

            process.OutputDataReceived += (_, e) =>
            {
                // despite its name, out_time_ms is in microseconds.
                const string key = @"out_time_ms=";

                if (e.Data?.StartsWith(key, StringComparison.Ordinal) == true && durationMicroseconds > 0
                                                                              && long.TryParse(e.Data.AsSpan(key.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out long time))
                    progress?.Invoke((float)Math.Clamp(time / durationMicroseconds, 0, 1));
            };

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // the process may have exited on its own in the meantime.
                }

                tryDelete(outputPath);
                throw;
            }

            if (process.ExitCode != 0)
            {
                tryDelete(outputPath);

                lock (lastErrorLines)
                    throw new InvalidOperationException($"ffmpeg failed with exit code {process.ExitCode}:\n{string.Join('\n', lastErrorLines)}");
            }

            progress?.Invoke(1);
        }

        /// <summary>
        /// Reports a failed <see cref="RunAsync"/> to the user, pointing them to the ffmpeg download if it isn't installed.
        /// </summary>
        public static void NotifyFailure(Exception e, string filename, INotificationOverlay? notifications, OsuGame? game)
        {
            if (e is not Win32Exception)
            {
                Logger.Error(e, $"Failed to reencode {filename}.");
                return;
            }

            notifications?.Post(new SimpleErrorNotification
            {
                Text = SlopEditorStrings.MediaReencoderFFmpegRequired(ffmpegInstallCommand),
                Activated = () =>
                {
                    game?.OpenUrlExternally(@"https://ffmpeg.org/download.html");
                    return true;
                }
            });
        }

        private static string ffmpegInstallCommand
        {
            get
            {
                switch (RuntimeInfo.OS)
                {
                    case RuntimeInfo.Platform.Windows:
                        return @"winget install Gyan.FFmpeg";

                    case RuntimeInfo.Platform.macOS:
                        return @"brew install ffmpeg";

                    default:
                        return @"sudo apt install ffmpeg";
                }
            }
        }

        private static void tryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // leftovers in the temporary directory are harmless.
            }
        }
    }
}
