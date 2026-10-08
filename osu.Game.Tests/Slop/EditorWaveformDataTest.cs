// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using ManagedBass;
using NUnit.Framework;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class EditorWaveformDataTest
    {
        private const int sample_rate = 44100;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // the "no sound" device is enough for decoding.
            Bass.Init(0);
            Assume.That(Bass.CurrentDevice, Is.GreaterThanOrEqualTo(0), "BASS is not available.");
        }

        [TestCase(0)]
        [TestCase(31)]
        [TestCase(32)]
        [TestCase(66150)]
        [TestCase(66173)]
        [TestCase(131071)]
        public void TestPeakIsAtExactSample(int impulseFrame)
        {
            var samples = new float[132300];
            samples[impulseFrame] = 0.8f;

            var data = analyse(samples);

            Assert.That(data.FrameCount, Is.EqualTo(samples.Length));
            Assert.That(data.SampleRate, Is.EqualTo(sample_rate));

            var level = data.Levels[0];
            int expectedBlock = impulseFrame / EditorWaveformData.BLOCK_FRAMES;

            Assert.That(argMax(level.Max), Is.EqualTo(expectedBlock));
            Assert.That(level.Max[expectedBlock], Is.EqualTo(0.8f).Within(0.001f));

            // the block's time matches the sample's time to within the block length.
            double blockStart = data.FrameToTime((long)expectedBlock * EditorWaveformData.BLOCK_FRAMES);
            double impulseTime = impulseFrame * 1000.0 / sample_rate;
            Assert.That(impulseTime, Is.InRange(blockStart, blockStart + data.FrameToTime(EditorWaveformData.BLOCK_FRAMES)));
        }

        [Test]
        public void TestSignedRange()
        {
            const int positive_frame = 1000;
            const int negative_frame = 1003;

            var samples = new float[10_000];
            samples[positive_frame] = 0.5f;
            samples[negative_frame] = -0.7f;

            var data = analyse(samples);
            var level = data.Levels[0];

            int block = positive_frame / EditorWaveformData.BLOCK_FRAMES;
            Assert.That(negative_frame / EditorWaveformData.BLOCK_FRAMES, Is.EqualTo(block));

            Assert.That(level.Max[block], Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(level.Min[block], Is.EqualTo(-0.7f).Within(0.001f));

            // other blocks are silent.
            Assert.That(level.Max[block + 1], Is.EqualTo(0));
            Assert.That(level.Min[block + 1], Is.EqualTo(0));

            // the normalisation considers negative samples.
            Assert.That(data.PeakMax, Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void TestBandsAreNotShifted()
        {
            const int impulse_frame = 100_000 + 13;

            var samples = new float[200_000];
            samples[impulse_frame] = 0.8f;

            var level = analyse(samples).Levels[0];
            int expectedBlock = impulse_frame / EditorWaveformData.BLOCK_FRAMES;

            // the zero-phase filters respond symmetrically around the impulse.
            Assert.That(argMax(level.Low), Is.EqualTo(expectedBlock), "low band is shifted");
            Assert.That(argMax(level.Mid), Is.EqualTo(expectedBlock), "mid band is shifted");
            Assert.That(argMax(level.High), Is.EqualTo(expectedBlock), "high band is shifted");
        }

        [Test]
        public void TestImpulseAcrossSegmentBoundary()
        {
            // the band filters process the audio in segments of 65536 samples.
            const int impulse_frame = 65536 - 5;

            var samples = new float[150_000];
            samples[impulse_frame] = 0.8f;

            var level = analyse(samples).Levels[0];
            int expectedBlock = impulse_frame / EditorWaveformData.BLOCK_FRAMES;

            Assert.That(argMax(level.Low), Is.EqualTo(expectedBlock));
            Assert.That(argMax(level.Mid), Is.EqualTo(expectedBlock));
            Assert.That(argMax(level.High), Is.EqualTo(expectedBlock));
        }

        [Test]
        public void TestFrequencyBands()
        {
            var samples = new float[sample_rate * 2];

            // first second: 60Hz, second second: 6000Hz.
            for (int i = 0; i < samples.Length; i++)
            {
                double frequency = i < sample_rate ? 60 : 6000;
                samples[i] = (float)(0.5 * Math.Sin(2 * Math.PI * frequency * i / sample_rate));
            }

            var level = analyse(samples).Levels[0];

            // sample well within each half, away from the transition.
            int lowBlock = sample_rate / 2 / EditorWaveformData.BLOCK_FRAMES;
            int highBlock = sample_rate * 3 / 2 / EditorWaveformData.BLOCK_FRAMES;

            Assert.That(level.Low[lowBlock], Is.GreaterThan(level.High[lowBlock] * 10));
            Assert.That(level.Low[lowBlock], Is.GreaterThan(level.Mid[lowBlock]));

            Assert.That(level.High[highBlock], Is.GreaterThan(level.Low[highBlock] * 10));
            Assert.That(level.High[highBlock], Is.GreaterThan(level.Mid[highBlock]));
        }

        [Test]
        public void TestCoarserLevelsKeepPeaks()
        {
            var samples = new float[100_000];
            samples[54321] = 0.6f;

            var data = analyse(samples);

            for (int i = 1; i < data.Levels.Count; i++)
            {
                var level = data.Levels[i];

                Assert.That(level.FramesPerBlock, Is.EqualTo(EditorWaveformData.BLOCK_FRAMES << i));
                Assert.That(level.Max.Max(), Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(argMax(level.Max), Is.EqualTo(54321 / level.FramesPerBlock));
            }

            Assert.That(data.Levels[^1].Count, Is.EqualTo(1));
        }

        private static EditorWaveformData analyse(float[] monoSamples)
        {
            var data = EditorWaveformData.Analyse(createWav(monoSamples), CancellationToken.None);
            Assert.That(data, Is.Not.Null);
            return data!;
        }

        private static int argMax(float[] values)
        {
            int index = 0;

            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > values[index])
                    index = i;
            }

            return index;
        }

        /// <summary>
        /// Creates a stereo 32-bit float WAV file with the same samples on both channels.
        /// </summary>
        private static MemoryStream createWav(float[] samples)
        {
            const int channels = 2;
            const int bytes_per_sample = 4;

            var stream = new MemoryStream();

            using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
            {
                int dataLength = samples.Length * channels * bytes_per_sample;

                writer.Write("RIFF"u8.ToArray());
                writer.Write(36 + dataLength);
                writer.Write("WAVE"u8.ToArray());

                writer.Write("fmt "u8.ToArray());
                writer.Write(16);
                writer.Write((short)3); // IEEE float
                writer.Write((short)channels);
                writer.Write(sample_rate);
                writer.Write(sample_rate * channels * bytes_per_sample);
                writer.Write((short)(channels * bytes_per_sample));
                writer.Write((short)(bytes_per_sample * 8));

                writer.Write("data"u8.ToArray());
                writer.Write(dataLength);

                foreach (float sample in samples)
                {
                    for (int c = 0; c < channels; c++)
                        writer.Write(sample);
                }
            }

            stream.Position = 0;
            return stream;
        }
    }
}
