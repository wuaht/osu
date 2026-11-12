// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays.Discord;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneTextFlowEmoji : OsuTestScene
    {
        private OsuTextFlowContainer flow = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("show text flow", () => Child = flow = new OsuTextFlowContainer { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y });
            AddStep("add text with emoji", () => flow.AddText("hello 💀 world 😀😀"));
            AddUntilStep("loaded", () => flow.ChildrenOfType<DrawableDiscordEmoji>().Any(e => e.IsLoaded));
        }

        [Test]
        public void TestEmojiShownAsImages()
        {
            AddAssert("emoji shown as images", () => flow.ChildrenOfType<DrawableDiscordEmoji>().Count(), () => Is.EqualTo(3));
            AddAssert("no surrogate halves rendered as text", () => !allText().Any(char.IsSurrogate));
            AddAssert("words around emoji kept", () => allText().Contains("hello", System.StringComparison.Ordinal) && allText().Contains("world", System.StringComparison.Ordinal));
        }

        private string allText() => string.Concat(flow.ChildrenOfType<SpriteText>().Select(s => s.Text.ToString()));
    }
}
