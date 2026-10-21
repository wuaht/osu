// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    /// <summary>
    /// Sliders spanning a timing point should end on the ticks of the later timing point.
    /// </summary>
    [HeadlessTest]
    [Category("slop")]
    public partial class TestSceneSliderEndTimingPointSnapping : EditorClockTestScene
    {
        private TestHitObjectComposer composer = null!;
        private EditorBeatmap editorBeatmap = null!;

        [SetUp]
        public void Setup() => Schedule(() =>
        {
            editorBeatmap = new EditorBeatmap(new OsuBeatmap
            {
                BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo },
            });

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies =
                [
                    (typeof(EditorBeatmap), editorBeatmap),
                    (typeof(IBeatSnapProvider), editorBeatmap)
                ],
                Children = new Drawable[]
                {
                    editorBeatmap,
                    new PopoverContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = composer = new TestHitObjectComposer()
                    }
                }
            };

            BeatDivisor.Value = 1;

            // 100 osu!px per second from the slider's start, and ticks every 400ms from 1500 onwards.
            composer.EditorBeatmap.Difficulty.SliderMultiplier = 1;
            composer.EditorBeatmap.ControlPointInfo.Clear();
            composer.EditorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 1000 });
            composer.EditorBeatmap.ControlPointInfo.Add(1500, new TimingControlPoint { BeatLength = 400 });
        });

        [Test]
        public void TestEndBeforeTimingPoint()
        {
            assertSnappedSliderDistance(120, 100);
            assertSnappedSliderDistance(90, 0);
        }

        [Test]
        public void TestEndSnapsToLaterTimingPoint()
        {
            // the start-based ticks would be at 1000 and 2000.
            assertSnappedSliderDistance(155, 150);
            assertSnappedSliderDistance(230, 230);
            assertSnappedSliderDistance(240, 230);
            assertSnappedSliderDistance(290, 270);
        }

        [Test]
        public void TestEndDoesNotExceedActualLength()
        {
            // the closest tick (2700) is past the actual end (2600), so the previous one is used.
            assertSnappedSliderDistance(260, 230);
        }

        private void assertSnappedSliderDistance(float distance, float expectedDistance)
            => AddAssert($"distance = {distance} -> distance = {expectedDistance} (snapped)",
                () => composer.DistanceSnapProvider.FindSnappedSliderDistance(distance, 0),
                () => Is.EqualTo(expectedDistance).Within(Precision.FLOAT_EPSILON));

        private partial class TestHitObjectComposer : OsuHitObjectComposer
        {
            public new EditorBeatmap EditorBeatmap => base.EditorBeatmap;

            public new IDistanceSnapProvider DistanceSnapProvider => base.DistanceSnapProvider;

            public TestHitObjectComposer()
                : base(new OsuRuleset())
            {
            }
        }
    }
}
