// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Edit;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneToolboxExpansionScroll : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private ExpandingToolboxContainer toolbox = null!;
        private GrowingGroup growing = null!;

        [Test]
        public void TestGroupGrowingAtBottomStaysInView()
        {
            AddStep("contract sidebars", () => config.SetValue(OsuSetting.EditorContractSidebars, true));
            AddStep("create toolbox", () =>
            {
                toolbox = new ExpandingToolboxContainer(60, 200);

                for (int i = 0; i < 5; i++)
                    toolbox.Add(new Box { RelativeSizeAxes = Axes.X, Height = 100 });

                toolbox.Add(growing = new GrowingGroup());

                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(200, 300),
                    Child = toolbox,
                };
            });

            AddUntilStep("contracted", () => !toolbox.Expanded.Value);
            AddStep("scroll to end", () => toolbox.ChildrenOfType<OsuScrollContainer>().First().ScrollToEnd(false));
            AddStep("hover bottom of toolbox", () => InputManager.MoveMouseTo(toolbox.ScreenSpaceDrawQuad.BottomLeft + new Vector2(20, -20)));
            AddUntilStep("expanded", () => toolbox.Expanded.Value);
            AddWaitStep("wait for expansion", 20);
            AddStep("group top in view", () =>
            {
                float top = growing.ScreenSpaceDrawQuad.TopLeft.Y;
                float toolboxTop = toolbox.ScreenSpaceDrawQuad.TopLeft.Y;
                Assert.That(top, Is.InRange(toolboxTop - 1, toolboxTop + 10), $"group height {growing.DrawHeight}, toolbox height {toolbox.DrawHeight}");
            });
        }

        private partial class GrowingGroup : Box
        {
            [Resolved]
            private IExpandingContainer expandingContainer { get; set; } = null!;

            public GrowingGroup()
            {
                RelativeSizeAxes = Axes.X;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                expandingContainer.Expanded.BindValueChanged(e => Height = e.NewValue ? 400 : 30, true);
            }
        }
    }
}
