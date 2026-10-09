// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneOffscreenObjects : TestSceneOsuEditor
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private Slider slider = null!;

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("add slider with offscreen body", () =>
            {
                EditorBeatmap.Clear();

                // head and tail are inside the playfield, but the arc goes above the top of the screen.
                EditorBeatmap.Add(slider = new Slider
                {
                    StartTime = 1000,
                    Position = new Vector2(100, 40),
                    Path = new SliderPath(new[]
                    {
                        new PathControlPoint(Vector2.Zero, PathType.PERFECT_CURVE),
                        new PathControlPoint(new Vector2(150, -120)),
                        new PathControlPoint(new Vector2(300, 0)),
                    }),
                });
            });
            AddStep("seek to slider", () => EditorClock.Seek(slider.StartTime));
        }

        [Test]
        public void TestOutlineDisplayed()
        {
            AddStep("enable", () => config.SetValue(OsuSetting.SlopEditorShowOffscreenObjects, true));
            AddUntilStep("outline visible", () => visibleOutlines(), () => Is.EqualTo(1));

            AddStep("move slider onscreen", () =>
            {
                slider.Position = new Vector2(100, 250);
                EditorBeatmap.Update(slider);
            });
            AddUntilStep("outline hidden", () => visibleOutlines(), () => Is.Zero);

            AddStep("disable", () => config.SetValue(OsuSetting.SlopEditorShowOffscreenObjects, false));
        }

        [Test]
        public void TestDisabledByDefault()
        {
            AddStep("disable", () => config.SetValue(OsuSetting.SlopEditorShowOffscreenObjects, false));
            AddWaitStep("wait", 3);
            AddAssert("no outline", () => visibleOutlines(), () => Is.Zero);
        }

        private int visibleOutlines() => Editor.ChildrenOfType<OffscreenObjectOverlay>().Single().ChildrenOfType<Container>().Count(c => c.Alpha > 0 && c.BorderThickness > 0);
    }
}
