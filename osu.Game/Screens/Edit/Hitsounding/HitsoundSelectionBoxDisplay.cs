// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Displays the time range selected by dragging (<see cref="HitsoundTimeline.SelectionBox"/>).
    /// </summary>
    public partial class HitsoundSelectionBoxDisplay : CompositeDrawable
    {
        [Resolved]
        private HitsoundTimeline timeline { get; set; } = null!;

        private readonly Container box;

        /// <summary>
        /// The vertical extent of the box, if it doesn't cover the whole height (e.g. when selecting a rectangle of lanes).
        /// </summary>
        public Func<(float top, float bottom)?>? VerticalRange { get; init; }

        public HitsoundSelectionBoxDisplay()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = box = new Container
            {
                RelativeSizeAxes = Axes.Y,
                Masking = true,
                BorderThickness = 2,
                BorderColour = Colour4.White,
                Alpha = 0,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.1f,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            if (timeline.SelectionBox.Value is not { } range)
            {
                box.Alpha = 0;
                return;
            }

            float startX = timeline.TimeToX(range.start, DrawWidth);

            box.Alpha = 1;
            box.X = startX;
            box.Width = timeline.TimeToX(range.end, DrawWidth) - startX;

            if (VerticalRange?.Invoke() is { } vertical)
            {
                box.RelativeSizeAxes = Axes.None;
                box.Y = vertical.top;
                box.Height = vertical.bottom - vertical.top;
            }
            else
            {
                box.RelativeSizeAxes = Axes.Y;
                box.Y = 0;
                box.Height = 1;
            }
        }
    }
}
