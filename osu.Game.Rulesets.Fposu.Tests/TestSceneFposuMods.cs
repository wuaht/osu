// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Fposu.Mods;
using osu.Game.Rulesets.Fposu.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Fposu.Tests
{
    [Category("slop")]
    public partial class TestSceneFposuMods : PlayerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        protected override Ruleset CreatePlayerRuleset() => new FposuRuleset();

        private DrawableFposuRuleset drawableRuleset => (DrawableFposuRuleset)Player.DrawableRuleset;

        private FposuScreen screen => drawableRuleset.Screen;

        [Test]
        public void TestModsReplaced()
        {
            AddAssert("flashlight", () => new FposuRuleset().CreateModFromAcronym("FL"), () => Is.TypeOf<FposuModFlashlight>());
            AddAssert("depth", () => new FposuRuleset().CreateModFromAcronym("DP"), () => Is.TypeOf<FposuModDepth>());
            AddAssert("no osu! variants", () => new FposuRuleset().CreateAllMods().Any(m => m.GetType() == typeof(OsuModFlashlight) || m.GetType() == typeof(OsuModDepth)),
                () => Is.False);
        }

        [Test]
        public void TestOverlaysDisplayedOnScreen()
        {
            loadPlayer(new OsuModBlinds());

            AddAssert("overlays proxied", () => drawableRuleset.Overlays.HasProxy);
            AddAssert("proxy displayed on screen", () => drawableRuleset.ChildrenOfType<FposuPlayfieldAdjustmentContainer>().First().RulesetOverlays.Children.Any(d => d.IsProxy));
        }

        [Test]
        public void TestDepth()
        {
            loadPlayer(new FposuModDepth());

            AddUntilStep("objects displayed in 3D", () => aliveObjects().Any(d => d.HasProxy));

            AddAssert("object at its depth projects onto its position on the screen", () =>
            {
                var drawable = aliveObjects().First();
                var layer = drawableRuleset.ChildrenOfType<FposuDepthLayer>().Single();

                layer.TryGetProjection(drawable, 0, out var projection, out _);

                Vector2 anchor = anchorOf(drawable);
                return (project(projection, anchor) - projectOnScreen(anchor)).Length;
            }, () => Is.LessThan(0.5f));

            AddAssert("further objects are closer to the centre of the view", () =>
            {
                var drawable = aliveObjects().First();
                var layer = drawableRuleset.ChildrenOfType<FposuDepthLayer>().Single();
                Vector2 centre = screen.ScreenSpaceDrawQuad.AABBFloat.Centre;
                Vector2 anchor = anchorOf(drawable);

                layer.TryGetProjection(drawable, 0, out var near, out float nearDepth);
                layer.TryGetProjection(drawable, 100, out var far, out float farDepth);

                return farDepth > nearDepth && (project(far, anchor) - centre).Length < (project(near, anchor) - centre).Length;
            });

            // the framework replaces the z coordinate of vertices with their draw depth (up to 1), so vertices with a smaller w would be clipped.
            AddAssert("projected vertices not clipped at the side of the view", () =>
            {
                var drawable = aliveObjects().First();
                var layer = drawableRuleset.ChildrenOfType<FposuDepthLayer>().Single();

                drawableRuleset.Camera.Rotate(60, 0);
                layer.TryGetProjection(drawable, 0, out var projection, out _);
                drawableRuleset.Camera.Reset();

                return Vector4.Transform(new Vector4(anchorOf(drawable).X, anchorOf(drawable).Y, 1, 1), projection).W;
            }, () => Is.GreaterThanOrEqualTo(1));

            AddAssert("cursor displayed in 3D", () => Player.DrawableRuleset.Playfield.Cursor!.ActiveCursor.HasProxy);
            AddAssert("trail and ripples displayed on screen", () => !Player.DrawableRuleset.Playfield.Cursor!.HasProxy);
        }

        [Test]
        public void TestDepthApproachCirclesHiddenAfterFadingOut()
        {
            DrawableHitCircle? circle = null;

            AddStep("load player", () => LoadPlayer(new Mod[] { new FposuModDepth(), new FposuRuleset().GetAutoplayMod()! }));
            AddUntilStep("player loaded", () => Player.IsLoaded && Player.Alpha == 1 && screen.IsLoaded);

            AddUntilStep("approach circle displayed", () =>
            {
                circle = Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().FirstOrDefault(c => !c.Judged);
                return circle != null && depthLayer().IsDisplayed(circle.ApproachCircle);
            });

            AddUntilStep("circle hit", () => circle!.IsHit);
            AddUntilStep("approach circle faded out", () => !circle!.ApproachCircle.IsPresent);

            // the circle itself is still displayed (with its hit animation), but its approach circle mustn't be.
            AddAssert("circle still displayed", () => depthLayer().IsDisplayed(circle!));
            AddAssert("approach circle not displayed", () => depthLayer().IsDisplayed(circle!.ApproachCircle), () => Is.False);
        }

        private FposuDepthLayer depthLayer() => drawableRuleset.ChildrenOfType<FposuDepthLayer>().Single();

        private void loadPlayer(Mod mod)
        {
            AddStep("load player", () => LoadPlayer(new[] { mod }));
            AddUntilStep("player loaded", () => Player.IsLoaded && Player.Alpha == 1 && screen.IsLoaded);
        }

        private DrawableHitObject[] aliveObjects() => Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.Where(d => d is DrawableHitCircle or DrawableSlider).ToArray();

        private static Vector2 anchorOf(DrawableHitObject drawable) => drawable is DrawableSlider slider ? slider.Ball.ScreenSpaceDrawQuad.Centre : drawable.ScreenSpaceDrawQuad.Centre;

        // as the GPU does: the vertex (x, y, 1, 1) as a row vector multiplied with the projection, followed by the division by w.
        private static Vector2 project(Matrix4 projection, Vector2 position)
        {
            var v = Vector4.Transform(new Vector4(position.X, position.Y, 1, 1), projection);
            return v.Xy / v.W;
        }

        // the projection of the screen (see FposuScreen).
        private Vector2 projectOnScreen(Vector2 screenSpacePosition)
        {
            var camera = drawableRuleset.Camera;
            Vector3 p = screen.Mesh.GetPosition(screen.ToUVFromScreenSpace(screenSpacePosition));

            var quad = screen.ScreenSpaceDrawQuad.AABBFloat;
            float focalLength = quad.Width / 2 / MathF.Tan(MathHelper.DegreesToRadians(config.Get<float>(OsuSetting.SlopFposuFov)) / 2);
            float depth = Vector3.Dot(p, camera.Forward);

            return new Vector2(
                quad.Centre.X + focalLength * Vector3.Dot(p, camera.Right) / depth,
                quad.Centre.Y - focalLength * Vector3.Dot(p, camera.Up) / depth);
        }
    }
}
