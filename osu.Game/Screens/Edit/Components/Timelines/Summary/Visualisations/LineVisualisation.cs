// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace osu.Game.Screens.Edit.Components.Timelines.Summary.Visualisations
{
    /// <summary>
    /// Represents a singular point on a timeline part as a thin vertical line spanning the full height of its parent,
    /// which allows reading its position more precisely than a rounded marker.
    /// </summary>
    public partial class LineVisualisation : Box
    {
        public const float LINE_WIDTH = 0.5f;

        public readonly double StartTime;

        public LineVisualisation(double startTime)
        {
            RelativePositionAxes = Axes.Both;
            RelativeSizeAxes = Axes.Y;

            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopCentre;

            Width = LINE_WIDTH;
            Height = 1;

            X = (float)startTime;
            StartTime = startTime;
        }

        /// <summary>
        /// Extra horizontal area around the line which still counts as hovering it, so that tooltips remain easy to reach despite the thin line.
        /// </summary>
        private const float hover_leniency = 3;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            var localPosition = ToLocalSpace(screenSpacePos);

            return localPosition.Y >= 0 && localPosition.Y <= DrawHeight
                                        && localPosition.X >= -hover_leniency && localPosition.X <= DrawWidth + hover_leniency;
        }
    }
}
