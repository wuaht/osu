// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders
{
    public partial class SliderEndDragMarker : SmoothPath
    {
        public Action<DragStartEvent>? StartDrag { get; set; }
        public Action<DragEvent>? Drag { get; set; }
        public Action? EndDrag { get; set; }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        /// <summary>
        /// Half of the thickness of the arc.
        /// </summary>
        public const float PATH_RADIUS = 2;

        /// <summary>
        /// The radius of the inner edge of the arc, which determines the gap between the slider end and the arc.
        /// </summary>
        private const float inner_radius = OsuHitObject.OBJECT_RADIUS - 5;

        /// <summary>
        /// The horizontal offset of the arc's centre from the slider end's centre.
        /// The containing drawable should use a padding of <c>ARC_CENTRE_OFFSET - PATH_RADIUS</c> to achieve this.
        /// </summary>
        public const float ARC_CENTRE_OFFSET = 2.5f;

        /// <summary>
        /// The area which counts as hovering the arc, as distances from the arc's centre.
        /// This covers the area of the previously thicker arc (and a bit more), so that the thin arc remains as easy to grab as before.
        /// </summary>
        private const float input_inner_radius = inner_radius - 2;

        private const float input_outer_radius = inner_radius + 12;

        private const float arc_radius = inner_radius + PATH_RADIUS;

        [BackgroundDependencyLoader]
        private void load()
        {
            var path = PathApproximator.CircularArcToPiecewiseLinear([
                new Vector2(0, arc_radius),
                new Vector2(arc_radius, 0),
                new Vector2(0, -arc_radius)
            ]);

            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            PathRadius = PATH_RADIUS;
            Vertices = path;
        }

        /// <summary>
        /// The screen-space bounding box of the visible arc.
        /// </summary>
        /// <remarks>
        /// This is tighter than <see cref="Drawable.ScreenSpaceDrawQuad"/>, whose rotated rectangle can extend well beyond the arc itself.
        /// </remarks>
        public RectangleF ScreenSpaceArcBounds
        {
            get
            {
                // converts from the space of the vertices (where the arc is centred at (0, 0)) to screen space.
                Vector2 offset = PositionInBoundingBox(Vector2.Zero);
                Vector2 toScreenSpace(Vector2 vertexSpacePosition) => ToScreenSpace(vertexSpacePosition + offset);

                const float outer_radius = arc_radius + PATH_RADIUS;

                // the rounded caps at both ends of the arc.
                Vector2 min = Vector2.ComponentMin(toScreenSpace(new Vector2(-PATH_RADIUS, arc_radius)), toScreenSpace(new Vector2(-PATH_RADIUS, -arc_radius)));
                Vector2 max = Vector2.ComponentMax(toScreenSpace(new Vector2(-PATH_RADIUS, arc_radius)), toScreenSpace(new Vector2(-PATH_RADIUS, -arc_radius)));

                // the outer edge of the arc.
                for (int angle = -90; angle <= 90; angle += 5)
                {
                    float radians = float.DegreesToRadians(angle);
                    Vector2 point = toScreenSpace(new Vector2(MathF.Cos(radians), MathF.Sin(radians)) * outer_radius);

                    min = Vector2.ComponentMin(min, point);
                    max = Vector2.ComponentMax(max, point);
                }

                return new RectangleF(min, max - min);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            // convert to the space of the vertices, where the arc is centred at (0, 0).
            Vector2 position = ToLocalSpace(screenSpacePos) - PositionInBoundingBox(Vector2.Zero);

            // only the right half, where the arc is.
            if (position.X < -PATH_RADIUS)
                return false;

            float distance = position.Length;
            return distance >= input_inner_radius && distance <= input_outer_radius;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateState();
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateState();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateState();
            base.OnHoverLost(e);
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            updateState();
            StartDrag?.Invoke(e);
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            updateState();
            base.OnDrag(e);
            Drag?.Invoke(e);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            updateState();
            EndDrag?.Invoke();
            base.OnDragEnd(e);
        }

        protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

        protected override bool OnClick(ClickEvent e) => e.Button == MouseButton.Left;

        private void updateState()
        {
            Colour = IsHovered || IsDragged ? colours.Red : colours.Yellow;
        }
    }
}
