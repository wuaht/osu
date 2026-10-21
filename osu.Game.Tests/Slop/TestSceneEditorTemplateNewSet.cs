// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    /// <summary>
    /// Creating a new beatmap set which uses a saved difficulty as a template.
    /// </summary>
    [Category("slop")]
    public partial class TestSceneEditorTemplateNewSet : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override bool IsolateSavingFromDatabase => false;

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null) => new DummyWorkingBeatmap(Audio, null);

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private string title = null!;

        private Guid templateSetId;

        private BeatmapSetInfo templateSet => realm.Run(r => r.Find<BeatmapSetInfo>(templateSetId)!.Detach());

        private WorkingBeatmap template => beatmapManager.GetWorkingBeatmap(templateSet.Beatmaps.Single(), true);

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("make beatmap unique", () =>
            {
                EditorBeatmap.Metadata.Title = title = Guid.NewGuid().ToString();
                EditorBeatmap.BeatmapInfo.DifficultyName = "Template";
            });
        }

        private void save()
        {
            AddStep("save", () => Editor.Save());
            AddStep("store template", () => templateSetId = realm.Run(r => r.All<BeatmapSetInfo>().AsEnumerable().Single(s => !s.DeletePending && s.Metadata.Title == title).ID));
        }

        [Test]
        public void TestTemplateKeepsSettingsButNotContent()
        {
            WorkingBeatmap? newSet = null;

            AddStep("add timing points", () =>
            {
                EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                EditorBeatmap.ControlPointInfo.Add(1000, new EffectControlPoint { KiaiMode = true });
                EditorBeatmap.ControlPointInfo.Add(2000, new TimingControlPoint { BeatLength = 300 });
            });
            AddStep("add hit objects", () =>
            {
                EditorBeatmap.Add(new HitCircle { StartTime = 0 });
                EditorBeatmap.Add(new HitCircle { StartTime = 10000 });
            });
            AddStep("add break", () => EditorBeatmap.Breaks.Add(new BreakPeriod(2000, 8000)));
            AddStep("add bookmark", () => EditorBeatmap.Bookmarks.Add(1500));
            AddStep("set difficulty", () =>
            {
                EditorBeatmap.Difficulty.ApproachRate = 9;
                EditorBeatmap.Difficulty.SliderTickRate = 2;
                EditorBeatmap.BeatmapInfo.Metadata.Artist = "Artist";
                EditorBeatmap.BeatmapInfo.Metadata.BackgroundFile = "bg.jpg";
            });
            save();

            AddStep("create new set", () => newSet = beatmapManager.CreateNewSetFromTemplate(template));

            AddAssert("new set is separate", () => newSet!.BeatmapSetInfo.ID != templateSet.ID && newSet.BeatmapInfo.ID != templateSet.Beatmaps.Single().ID);
            AddAssert("new set has one difficulty", () => newSet!.BeatmapSetInfo.Beatmaps.Count, () => Is.EqualTo(1));
            AddAssert("metadata kept", () => newSet!.Metadata.Title == title && newSet.Metadata.Artist == "Artist");
            AddAssert("background removed", () => newSet!.Metadata.BackgroundFile, () => Is.Empty);
            AddAssert("difficulty kept", () => newSet!.BeatmapInfo.Difficulty.ApproachRate == 9 && newSet.BeatmapInfo.Difficulty.SliderTickRate == 2);
            AddAssert("difficulty is a copy", () => !ReferenceEquals(newSet!.BeatmapInfo.Difficulty, template.BeatmapInfo.Difficulty));
            AddAssert("no hit objects", () => newSet!.Beatmap.HitObjects, () => Is.Empty);
            AddAssert("no breaks", () => newSet!.Beatmap.Breaks, () => Is.Empty);
            AddAssert("bookmarks kept", () => newSet!.Beatmap.Bookmarks, () => Is.EqualTo(new[] { 1500 }));
            AddAssert("uninherited timing points kept", () => newSet!.Beatmap.ControlPointInfo.TimingPoints.Select(t => (t.Time, t.BeatLength)),
                () => Is.EqualTo(new[] { (0d, 500d), (2000d, 300d) }));
            AddAssert("inherited timing points removed", () => newSet!.Beatmap.ControlPointInfo.EffectPoints, () => Is.Empty);
            AddAssert("not online", () => newSet!.BeatmapInfo.OnlineID, () => Is.EqualTo(-1));
            AddAssert("template hit objects unchanged", () => template.Beatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("template breaks unchanged", () => template.Beatmap.Breaks, () => Is.Not.Empty);
            AddAssert("template background unchanged", () => template.Metadata.BackgroundFile, () => Is.EqualTo("bg.jpg"));
        }

        [Test]
        public void TestEmptyTemplate()
        {
            WorkingBeatmap? newSet = null;

            save();
            AddStep("create new set", () => newSet = beatmapManager.CreateNewSetFromTemplate(template));
            AddAssert("new set created", () => newSet!.BeatmapSetInfo.ID != templateSet.ID);
            AddAssert("no timing points", () => newSet!.Beatmap.ControlPointInfo.TimingPoints, () => Is.Empty);
        }

        [Test]
        public void TestTemplateCanBeUsedTwice()
        {
            WorkingBeatmap? first = null;
            WorkingBeatmap? second = null;

            save();
            AddStep("create new sets", () =>
            {
                first = beatmapManager.CreateNewSetFromTemplate(template);
                second = beatmapManager.CreateNewSetFromTemplate(template);
            });
            AddAssert("sets are separate", () => first!.BeatmapSetInfo.ID != second!.BeatmapSetInfo.ID && first.BeatmapInfo.ID != second.BeatmapInfo.ID);
        }
    }
}
