// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Containers.BBCode;
using osu.Game.Overlays;
using osu.Game.Overlays.Discord;
using osu.Game.Tests.Visual;
using osuTK.Graphics;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneDrawableBBCode : OsuTestScene
    {
        private const string html = "<div class='bbcode bbcode--profile-page'>"
                                    + "<div class=\"bbcode__align-centre\">first<br />second<br /><br />after   empty &amp; line ✅</div>"
                                    + "<div class=\"js-spoilerbox bbcode-spoilerbox\"><a class=\"js-spoilerbox__link bbcode-spoilerbox__link\" href=\"#\">"
                                    + "<span class=\"bbcode-spoilerbox__link-icon\"></span><span class=\"bbcode-spoilerbox__link-text\">goodbye wave</span></a>"
                                    + "<div class=\"js-spoilerbox__body bbcode-spoilerbox__body\">hidden<img src=\"https://localhost/missing.png\" /></div></div>"
                                    + "</div>";

        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private DrawableBBCode bbcode = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("show bbcode", () => Child = bbcode = new DrawableBBCode(html) { Width = 0.5f, RelativeSizeAxes = Axes.X });
            AddUntilStep("loaded", () => bbcode.IsLoaded);
        }

        [Test]
        public void TestRendering()
        {
            AddAssert("whitespace collapsed", () => allText().Contains("after empty & line", System.StringComparison.Ordinal));
            AddAssert("empty line has a spacer", () => bbcode.ChildrenOfType<SpriteText>().Any(s => s.Text.ToString() == "|" && s.Colour == Color4.Transparent));
            AddAssert("emoji shown as image", () => bbcode.ChildrenOfType<DrawableDiscordEmoji>().Count(), () => Is.EqualTo(1));
            AddAssert("box title shown", () => allText().Contains("goodbye wave", System.StringComparison.Ordinal));
            AddAssert("no tag text", () => !allText().Contains('[') && !allText().Contains('<'));
            AddAssert("failed image shows no url", () => !allText().Contains("missing.png", System.StringComparison.Ordinal));
            AddAssert("box body hidden", () => !isVisible("hidden"));

            AddStep("open box", () => bbcode.ChildrenOfType<OsuClickableContainer>().First().TriggerClick());
            AddAssert("box body shown", () => isVisible("hidden"));
        }

        private string allText() => string.Concat(bbcode.ChildrenOfType<SpriteText>().Select(s => s.Text.ToString()));

        private bool isVisible(string text)
        {
            Drawable? drawable = bbcode.ChildrenOfType<SpriteText>().Single(s => s.Text.ToString() == text);

            for (; drawable != null && drawable != bbcode; drawable = drawable.Parent)
            {
                if (!drawable.IsPresent)
                    return false;
            }

            return true;
        }
    }
}
