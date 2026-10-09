// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Fposu.UI;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Fposu.Tests
{
    [Category("slop")]
    public partial class TestSceneFposuGameplay : PlayerTestScene
    {
        private const int dpi = 400;
        private const float cm_per_360 = 30;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        protected override Ruleset CreatePlayerRuleset() => new FposuRuleset();

        private DrawableFposuRuleset drawableRuleset => (DrawableFposuRuleset)Player.DrawableRuleset;

        private FposuScreen screen => drawableRuleset.Screen;

        public override void SetUpSteps()
        {
            AddStep("set sensitivity", () =>
            {
                config.SetValue(OsuSetting.SlopFposuMouseDpi, dpi);
                config.SetValue(OsuSetting.SlopFposuCmPer360, cm_per_360);
                config.SetValue(OsuSetting.SlopFposuAbsoluteMode, false);
                config.SetValue(OsuSetting.SlopFposuInvertHorizontal, false);
                config.SetValue(OsuSetting.SlopFposuInvertVertical, false);
            });

            base.SetUpSteps();

            AddUntilStep("screen loaded", () => Player.DrawableRuleset is DrawableFposuRuleset && screen.IsLoaded);
        }

        [Test]
        public void TestAbsoluteMode()
        {
            AddStep("disable relative movement", () => drawableRuleset.FposuInputManager.MouseInput.AvailabilityOverride = false);

            AddStep("move mouse", () => InputManager.MoveMouseTo(screen.ToScreenSpaceFromUV(new Vector2(0.3f, 0.6f))));
            AddUntilStep("camera looks at cursor", () => isClose(lookedAtUV(), new Vector2(0.3f, 0.6f), 0.002f));
        }

        [Test]
        public void TestRelativeMovementTurnsCamera()
        {
            AddStep("enable relative movement", () => drawableRuleset.FposuInputManager.MouseInput.AvailabilityOverride = true);
            AddStep("reset camera", () => drawableRuleset.Camera.Reset());

            AddStep("move mouse to the right", () => drawableRuleset.FposuInputManager.MouseInput.AddMovement(new Vector2(countsFor(10), 0)));
            AddUntilStep("camera turned to the right", () => drawableRuleset.Camera.Yaw, () => Is.EqualTo(10).Within(0.01));

            AddStep("move mouse down", () => drawableRuleset.FposuInputManager.MouseInput.AddMovement(new Vector2(0, countsFor(5))));
            AddUntilStep("camera turned downwards", () => drawableRuleset.Camera.Pitch, () => Is.EqualTo(-5).Within(0.01));

            AddUntilStep("cursor at centre of view", () => isClose(cursorUV(), lookedAtUV(), 0.001f));
            AddAssert("cursor right of and below centre", () => cursorUV().X > 0.5f && cursorUV().Y > 0.5f);

            AddAssert("gameplay cursor at cursor position", () => isClose(
                Player.DrawableRuleset.Playfield.Cursor!.ChildrenOfType<OsuCursor>().Single().ScreenSpaceDrawQuad.Centre,
                drawableRuleset.FposuInputManager.CurrentState.Mouse.Position, 1));

            // the position of the mouse is ignored while the camera controls the cursor.
            Vector2 cursorBefore = Vector2.Zero;
            AddStep("store cursor", () => cursorBefore = drawableRuleset.FposuInputManager.CurrentState.Mouse.Position);
            AddStep("move mouse elsewhere", () => InputManager.MoveMouseTo(screen.ToScreenSpaceFromUV(new Vector2(0.1f))));
            AddWaitStep("wait", 2);
            AddAssert("cursor unchanged", () => drawableRuleset.FposuInputManager.CurrentState.Mouse.Position, () => Is.EqualTo(cursorBefore));
        }

        [Test]
        public void TestLookingAwayMovesCursorOffScreen()
        {
            AddStep("enable relative movement", () => drawableRuleset.FposuInputManager.MouseInput.AvailabilityOverride = true);
            AddStep("reset camera", () => drawableRuleset.Camera.Reset());

            AddStep("turn around", () => drawableRuleset.FposuInputManager.MouseInput.AddMovement(new Vector2(countsFor(180), 0)));
            AddUntilStep("camera turned around", () => Math.Abs(drawableRuleset.Camera.Yaw), () => Is.EqualTo(180).Within(0.01));
            AddUntilStep("cursor off screen", () => isClose(cursorUV(), new Vector2(-1), 0.001f));
        }

        [Test]
        public void TestBreakAndSkipOverlaysDisplayedOnScreen()
        {
            AddAssert("break overlay on screen", () => screen.ChildrenOfType<BreakOverlay>().Single(), () => Is.SameAs(Player.BreakOverlay));
            AddAssert("skip overlays not above screen", () => Player.ChildrenOfType<SkipOverlay>().All(o => o.FindClosestParent<FposuScreen>() == screen));
        }

        private static bool isClose(Vector2 a, Vector2 b, float tolerance) => (a - b).Length <= tolerance;

        private static float countsFor(float degrees) => degrees / FposuCamera.DegreesPerCount(dpi, cm_per_360);

        private Vector2 cursorUV() => screen.ToUVFromScreenSpace(drawableRuleset.FposuInputManager.CurrentState.Mouse.Position);

        private Vector2 lookedAtUV() => screen.Mesh.Intersect(drawableRuleset.Camera.Forward) ?? new Vector2(-1);
    }
}
