// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Scoring;
using osu.Game.Users;

namespace osu.Game.Rulesets.Mods
{
    public static class ModExtensions
    {
        public static Score CreateScoreFromReplayData(this ICreateReplayData mod, IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            var replayData = mod.CreateReplayData(beatmap, mods);

            return new Score
            {
                Replay = replayData.Replay,
                ScoreInfo =
                {
                    Date = DateTimeOffset.Now,
                    User = new APIUser
                    {
                        Id = replayData.User.OnlineID,
                        Username = replayData.User.Username,
                        IsBot = replayData.User.IsBot,
                        // miku is from japan.
                        CountryCode = replayData.User.IsBot && replayData.User.Username == SlopAutoplaySettings.MIKU_NAME ? CountryCode.JP : CountryCode.Unknown,
                    }
                }
            };
        }

        public static IEnumerable<Mod> AsOrdered(this IEnumerable<Mod> mods) => mods
                                                                                .OrderBy(m => m.Type)
                                                                                .ThenBy(m => m.Acronym);
    }
}
