// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Hitsounding;
using osuTK;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class HitsoundEditorModelTest
    {
        [TestCase("Hitsounds", true)]
        [TestCase("Someone's Hitsound Diff", true)]
        [TestCase("HS", true)]
        [TestCase("Insane (HS)", true)]
        [TestCase("Hard", false)]
        [TestCase("Hush", false)]
        [TestCase("Chaos", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void TestHitsoundDifficultyName(string? name, bool expected)
            => Assert.That(HitsoundEditor.IsHitsoundDifficultyName(name), Is.EqualTo(expected));

        [Test]
        public void TestColumnStateGroupsAdditionsByBank()
        {
            var state = HitsoundColumnState.Create(1000, new IList<HitSampleInfo>[]
            {
                new List<HitSampleInfo>
                {
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT, volume: 60),
                    new HitSampleInfo(HitSampleInfo.HIT_CLAP, HitSampleInfo.BANK_DRUM, volume: 60),
                },
                new List<HitSampleInfo>
                {
                    new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_DRUM, volume: 80),
                    new HitSampleInfo(HitSampleInfo.HIT_WHISTLE, HitSampleInfo.BANK_NORMAL, volume: 80),
                    new HitSampleInfo(HitSampleInfo.HIT_FINISH, HitSampleInfo.BANK_NORMAL, volume: 80),
                },
            });

            Assert.That(state.NormalBanks, Is.EqualTo(new[] { HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_DRUM }));
            Assert.That(state.AdditionGroups.Select(g => g.Bank), Is.EqualTo(new[] { HitSampleInfo.BANK_NORMAL, HitSampleInfo.BANK_DRUM }));
            Assert.That(state.AdditionGroups[0].Names, Is.EquivalentTo(new[] { HitSampleInfo.HIT_WHISTLE, HitSampleInfo.HIT_FINISH }));
            Assert.That(state.Volume, Is.EqualTo(80));
            Assert.That(state.RequiredTargetCount, Is.EqualTo(2));
        }

        [Test]
        public void TestCopyMatchesWithinLeniency()
        {
            var target = createObject(1003, HitSampleInfo.BANK_NORMAL);
            var source = new[] { state(1000, HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_DRUM, 70, HitSampleInfo.HIT_CLAP) };

            var result = HitsoundCopier.Copy(source, [], HitsoundMap.Create(new[] { target }, 0), new HitsoundCopyOptions());

            Assert.That(result.ChangedHitsounds, Is.EqualTo(1));
            Assert.That(HitsoundSamples.GetNormalBank(target.Samples), Is.EqualTo(HitSampleInfo.BANK_SOFT));
            Assert.That(target.Samples.Single(s => s.Name == HitSampleInfo.HIT_CLAP).Bank, Is.EqualTo(HitSampleInfo.BANK_DRUM));
            Assert.That(target.Samples.All(s => s.Volume == 70));
        }

        [Test]
        public void TestCopyOutsideLeniencyIsUnmatched()
        {
            var target = createObject(1010, HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_WHISTLE);
            var source = new[] { state(1000, HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_SOFT, 70, HitSampleInfo.HIT_CLAP) };

            HitsoundCopier.Copy(source, [], HitsoundMap.Create(new[] { target }, 0), new HitsoundCopyOptions { OverwriteUnmatched = false });

            Assert.That(HitsoundSamples.GetNormalBank(target.Samples), Is.EqualTo(HitSampleInfo.BANK_NORMAL));
            Assert.That(target.Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestOverwriteUnmatchedRemovesAdditions(bool overwrite)
        {
            var target = createObject(2000, HitSampleInfo.BANK_NORMAL, HitSampleInfo.HIT_WHISTLE);

            HitsoundCopier.Copy([], [], HitsoundMap.Create(new[] { target }, 0), new HitsoundCopyOptions { OverwriteUnmatched = overwrite });

            Assert.That(target.Samples.Any(HitsoundSamples.IsAddition), Is.EqualTo(!overwrite));
            Assert.That(target.Samples.Any(HitsoundSamples.IsNormal));
        }

        [TestCase(true, 5)]
        [TestCase(false, 80)]
        public void TestPreserveMutedVolumes(bool preserve, int expectedVolume)
        {
            var target = createObject(1000, HitSampleInfo.BANK_NORMAL, volume: 5);
            var source = new[] { state(1000, HitSampleInfo.BANK_NORMAL, HitSampleInfo.BANK_NORMAL, 80) };

            HitsoundCopier.Copy(source, [], HitsoundMap.Create(new[] { target }, 0), new HitsoundCopyOptions { PreserveMutedVolumes = preserve });

            Assert.That(target.Samples.All(s => s.Volume == expectedVolume));
        }

        [Test]
        public void TestCopyReportsDroppedHitsounds()
        {
            var target = createObject(1000, HitSampleInfo.BANK_NORMAL);
            var source = new[]
            {
                new HitsoundColumnState(1000,
                    new[] { HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_DRUM },
                    new[]
                    {
                        new HitsoundAdditionGroup(HitSampleInfo.BANK_NORMAL, new[] { HitSampleInfo.HIT_WHISTLE }),
                        new HitsoundAdditionGroup(HitSampleInfo.BANK_DRUM, new[] { HitSampleInfo.HIT_CLAP, HitSampleInfo.HIT_FINISH }),
                    }, 100, 0)
            };

            var result = HitsoundCopier.Copy(source, [], HitsoundMap.Create(new[] { target }, 0), new HitsoundCopyOptions());

            // one hitnormal bank and the second addition group (two additions) don't fit onto a single object.
            Assert.That(result.DroppedHitsounds, Is.EqualTo(3));
        }

        [Test]
        public void TestCopyToStackedObjects()
        {
            var first = createObject(1000, HitSampleInfo.BANK_NORMAL);
            var second = createObject(1000, HitSampleInfo.BANK_NORMAL);
            var source = new[]
            {
                new HitsoundColumnState(1000,
                    new[] { HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_DRUM },
                    new[] { new HitsoundAdditionGroup(HitSampleInfo.BANK_DRUM, new[] { HitSampleInfo.HIT_CLAP }) }, 100, 0)
            };

            var result = HitsoundCopier.Copy(source, [], HitsoundMap.Create(new[] { first, second }, 0), new HitsoundCopyOptions());

            Assert.That(result.DroppedHitsounds, Is.Zero);
            Assert.That(HitsoundSamples.GetNormalBank(first.Samples), Is.EqualTo(HitSampleInfo.BANK_SOFT));
            Assert.That(HitsoundSamples.GetNormalBank(second.Samples), Is.EqualTo(HitSampleInfo.BANK_DRUM));
            Assert.That(first.Samples.Any(s => s.Name == HitSampleInfo.HIT_CLAP));
            Assert.That(second.Samples.Any(HitsoundSamples.IsAddition), Is.False);
        }

        [Test]
        public void TestMuteUnmatchedSliderEnds()
        {
            var slider = createSlider(1000);
            var map = HitsoundMap.Create(new[] { slider }, 0);
            var head = map.Columns[0];

            var source = new[] { state(head.Time, HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_SOFT, 100, HitSampleInfo.HIT_WHISTLE) };

            HitsoundCopier.Copy(source, [], map, new HitsoundCopyOptions { MuteUnmatchedSliderEnds = true });

            Assert.That(slider.NodeSamples[0].Any(s => s.Name == HitSampleInfo.HIT_WHISTLE));
            Assert.That(slider.NodeSamples[^1].All(s => s.Volume == 5));
        }

        [Test]
        public void TestMapCreatesSliderTargets()
        {
            var slider = createSlider(1000, repeats: 1);
            var map = HitsoundMap.Create(new HitObject[] { slider, createObject(5000, HitSampleInfo.BANK_NORMAL) }, 0);

            Assert.That(map.Columns.Select(c => c.Targets.Single().Kind), Is.EqualTo(new[]
            {
                HitsoundTargetKind.Head, HitsoundTargetKind.Repeat, HitsoundTargetKind.Tail, HitsoundTargetKind.Object
            }));
            Assert.That(map.Bodies, Has.Count.EqualTo(1));
        }

        [Test]
        public void TestMergeOnlyFillsGaps()
        {
            var primary = new[] { state(1000, HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_SOFT, 100) };
            var other = new[]
            {
                state(1004, HitSampleInfo.BANK_DRUM, HitSampleInfo.BANK_DRUM, 100),
                state(500, HitSampleInfo.BANK_DRUM, HitSampleInfo.BANK_DRUM, 100),
                state(1500, HitSampleInfo.BANK_DRUM, HitSampleInfo.BANK_DRUM, 100),
            }.OrderBy(s => s.Time).ToArray();

            var merged = HitsoundDifficultyBuilder.Merge(primary, new[] { other });

            Assert.That(merged.Select(s => s.Time), Is.EqualTo(new double[] { 500, 1000, 1500 }));
            Assert.That(merged[1].NormalBanks.Single(), Is.EqualTo(HitSampleInfo.BANK_SOFT));
        }

        [Test]
        public void TestMergePrefersEarlierDifficulties()
        {
            var first = new[] { state(2000, HitSampleInfo.BANK_SOFT, HitSampleInfo.BANK_SOFT, 100) };
            var second = new[] { state(2002, HitSampleInfo.BANK_DRUM, HitSampleInfo.BANK_DRUM, 100) };

            var merged = HitsoundDifficultyBuilder.Merge([], new[] { first, second });

            Assert.That(merged.Single().NormalBanks.Single(), Is.EqualTo(HitSampleInfo.BANK_SOFT));
        }

        private static HitsoundColumnState state(double time, string normalBank, string additionBank, int volume, params string[] additions)
            => new HitsoundColumnState(time, new[] { normalBank },
                additions.Length > 0 ? new[] { new HitsoundAdditionGroup(additionBank, additions) } : [], volume, 0);

        private static HitObject createObject(double time, string bank, string? addition = null, int volume = 100)
        {
            var samples = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, bank, volume: volume) };

            if (addition != null)
                samples.Add(new HitSampleInfo(addition, bank, volume: volume));

            return new HitObject { StartTime = time, Samples = samples };
        }

        private static Slider createSlider(double time, int repeats = 0)
        {
            var normal = new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_NORMAL) };

            var slider = new Slider
            {
                StartTime = time,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }),
                RepeatCount = repeats,
                Samples = normal,
                NodeSamples = Enumerable.Range(0, repeats + 2).Select(_ => (IList<HitSampleInfo>)normal.ToList()).ToList(),
            };

            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return slider;
        }
    }
}
