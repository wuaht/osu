// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Fposu.Tests
{
    /// <summary>
    /// FPoSu plays osu! beatmaps as they are, with the configuration and settings of osu!.
    /// </summary>
    [Category("slop")]
    public partial class TestSceneFposuOsuVariant : OsuTestScene
    {
        [Resolved]
        private IRulesetConfigCache configCache { get; set; } = null!;

        [Test]
        public void TestOsuBeatmapsNotConverted()
        {
            AddAssert("osu! beatmap allowed without conversion", () => osuBeatmap().AllowGameplayWithRuleset(new FposuRuleset().RulesetInfo, false));
            AddAssert("osu! beatmap native", () => osuBeatmap().IsNativeTo(new FposuRuleset().RulesetInfo));
            AddAssert("other beatmaps not allowed without conversion",
                () => new BeatmapInfo(new CatchRuleset().RulesetInfo).AllowGameplayWithRuleset(new FposuRuleset().RulesetInfo, false), () => Is.False);
            AddAssert("FPoSu not native to osu!", () => new BeatmapInfo(new FposuRuleset().RulesetInfo).IsNativeTo(new OsuRuleset().RulesetInfo), () => Is.False);
        }

        [Test]
        public void TestOsuConfigurationUsed()
        {
            AddAssert("same configuration as osu!", () => configCache.GetConfigFor(new FposuRuleset()), () => Is.SameAs(configCache.GetConfigFor(new OsuRuleset())));
            AddAssert("no separate settings", () => new FposuRuleset().CreateSettings(), () => Is.Null);
        }

        private static BeatmapInfo osuBeatmap() => new BeatmapInfo(new OsuRuleset().RulesetInfo);
    }
}
