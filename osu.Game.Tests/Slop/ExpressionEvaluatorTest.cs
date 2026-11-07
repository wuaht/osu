// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Utils;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    [SetCulture("en-US")]
    public class ExpressionEvaluatorTest
    {
        [TestCase("5", 5, true)]
        [TestCase("-0.5", -0.5, true)]
        [TestCase("(-2)", -2, true)]
        [TestCase("1.5e2", 150, true)]
        [TestCase("1 + 2 * 3", 7, false)]
        [TestCase("(1 + 2) * 3", 9, false)]
        [TestCase("10 / 4", 2.5, false)]
        [TestCase("7 % 4", 3, false)]
        [TestCase("--5", 5, false)]
        [TestCase("+5", 5, false)]
        [TestCase("sqrt(16) + POW(2, 3)", 12, false)]
        [TestCase("Max(1, Min(5, 3))", 3, false)]
        [TestCase("Round(2.25, 1)", 2.2, false)]
        public void TestValid(string text, double expected, bool literal)
        {
            Assert.That(ExpressionEvaluator.TryEvaluate(text, false, out double result, out bool isLiteral), Is.True);
            Assert.That(result, Is.EqualTo(expected).Within(1e-9));
            Assert.That(isLiteral, Is.EqualTo(literal));
        }

        [TestCase("/5", 72)]
        [TestCase("1/4", 90)]
        [TestCase("-1/4", -90)]
        [TestCase("360/5", 72)]
        [TestCase("0.5", 0.5)] // literals are taken as degrees
        [TestCase("0.5 * 1", 180)]
        [TestCase("+0.5", 180)]
        [TestCase("1 - 1", 0)]
        public void TestAngleMode(string text, double expected)
        {
            Assert.That(ExpressionEvaluator.TryEvaluate(text, true, out double result, out _), Is.True);
            Assert.That(result, Is.EqualTo(expected).Within(1e-9));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("/5")] // shorthand only exists for angles
        [TestCase("1 +")]
        [TestCase("(1")]
        [TestCase("1 / 0")]
        [TestCase("foo(1)")]
        [TestCase("sqrt(1, 2)")]
        [TestCase("Round(1, 99)")]
        [TestCase("1 2")]
        public void TestInvalid(string text)
        {
            Assert.That(ExpressionEvaluator.TryEvaluate(text, false, out _, out _), Is.False);
        }

        [Test]
        [SetCulture("de-DE")]
        public void TestPlainNumberUsesCurrentCulture()
        {
            Assert.That(ExpressionEvaluator.TryEvaluate("1,5", false, out double result, out bool isLiteral), Is.True);
            Assert.That(result, Is.EqualTo(1.5));
            Assert.That(isLiteral, Is.True);

            Assert.That(ExpressionEvaluator.TryEvaluate("1.5 * 2", false, out result, out _), Is.True);
            Assert.That(result, Is.EqualTo(3));
        }
    }
}
