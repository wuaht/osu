// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.Edit.Timing;
using osuTK;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class EditorTransformTest
    {
        [Test]
        public void TestEqualize()
        {
            var beatmap = createBeatmap((0, 500), (1000, 250), (3000, 1000));

            var before = addSlider(beatmap, 500, 1.2);
            var first = addSlider(beatmap, 1000, 1);
            var second = addSlider(beatmap, 1500, 3);
            var after = addSlider(beatmap, 3000, 1);
            beatmap.Add(new HitCircle { StartTime = 2000 });

            var point = beatmap.ControlPointInfo.TimingPoints[1];

            Assert.That(SliderVelocityEqualizer.CanEqualize(beatmap, point), Is.True);
            Assert.That(SliderVelocityEqualizer.Equalize(beatmap, point), Is.EqualTo(2));

            // half the beat length, so half the slider velocity moves at the same speed.
            Assert.That(first.SliderVelocityMultiplier, Is.EqualTo(0.6).Within(0.0001));
            Assert.That(second.SliderVelocityMultiplier, Is.EqualTo(0.6).Within(0.0001));
            Assert.That(before.SliderVelocityMultiplier, Is.EqualTo(1.2));
            Assert.That(after.SliderVelocityMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void TestEqualizeKeepsSliderEndsSnapped()
        {
            var beatmap = createBeatmap((0, 500), (1000, 250));

            addSlider(beatmap, 500, 1.2);
            var slider = addSlider(beatmap, 1000, 1);
            slider.RepeatCount = 1;
            beatmap.Update(slider);

            SliderVelocityEqualizer.Equalize(beatmap, beatmap.ControlPointInfo.TimingPoints[1]);

            double tick = beatmap.GetBeatLengthAtTime(slider.StartTime);
            double spanDuration = slider.Duration / slider.SpanCount();

            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(0.6).Within(0.0001));
            Assert.That(spanDuration / tick, Is.EqualTo(Math.Round(spanDuration / tick)).Within(0.001));
            Assert.That(spanDuration, Is.GreaterThan(0));
        }

        [Test]
        public void TestEqualizeWithoutObjectsBefore()
        {
            var beatmap = createBeatmap((0, 500), (1000, 1000));
            var slider = addSlider(beatmap, 1000, 1.5);

            Assert.That(SliderVelocityEqualizer.Equalize(beatmap, beatmap.ControlPointInfo.TimingPoints[1]), Is.EqualTo(1));
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(2));
        }

        [Test]
        public void TestCannotEqualizeFirstTimingPoint()
        {
            var beatmap = createBeatmap((0, 500), (1000, 250));
            var slider = addSlider(beatmap, 0, 1.5);
            var point = beatmap.ControlPointInfo.TimingPoints[0];

            Assert.That(SliderVelocityEqualizer.CanEqualize(beatmap, point), Is.False);
            Assert.That(SliderVelocityEqualizer.Equalize(beatmap, point), Is.EqualTo(0));
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(1.5));
        }

        [Test]
        public void TestEqualizeEmptySection()
        {
            var beatmap = createBeatmap((0, 500), (1000, 250), (2000, 500));
            addSlider(beatmap, 2000, 1);

            Assert.That(SliderVelocityEqualizer.Equalize(beatmap, beatmap.ControlPointInfo.TimingPoints[1]), Is.EqualTo(0));
        }

        [TestCase(2, 0, 250)]
        [TestCase(1, 60, 60000 / 180.0)]
        [TestCase(0, 0, 500)]
        [TestCase(1, -500, 500)]
        public void TestTransformBeatLength(double multiplier, double additive, double expected)
            => Assert.That(PropertyTransformer.TransformBeatLength(500, new PropertyTransform(multiplier, additive)), Is.EqualTo(expected).Within(0.0001));

        [TestCase(1.5, 2, 0, 3)]
        [TestCase(1, 100, 0, 10)]
        [TestCase(1, 0, 0, 0.1)]
        [TestCase(1, 1, -5, 0.1)]
        public void TestTransformSliderVelocity(double velocity, double multiplier, double additive, double expected)
            => Assert.That(PropertyTransformer.TransformSliderVelocity(velocity, new PropertyTransform(multiplier, additive)), Is.EqualTo(expected).Within(0.0001));

        [TestCase(50, 1, 10, 60)]
        [TestCase(60, 2, 0, 100)]
        [TestCase(50, 1, -200, 5)]
        [TestCase(50, 1, 0.4, 50)]
        [TestCase(50, 1, 0.6, 51)]
        public void TestTransformVolume(int volume, double multiplier, double additive, int expected)
            => Assert.That(PropertyTransformer.TransformVolume(volume, new PropertyTransform(multiplier, additive)), Is.EqualTo(expected));

        [Test]
        public void TestApply()
        {
            var beatmap = createBeatmap((0, 500), (1000, 500), (5000, 500));

            var slider = addSlider(beatmap, 1000, 1);
            var circle = new HitCircle { StartTime = 2000, Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 40) } };
            beatmap.Add(circle);

            PropertyTransformer.Apply(beatmap, new HitObject[] { slider, circle }, new PropertyTransform(2, 0), new PropertyTransform(1, 0.5), new PropertyTransform(1, 10));

            // only the timing point within the selection is changed.
            Assert.That(beatmap.ControlPointInfo.TimingPoints.Select(p => p.BeatLength), Is.EqualTo(new[] { 500, 250, 500 }));

            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(1.5));
            Assert.That(PropertyTransformer.GetSampleLists(slider).SelectMany(s => s).Select(s => s.Volume), Is.All.EqualTo(60));
            Assert.That(circle.Samples.Single().Volume, Is.EqualTo(50));
        }

        [Test]
        public void TestApplyIdentityLeavesObjectsUnchanged()
        {
            var beatmap = createBeatmap((0, 500));
            var slider = addSlider(beatmap, 1000, 1.3);

            PropertyTransformer.Apply(beatmap, new HitObject[] { slider }, PropertyTransform.IDENTITY, PropertyTransform.IDENTITY, PropertyTransform.IDENTITY);

            Assert.That(beatmap.ControlPointInfo.TimingPoints.Single().BeatLength, Is.EqualTo(500));
            Assert.That(slider.SliderVelocityMultiplier, Is.EqualTo(1.3));
            Assert.That(PropertyTransformer.GetSampleLists(slider).SelectMany(s => s).Select(s => s.Volume), Is.All.EqualTo(50));
        }

        [Test]
        public void TestNoAffectedTimingPointsForEmptySelection()
        {
            var beatmap = createBeatmap((0, 500));

            Assert.That(PropertyTransformer.GetAffectedTimingPoints(beatmap, new List<HitObject>()), Is.Empty);
        }

        private static EditorBeatmap createBeatmap(params (double time, double beatLength)[] timingPoints)
        {
            var osuBeatmap = new OsuBeatmap
            {
                BeatmapInfo =
                {
                    Ruleset = new OsuRuleset().RulesetInfo,
                },
            };

            foreach (var (time, beatLength) in timingPoints)
                osuBeatmap.ControlPointInfo.Add(time, new TimingControlPoint { BeatLength = beatLength });

            return new EditorBeatmap(osuBeatmap);
        }

        private static Slider addSlider(EditorBeatmap beatmap, double time, double velocity)
        {
            var slider = new Slider
            {
                StartTime = time,
                SliderVelocityMultiplier = velocity,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }),
                Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 50) },
                NodeSamples =
                {
                    new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 50) },
                    new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, volume: 50) },
                },
            };

            beatmap.Add(slider);
            return slider;
        }
    }
}
