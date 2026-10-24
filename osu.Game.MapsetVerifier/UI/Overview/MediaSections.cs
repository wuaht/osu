// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MapsetVerifier.Server.Model.AudioAnalysis;
using MapsetVerifier.Server.Model.VideoAnalysis;
using osu.Framework.Allocation;
using osu.Game.Graphics.Containers;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.MapsetVerifier.UI.Overview
{
    public partial class AudioSection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionAudio;
        public override IconUsage Icon => FontAwesome.Solid.Music;
        protected override int AnalysisIndex => 3;

        private const int max_timestamps = 40;

        private readonly Bindable<bool> advancedAnalysis = new Bindable<bool>();

        private sealed record AudioData(AudioAnalysisResult Audio, FrequencyAnalysisResult? Frequency, SpectralAnalysisResult? Spectral, string? AdvancedError);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopMapsetVerifierShowAdvancedAudioAnalysis, advancedAnalysis);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            advancedAnalysis.BindValueChanged(_ => Scheduler.AddOnce(Refresh));
        }

        protected override object? GetData(VerifierOverview overview) =>
            overview.Audio is AudioAnalysisResult audio ? new AudioData(audio, overview.Frequency, overview.Spectral, overview.AdvancedAudioError) : null;

        protected override string? GetError(object data) => ((AudioData)data).Audio.Success ? null : ((AudioData)data).Audio.ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            var audioData = (AudioData)data;
            var audio = audioData.Audio;

            if (audio.FormatAnalysis is FormatAnalysisResult format)
            {
                var grid = new KeyValueGrid(180)
                           .Add(VerifierStrings.Format, format.Format)
                           .Add(VerifierStrings.Codec, format.Codec)
                           .Add(VerifierStrings.SampleRate, $@"{format.SampleRate} Hz", format.IsStandardSampleRate ? null : VerifierDisplay.WARNING)
                           .Add(VerifierStrings.Channels, format.Channels.ToString())
                           .Add(VerifierStrings.Duration, format.DurationFormatted)
                           .Add(VerifierStrings.Size, format.FileSizeFormatted);

                if (audio.BitrateAnalysis is BitrateAnalysisResult bitrate)
                {
                    grid.Add(VerifierStrings.Bitrate, VerifierStrings.BitrateValue(Format(bitrate.AverageBitrate, @"0")),
                        tooltip: bitrate.MinBitrate != null && bitrate.MaxBitrate != null
                            ? VerifierStrings.BitrateValue($@"{Format(bitrate.MinBitrate.Value, @"0")} - {Format(bitrate.MaxBitrate.Value, @"0")}")
                            : default);
                    grid.Add(VerifierStrings.Vbr, YesNo(bitrate.IsVbr));
                }

                yield return new OverviewCard(VerifierStrings.AudioFile, audio.AudioFilePath).Add(grid);
            }

            if (audio.ChannelAnalysis is ChannelAnalysisResult channels)
            {
                Color4? severityColour = channels.Severity switch
                {
                    ImbalanceSeverity.Severe => VerifierDisplay.PROBLEM,
                    ImbalanceSeverity.Warning => VerifierDisplay.WARNING,
                    ImbalanceSeverity.Minor => VerifierDisplay.CHANGED,
                    _ => null,
                };

                var card = new OverviewCard(VerifierStrings.Channels, channels.IsMono ? VerifierStrings.Mono : channels.IsStereo ? VerifierStrings.Stereo : channels.ChannelCount.ToString())
                    .Add(new KeyValueGrid(180)
                         .Add(VerifierStrings.Balance, $@"{Format(channels.BalanceRatio * 100, @"0.#")}%", severityColour, channels.Severity.ToString())
                         .Add(VerifierStrings.LouderChannel, string.IsNullOrEmpty(channels.LouderChannel) ? VerifierStrings.None.ToString() : channels.LouderChannel)
                         .Add(VerifierStrings.StereoWidth, Format(channels.StereoWidth))
                         .Add(VerifierStrings.PhaseCorrelation, Format(channels.PhaseCorrelation)));

                var balance = channels.BalanceOverTime?.ToArray() ?? Array.Empty<ChannelBalanceDataPoint>();

                if (balance.Length > 1 && channels.IsStereo)
                {
                    card.Add(new TimeGraph(new[]
                        {
                            new TimeGraph.Series(@"L", balance.Select(b => (b.TimeMs, (double)b.LeftLevel)).ToArray(), Colours.Blue),
                            new TimeGraph.Series(@"R", balance.Select(b => (b.TimeMs, (double)b.RightLevel)).ToArray(), Colours.Pink),
                        }, balance[0].TimeMs, balance[^1].TimeMs, v => Format(v, @"0.###"), null),
                        new GraphLegend(new[] { ((LocalisableString)@"L", Colours.Blue), ((LocalisableString)@"R", Colours.Pink) }));
                }

                yield return card;
            }

            if (audio.DynamicRangeAnalysis is DynamicRangeResult dynamics)
            {
                Color4? compressionColour = dynamics.CompressionSeverity switch
                {
                    CompressionSeverity.Heavy => VerifierDisplay.PROBLEM,
                    CompressionSeverity.Moderate => VerifierDisplay.WARNING,
                    _ => null,
                };

                var card = new OverviewCard(VerifierStrings.Loudness)
                    .Add(new KeyValueGrid(180)
                         .Add(VerifierStrings.IntegratedLoudness, $@"{Format(dynamics.IntegratedLoudness, @"0.#")} LUFS")
                         .Add(VerifierStrings.LoudnessRange, $@"{Format(dynamics.LoudnessRange, @"0.#")} LU")
                         .Add(VerifierStrings.TruePeak, $@"{Format(dynamics.TruePeak, @"0.#")} dB", dynamics.TruePeak > 0 ? VerifierDisplay.WARNING : null)
                         .Add(VerifierStrings.DynamicRange, $@"{Format(dynamics.DynamicRange, @"0.#")} dB")
                         .Add(VerifierStrings.CrestFactor, Format(dynamics.CrestFactor, @"0.#"))
                         .Add(VerifierStrings.Compression, dynamics.CompressionDetected ? dynamics.CompressionSeverity.ToString() : VerifierStrings.None.ToString(), compressionColour)
                         .Add(VerifierStrings.Clipping, dynamics.ClippingDetected ? VerifierStrings.ClippingCount(dynamics.ClippingCount) : VerifierStrings.None,
                             dynamics.ClippingDetected ? VerifierDisplay.PROBLEM : null));

                var loudness = dynamics.LoudnessOverTime?.Where(l => !double.IsInfinity(l.ShortTermLoudness) && !double.IsNaN(l.ShortTermLoudness)).ToArray()
                               ?? Array.Empty<LoudnessDataPoint>();

                if (loudness.Length > 1)
                {
                    card.Add(new TimeGraph(new[]
                        {
                            new TimeGraph.Series(VerifierStrings.Loudness, loudness.Select(l => (l.TimeMs, Math.Max(l.ShortTermLoudness, -60))).ToArray(), ColourProvider.Highlight1),
                        }, loudness[0].TimeMs, loudness[^1].TimeMs, v => $@"{Format(v, @"0.#")} LUFS", null));
                }

                var clipping = dynamics.ClippingMarkers?.ToArray() ?? Array.Empty<ClippingMarker>();

                if (clipping.Length > 0)
                {
                    string timestamps = string.Join(@", ", clipping.Take(max_timestamps).Select(c => VerifierDisplay.FormatTime(c.TimeMs)));

                    if (clipping.Length > max_timestamps)
                        timestamps += @", …";

                    card.Add(new IssueTextFlow(timestamps, VerifierSession.GENERAL, 13));
                }

                yield return card;
            }

            if (!advancedAnalysis.Value)
            {
                yield return new OverviewCard(VerifierStrings.FrequencyAnalysis).Add(new SectionPlaceholder(false, VerifierStrings.AdvancedAudioDisabled));

                yield break;
            }

            if (audioData.AdvancedError != null)
            {
                yield return new OverviewCard(VerifierStrings.FrequencyAnalysis).Add(new SectionPlaceholder(false, audioData.AdvancedError));

                yield break;
            }

            if (audioData.Frequency is not FrequencyAnalysisResult frequency || audioData.Spectral is not SpectralAnalysisResult spectral)
            {
                yield return new OverviewCard(VerifierStrings.FrequencyAnalysis).Add(new SectionPlaceholder(true, VerifierStrings.AnalysingAudio));

                yield break;
            }

            var fft = frequency.FftData?.Where(f => f.FrequencyHz >= 20 && !double.IsInfinity(f.MagnitudeDb)).ToArray() ?? Array.Empty<FftDataPoint>();

            var frequencyCard = new OverviewCard(VerifierStrings.FrequencyAnalysis)
                .Add(new KeyValueGrid(180)
                     .Add(VerifierStrings.DominantFrequency, $@"{Format(frequency.DominantFrequency, @"0")} Hz")
                     .Add(VerifierStrings.FundamentalFrequency, $@"{Format(frequency.FundamentalFrequency, @"0")} Hz"));

            if (fft.Length > 1)
            {
                frequencyCard.Add(new TimeGraph(new[]
                    {
                        new TimeGraph.Series(VerifierStrings.FrequencyAnalysis, fft.Select(f => (Math.Log10(f.FrequencyHz), f.MagnitudeDb)).ToArray(), ColourProvider.Highlight1),
                    }, Math.Log10(fft[0].FrequencyHz), Math.Log10(fft[^1].FrequencyHz), v => $@"{Format(v, @"0.#")} dB", null,
                    formatPosition: x => $@"{Format(Math.Pow(10, x), @"0")} Hz"));
            }

            yield return frequencyCard;

            yield return new OverviewCard(VerifierStrings.Spectrogram).Add(new Spectrogram(spectral));
        }

        /// <summary>
        /// The magnitudes of the frequencies of the audio over time, with a logarithmic frequency axis.
        /// </summary>
        private partial class Spectrogram : CompositeDrawable
        {
            private const int image_height = 200;
            private const float min_frequency = 20;

            private readonly SpectralAnalysisResult spectral;

            private Sprite sprite = null!;
            private Texture? texture;

            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

            [Resolved]
            private IRenderer renderer { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public Spectrogram(SpectralAnalysisResult spectral)
            {
                this.spectral = spectral;

                RelativeSizeAxes = Axes.X;
                Height = 180;
                Masking = true;
                CornerRadius = 4;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                    },
                    new SectionPlaceholder(true, VerifierStrings.AnalysingAudio)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                    },
                    sprite = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        FillMode = FillMode.Stretch,
                        Alpha = 0,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                var token = cancellation.Token;

                Task.Run(() =>
                {
                    var image = createImage(token);

                    if (image == null)
                        return;

                    Schedule(() =>
                    {
                        texture = renderer.CreateTexture(image.Width, image.Height);
                        texture.SetData(new TextureUpload(image));
                        sprite.Texture = texture;
                        sprite.FadeIn(300, Easing.OutQuint);
                    });
                }, token);
            }

            private Image<Rgba32>? createImage(CancellationToken token)
            {
                var frames = spectral.SpectrogramData?.ToArray();
                var bins = spectral.FrequencyBins?.ToArray();

                if (frames == null || bins == null || frames.Length == 0 || bins.Length < 2)
                    return null;

                double minDb = spectral.MinDb;
                double range = Math.Max(spectral.MaxDb - minDb, 1);
                double maxFrequency = bins[^1];
                double logMin = Math.Log10(min_frequency);
                double logRange = Math.Log10(maxFrequency) - logMin;

                // The frequency bin shown in each row, from the highest frequency at the top.
                int[] rowBins = new int[image_height];

                for (int y = 0; y < image_height; y++)
                {
                    double frequency = Math.Pow(10, logMin + logRange * (1 - (double)y / (image_height - 1)));
                    int index = Array.BinarySearch(bins, frequency);
                    rowBins[y] = Math.Clamp(index < 0 ? ~index : index, 0, bins.Length - 1);
                }

                int width = Math.Min(frames.Length, 4096);
                var image = new Image<Rgba32>(width, image_height);

                for (int x = 0; x < width; x++)
                {
                    if (token.IsCancellationRequested)
                        return null;

                    var magnitudes = frames[(int)((long)x * frames.Length / width)].Magnitudes as IList<float> ?? frames[(int)((long)x * frames.Length / width)].Magnitudes.ToArray();

                    for (int y = 0; y < image_height; y++)
                    {
                        int bin = rowBins[y];
                        float value = bin < magnitudes.Count ? (float)Math.Clamp((magnitudes[bin] - minDb) / range, 0, 1) : 0;
                        image[x, y] = colourMap(value);
                    }
                }

                return image;
            }

            /// <summary>
            /// A colour map from dark blue over purple and orange to yellow.
            /// </summary>
            private static Rgba32 colourMap(float value)
            {
                ReadOnlySpan<Vector3> stops = stackalloc Vector3[]
                {
                    new Vector3(0.02f, 0.02f, 0.08f),
                    new Vector3(0.3f, 0.07f, 0.45f),
                    new Vector3(0.75f, 0.2f, 0.4f),
                    new Vector3(0.98f, 0.55f, 0.15f),
                    new Vector3(0.99f, 0.95f, 0.6f),
                };

                float scaled = value * (stops.Length - 1);
                int index = Math.Min((int)scaled, stops.Length - 2);
                var colour = Vector3.Lerp(stops[index], stops[index + 1], scaled - index);

                return new Rgba32(colour.X, colour.Y, colour.Z, 1);
            }

            protected override void Dispose(bool isDisposing)
            {
                cancellation.Cancel();
                texture?.Dispose();
                base.Dispose(isDisposing);
            }
        }
    }

    public partial class VideoSection : OverviewSection
    {
        public override LocalisableString Title => VerifierStrings.SectionVideo;
        public override IconUsage Icon => FontAwesome.Solid.Film;
        protected override int AnalysisIndex => 4;

        protected override object? GetData(VerifierOverview overview) => overview.Video;

        protected override string? GetError(object data) => ((VideoAnalysisResult)data).Success ? null : ((VideoAnalysisResult)data).ErrorMessage;

        protected override IEnumerable<Drawable> CreateContent(object data)
        {
            var videos = ((VideoAnalysisResult)data).Videos;

            if (videos.Count == 0)
            {
                yield return new OverviewCard().Add(new SectionPlaceholder(false, VerifierStrings.NoVideos));

                yield break;
            }

            foreach (var video in videos)
            {
                var card = new OverviewCard(video.FileName, video.UsedByDifficulties.Count > 0 ? $@"{VerifierStrings.UsedBy}: {string.Join(@", ", video.UsedByDifficulties)}" : default(LocalisableString));

                if (video.Exists)
                {
                    string codec = video.VideoCodec ?? VerifierStrings.NotAvailable.ToString();

                    if (!string.IsNullOrEmpty(video.VideoCodecProfile))
                        codec += $@" ({video.VideoCodecProfile})";

                    card.Add(new KeyValueGrid(180)
                             .Add(VerifierStrings.Container, video.Container)
                             .Add(VerifierStrings.Codec, codec)
                             .Add(VerifierStrings.Resolution, video.Resolution)
                             .Add(VerifierStrings.FrameRate,
                                 video.FrameRate is double frameRate ? $@"{Format(frameRate)} fps" + (video.IsVariableFrameRate ? $@" ({VerifierStrings.VariableFrameRate})" : string.Empty)
                                     : VerifierStrings.NotAvailable.ToString(),
                                 video.IsVariableFrameRate ? VerifierDisplay.WARNING : null)
                             .Add(VerifierStrings.Bitrate, VerifierStrings.BitrateValue(Format(video.VideoBitrateKbps ?? video.OverallBitrateKbps, @"0")))
                             .Add(VerifierStrings.AudioTrack, video.HasAudioTrack ? $@"{video.AudioCodec} · {video.AudioChannels} ch · {video.AudioSampleRate} Hz" : YesNo(false),
                                 video.HasAudioTrack ? VerifierDisplay.WARNING : null)
                             .Add(VerifierStrings.Duration, video.DurationFormatted)
                             .Add(VerifierStrings.Offset, $@"{video.OffsetMs} ms")
                             .Add(VerifierStrings.Size, video.FileSizeFormatted));
                }

                if (video.Warnings.Count > 0)
                {
                    var warnings = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                    };

                    foreach (string warning in video.Warnings)
                    {
                        var text = new OsuTextFlowContainer(t => t.Font = OsuFont.Default.With(size: 13, weight: FontWeight.SemiBold))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        };

                        text.AddIcon(FontAwesome.Solid.ExclamationTriangle, t =>
                        {
                            t.Colour = VerifierDisplay.WARNING;
                            t.Font = t.Font.With(size: 12);
                        });
                        text.AddText($@"  {warning}");

                        warnings.Add(text);
                    }

                    card.Add(warnings);
                }

                yield return card;
            }
        }
    }
}
