// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Screens.Edit.Setup;
using osu.Game.Utils;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class EditorSetupValuesTest
    {
        [TestCase(1, 0.5, 0.5)]
        [TestCase(0.5, 1, 1)]
        [TestCase(1, 1.5, 2)]
        [TestCase(2, 1.5, 1)]
        [TestCase(2, 2.5, 3)]
        [TestCase(3, 2.5, 2)]
        [TestCase(1, 0.1, 0.5)]
        [TestCase(3, 10, 4)]
        public void TestTickRate(double initial, double newValue, double expected)
        {
            var bindable = DifficultySection.CreateTickRateBindable(initial);
            bindable.Value = newValue;

            Assert.That(bindable.Value, Is.EqualTo(expected));
        }

        [TestCase(0.5)]
        [TestCase(1.5)]
        [TestCase(3)]
        [TestCase(4)]
        public void TestTickRateIsKeptOnCreation(double tickRate)
            => Assert.That(DifficultySection.CreateTickRateBindable(tickRate).Value, Is.EqualTo(tickRate));

        [Test]
        public void TestRandomColoursKeepLockedColours()
        {
            var colours = new[] { Colour4.Red, Colour4.Green, Colour4.Blue };
            var candidates = new[] { Colour4.White, Colour4.Black, Colour4.Yellow, Colour4.Cyan };

            var result = BackgroundComboColourExtractor.PickRandomColours(colours, i => i == 1, candidates, new Random(1234));

            Assert.That(result, Has.Length.EqualTo(3));
            Assert.That(result[1], Is.EqualTo(Colour4.Green));
            Assert.That(candidates, Does.Contain(result[0]));
            Assert.That(candidates, Does.Contain(result[2]));
        }

        [Test]
        public void TestRandomColoursAvoidDuplicates()
        {
            var colours = new[] { Colour4.Red, Colour4.Green, Colour4.Blue, Colour4.Gray };
            var candidates = new[] { Colour4.White, Colour4.Black, Colour4.Yellow, Colour4.Cyan, Colour4.Magenta };

            for (int seed = 0; seed < 50; seed++)
            {
                var result = BackgroundComboColourExtractor.PickRandomColours(colours, _ => false, candidates, new Random(seed));
                Assert.That(result.Distinct().Count(), Is.EqualTo(result.Length), $"seed {seed}");
            }
        }

        [Test]
        public void TestRandomColoursWithoutCandidates()
        {
            var colours = new[] { Colour4.Red, Colour4.Green };

            Assert.That(BackgroundComboColourExtractor.PickRandomColours(colours, _ => false, Array.Empty<Colour4>(), new Random()), Is.EqualTo(colours));
        }

        [Test]
        public void TestRandomColoursAllLocked()
        {
            var colours = new[] { Colour4.Red, Colour4.Green };

            Assert.That(BackgroundComboColourExtractor.PickRandomColours(colours, _ => true, new[] { Colour4.White }, new Random()), Is.EqualTo(colours));
        }
    }
}
