// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.UI;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneEditorNewComboHotkey : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = new TestBeatmap(ruleset, false);
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            return beatmap;
        }

        [Test]
        public void TestNewComboHotkeyAppliesToNextPlacement()
        {
            Playfield playfield = null!;

            AddStep("select circle placement tool", () => InputManager.Key(Key.Number2));
            AddStep("grab playfield", () => playfield = this.ChildrenOfType<Playfield>().Single());

            AddStep("move mouse", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(new Vector2(100, 100))));
            AddStep("place circle", () => InputManager.Click(MouseButton.Left));
            AddAssert("one object", () => EditorBeatmap.HitObjects, () => Has.Count.EqualTo(1));

            AddStep("seek forward", () => EditorClock.Seek(500));
            AddStep("move mouse", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(new Vector2(300, 200))));
            AddStep("press Q", () => InputManager.Key(Key.Q));
            AddStep("place circle", () => InputManager.Click(MouseButton.Left));

            AddAssert("two objects", () => EditorBeatmap.HitObjects, () => Has.Count.EqualTo(2));
            AddAssert("second object has new combo", () => ((IHasComboInformation)EditorBeatmap.HitObjects[1]).NewCombo);

            AddStep("seek forward", () => EditorClock.Seek(1000));
            AddStep("move mouse", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(new Vector2(200, 300))));
            AddStep("press W", () => InputManager.Key(Key.W));
            AddStep("place circle", () => InputManager.Click(MouseButton.Left));

            AddAssert("three objects", () => EditorBeatmap.HitObjects, () => Has.Count.EqualTo(3));
            AddAssert("third object has whistle", () => EditorBeatmap.HitObjects[2].Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE));
        }
    }
}
