// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Online.BnTracker;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// A bar showing how many of the relevant nominators are at each status, like the progress bar of beatmap sets on the BN Tracker website.
    /// Nominators who weren't asked yet leave the bar empty.
    /// </summary>
    public partial class StatusProgressBar : CompositeDrawable
    {
        /// <summary>
        /// The statuses shown in the bar, in order. The same order as on the website.
        /// </summary>
        public static readonly BnNominationStatus[] ORDER =
        {
            BnNominationStatus.Accepted,
            BnNominationStatus.Pending,
            BnNominationStatus.Maybe,
            BnNominationStatus.Unlikely,
            BnNominationStatus.Declined,
        };

        private readonly FillFlowContainer segments;

        public StatusProgressBar()
        {
            Masking = true;
            CornerRadius = 3;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0.14f,
                },
                segments = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                },
            };
        }

        /// <param name="statusCounts">How many nominators are at each status.</param>
        /// <param name="total">How many nominators are relevant to the beatmap set.</param>
        public void SetCounts(IReadOnlyDictionary<BnNominationStatus, int> statusCounts, int total)
        {
            segments.Clear();

            if (total <= 0)
                return;

            foreach (var status in ORDER)
            {
                int count = statusCounts.GetValueOrDefault(status);

                if (count <= 0)
                    continue;

                segments.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = (float)count / total,
                    Colour = NominatorsDisplay.GetColour(status),
                });
            }
        }
    }
}
