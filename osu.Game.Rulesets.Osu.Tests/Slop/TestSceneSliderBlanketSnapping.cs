// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Lines;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Tests.Beatmaps;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneSliderBlanketSnapping : TestSceneOsuEditor
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new TestBeatmap(ruleset);

        private Slider inner = null!;
        private Slider outer = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(new HitObject[]
                {
                    // an arc with radius 50 around (150, 150).
                    inner = createArcSlider(0, new Vector2(100, 150), 50),
                    // a longer arc with radius 120 around (370, 200), far from the inner one.
                    outer = createArcSlider(500, new Vector2(250, 200), 120),
                });
            });

            // both sliders are visible.
            AddStep("seek between sliders", () => EditorClock.Seek(250));
        }

        public override void TearDownSteps()
        {
            base.TearDownSteps();

            // as for upstream tests (see SlopTestDefaults), which may run afterwards.
            AddStep("disable slider blanket snapping", () => config.SetValue(OsuSetting.SlopEditorSliderBlanketSnap, false));
            AddStep("disable blanket snapping", () => config.SetValue(OsuSetting.SlopEditorBlanketSnap, false));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestSnapsConcentric(bool moveOuter)
        {
            AddStep("enable slider blanket snapping", () => config.SetValue(OsuSetting.SlopEditorSliderBlanketSnap, true));

            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(moveOuter ? outer : inner));
            dragArcCentreNextTo(() => moveOuter ? outer : inner, () => arcCentre(moveOuter ? inner : outer));

            AddAssert("arcs share their centre", () => Precision.AlmostEquals(arcCentre(inner), arcCentre(outer), 0.1f));
            AddAssert("radii unchanged", () => Precision.AlmostEquals(arcRadius(inner), 50, 0.1f) && Precision.AlmostEquals(arcRadius(outer), 120, 0.1f));
        }

        [Test]
        public void TestSliderSnapsAroundCircle()
        {
            HitCircle circle = null!;

            AddStep("enable only blanket snapping", () =>
            {
                config.SetValue(OsuSetting.SlopEditorBlanketSnap, true);
                config.SetValue(OsuSetting.SlopEditorSliderBlanketSnap, false);
            });

            AddStep("add circle", () => EditorBeatmap.Add(circle = new HitCircle { StartTime = 250, Position = new Vector2(380, 120) }));
            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(outer));
            dragArcCentreNextTo(() => outer, () => circle.Position);

            AddAssert("slider blankets circle", () => Precision.AlmostEquals(arcCentre(outer), circle.Position, 0.1f));
            AddAssert("radius unchanged", () => Precision.AlmostEquals(arcRadius(outer), 120, 0.1f));
        }

        [Test]
        public void TestSliderDoesNotSnapAroundCircleWhenDisabled()
        {
            HitCircle circle = null!;

            AddStep("disable blanket snapping", () => config.SetValue(OsuSetting.SlopEditorBlanketSnap, false));
            AddStep("add circle", () => EditorBeatmap.Add(circle = new HitCircle { StartTime = 250, Position = new Vector2(380, 120) }));
            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(outer));
            dragArcCentreNextTo(() => outer, () => circle.Position);

            AddAssert("slider doesn't blanket circle", () => Precision.AlmostEquals(arcCentre(outer), circle.Position, 0.1f), () => Is.False);
        }

        [Test]
        public void TestGuideLinesDisplayed()
        {
            AddStep("enable slider blanket snapping", () => config.SetValue(OsuSetting.SlopEditorSliderBlanketSnap, true));

            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(outer));
            AddStep("move mouse to slider body", () => InputManager.MoveMouseTo(playfield().GamefieldToScreenSpace(bodyPoint(outer))));
            AddStep("press", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag", () => InputManager.MoveMouseTo(playfield().GamefieldToScreenSpace(bodyPoint(outer) + arcCentre(inner) - arcCentre(outer) + new Vector2(2))));

            // the full circles and the arcs of both sliders.
            AddUntilStep("guide lines displayed", () => this.ChildrenOfType<PatternSnapGuideOverlay>().Single().ChildrenOfType<Path>().Count(), () => Is.EqualTo(4));

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
        }

        [Test]
        public void TestNoSnappingWhenDisabled()
        {
            AddStep("disable slider blanket snapping", () => config.SetValue(OsuSetting.SlopEditorSliderBlanketSnap, false));

            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(outer));
            dragArcCentreNextTo(() => outer, () => arcCentre(inner));

            AddAssert("arcs don't share their centre", () => Precision.AlmostEquals(arcCentre(inner), arcCentre(outer), 0.1f), () => Is.False);
        }

        /// <summary>
        /// Drags a slider by its body (dragging its head would move the control point), such that the centre of its arc ends up 2 osu!pixels away from a position.
        /// </summary>
        private void dragArcCentreNextTo(System.Func<Slider> slider, System.Func<Vector2> position)
        {
            Vector2 target = default;

            AddStep("move mouse to slider body", () =>
            {
                target = bodyPoint(slider()) + position() - arcCentre(slider()) + new Vector2(2);
                InputManager.MoveMouseTo(playfield().GamefieldToScreenSpace(bodyPoint(slider())));
            });
            AddStep("press", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag", () => InputManager.MoveMouseTo(playfield().GamefieldToScreenSpace(target)));
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));
        }

        // not the middle, where the middle control point of the arc is.
        private static Vector2 bodyPoint(Slider slider) => slider.Position + slider.Path.PositionAt(0.25);

        private OsuPlayfield playfield() => this.ChildrenOfType<OsuPlayfield>().Single();

        private static Vector2 arcCentre(Slider slider) => PatternSnapping.GetArcs(slider).Single().Centre;

        private static float arcRadius(Slider slider) => PatternSnapping.GetArcs(slider).Single().Radius;

        /// <summary>
        /// Creates a slider along a half circle, starting left of its centre.
        /// </summary>
        private static Slider createArcSlider(double startTime, Vector2 position, float radius) => new Slider
        {
            StartTime = startTime,
            Position = position,
            Path = new SliderPath(new[]
            {
                new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                new PathControlPoint(new Vector2(radius, radius)),
                new PathControlPoint(new Vector2(2 * radius, 0)),
            }),
        };
    }
}
