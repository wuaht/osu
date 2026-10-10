// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Displays the hitsounds of a slider body as bars on the lanes of "sliderslide" (the hitnormal bank) and "sliderwhistle".
    /// </summary>
    public partial class HitsoundBodyPiece : CompositeDrawable
    {
        private readonly Container<Box> bars;

        public HitsoundBodyPiece()
        {
            RelativeSizeAxes = Axes.Y;

            InternalChild = bars = new Container<Box> { RelativeSizeAxes = Axes.Both };
        }

        public void Apply(HitsoundBody body, IReadOnlyList<HitsoundLane> lanes, float laneHeight)
        {
            Alpha = 1;

            while (bars.Count < lanes.Count)
            {
                bars.Add(new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Origin = Anchor.CentreLeft,
                });
            }

            float height = Math.Clamp(laneHeight * 0.3f, 3, 8);

            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];

                if (i >= lanes.Count || !body.Has(lanes[i]))
                {
                    bar.Alpha = 0;
                    continue;
                }

                bar.Alpha = 0.4f;
                bar.Height = height;
                bar.Y = (i + 0.5f) * laneHeight;
                bar.Colour = HitsoundLane.GetBankColour(lanes[i].Bank);
            }
        }
    }
}
