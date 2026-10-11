// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Screens.Edit.Timing;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class TimingDetectionTest
    {
        private const double sample_rate = TimingDetection.DESIGN_SAMPLE_RATE;

        [TestCase(150, 500)]
        [TestCase(174, 1234)]
        [TestCase(96, 80)]
        [TestCase(205, 300)]
        public void TestClickTrack(double bpm, double firstBeat)
        {
            double beatLength = 60000 / bpm;

            var result = TimingDetection.Detect(TimingDetection.CalculateEnvelope(createClickTrack(beatLength, firstBeat, 60), sample_rate));

            Assert.That(result.BeatLength, Is.Not.Null);
            Assert.That(result.Accurate, Is.True);
            Assert.That(result.BPM, Is.EqualTo(bpm).Within(0.05));

            Assert.That(result.Offset, Is.Not.Null);

            // the offset is calibrated to the onsets of real songs. clicks have much sharper onsets, so they are detected earlier.
            const double click_calibration = -22.5;

            double expectedOffset = TimingDetection.NormaliseOffset(firstBeat + click_calibration, beatLength);
            double offset = TimingDetection.NormaliseOffset(result.Offset!.Value, beatLength, expectedOffset - beatLength / 2);

            Assert.That(offset, Is.EqualTo(expectedOffset).Within(5));
        }

        [Test]
        public void TestFastTempoDetectedAsHalf()
        {
            // like TimingAnlyz, beat lengths above 210 BPM are replaced with a multiple of them.
            var result = TimingDetection.Detect(TimingDetection.CalculateEnvelope(createClickTrack(60000 / 240.0, 100, 60), sample_rate));

            Assert.That(result.BPM, Is.EqualTo(120).Within(0.05));
        }

        [Test]
        public void TestNoWarningsForSteadyTrack()
        {
            var result = TimingDetection.Detect(TimingDetection.CalculateEnvelope(createClickTrack(400, 500, 60), sample_rate));

            Assert.That(result.Warnings, Has.Length.EqualTo(6));
            Assert.That(result.Warnings, Has.All.EqualTo(TimingWarning.None));
        }

        [Test]
        public void TestWarningForTempoChange()
        {
            // 150 BPM for 40 seconds, then 165 BPM.
            float[] first = createClickTrack(400, 500, 40);
            float[] second = createClickTrack(60000 / 165.0, 0, 20);
            float[] samples = new float[first.Length + second.Length];
            first.CopyTo(samples, 0);
            second.CopyTo(samples, first.Length);

            float[] envelope = TimingDetection.CalculateEnvelope(samples, sample_rate);
            var warnings = TimingDetection.FindWarnings(envelope, 400, TimingDetection.DetectOffset(envelope, 400));

            Assert.That(warnings[..4], Has.All.EqualTo(TimingWarning.None));
            Assert.That(warnings[4..], Has.All.EqualTo(TimingWarning.NeedsTimingSection));
        }

        [Test]
        public void TestSilence()
        {
            var result = TimingDetection.Detect(TimingDetection.CalculateEnvelope(new float[(int)(sample_rate * 20)], sample_rate));

            Assert.That(result.BeatLength, Is.Null);
            Assert.That(result.Offset, Is.Null);
        }

        /// <summary>
        /// Creates a track of short noise bursts on every beat, louder on the first beat of each bar.
        /// </summary>
        private static float[] createClickTrack(double beatLength, double firstBeat, double seconds)
        {
            var random = new Random(1234);
            float[] samples = new float[(int)(seconds * sample_rate)];

            for (int beat = 0;; beat++)
            {
                double time = firstBeat + beat * beatLength;
                int start = (int)Math.Round(time / 1000 * sample_rate);

                if (start >= samples.Length)
                    break;

                float volume = beat % 4 == 0 ? 0.8f : 0.5f;

                for (int i = 0; i < sample_rate * 0.03 && start + i < samples.Length; i++)
                    samples[start + i] += volume * (float)(random.NextDouble() * 2 - 1) * (float)Math.Exp(-i / (sample_rate * 0.008));
            }

            return samples;
        }
    }
}
