// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneTimelineUnsnapIndicators : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = new TestBeatmap(ruleset, false);
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            return beatmap;
        }

        [Test]
        public void TestUnsnappedObjectShowsIcon()
        {
            HitCircle unsnapped = null!;

            AddStep("add objects", () =>
            {
                EditorBeatmap.Add(new HitCircle { StartTime = 1000 });
                EditorBeatmap.Add(unsnapped = new HitCircle { StartTime = 1507 });
            });
            AddStep("seek to objects", () => EditorClock.Seek(1000));

            AddUntilStep("one icon shown", () => icons().Count(), () => Is.EqualTo(1));

            AddStep("snap object", () =>
            {
                unsnapped.StartTime = 1500;
                EditorBeatmap.Update(unsnapped);
            });
            AddUntilStep("no icon shown", () => icons().Count(), () => Is.EqualTo(0));
        }

        private IEnumerable<SpriteIcon> icons() => this.ChildrenOfType<TimelineUnsnapIndicators>().SelectMany(i => i.ChildrenOfType<SpriteIcon>());
    }
}
