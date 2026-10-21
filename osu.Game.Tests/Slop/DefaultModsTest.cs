// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class DefaultModsTest
    {
        [Test]
        public void TestRoundTrip()
        {
            string value = DefaultMods.Set(string.Empty, "osu", new Mod[] { new OsuModHidden(), new OsuModDoubleTime { SpeedChange = { Value = 1.3 } } });

            var mods = DefaultMods.Get(value, new OsuRuleset());

            Assert.That(mods, Has.Length.EqualTo(2));
            Assert.That(mods[0], Is.TypeOf<OsuModHidden>());
            Assert.That(((OsuModDoubleTime)mods[1]).SpeedChange.Value, Is.EqualTo(1.3));
        }

        [Test]
        public void TestRulesetsAreSeparate()
        {
            string value = DefaultMods.Set(string.Empty, "osu", new Mod[] { new OsuModHidden() });
            value = DefaultMods.Set(value, "mania", new Mod[] { new ManiaModMirror() });

            Assert.That(DefaultMods.Get(value, new OsuRuleset()).Single(), Is.TypeOf<OsuModHidden>());
            Assert.That(DefaultMods.Get(value, new ManiaRuleset()).Single(), Is.TypeOf<ManiaModMirror>());
        }

        [Test]
        public void TestEmptyModsRemoveDefaults()
        {
            string value = DefaultMods.Set(string.Empty, "osu", new Mod[] { new OsuModHidden() });
            value = DefaultMods.Set(value, "osu", Array.Empty<Mod>());

            Assert.That(value, Is.Empty);
            Assert.That(DefaultMods.Get(value, new OsuRuleset()), Is.Empty);
        }

        [Test]
        public void TestSystemModsAreNotStored()
        {
            string value = DefaultMods.Set(string.Empty, "osu", new Mod[] { new ModTouchDevice() });

            Assert.That(value, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("not json")]
        [TestCase("[1, 2, 3]")]
        [TestCase("{\"osu\": 5}")]
        public void TestInvalidValue(string? value)
        {
            Assert.That(DefaultMods.Get(value, new OsuRuleset()), Is.Empty);
            Assert.That(DefaultMods.Set(value, "osu", new Mod[] { new OsuModHidden() }), Is.Not.Empty);
        }

        [Test]
        public void TestUnknownAndDuplicateModsAreSkipped()
        {
            const string value = "{\"osu\": [{\"acronym\": \"XYZ\"}, {\"acronym\": \"HD\"}, {\"acronym\": \"HD\"}, {\"acronym\": \"TD\"}]}";

            Assert.That(DefaultMods.Get(value, new OsuRuleset()).Single(), Is.TypeOf<OsuModHidden>());
        }

        [Test]
        public void TestIncompatibleModsAreSkipped()
        {
            const string value = "{\"osu\": [{\"acronym\": \"HR\"}, {\"acronym\": \"EZ\"}, {\"acronym\": \"HD\"}]}";

            var mods = DefaultMods.Get(value, new OsuRuleset());

            Assert.That(mods.Select(m => m.Acronym), Is.EqualTo(new[] { "HR", "HD" }));
        }
    }
}
