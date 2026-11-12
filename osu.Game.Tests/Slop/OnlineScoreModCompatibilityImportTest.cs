// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Tests.Beatmaps.IO;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Scores.IO;

namespace osu.Game.Tests.Slop
{
    [TestFixture]
    [Category("slop")]
    public class OnlineScoreModCompatibilityImportTest : ImportTest
    {
        [TestCase(5368344333, false)]
        [TestCase(0, true)]
        public void TestHiddenFreezeFrameImport(long onlineId, bool shouldThrow)
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost())
            {
                try
                {
                    var osu = LoadOsuIntoHost(host, true);
                    var beatmap = BeatmapImportHelper.LoadOszIntoOsu(osu, TestResources.GetQuickTestBeatmapForImport()).GetResultSafely();

                    // HD + FR became incompatible after scores with this combination were already submitted online.
                    var toImport = new ScoreInfo
                    {
                        User = new APIUser { Username = "Test user" },
                        BeatmapInfo = beatmap.Beatmaps.First(),
                        Ruleset = new OsuRuleset().RulesetInfo,
                        OnlineID = onlineId,
                        Mods = new Mod[] { new OsuModHidden(), new OsuModFreezeFrame() },
                    };

                    if (shouldThrow)
                        Assert.Throws<InvalidOperationException>(() => ImportScoreTest.LoadScoreIntoOsu(osu, toImport));
                    else
                        Assert.That(ImportScoreTest.LoadScoreIntoOsu(osu, toImport).Mods, Has.Length.EqualTo(2));
                }
                finally
                {
                    host.Exit();
                }
            }
        }
    }
}
