// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.PatternGallery;
using osuTK;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class PatternGalleryTest
    {
        [Test]
        public void TestBeatsAcrossTimingPoints()
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 500 });
            controlPoints.Add(1000, new TimingControlPoint { BeatLength = 250 });

            Assert.That(PatternPlacement.BeatsBetween(controlPoints, 500, 1500), Is.EqualTo(3).Within(1e-9));
            Assert.That(PatternPlacement.TimeAfterBeats(controlPoints, 500, 3), Is.EqualTo(1500).Within(1e-9));
            Assert.That(PatternPlacement.BeatsBetween(controlPoints, 1500, 500), Is.EqualTo(-3).Within(1e-9));
        }

        [Test]
        public void TestSaveAndInsertScaledToTiming()
        {
            using var storage = new TemporaryNativeStorage(nameof(PatternGalleryTest));

            var source = createBeatmap(500);
            source.HitObjects.Add(new HitCircle { StartTime = 1000, Position = new Vector2(100) });
            source.HitObjects.Add(new HitCircle { StartTime = 2000, Position = new Vector2(200) });

            foreach (var h in source.HitObjects)
                h.ApplyDefaults(source.ControlPointInfo, source.Difficulty);

            var store = new PatternStore(storage);
            var pattern = store.Add("jump", source, source.HitObjects);

            Assert.That(pattern.CircleCount, Is.EqualTo(2));
            Assert.That(pattern.DurationBeats, Is.EqualTo(2).Within(1e-9));

            // a new store reads the index written by the first one.
            var reopened = new PatternStore(storage);
            Assert.That(reopened.Patterns.Single().Name, Is.EqualTo("jump"));

            var target = createBeatmap(250);
            var objects = PatternPlacement.Place(reopened.Open(reopened.Patterns.Single())!, target, target.BeatmapInfo.Ruleset, 10000, true, null, true);

            Assert.That(objects.Select(h => h.StartTime), Is.EqualTo(new[] { 10000, 10500 }).Within(1e-6));
            Assert.That(objects.OfType<HitCircle>().Select(h => h.Position), Is.EqualTo(new[] { new Vector2(100), new Vector2(200) }));
        }

        [Test]
        public void TestManiaColumnsSpreadOverTargetKeyCount()
        {
            using var storage = new TemporaryNativeStorage(nameof(PatternGalleryTest));

            var source = new Beatmap { BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo }, Difficulty = { CircleSize = 4 } };
            source.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            for (int column = 0; column < 4; column++)
                source.HitObjects.Add(new Note { StartTime = 1000 + column * 250, Column = column });

            var store = new PatternStore(storage);
            var pattern = store.Add("stair", source, source.HitObjects);

            var target = new Beatmap { BeatmapInfo = { Ruleset = new ManiaRuleset().RulesetInfo }, Difficulty = { CircleSize = 7 } };
            target.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            var columns = PatternPlacement.Place(store.Open(pattern)!, target, target.BeatmapInfo.Ruleset, 0, true, null, true)
                                          .Cast<ManiaHitObject>().OrderBy(h => h.StartTime).Select(h => h.Column).ToList();

            Assert.That(columns, Has.Count.EqualTo(4));
            Assert.That(columns, Is.Ordered.Ascending);
            Assert.That(columns, Is.Unique);
            Assert.That(columns, Has.All.InRange(0, 6));
            Assert.That(columns.Last(), Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void TestDeleteCanBeUndoneAndTimingIsTrimmed()
        {
            using var storage = new TemporaryNativeStorage(nameof(PatternGalleryTest));

            var source = createBeatmap(500);
            source.ControlPointInfo.Add(5000, new TimingControlPoint { BeatLength = 300 });
            source.ControlPointInfo.Add(20000, new TimingControlPoint { BeatLength = 400 });
            source.HitObjects.Add(new HitCircle { StartTime = 1000 });
            source.HitObjects.Add(new HitCircle { StartTime = 6000 });

            var store = new PatternStore(storage);
            var pattern = store.Add("trim", source, source.HitObjects);

            Assert.That(store.Open(pattern)!.ControlPointInfo.TimingPoints.Select(t => t.BeatLength), Is.EqualTo(new[] { 500, 300 }));

            store.Delete(pattern);
            Assert.That(store.Patterns, Is.Empty);

            store.Restore(pattern);
            Assert.That(store.Patterns.Single(), Is.EqualTo(pattern));
            Assert.That(store.Open(pattern), Is.Not.Null);

            // a deleted pattern's file goes away on the next start.
            store.Delete(pattern);
            new PatternStore(storage);
            Assert.That(storage.GetStorageForDirectory(PatternStore.DIRECTORY).GetFiles(string.Empty, "*.osu"), Is.Empty);
        }

        private static Beatmap createBeatmap(double beatLength)
        {
            var beatmap = new Beatmap { BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo } };
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = beatLength });
            return beatmap;
        }
    }
}
