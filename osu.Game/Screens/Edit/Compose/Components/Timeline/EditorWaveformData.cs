// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ManagedBass;
using osu.Framework.Audio.Callbacks;
using osu.Framework.Logging;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// Sample-accurate waveform data of an audio track, used by <see cref="EditorWaveformGraph"/>.
    /// </summary>
    /// <remarks>
    /// The audio is decoded with the same decoder (and flags) as the track used for playback, so sample <c>n</c> is located at exactly <c>n / SampleRate</c> seconds.
    /// The minimum and maximum sample values of blocks of <see cref="BLOCK_FRAMES"/> samples are stored, without any smoothing which could hide or move peaks.
    /// When zoomed in far enough, this shows the actual shape of the audio signal.
    /// Frequency bands are separated with zero-phase (forward-backward) filters, so they are not shifted in time either.
    /// </remarks>
    public class EditorWaveformData
    {
        /// <summary>
        /// The number of samples (per channel) per block at the finest level. About 0.18ms at 44.1kHz, which is less than a pixel at the timeline's maximum zoom.
        /// </summary>
        public const int BLOCK_FRAMES = 8;

        /// <summary>
        /// The upper cutoff frequency of the low (bass) band.
        /// </summary>
        public const double LOW_CUTOFF = 200;

        /// <summary>
        /// The lower cutoff frequency of the high (treble) band. The mid band lies between <see cref="LOW_CUTOFF"/> and this.
        /// </summary>
        public const double HIGH_CUTOFF = 2000;

        public readonly double SampleRate;

        /// <summary>
        /// The total number of samples (per channel).
        /// </summary>
        public readonly long FrameCount;

        /// <summary>
        /// The block levels, from finest (<see cref="BLOCK_FRAMES"/> samples per block) to coarsest. Each level halves the number of blocks.
        /// </summary>
        public readonly IReadOnlyList<Level> Levels;

        /// <summary>
        /// The maximum values of the whole track, used for normalisation. Never zero.
        /// </summary>
        public readonly float PeakMax, LowMax, MidMax, HighMax;

        private EditorWaveformData(double sampleRate, long frameCount, IReadOnlyList<Level> levels)
        {
            SampleRate = sampleRate;
            FrameCount = frameCount;
            Levels = levels;

            var finest = levels[0];
            PeakMax = nonZero(Math.Max(maximum(finest.Max), -minimum(finest.Min)));
            LowMax = nonZero(maximum(finest.Low));
            MidMax = nonZero(maximum(finest.Mid));
            HighMax = nonZero(maximum(finest.High));

            static float maximum(float[] values)
            {
                float result = 0;
                foreach (float v in values)
                    result = Math.Max(result, v);
                return result;
            }

            static float minimum(float[] values)
            {
                float result = 0;
                foreach (float v in values)
                    result = Math.Min(result, v);
                return result;
            }

            static float nonZero(float value) => value > 0 ? value : 1;
        }

        /// <summary>
        /// The time at which a sample is played, in milliseconds.
        /// </summary>
        public double FrameToTime(long frame) => frame / SampleRate * 1000;

        public class Level
        {
            public readonly int FramesPerBlock;

            /// <summary>
            /// The minimum and maximum sample values of any channel in each block.
            /// </summary>
            public readonly float[] Min, Max;

            /// <summary>
            /// The maximum absolute sample values of the (mono) frequency bands in each block.
            /// </summary>
            public readonly float[] Low, Mid, High;

            public int Count => Max.Length;

            public Level(int framesPerBlock, float[] min, float[] max, float[] low, float[] mid, float[] high)
            {
                FramesPerBlock = framesPerBlock;
                Min = min;
                Max = max;
                Low = low;
                Mid = mid;
                High = high;
            }

            /// <summary>
            /// Creates the next coarser level, where each block covers two blocks of this level.
            /// </summary>
            public Level CreateCoarser()
            {
                int count = (Count + 1) / 2;

                return new Level(FramesPerBlock * 2, halve(Min, Math.Min), halve(Max, Math.Max), halve(Low, Math.Max), halve(Mid, Math.Max), halve(High, Math.Max));

                float[] halve(float[] values, Func<float, float, float> combine)
                {
                    float[] result = new float[count];

                    for (int i = 0; i < count; i++)
                    {
                        int first = i * 2;
                        result[i] = first + 1 < values.Length ? combine(values[first], values[first + 1]) : values[first];
                    }

                    return result;
                }
            }
        }

        #region Analysis

        /// <summary>
        /// The number of samples processed at once by the backward pass of the band filters.
        /// </summary>
        private const int segment_frames = 65536;

        /// <summary>
        /// The number of additional samples the backward pass of the band filters starts before the end of a segment,
        /// so that its (initially empty) filter state has settled by the time the segment is reached.
        /// </summary>
        private const int segment_overlap_frames = 16384;

        /// <summary>
        /// Decodes and analyses the given audio data. Blocking.
        /// </summary>
        /// <param name="data">The audio file. Disposed when done.</param>
        /// <param name="cancellationToken">A token to cancel the analysis.</param>
        /// <returns>The data, or <see langword="null"/> if the audio could not be decoded.</returns>
        public static EditorWaveformData? Analyse(Stream data, CancellationToken cancellationToken)
        {
            using (data)
            {
                if (Bass.CurrentDevice < 0)
                {
                    Logger.Log("Failed to analyse waveform as no bass device is available.");
                    return null;
                }

                var fileCallbacks = new FileCallbacks(new DataStreamFileProcedures(data));

                // The same flags as the playback track (see TrackBass), with float samples for accuracy.
                int stream = Bass.CreateStream(StreamSystem.NoBuffer, BassFlags.Decode | BassFlags.Prescan | BassFlags.Float, fileCallbacks.Callbacks, fileCallbacks.Handle);

                try
                {
                    if (stream == 0 || !Bass.ChannelGetInfo(stream, out ChannelInfo info) || info.Channels < 1 || info.Frequency <= 0)
                    {
                        Logger.Log($"Failed to analyse waveform ({Bass.LastError}).");
                        return null;
                    }

                    return analyse(stream, info, cancellationToken);
                }
                finally
                {
                    if (stream != 0)
                        Bass.StreamFree(stream);

                    fileCallbacks.Dispose();
                }
            }
        }

        private static EditorWaveformData? analyse(int stream, ChannelInfo info, CancellationToken cancellationToken)
        {
            const int bytes_per_sample = 4;

            int channels = info.Channels;
            double sampleRate = info.Frequency;

            long expectedFrames = Math.Max(0, Bass.ChannelGetLength(stream) / (bytes_per_sample * channels));
            var accumulator = new BlockAccumulator((int)(expectedFrames / BLOCK_FRAMES) + 1);
            var bands = new BandSeparator(sampleRate, accumulator);

            float[] buffer = new float[4096 * channels];
            long frame = 0;
            int read;

            while ((read = Bass.ChannelGetData(stream, buffer, buffer.Length * bytes_per_sample)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int framesRead = read / (bytes_per_sample * channels);

                for (int f = 0; f < framesRead; f++)
                {
                    int offset = f * channels;
                    float min = float.MaxValue;
                    float max = float.MinValue;
                    float sum = 0;

                    for (int c = 0; c < channels; c++)
                    {
                        float sample = buffer[offset + c];
                        min = Math.Min(min, sample);
                        max = Math.Max(max, sample);
                        sum += sample;
                    }

                    accumulator.AddSample(frame, min, max);
                    bands.Add(sum / channels);
                    frame++;
                }

                bands.ProcessAvailable(cancellationToken);
            }

            if (read < 0 && Bass.LastError != Errors.Ended)
            {
                Logger.Log($"Failed to analyse waveform ({Bass.LastError}).");
                return null;
            }

            bands.Finish(cancellationToken);

            if (frame == 0)
                return null;

            var levels = new List<Level> { accumulator.CreateLevel((int)((frame + BLOCK_FRAMES - 1) / BLOCK_FRAMES)) };

            while (levels[^1].Count > 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
                levels.Add(levels[^1].CreateCoarser());
            }

            return new EditorWaveformData(sampleRate, frame, levels);
        }

        /// <summary>
        /// Collects the per-block maxima of the finest level.
        /// </summary>
        private class BlockAccumulator
        {
            private float[] min, max, low, mid, high;

            /// <summary>
            /// The number of blocks which contain samples. Blocks are filled in order.
            /// </summary>
            private int usedBlocks;

            public BlockAccumulator(int capacity)
            {
                min = new float[capacity];
                max = new float[capacity];
                low = new float[capacity];
                mid = new float[capacity];
                high = new float[capacity];
            }

            public void AddSample(long frame, float minValue, float maxValue)
            {
                int block = ensureCapacity(frame);

                // the first sample of a block initialises it, as the arrays are zero-initialised.
                if (block >= usedBlocks)
                {
                    usedBlocks = block + 1;
                    min[block] = minValue;
                    max[block] = maxValue;
                    return;
                }

                min[block] = Math.Min(min[block], minValue);
                max[block] = Math.Max(max[block], maxValue);
            }

            public void AddBands(long frame, float lowValue, float midValue, float highValue)
            {
                int block = ensureCapacity(frame);
                low[block] = Math.Max(low[block], Math.Abs(lowValue));
                mid[block] = Math.Max(mid[block], Math.Abs(midValue));
                high[block] = Math.Max(high[block], Math.Abs(highValue));
            }

            public Level CreateLevel(int count)
            {
                ensureCapacity((long)count * BLOCK_FRAMES - 1);
                return new Level(BLOCK_FRAMES, min[..count], max[..count], low[..count], mid[..count], high[..count]);
            }

            private int ensureCapacity(long frame)
            {
                int block = (int)(frame / BLOCK_FRAMES);

                if (block >= max.Length)
                {
                    int newLength = Math.Max(block + 1, max.Length * 2);
                    Array.Resize(ref min, newLength);
                    Array.Resize(ref max, newLength);
                    Array.Resize(ref low, newLength);
                    Array.Resize(ref mid, newLength);
                    Array.Resize(ref high, newLength);
                }

                return block;
            }
        }

        /// <summary>
        /// Separates a mono signal into frequency bands using zero-phase filtering.
        /// </summary>
        /// <remarks>
        /// Each band is filtered forwards while decoding, and backwards in segments.
        /// The backward pass of each segment starts <see cref="segment_overlap_frames"/> samples after the segment's end,
        /// which is long enough for the filter state to fully settle, so the result matches filtering the whole signal backwards.
        /// </remarks>
        private class BandSeparator
        {
            private readonly BlockAccumulator accumulator;

            private readonly Biquad[] lowForward, midForward, highForward;
            private readonly Biquad[] lowBackward, midBackward, highBackward;

            private readonly float[] lowBuffer = new float[segment_frames + segment_overlap_frames];
            private readonly float[] midBuffer = new float[segment_frames + segment_overlap_frames];
            private readonly float[] highBuffer = new float[segment_frames + segment_overlap_frames];

            /// <summary>
            /// The frame of the first sample in the buffers.
            /// </summary>
            private long bufferStartFrame;

            private int bufferedFrames;

            public BandSeparator(double sampleRate, BlockAccumulator accumulator)
            {
                this.accumulator = accumulator;

                lowForward = createLow();
                midForward = createMid();
                highForward = createHigh();
                lowBackward = createLow();
                midBackward = createMid();
                highBackward = createHigh();

                // frequencies above the nyquist frequency can't be represented, so the respective filter is left out.
                Biquad[] createLow() => new[] { Biquad.LowPass(sampleRate, LOW_CUTOFF) };
                Biquad[] createMid() => new[] { Biquad.HighPass(sampleRate, LOW_CUTOFF), Biquad.LowPass(sampleRate, HIGH_CUTOFF) };
                Biquad[] createHigh() => new[] { Biquad.HighPass(sampleRate, HIGH_CUTOFF) };
            }

            public void Add(float sample)
            {
                lowBuffer[bufferedFrames] = process(lowForward, sample);
                midBuffer[bufferedFrames] = process(midForward, sample);
                highBuffer[bufferedFrames] = process(highForward, sample);
                bufferedFrames++;

                // keep the buffers from overflowing in case ProcessAvailable isn't called often enough.
                if (bufferedFrames == lowBuffer.Length)
                    processSegment(segment_frames);
            }

            /// <summary>
            /// Processes all complete segments.
            /// </summary>
            public void ProcessAvailable(CancellationToken cancellationToken)
            {
                while (bufferedFrames >= segment_frames + segment_overlap_frames)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    processSegment(segment_frames);
                }
            }

            /// <summary>
            /// Processes all remaining samples, as the end of the signal was reached.
            /// </summary>
            public void Finish(CancellationToken cancellationToken)
            {
                ProcessAvailable(cancellationToken);

                if (bufferedFrames > 0)
                    processSegment(bufferedFrames);
            }

            /// <summary>
            /// Runs the backward pass over the buffered samples, and stores the results of the first <paramref name="frames"/> samples.
            /// </summary>
            private void processSegment(int frames)
            {
                reset(lowBackward);
                reset(midBackward);
                reset(highBackward);

                for (int i = bufferedFrames - 1; i >= 0; i--)
                {
                    float lowValue = process(lowBackward, lowBuffer[i]);
                    float midValue = process(midBackward, midBuffer[i]);
                    float highValue = process(highBackward, highBuffer[i]);

                    if (i < frames)
                        accumulator.AddBands(bufferStartFrame + i, lowValue, midValue, highValue);
                }

                int remaining = bufferedFrames - frames;

                Array.Copy(lowBuffer, frames, lowBuffer, 0, remaining);
                Array.Copy(midBuffer, frames, midBuffer, 0, remaining);
                Array.Copy(highBuffer, frames, highBuffer, 0, remaining);

                bufferStartFrame += frames;
                bufferedFrames = remaining;
            }

            private static float process(Biquad[] filters, float sample)
            {
                double value = sample;

                foreach (var filter in filters)
                    value = filter.Process(value);

                return (float)value;
            }

            private static void reset(Biquad[] filters)
            {
                foreach (var filter in filters)
                    filter.Reset();
            }
        }

        /// <summary>
        /// A second order (12dB/octave) Butterworth filter.
        /// Applied forwards and backwards, the result has no phase shift and a 24dB/octave slope.
        /// </summary>
        private class Biquad
        {
            private readonly double b0, b1, b2, a1, a2;
            private double z1, z2;

            private Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
            {
                this.b0 = b0 / a0;
                this.b1 = b1 / a0;
                this.b2 = b2 / a0;
                this.a1 = a1 / a0;
                this.a2 = a2 / a0;
            }

            public static Biquad LowPass(double sampleRate, double frequency)
            {
                // a cutoff above the nyquist frequency lets everything pass.
                if (frequency >= sampleRate / 2)
                    return new Biquad(1, 0, 0, 1, 0, 0);

                (double cos, double alpha) = parameters(sampleRate, frequency);
                return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
            }

            public static Biquad HighPass(double sampleRate, double frequency)
            {
                // a cutoff above the nyquist frequency lets nothing pass.
                if (frequency >= sampleRate / 2)
                    return new Biquad(0, 0, 0, 1, 0, 0);

                (double cos, double alpha) = parameters(sampleRate, frequency);
                return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
            }

            private static (double cos, double alpha) parameters(double sampleRate, double frequency)
            {
                double w0 = 2 * Math.PI * frequency / sampleRate;
                return (Math.Cos(w0), Math.Sin(w0) / (2 * Math.Sqrt(0.5)));
            }

            public double Process(double x)
            {
                double y = b0 * x + z1;
                z1 = b1 * x - a1 * y + z2;
                z2 = b2 * x - a2 * y;
                return y;
            }

            public void Reset() => z1 = z2 = 0;
        }

        #endregion
    }
}
