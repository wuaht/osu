// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The beat snap ticks of the current beat divisor, like in the timeline.
    /// </summary>
    public partial class HitsoundTicks : CompositeDrawable
    {
        /// <summary>
        /// Ticks which would be closer than this many pixels to each other aren't displayed, except for bar lines.
        /// </summary>
        private const float min_tick_spacing = 5;

        [Resolved]
        private HitsoundTimeline timeline { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private readonly float opacity;

        /// <param name="opacity">The opacity of the ticks.</param>
        public HitsoundTicks(float opacity = 1)
        {
            this.opacity = opacity;

            RelativeSizeAxes = Axes.Both;
        }

        protected override void Update()
        {
            base.Update();

            int used = 0;

            double start = timeline.XToTime(-2, DrawWidth);
            double end = timeline.XToTime(DrawWidth + 2, DrawWidth);

            var timingPoints = editorBeatmap.ControlPointInfo.TimingPoints;
            int divisor = beatDivisor.Value;

            for (int i = 0; i < timingPoints.Count; i++)
            {
                var point = timingPoints[i];
                double until = i + 1 < timingPoints.Count ? timingPoints[i + 1].Time : clock.TrackLength;

                if (until < start)
                    continue;

                if (point.Time > end)
                    break;

                double step = point.BeatLength / divisor;

                // avoid iterating over a huge number of ticks for extremely short beat lengths.
                if (step * timeline.CurrentZoom < 0.05)
                    continue;

                int barLength = point.TimeSignature.Numerator * divisor;
                int beat = Math.Max(0, (int)Math.Floor((start - point.Time) / step));

                for (double t = point.Time + beat * step; t < until && t <= end; beat++, t = point.Time + beat * step)
                {
                    bool isBarLine = beat % barLength == 0;
                    int tickDivisor = BindableBeatDivisor.GetDivisorForBeatIndex(beat, divisor);

                    if (!isBarLine && point.BeatLength / tickDivisor * timeline.CurrentZoom < min_tick_spacing)
                        continue;

                    var tick = getTick(used++);

                    tick.X = timeline.TimeToX(t, DrawWidth);
                    tick.Width = isBarLine ? 2 : 1;
                    tick.Colour = BindableBeatDivisor.GetColourFor(tickDivisor, colours);
                    tick.Alpha = opacity * (isBarLine ? 0.7f : tickDivisor == 1 ? 0.45f : 0.25f);
                }
            }

            for (int i = used; i < InternalChildren.Count; i++)
                InternalChildren[i].Alpha = 0;
        }

        private Drawable getTick(int index)
        {
            if (index < InternalChildren.Count)
                return InternalChildren[index];

            var tick = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Origin = Anchor.TopCentre,
            };

            AddInternal(tick);
            return tick;
        }
    }
}
