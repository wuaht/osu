// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Overlays.Visualizer;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class SpectrumAnalyserTest
    {
        private const int bins = 256;
        private const int band_count = 64;

        [Test]
        public void TestSilenceProducesNoOutput()
        {
            var analyser = new SpectrumAnalyser(band_count);

            run(analyser, 2000, 60, _ => new float[bins]);

            Assert.That(analyser.Bands.ToArray(), Is.All.EqualTo(0));
            Assert.That(analyser.Bass, Is.EqualTo(0));
            Assert.That(analyser.Loudness, Is.EqualTo(0));
            Assert.That(analyser.Beat, Is.EqualTo(0));
        }

        [Test]
        public void TestOutputStaysInRange()
        {
            var analyser = new SpectrumAnalyser(band_count);
            var random = new Random(1234);

            run(analyser, 5000, 60, _ =>
            {
                float[] spectrum = new float[bins];
                for (int i = 0; i < bins; i++)
                    spectrum[i] = (float)random.NextDouble() * 2;
                return spectrum;
            }, () =>
            {
                Assert.That(analyser.Bands.ToArray(), Is.All.InRange(0f, 1f));
                Assert.That(analyser.Bass, Is.InRange(0f, 1f));
                Assert.That(analyser.Loudness, Is.InRange(0f, 1f));
                Assert.That(analyser.Beat, Is.InRange(0f, 1f));
            });
        }

        [Test]
        public void TestLouderBandsProduceHigherLevels()
        {
            var analyser = new SpectrumAnalyser(band_count);

            // equal frequency ranges with different levels.
            run(analyser, 2000, 60, _ => spectrumWithRanges((20, 30, 0.5f), (40, 60, 0.05f), (70, 100, 0.005f)));

            float loud = bandLevelAtBin(analyser, 25);
            float medium = bandLevelAtBin(analyser, 50);
            float quiet = bandLevelAtBin(analyser, 85);

            Assert.That(loud, Is.GreaterThan(medium));
            Assert.That(medium, Is.GreaterThan(quiet));
            Assert.That(quiet, Is.GreaterThan(0));
        }

        [Test]
        public void TestOutputIsIndependentOfInputScale()
        {
            var loud = new SpectrumAnalyser(band_count);
            var quiet = new SpectrumAnalyser(band_count);

            run(loud, 3000, 60, _ => scaled(musicLikeSpectrum(), 1));
            run(quiet, 3000, 60, _ => scaled(musicLikeSpectrum(), 0.1f));

            Assert.That(quiet.Bands.ToArray(), Is.EqualTo(loud.Bands.ToArray()).Within(0.01f));
            Assert.That(quiet.Bass, Is.EqualTo(loud.Bass).Within(0.01f));
            Assert.That(quiet.Loudness, Is.EqualTo(loud.Loudness).Within(0.01f));
        }

        [Test]
        public void TestOutputIsIndependentOfFrameRate()
        {
            var slow = new SpectrumAnalyser(band_count);
            var fast = new SpectrumAnalyser(band_count);

            // a slowly modulated spectrum which both frame rates sample adequately.
            Func<double, float[]> signal = time => scaled(musicLikeSpectrum(), 0.6f + 0.4f * (float)Math.Sin(time / 400));

            run(slow, 2500, 60, signal);
            run(fast, 2500, 240, signal);

            Assert.That(fast.Bands.ToArray(), Is.EqualTo(slow.Bands.ToArray()).Within(0.05f));
            Assert.That(fast.Bass, Is.EqualTo(slow.Bass).Within(0.05f));
            Assert.That(fast.Loudness, Is.EqualTo(slow.Loudness).Within(0.05f));
        }

        [TestCase(60)]
        [TestCase(144)]
        [TestCase(240)]
        public void TestBeatsAreDetectedOnBassPulses(double fps)
        {
            var analyser = new SpectrumAnalyser(band_count);
            int beats = 0;

            // 120 bpm kick drum: a 60 ms bass pulse every 500 ms, over a constant bed of mids.
            run(analyser, 10000, fps, time =>
            {
                float[] spectrum = spectrumWithRanges((10, 100, 0.05f));
                bool pulse = time % 500 < 60;

                for (int i = 0; i < SpectrumAnalyser.BASS_BINS; i++)
                    spectrum[i] = pulse ? 0.6f : 0.02f;

                return spectrum;
            }, () =>
            {
                if (analyser.BeatOnset)
                    beats++;
            });

            Assert.That(beats, Is.InRange(19, 20));
        }

        [Test]
        public void TestConstantBassProducesNoBeats()
        {
            var analyser = new SpectrumAnalyser(band_count);
            int beats = 0;

            run(analyser, 3000, 60, _ => spectrumWithRanges((0, SpectrumAnalyser.BASS_BINS, 0.6f)));

            // only the very first frame may register as an onset, coming from silence.
            run(analyser, 5000, 60, _ => spectrumWithRanges((0, SpectrumAnalyser.BASS_BINS, 0.6f)), () =>
            {
                if (analyser.BeatOnset)
                    beats++;
            });

            Assert.That(beats, Is.Zero);
        }

        [Test]
        public void TestResetReturnsToSilence()
        {
            var analyser = new SpectrumAnalyser(band_count);

            run(analyser, 1000, 60, _ => musicLikeSpectrum());
            Assert.That(analyser.Loudness, Is.GreaterThan(0));

            analyser.Reset();

            Assert.That(analyser.Bands.ToArray(), Is.All.EqualTo(0));
            Assert.That(analyser.Bass, Is.EqualTo(0));
            Assert.That(analyser.Loudness, Is.EqualTo(0));
            Assert.That(analyser.Beat, Is.EqualTo(0));
        }

        [Test]
        public void TestShortInputIsHandled()
        {
            var analyser = new SpectrumAnalyser(band_count);

            Assert.DoesNotThrow(() => analyser.Update(ReadOnlySpan<float>.Empty, 16));
            Assert.DoesNotThrow(() => analyser.Update(new[] { 0.5f, 0.5f }, 16));
        }

        private static void run(SpectrumAnalyser analyser, double duration, double fps, Func<double, float[]> spectrumAt, Action? afterUpdate = null)
        {
            double frameTime = 1000 / fps;

            for (double time = 0; time < duration; time += frameTime)
            {
                analyser.Update(spectrumAt(time), frameTime);
                afterUpdate?.Invoke();
            }
        }

        private static float bandLevelAtBin(SpectrumAnalyser analyser, float bin)
        {
            float ratio = SpectrumAnalyser.LAST_BIN / SpectrumAnalyser.FIRST_BIN;
            int band = (int)(Math.Log(bin / SpectrumAnalyser.FIRST_BIN) / Math.Log(ratio) * analyser.BandCount);

            return analyser.Bands[Math.Clamp(band, 0, analyser.BandCount - 1)];
        }

        private static float[] spectrumWithRanges(params (int start, int end, float level)[] ranges)
        {
            float[] spectrum = new float[bins];

            foreach (var (start, end, level) in ranges)
            {
                for (int i = start; i < end; i++)
                    spectrum[i] = level;
            }

            return spectrum;
        }

        /// <summary>
        /// A spectrum falling off towards high frequencies, as is typical for music.
        /// </summary>
        private static float[] musicLikeSpectrum()
        {
            float[] spectrum = new float[bins];

            for (int i = 0; i < bins; i++)
                spectrum[i] = 0.5f / (1 + i * 0.25f);

            return spectrum;
        }

        private static float[] scaled(float[] spectrum, float scale)
        {
            for (int i = 0; i < spectrum.Length; i++)
                spectrum[i] *= scale;

            return spectrum;
        }
    }
}
