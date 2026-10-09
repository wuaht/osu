// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Fposu.UI;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Fposu.Tests
{
    [Category("slop")]
    public partial class TestSceneFposuAutoplay : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new FposuRuleset();

        protected override bool Autoplay => true;

        private DrawableFposuRuleset drawableRuleset => (DrawableFposuRuleset)Player.DrawableRuleset;

        [Test]
        public void TestAutoplay()
        {
            AddUntilStep("objects hit", () => Player.ScoreProcessor.TotalScore.Value, () => Is.GreaterThan(0));
            AddAssert("no misses", () => Player.ScoreProcessor.Accuracy.Value, () => Is.EqualTo(1));

            AddUntilStep("camera turned", () => drawableRuleset.Camera.Yaw != 0 || drawableRuleset.Camera.Pitch != 0);

            // like McOsu, the camera follows the cursor of the replay.
            AddAssert("camera looks at cursor", () =>
            {
                var screen = drawableRuleset.Screen;

                Vector2 cursor = screen.ToUVFromScreenSpace(drawableRuleset.FposuInputManager.CurrentState.Mouse.Position);
                Vector2 lookedAt = screen.Mesh.Intersect(drawableRuleset.Camera.Forward) ?? new Vector2(-1);

                return (cursor - lookedAt).Length < 0.002f;
            });
        }
    }
}
