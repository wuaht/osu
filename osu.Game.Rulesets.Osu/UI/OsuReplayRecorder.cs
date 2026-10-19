// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class OsuReplayRecorder : ReplayRecorder<OsuAction>
    {
        private readonly Spinner[] spinners;

        public OsuReplayRecorder(Score score, IEnumerable<Spinner>? spinners = null)
            : base(score)
        {
            this.spinners = spinners?.ToArray() ?? [];
        }

        protected override bool RequiresContinuousRecording
        {
            get
            {
                double time = Time.Current;

                foreach (var spinner in spinners)
                {
                    if (time >= spinner.StartTime && time <= spinner.EndTime)
                        return true;
                }

                return false;
            }
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<OsuAction> actions, ReplayFrame previousFrame)
            => new OsuReplayFrame(Time.Current, mousePosition, actions.ToArray());
    }
}
