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
        /// <summary>
        /// The width of the line in screen-space pixels.
        /// </summary>
        public const float LINE_WIDTH = 1;

        public readonly double StartTime;

        public LineVisualisation(double startTime)
        {
            RelativePositionAxes = Axes.Both;
            RelativeSizeAxes = Axes.Y;

            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopCentre;

            Height = 1;

            X = (float)startTime;
            StartTime = startTime;
        }

        protected override void Update()
        {
            base.Update();
            Width = GetPixelWidth(this);
        }

        /// <summary>
        /// Computes the local width that makes a line exactly <see cref="LINE_WIDTH"/> screen-space pixels wide.
        /// A line thinner than one pixel can fall between pixel centres and is then not drawn at all, depending on its position.
        /// </summary>
        public static float GetPixelWidth(Drawable line)
        {
            if (line.Parent == null)
                return LINE_WIDTH;

            float pixelsPerUnit = (line.Parent.ToScreenSpace(Vector2.UnitX) - line.Parent.ToScreenSpace(Vector2.Zero)).Length;
            return pixelsPerUnit > 0 ? LINE_WIDTH / pixelsPerUnit : LINE_WIDTH;
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
