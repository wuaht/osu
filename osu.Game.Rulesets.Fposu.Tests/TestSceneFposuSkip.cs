// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Fposu.UI;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Fposu.Tests
{
    [Category("slop")]
    public partial class TestSceneFposuSkip : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new FposuRuleset();

        // the first object late enough to be skipped to.
        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new Beatmap
        {
            BeatmapInfo = { Ruleset = ruleset },
            HitObjects =
            {
                new HitCircle { StartTime = 10000, Position = new Vector2(256, 192) },
            },
        };

        private DrawableFposuRuleset drawableRuleset => (DrawableFposuRuleset)Player.DrawableRuleset;

        private FposuScreen screen => drawableRuleset.Screen;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("disable mouse buttons", () => LocalConfig.SetValue(OsuSetting.MouseDisableButtons, true));

            AddUntilStep("screen loaded", () => Player.DrawableRuleset is DrawableFposuRuleset && screen.IsLoaded);
            AddStep("camera controls cursor", () => drawableRuleset.FposuInputManager.MouseInput.AvailabilityOverride = true);
        }

        [Test]
        public void TestSkipWithMouseButtonsDisabled()
        {
            double timeBefore = 0;

            AddUntilStep("skip button visible", () => skipButton()?.IsPresent == true);
            // the mouse itself is elsewhere (but within the game), as the camera controls the cursor.
            AddStep("move mouse away", () => InputManager.MoveMouseTo(drawableRuleset.ScreenSpaceDrawQuad.Centre));
            AddStep("look at skip button", () => drawableRuleset.Camera.LookAt(screen.Mesh.GetPosition(screen.ToUVFromScreenSpace(skipButton()!.ScreenSpaceDrawQuad.Centre))));
            AddUntilStep("skip button hovered", () => skipButton()!.IsHovered);

            AddStep("click", () =>
            {
                timeBefore = Player.GameplayClockContainer.CurrentTime;
                InputManager.Click(MouseButton.Left);
            });
            // shortly after, so that the time hasn't progressed as far by itself.
            AddWaitStep("wait", 5);
            AddAssert("skipped", () => Player.GameplayClockContainer.CurrentTime, () => Is.GreaterThan(timeBefore + 2000));
        }

        private OsuClickableContainer? skipButton() => screen.ChildrenOfType<SkipOverlay>().FirstOrDefault()?.ChildrenOfType<OsuClickableContainer>().FirstOrDefault();
    }
}
