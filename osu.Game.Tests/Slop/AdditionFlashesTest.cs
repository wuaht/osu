// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class AdditionFlashesTest
    {
        [Test]
        public void TestEmpty()
            => Assert.That(getNames(Array.Empty<HitObject>(), 0, 1000), Is.Empty);

        [Test]
        public void TestRangeIsHalfOpen()
        {
            var circle = circleAt(1000, HitSampleInfo.HIT_WHISTLE);

            Assert.That(getNames(new[] { circle }, 1000, 1500), Is.Empty, "a node at the start of the range was already passed");
            Assert.That(getNames(new[] { circle }, 500, 1000), Is.EquivalentTo(new[] { HitSampleInfo.HIT_WHISTLE }), "a node at the end of the range was just passed");
            Assert.That(getNames(new[] { circle }, 999, 1001), Is.EquivalentTo(new[] { HitSampleInfo.HIT_WHISTLE }));
            Assert.That(getNames(new[] { circle }, 1001, 1500), Is.Empty);
        }

        [Test]
        public void TestReversedOrEmptyRange()
        {
            var circle = circleAt(1000, HitSampleInfo.HIT_CLAP);

            Assert.That(getNames(new[] { circle }, 1500, 500), Is.Empty);
            Assert.That(getNames(new[] { circle }, 1000, 1000), Is.Empty);
        }

        [Test]
        public void TestOnlyAdditionsAreReturned()
        {
            var circle = new TestHitObject
            {
                StartTime = 1000,
                Samples =
                {
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL),
                    new HitSampleInfo(HitSampleInfo.HIT_FINISH),
                    new HitSampleInfo(HitSampleInfo.HIT_FLOURISH),
                }
            };

            Assert.That(getNames(new[] { circle }, 0, 2000), Is.EquivalentTo(new[] { HitSampleInfo.HIT_FINISH }));
        }

        [Test]
        public void TestNamesOfMultipleObjectsAreMerged()
        {
            var objects = new[]
            {
                circleAt(100, HitSampleInfo.HIT_WHISTLE),
                circleAt(200, HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_CLAP),
                circleAt(300, HitSampleInfo.HIT_FINISH),
            };

            Assert.That(getNames(objects, 0, 250), Is.EquivalentTo(new[] { HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_CLAP }));
            Assert.That(getNames(objects, 0, 300), Is.EquivalentTo(HitSampleInfo.ALL_ADDITIONS));
        }

        [Test]
        public void TestRepeatNodes()
        {
            // nodes at 1000 (whistle), 1500 (clap), 2000 (finish).
            var slider = new TestRepeatObject
            {
                StartTime = 1000,
                Duration = 1000,
                RepeatCount = 1,
            };

            slider.NodeSamples.Add(samples(HitSampleInfo.HIT_WHISTLE));
            slider.NodeSamples.Add(samples(HitSampleInfo.HIT_CLAP));
            slider.NodeSamples.Add(samples(HitSampleInfo.HIT_FINISH));

            Assert.That(getNames(new[] { slider }, 1200, 1600), Is.EquivalentTo(new[] { HitSampleInfo.HIT_CLAP }), "only the repeat is passed");
            Assert.That(getNames(new[] { slider }, 1600, 2000), Is.EquivalentTo(new[] { HitSampleInfo.HIT_FINISH }), "the end of the slider is passed");
            Assert.That(getNames(new[] { slider }, 1000, 1499), Is.Empty, "the body of the slider doesn't have any nodes");
            Assert.That(getNames(new[] { slider }, 2000, 2500), Is.Empty, "the slider was already passed");
        }

        [Test]
        public void TestMissingNodeSamplesFallBackToSamples()
        {
            var slider = new TestRepeatObject
            {
                StartTime = 1000,
                Duration = 1000,
                Samples = { new HitSampleInfo(HitSampleInfo.HIT_FINISH) },
            };

            slider.NodeSamples.Add(samples(HitSampleInfo.HIT_WHISTLE));

            Assert.That(getNames(new[] { slider }, 500, 1000), Is.EquivalentTo(new[] { HitSampleInfo.HIT_WHISTLE }));
            Assert.That(getNames(new[] { slider }, 1500, 2000), Is.EquivalentTo(new[] { HitSampleInfo.HIT_FINISH }));
        }

        [Test]
        public void TestZeroLengthRepeatObject()
        {
            var slider = new TestRepeatObject
            {
                StartTime = 1000,
                Duration = 0,
                RepeatCount = 2,
                Samples = { new HitSampleInfo(HitSampleInfo.HIT_CLAP) },
            };

            Assert.That(getNames(new[] { slider }, 500, 1000), Is.EquivalentTo(new[] { HitSampleInfo.HIT_CLAP }));
            Assert.That(getNames(new[] { slider }, 1000, 1500), Is.Empty);
        }

        [Test]
        public void TestDurationObjectOnlyFlashesAtItsEnd()
        {
            var spinner = new TestDurationObject
            {
                StartTime = 1000,
                Duration = 1000,
                Samples = { new HitSampleInfo(HitSampleInfo.HIT_WHISTLE) },
            };

            Assert.That(getNames(new[] { spinner }, 500, 1500), Is.Empty, "spinners don't play anything at their start");
            Assert.That(getNames(new[] { spinner }, 1500, 2000), Is.EquivalentTo(new[] { HitSampleInfo.HIT_WHISTLE }));
        }

        private static HashSet<string> getNames(IEnumerable<HitObject> hitObjects, double from, double to)
            => HitObjectComposer<HitObject, TestAction>.GetPassedSampleNames(hitObjects, from, to);

        private static TestHitObject circleAt(double time, params string[] names)
        {
            var circle = new TestHitObject { StartTime = time };
            circle.SamplesBindable.AddRange(samples(names));
            return circle;
        }

        private static List<HitSampleInfo> samples(params string[] names) => names.Select(n => new HitSampleInfo(n)).ToList();

        private enum TestAction
        {
        }

        private class TestHitObject : HitObject
        {
        }

        private class TestDurationObject : HitObject, IHasDuration
        {
            public double EndTime => StartTime + Duration;

            public double Duration { get; set; }
        }

        private class TestRepeatObject : HitObject, IHasRepeats
        {
            public double EndTime => StartTime + Duration;

            public double Duration { get; set; }

            public int RepeatCount { get; set; }

            public IList<IList<HitSampleInfo>> NodeSamples { get; } = new List<IList<HitSampleInfo>>();
        }
    }
}
