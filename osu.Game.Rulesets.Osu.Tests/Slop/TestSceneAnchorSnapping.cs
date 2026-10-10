// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders.Components;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit.Components;
using osu.Game.Tests.Beatmaps;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneAnchorSnapping : TestSceneOsuEditor
    {
        private static readonly Vector2 anchor_position = new Vector2(200, 200);

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new TestBeatmap(ruleset);

        private Slider slider = null!;
        private HitCircle circle = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(new HitObject[]
                {
                    slider = new Slider
                    {
                        StartTime = 0,
                        Position = new Vector2(100, 100),
                        Path = new SliderPath(new[]
                        {
                            new PathControlPoint(Vector2.Zero, PathType.BEZIER),
                            new PathControlPoint(anchor_position - new Vector2(100, 100)),
                            new PathControlPoint(new Vector2(200, 0)),
                        }),
                    },
                    circle = new HitCircle { StartTime = 1000, Position = new Vector2(400, 300) },
                });
            });

            // both objects are visible.
            AddStep("seek to slider", () => EditorClock.Seek(500));
        }

        [Test]
        public void TestToggleViaContextMenu()
        {
            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(slider));
            AddStep("move mouse to anchor", () => InputManager.MoveMouseTo(this.ChildrenOfType<PathControlPointPiece<Slider>>().ElementAt(1)));
            AddStep("right click anchor", () => InputManager.Click(MouseButton.Right));

            AddStep("click snap item", () => snapMenuItem().Action.Value!.Invoke());
            AddAssert("anchor is snap target", () => slider.Path.ControlPoints[1].IsSnapTarget);
            AddAssert("other anchors aren't", () => !slider.Path.ControlPoints[0].IsSnapTarget && !slider.Path.ControlPoints[2].IsSnapTarget);

            AddStep("right click anchor again", () => InputManager.Click(MouseButton.Right));
            AddStep("click snap item again", () => snapMenuItem().Action.Value!.Invoke());
            AddAssert("anchor isn't snap target", () => slider.Path.ControlPoints[1].IsSnapTarget, () => Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestObjectSnapsToAnchor(bool snapTarget)
        {
            AddStep("set snap target", () => slider.Path.ControlPoints[1].IsSnapTarget = snapTarget);

            AddStep("select circle", () => EditorBeatmap.SelectedHitObjects.Add(circle));
            AddStep("move mouse to circle", () => InputManager.MoveMouseTo(playfield().GamefieldToScreenSpace(circle.Position)));
            AddStep("press", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag next to anchor", () => InputManager.MoveMouseTo(playfield().GamefieldToScreenSpace(anchor_position + new Vector2(2, 2))));
            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert(snapTarget ? "circle snapped to anchor" : "circle not snapped", () => Precision.AlmostEquals(circle.Position, anchor_position, 0.1f), () => Is.EqualTo(snapTarget));
        }

        [Test]
        public void TestSnapTargetRingDisplayedWhileSliderSelected()
        {
            AddStep("set snap target", () => slider.Path.ControlPoints[1].IsSnapTarget = true);
            AddAssert("no ring displayed without selection", () => displayedRings(), () => Is.Empty);

            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(slider));
            AddAssert("anchor not selected", () => this.ChildrenOfType<PathControlPointPiece<Slider>>().Any(p => p.IsSelected.Value), () => Is.False);
            AddAssert("ring displayed at anchor",
                () => Vector2.Distance(displayedRings().Single().ScreenSpaceDrawQuad.Centre, playfield().GamefieldToScreenSpace(anchor_position)), () => Is.LessThan(0.5f));
            AddAssert("ring is thicker than 1px", () => displayedRings().Single().BorderThickness, () => Is.GreaterThan(1));

            AddStep("deselect slider", () => EditorBeatmap.SelectedHitObjects.Clear());
            AddAssert("ring hidden without selection", () => displayedRings(), () => Is.Empty);

            AddStep("select slider again", () => EditorBeatmap.SelectedHitObjects.Add(slider));
            AddStep("unset snap target", () => slider.Path.ControlPoints[1].IsSnapTarget = false);
            AddAssert("ring hidden", () => displayedRings(), () => Is.Empty);
        }

        private EditorAnchorShapeContainer[] displayedRings()
            => this.ChildrenOfType<SnapTargetAnchorOverlay>().Single().ChildrenOfType<EditorAnchorShapeContainer>().Where(r => r.Alpha > 0).ToArray();

        private OsuPlayfield playfield() => this.ChildrenOfType<OsuPlayfield>().Single();

        // as displayed by the context menu, which is open now.
        private Framework.Graphics.UserInterface.MenuItem snapMenuItem()
            => this.ChildrenOfType<DrawableOsuMenuItem>().Single(item => item.Item.Text.Value.ToString() == "Snap objects to anchor").Item;
    }
}
