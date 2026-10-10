// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Screens.Edit.Hitsounding;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneHitsoundTooltip : OsuTestScene
    {
        private TextFlowContainer flow = null!;

        [Test]
        public void TestKeywordsHighlighted()
        {
            AddStep("format text", () =>
            {
                Child = flow = new TextFlowContainer { Width = 500, AutoSizeAxes = osu.Framework.Graphics.Axes.Y };
                HitsoundTooltip.AddFormattedText(flow, "Hitsounds for sounds:\nVolume area: right drag to draw, Shift+W/E/R set the bank.\nKick: Soft Whistle at 60%, plain text.");
            });

            AddAssert("title not bold", () => fontWeightOf("Hitsounds for sounds:"), () => Is.EqualTo("SemiBold"));
            AddAssert("title distinguishable from headings", () => colourOf("Hitsounds for sounds:"), () => Is.Not.EqualTo(colourOf("Volume area:")));
            AddAssert("heading bold", () => fontWeightOf("Volume area:"), () => Is.EqualTo("Bold"));
            AddAssert("key combination highlighted", () => colourOf("Shift+W/E/R"), () => Is.Not.EqualTo(colourOf(" set the bank.")));
            AddAssert("mouse action bold", () => fontWeightOf("right drag"), () => Is.EqualTo("SemiBold"));
            AddAssert("bank in its colour", () => colourOf("Soft"), () => Is.EqualTo((ColourInfo)HitsoundLane.GetBankColour(HitSampleInfo.BANK_SOFT)));
            AddAssert("bank not bold", () => fontWeightOf("Soft"), () => Is.EqualTo("Regular"));
            AddAssert("sample not bold", () => fontWeightOf("Whistle"), () => Is.EqualTo("Regular"));
            AddAssert("sample brighter than plain text", () => colourOf("Whistle"), () => Is.Not.EqualTo(colourOf(", plain text.")));
            AddAssert("value bold", () => fontWeightOf("60%"), () => Is.EqualTo("SemiBold"));
            AddAssert("second heading bold", () => fontWeightOf("Kick:"), () => Is.EqualTo("Bold"));
            AddAssert("plain text regular", () => fontWeightOf(", plain text."), () => Is.EqualTo("Regular"));
        }

        /// <summary>
        /// The text flow splits text into words, so the parts of a span are found by their combined text.
        /// </summary>
        private SpriteText[] partsOf(string text)
        {
            var sprites = flow.ChildrenOfType<SpriteText>().ToArray();

            for (int start = 0; start < sprites.Length; start++)
            {
                string combined = string.Empty;

                for (int end = start; end < sprites.Length && combined.Length < text.Length; end++)
                {
                    combined += sprites[end].Text.ToString();

                    if (combined == text)
                        return sprites[start..(end + 1)];
                }
            }

            Assert.Fail($"\"{text}\" wasn't found.");
            return null!;
        }

        private string fontWeightOf(string text)
        {
            var weights = partsOf(text).Select(s => s.Font.Weight).Distinct().ToArray();
            return weights.Length == 1 ? weights[0] ?? string.Empty : "mixed";
        }

        private ColourInfo colourOf(string text) => partsOf(text)[0].Colour;
    }
}
