// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders.Components;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Tests.Beatmaps;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneSliderLengthAdjustment : TestSceneOsuEditor
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new TestBeatmap(ruleset);

        private Slider slider = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = 0,
                    Position = new Vector2(100, 192),
                    Path = new SliderPath(new[]
                    {
                        new PathControlPoint(Vector2.Zero, PathType.LINEAR),
                        new PathControlPoint(new Vector2(100, 0)),
                    }),
                });
            });
            AddStep("seek to start", () => EditorClock.Seek(0));
            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(slider));
        }

        public override void TearDownSteps()
        {
            base.TearDownSteps();
            AddStep("reset timeline velocity modifier", () => config.SetValue(OsuSetting.SlopEditorTimelineSliderVelocityWithAlt, false));
        }

        [Test]
        public void TestTimelineCtrlDragChangesLength()
        {
            double originalDuration = 0;

            AddStep("store duration", () => originalDuration = slider.Duration);
            dragTimelineEnd(1.6f, Key.ControlLeft);

            AddAssert("velocity unchanged", () => slider.SliderVelocityMultiplier, () => Is.EqualTo(1));
            AddAssert("length scaled with duration", () => slider.Path.Distance / 100, () => Is.EqualTo(slider.Duration / originalDuration).Within(0.01));
            AddAssert("slider longer", () => slider.Duration, () => Is.GreaterThan(originalDuration * 1.3));
            AddAssert("end snapped to beat", () => EditorBeatmap.SnapTime(slider.EndTime, slider.StartTime), () => Is.EqualTo(slider.EndTime).Within(1));
            AddAssert("path extended in a straight line", () => slider.Path.PositionAt(1).Y, () => Is.EqualTo(0).Within(0.01));
        }

        [Test]
        public void TestTimelineCtrlShiftDragDoesNotSnap()
        {
            double originalDuration = 0;
            double expectedDuration = 0;

            AddStep("store duration", () => originalDuration = slider.Duration);
            dragTimelineEnd(1.37f, Key.ControlLeft, Key.ShiftLeft, () => expectedDuration = timelineTimeAtMouse() - slider.StartTime);

            AddAssert("velocity unchanged", () => slider.SliderVelocityMultiplier, () => Is.EqualTo(1));
            AddAssert("duration follows mouse", () => slider.Duration, () => Is.EqualTo(expectedDuration).Within(1));
            AddAssert("length scaled with duration", () => slider.Path.Distance / 100, () => Is.EqualTo(slider.Duration / originalDuration).Within(0.01));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestTimelineVelocityModifier(bool withAlt)
        {
            AddStep("set velocity modifier", () => config.SetValue(OsuSetting.SlopEditorTimelineSliderVelocityWithAlt, withAlt));

            dragTimelineEnd(1.6f, withAlt ? Key.AltLeft : Key.ShiftLeft);
            AddAssert("velocity changed", () => slider.SliderVelocityMultiplier, () => Is.Not.EqualTo(1));
            AddAssert("length unchanged", () => slider.Path.Distance, () => Is.EqualTo(100).Within(0.01));
        }

        [Test]
        public void TestTimelineShiftDoesNotChangeVelocityWithAltModifier()
        {
            AddStep("change velocity with alt", () => config.SetValue(OsuSetting.SlopEditorTimelineSliderVelocityWithAlt, true));

            dragTimelineEnd(1.6f, Key.ShiftLeft);
            AddAssert("velocity unchanged", () => slider.SliderVelocityMultiplier, () => Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPlayfieldLengthNotLimitedToPath(bool alt)
        {
            dragEndMarkerBy(new Vector2(73.3f, 0), alt);

            AddAssert("velocity unchanged", () => slider.SliderVelocityMultiplier, () => Is.EqualTo(1));
            AddAssert("slider extended beyond its path", () => slider.Path.Distance, () => Is.GreaterThan(150));
            AddAssert("path extended in a straight line", () => slider.Path.PositionAt(1).Y, () => Is.EqualTo(0).Within(0.01));

            if (alt)
                AddAssert("length not snapped", () => slider.Path.Distance, () => Is.EqualTo(173.3).Within(0.5));
            else
                AddAssert("end snapped to beat", () => EditorBeatmap.SnapTime(slider.EndTime, slider.StartTime), () => Is.EqualTo(slider.EndTime).Within(1));
        }

        [Test]
        public void TestAnchorDragWithAltDoesNotSnapLength()
        {
            AddStep("move mouse to end anchor", () => InputManager.MoveMouseTo(this.ChildrenOfType<PathControlPointPiece<Slider>>().Single(p => p.ControlPoint == slider.Path.ControlPoints[1])));
            AddStep("hold alt", () => InputManager.PressKey(Key.AltLeft));
            AddStep("press", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag", () => InputManager.MoveMouseTo(playfield().GamefieldToScreenSpace(new Vector2(273.3f, 192))));
            AddStep("release", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
                InputManager.ReleaseKey(Key.AltLeft);
            });

            AddAssert("length follows anchor", () => slider.Path.Distance, () => Is.EqualTo(173.3).Within(0.5));
        }

        private void dragTimelineEnd(float durationFactor, Key modifier, Key? secondModifier = null, System.Action? beforeRelease = null)
        {
            AddStep("move mouse to timeline end", () =>
                InputManager.MoveMouseTo(this.ChildrenOfType<TimelineHitObjectBlueprint.DragArea>().Single(d => d.HandlePositionalInput)));
            AddStep("hold modifiers", () =>
            {
                InputManager.PressKey(modifier);
                if (secondModifier is Key key)
                    InputManager.PressKey(key);
            });
            AddStep("press", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag", () =>
            {
                var quad = this.ChildrenOfType<TimelineHitObjectBlueprint>().Single().SelectionQuad;
                InputManager.MoveMouseTo(new Vector2(quad.TopLeft.X + (quad.TopRight.X - quad.TopLeft.X) * durationFactor, quad.Centre.Y));
            });
            AddWaitStep("wait for drag", 2);
            if (beforeRelease != null)
                AddStep("store mouse time", beforeRelease);
            AddStep("release", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
                if (secondModifier is Key key)
                    InputManager.ReleaseKey(key);
                InputManager.ReleaseKey(modifier);
            });
        }

        private void dragEndMarkerBy(Vector2 gamefieldDelta, bool alt)
        {
            Vector2 start = default;

            AddStep("move mouse to end marker", () =>
            {
                var marker = this.ChildrenOfType<SliderEndDragMarker>().Single();
                start = (marker.ScreenSpaceDrawQuad.TopRight + marker.ScreenSpaceDrawQuad.BottomRight) / 2;
                InputManager.MoveMouseTo(start);
            });
            if (alt)
                AddStep("hold alt", () => InputManager.PressKey(Key.AltLeft));
            AddStep("press", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag", () => InputManager.MoveMouseTo(start + playfield().GamefieldToScreenSpace(gamefieldDelta) - playfield().GamefieldToScreenSpace(Vector2.Zero)));
            AddStep("release", () =>
            {
                InputManager.ReleaseButton(MouseButton.Left);
                if (alt)
                    InputManager.ReleaseKey(Key.AltLeft);
            });
        }

        private double timelineTimeAtMouse() => this.ChildrenOfType<Timeline>().Single().TimeAtScreenSpacePosition(InputManager.CurrentState.Mouse.Position);

        private OsuPlayfield playfield() => this.ChildrenOfType<OsuPlayfield>().Single();
    }
}
