// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneSliderPartHitsounds : TestSceneOsuEditor
    {
        private Slider slider = null!;

        private double lastClickTime = double.MinValue;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("add slider", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = 1000,
                    Position = new Vector2(100, 192),
                    Path = new SliderPath(new[]
                    {
                        new PathControlPoint(Vector2.Zero, PathType.LINEAR),
                        new PathControlPoint(new Vector2(300, 0)),
                    }),
                    Samples = { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                    NodeSamples =
                    {
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                        new List<HitSampleInfo> { new HitSampleInfo(HitSampleInfo.HIT_NORMAL) },
                    },
                });
            });
            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.Add(slider));
        }

        [Test]
        public void TestHitsoundsApplyToSelectedPartOnly()
        {
            clickAt(() => slider.Path.PositionAt(0));
            AddAssert("head selected", () => EditorBeatmap.SelectedHitObjectPart.Value?.NodeIndex, () => Is.EqualTo(0));

            AddStep("toggle whistle", () => InputManager.Key(Key.W));
            AddAssert("head has whistle", () => hasSample(slider.NodeSamples[0], HitSampleInfo.HIT_WHISTLE));
            AddAssert("tail has no whistle", () => !hasSample(slider.NodeSamples[1], HitSampleInfo.HIT_WHISTLE));
            AddAssert("body has no whistle", () => !hasSample(slider.Samples, HitSampleInfo.HIT_WHISTLE));

            clickAt(() => slider.Path.PositionAt(1));
            AddAssert("tail selected", () => EditorBeatmap.SelectedHitObjectPart.Value?.NodeIndex, () => Is.EqualTo(1));

            AddStep("toggle clap", () => InputManager.Key(Key.R));
            AddAssert("tail has clap", () => hasSample(slider.NodeSamples[1], HitSampleInfo.HIT_CLAP));
            AddAssert("head has no clap", () => !hasSample(slider.NodeSamples[0], HitSampleInfo.HIT_CLAP));
            AddAssert("body has no clap", () => !hasSample(slider.Samples, HitSampleInfo.HIT_CLAP));

            clickAt(() => slider.Path.PositionAt(0.5));
            AddAssert("body selected", () => EditorBeatmap.SelectedHitObjectPart.Value is { NodeIndex: null });

            AddStep("toggle finish", () => InputManager.Key(Key.E));
            AddAssert("body has finish", () => hasSample(slider.Samples, HitSampleInfo.HIT_FINISH));
            AddAssert("nodes have no finish", () => slider.NodeSamples.All(n => !hasSample(n, HitSampleInfo.HIT_FINISH)));

            clickAt(() => slider.Path.PositionAt(0.5));
            AddAssert("whole slider selected", () => EditorBeatmap.SelectedHitObjectPart.Value, () => Is.Null);

            AddStep("toggle finish", () => InputManager.Key(Key.E));
            AddAssert("all parts have finish", () => hasSample(slider.Samples, HitSampleInfo.HIT_FINISH) && slider.NodeSamples.All(n => hasSample(n, HitSampleInfo.HIT_FINISH)));
        }

        [Test]
        public void TestPartResetOnSelectionChange()
        {
            clickAt(() => slider.Path.PositionAt(0));
            AddAssert("head selected", () => EditorBeatmap.SelectedHitObjectPart.Value?.NodeIndex, () => Is.EqualTo(0));

            AddStep("deselect", () => EditorBeatmap.SelectedHitObjects.Clear());
            AddAssert("no part selected", () => EditorBeatmap.SelectedHitObjectPart.Value, () => Is.Null);
        }

        [Test]
        public void TestRepeatedClicksCycleThroughNodesAtPosition()
        {
            AddStep("add two repeats", () =>
            {
                // node samples are added automatically for the new repeats.
                slider.RepeatCount = 2;
                EditorBeatmap.Update(slider);
            });
            AddAssert("slider has four nodes", () => slider.NodeSamples.Count, () => Is.EqualTo(4));

            // with two repeats, nodes 0 (head) and 2 (second repeat) are located at the start of the path.
            clickAt(() => slider.Path.PositionAt(0));
            AddAssert("head selected", () => EditorBeatmap.SelectedHitObjectPart.Value?.NodeIndex, () => Is.EqualTo(0));

            clickAt(() => slider.Path.PositionAt(0));
            AddAssert("second repeat selected", () => EditorBeatmap.SelectedHitObjectPart.Value?.NodeIndex, () => Is.EqualTo(2));

            clickAt(() => slider.Path.PositionAt(0));
            AddAssert("whole slider selected", () => EditorBeatmap.SelectedHitObjectPart.Value, () => Is.Null);

            // nodes 1 (first repeat) and 3 (tail) are located at the end of the path.
            clickAt(() => slider.Path.PositionAt(1));
            AddAssert("first repeat selected", () => EditorBeatmap.SelectedHitObjectPart.Value?.NodeIndex, () => Is.EqualTo(1));

            clickAt(() => slider.Path.PositionAt(1));
            AddAssert("tail selected", () => EditorBeatmap.SelectedHitObjectPart.Value?.NodeIndex, () => Is.EqualTo(3));
        }

        private static bool hasSample(IEnumerable<HitSampleInfo> samples, string name) => samples.Any(s => s.Name == name);

        /// <summary>
        /// Clicks at a position relative to the slider's position, making sure the click isn't treated as a double click.
        /// </summary>
        private void clickAt(System.Func<Vector2> pathPosition)
        {
            AddUntilStep("wait to avoid double click", () => InputManager.Time.Current > lastClickTime + 500);
            AddStep("click", () =>
            {
                var drawableSlider = this.ChildrenOfType<DrawableSlider>().Single();
                InputManager.MoveMouseTo(drawableSlider.Parent!.ToScreenSpace(slider.StackedPosition + pathPosition()));
                InputManager.Click(MouseButton.Left);
                lastClickTime = InputManager.Time.Current;
            });
        }
    }
}
