// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Fposu.UI;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Fposu.Tests
{
    [Category("slop")]
    public partial class TestSceneFposuRulesetIcon : OsuTestScene
    {
        [Test]
        public void TestIconLoaded()
        {
            FposuRulesetIcon icon = null!;

            AddStep("create icon", () => Child = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Spacing = new Vector2(20),
                Children = new Drawable[]
                {
                    icon = (FposuRulesetIcon)new FposuRuleset().CreateIcon(),
                    new FposuRuleset().CreateIcon().With(d => d.Size = new Vector2(90)),
                }
            });

            AddUntilStep("texture loaded", () => icon.Texture != null);
            AddAssert("texture is the icon", () => icon.Texture.Width, () => Is.EqualTo(90));
        }
    }
}
