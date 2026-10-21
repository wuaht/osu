// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class ConvertTestPlayTest
    {
        private static readonly RulesetInfo[] all_rulesets =
        {
            new OsuRuleset().RulesetInfo,
            new TaikoRuleset().RulesetInfo,
            new CatchRuleset().RulesetInfo,
            new ManiaRuleset().RulesetInfo,
        };

        [Test]
        public void TestOsuBeatmapConvertsToOtherRulesets()
        {
            var beatmap = new Beatmap
            {
                BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo },
                HitObjects = { new HitCircle { StartTime = 1000, Position = new Vector2(100) } },
            };

            var targets = Editor.GetConvertTargets(beatmap, all_rulesets).Select(r => r.ShortName);

            Assert.That(targets, Is.EquivalentTo(new[] { "taiko", "fruits", "mania" }));
        }

        [Test]
        public void TestOtherRulesetBeatmapHasNoConverts()
        {
            var beatmap = new Beatmap
            {
                BeatmapInfo = { Ruleset = new TaikoRuleset().RulesetInfo },
                HitObjects = { new Hit { StartTime = 1000 } },
            };

            Assert.That(Editor.GetConvertTargets(beatmap, all_rulesets), Is.Empty);
        }

        [Test]
        public void TestUnavailableRulesetIsSkipped()
        {
            var beatmap = new Beatmap
            {
                BeatmapInfo = { Ruleset = new OsuRuleset().RulesetInfo },
                HitObjects = { new HitCircle { StartTime = 1000, Position = new Vector2(100) } },
            };

            var unavailableMania = new ManiaRuleset().RulesetInfo;
            unavailableMania.Available = false;

            var targets = Editor.GetConvertTargets(beatmap, new[] { new OsuRuleset().RulesetInfo, unavailableMania }).ToArray();

            Assert.That(targets, Is.Empty);
        }
    }
}
