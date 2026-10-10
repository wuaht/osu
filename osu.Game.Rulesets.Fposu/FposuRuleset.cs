// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Fposu.Mods;
using osu.Game.Rulesets.Fposu.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Fposu
{
    /// <summary>
    /// osu! in first person for aim training, based on McOsu's FPoSu.
    /// </summary>
    /// <remarks>
    /// Gameplay is the same as in osu!, which is displayed on a screen in front of a first person camera.
    /// The camera is turned with the mouse, and the cursor is where the centre of the view hits the screen.
    /// Everything except the presentation (beatmaps, mods, scoring, difficulty etc.) is taken from the <see cref="OsuRuleset"/>.
    /// </remarks>
    public class FposuRuleset : Ruleset, IRulesetVariant
    {
        public const string SHORT_NAME = "fposu";

        // osu! beatmaps are played as they are, with the configuration and settings of osu!.
        public string BaseRulesetShortName => OsuRuleset.SHORT_NAME;

        /// <summary>
        /// The osu! ruleset, which everything except the presentation is taken from.
        /// </summary>
        private readonly OsuRuleset osuRuleset = new OsuRuleset();

        public override string Description => "FPoSu";

        public override string ShortName => SHORT_NAME;

        public override string PlayingVerb => "Aiming at circles";

        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

        public override Drawable CreateIcon() => new FposuRulesetIcon(this);

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null) => new DrawableFposuRuleset(this, beatmap, mods);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new OsuBeatmapConverter(beatmap, this);

        public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap) => new OsuBeatmapProcessor(beatmap);

        public override ScoreProcessor CreateScoreProcessor() => osuRuleset.CreateScoreProcessor();

        public override HealthProcessor CreateHealthProcessor(double drainStartTime) => osuRuleset.CreateHealthProcessor(drainStartTime);

        public override ScoreMultiplierCalculator CreateScoreMultiplierCalculator(ScoreMultiplierContext context) => osuRuleset.CreateScoreMultiplierCalculator(context);

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new OsuDifficultyCalculator(RulesetInfo, beatmap);

        // like McOsu, FPoSu awards the same performance as osu!.
        public override PerformanceCalculator CreatePerformanceCalculator() => osuRuleset.CreatePerformanceCalculator();

        public override IEnumerable<Mod> GetModsFor(ModType type) => osuRuleset.GetModsFor(type).Select(replaceMod);

        /// <summary>
        /// Replaces mods of osu! which have to work differently in first person.
        /// </summary>
        private static Mod replaceMod(Mod mod)
        {
            switch (mod)
            {
                case MultiMod multiMod:
                    return new MultiMod(multiMod.Mods.Select(replaceMod).ToArray());

                case OsuModFlashlight when mod.GetType() == typeof(OsuModFlashlight):
                    return new FposuModFlashlight();

                case OsuModDepth when mod.GetType() == typeof(OsuModDepth):
                    return new FposuModDepth();

                default:
                    return mod;
            }
        }

        /// <summary>
        /// The ruleset info of the osu! ruleset, whose key bindings are used.
        /// </summary>
        public RulesetInfo OsuRulesetInfo => osuRuleset.RulesetInfo;

        // the key bindings of osu! are used (see DrawableFposuRuleset), so FPoSu has none of its own.
        public override IEnumerable<int> GameplayVariants => Array.Empty<int>();

        public override IConvertibleReplayFrame CreateConvertibleReplayFrame() => new OsuReplayFrame();

        // the configuration and settings of osu! are used (see IRulesetVariant), so FPoSu has none of its own.

        public override ISkin? CreateSkinTransformer(ISkin skin, IBeatmap beatmap) => osuRuleset.CreateSkinTransformer(skin, beatmap);

        public override IEnumerable<HitResult> GetValidHitResults() => osuRuleset.GetValidHitResults();

        public override LocalisableString GetDisplayNameForHitResult(HitResult result) => osuRuleset.GetDisplayNameForHitResult(result);

        public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap) => osuRuleset.CreateStatisticsForScore(score, playableBeatmap);

        public override BeatmapDifficulty GetAdjustedDisplayDifficulty(IBeatmapInfo difficulty, IReadOnlyCollection<Mod> mods) => osuRuleset.GetAdjustedDisplayDifficulty(difficulty, mods);

        public override IEnumerable<RulesetBeatmapAttribute> GetBeatmapAttributesForDisplay(IBeatmapInfo beatmapInfo, IReadOnlyCollection<Mod> mods)
            => osuRuleset.GetBeatmapAttributesForDisplay(beatmapInfo, mods);
    }
}
