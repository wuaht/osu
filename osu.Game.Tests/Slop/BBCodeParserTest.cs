// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Graphics.Containers.BBCode;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class BBCodeParserTest
    {
        [Test]
        public void TestNestedTags()
        {
            var nodes = BBCodeParser.Parse("a [b]bold [i]both[/i][/b] c");

            Assert.That(nodes, Has.Count.EqualTo(3));
            var bold = (BBCodeTag)nodes[1];
            Assert.That(bold.Name, Is.EqualTo("b"));
            Assert.That(((BBCodeTag)bold.Children[1]).Name, Is.EqualTo("i"));
            Assert.That(nodes[2], Is.EqualTo(new BBCodeText(" c")));
        }

        [Test]
        public void TestUnclosedAndUnknownTagsStayText()
        {
            var nodes = BBCodeParser.Parse("[b]open [foo]x[/foo] [color]y[/color]");

            Assert.That(nodes, Is.EqualTo(new BBCodeNode[] { new BBCodeText("[b]open [foo]x[/foo] [color]y[/color]") }));
        }

        [Test]
        public void TestRawContentIsNotParsed()
        {
            var nodes = BBCodeParser.Parse("[code][b]x[/b][/code]");

            var code = (BBCodeTag)nodes[0];
            Assert.That(code.Name, Is.EqualTo("code"));
            Assert.That(code.Children[0], Is.EqualTo(new BBCodeText("[b]x[/b]")));
        }

        [Test]
        public void TestListItems()
        {
            var nodes = BBCodeParser.Parse("[list=1]title[*]one[*]two[/list]");

            var list = (BBCodeTag)nodes[0];
            Assert.That(list.Argument, Is.EqualTo("1"));
            Assert.That(list.Children, Has.Count.EqualTo(3));
            Assert.That(list.Children[0], Is.EqualTo(new BBCodeText("title")));
            Assert.That(((BBCodeTag)list.Children[2]).Children[0], Is.EqualTo(new BBCodeText("two")));
        }

        [Test]
        public void TestUnclosedTagInsideClosedTag()
        {
            var nodes = BBCodeParser.Parse("[centre]a [u]b[/centre]");

            var centre = (BBCodeTag)nodes[0];
            Assert.That(centre.Children, Is.EqualTo(new BBCodeNode[] { new BBCodeText("a [u]b") }));
        }
    }
}
