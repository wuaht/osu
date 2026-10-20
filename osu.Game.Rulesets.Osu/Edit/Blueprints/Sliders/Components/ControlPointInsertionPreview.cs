// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders.Components
{
    /// <summary>
    /// Previews where a new control point would be inserted into a slider's path, and the resulting path.
    /// </summary>
    public partial class ControlPointInsertionPreview : CompositeDrawable
    {
        private static readonly Color4 guide_colour = new Color4(255, 255, 0, 255);

        private const float marker_size = 6;

        private readonly Slider slider;

        private readonly ManualSliderBody body;
        private readonly SmoothPath guideLine;
        private readonly Box previousMarker;
        private readonly Box nextMarker;

        private readonly List<Vector2> vertices = new List<Vector2>();

        [Resolved]
        private IDistanceSnapProvider? distanceSnapProvider { get; set; }

        public ControlPointInsertionPreview(Slider slider)
        {
            this.slider = slider;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChildren = new Drawable[]
            {
                body = new ManualSliderBody
                {
                    AccentColour = Color4.Transparent,
                    BorderColour = Color4.White,
                    BorderSize = 0.8f,
                },
                guideLine = new SmoothPath
                {
                    PathRadius = 1,
                    Colour = guide_colour,
                },
                previousMarker = createMarker(),
                nextMarker = createMarker(),
            };
        }

        private static Box createMarker() => new Box
        {
            Origin = Anchor.Centre,
            Size = new Vector2(marker_size),
            Colour = guide_colour,
        };

        /// <summary>
        /// Finds the index at which a control point at <paramref name="position"/> would be inserted.
        /// </summary>
        /// <param name="controlPoints">The control points of the path.</param>
        /// <param name="position">The position of the new control point, relative to the start of the path.</param>
        /// <param name="atEnd">Whether to insert at the start or end of the path (whichever is closer) rather than into the closest segment.</param>
        public static int GetInsertionIndex(IReadOnlyList<PathControlPoint> controlPoints, Vector2 position, bool atEnd = false)
        {
            if (atEnd && controlPoints.Count > 0)
            {
                return Vector2.DistanceSquared(position, controlPoints[0].Position) < Vector2.DistanceSquared(position, controlPoints[^1].Position)
                    ? 0
                    : controlPoints.Count;
            }

            int insertionIndex = 0;
            float minDistance = float.MaxValue;

            for (int i = 0; i < controlPoints.Count - 1; i++)
            {
                float dist = new Line(controlPoints[i].Position, controlPoints[i + 1].Position).DistanceToPoint(position);

                if (dist < minDistance)
                {
                    insertionIndex = i + 1;
                    minDistance = dist;
                }
            }

            return insertionIndex;
        }

        /// <summary>
        /// Inserts <paramref name="controlPoint"/> into <paramref name="controlPoints"/> at <paramref name="index"/>.
        /// When inserting a new head, it takes over the type of the previous head so that it extends the first segment.
        /// </summary>
        public static void Insert(IList<PathControlPoint> controlPoints, int index, PathControlPoint controlPoint)
        {
            if (index == 0 && controlPoints.Count > 0)
            {
                controlPoint.Type = controlPoints[0].Type;
                controlPoints[0].Type = null;
            }

            controlPoints.Insert(index, controlPoint);
        }

        /// <summary>
        /// Shows the preview for a control point inserted at <paramref name="cursorPosition"/>.
        /// </summary>
        /// <param name="cursorPosition">The cursor position, in the parent's (playfield) space.</param>
        /// <param name="atEnd">Whether the control point is inserted at the start or end of the path (see <see cref="GetInsertionIndex"/>).</param>
        public void UpdatePreview(Vector2 cursorPosition, bool atEnd = false)
        {
            var controlPoints = slider.Path.ControlPoints;

            if (controlPoints.Count < 2)
            {
                Alpha = 0;
                return;
            }

            Vector2 position = cursorPosition - slider.Position;
            int insertionIndex = GetInsertionIndex(controlPoints, position, atEnd);

            var newControlPoints = controlPoints.Select(p => new PathControlPoint(p.Position, p.Type)).ToList();
            Insert(newControlPoints, insertionIndex, new PathControlPoint(position));

            var newPath = new SliderPath(newControlPoints.ToArray());
            newPath.ExpectedDistance.Value = distanceSnapProvider?.FindSnappedDistance((float)newPath.CalculatedDistance, slider.StartTime, slider) ?? newPath.CalculatedDistance;

            vertices.Clear();
            newPath.GetPathToProgress(vertices, 0, 1);

            Vector2 offset = slider.StackedPosition;

            body.PathRadius = (float)slider.Radius;
            body.SetVertices(vertices);
            body.Position = offset;
            body.OriginPosition = body.PathOffset;

            // when inserting at either end, there's only a neighbour on one side.
            Vector2? previous = insertionIndex > 0 ? controlPoints[insertionIndex - 1].Position : null;
            Vector2? next = insertionIndex < controlPoints.Count ? controlPoints[insertionIndex].Position : null;

            guideLine.ClearVertices();
            if (previous != null)
                guideLine.AddVertex(previous.Value);
            guideLine.AddVertex(position);
            if (next != null)
                guideLine.AddVertex(next.Value);
            guideLine.Position = offset;
            guideLine.OriginPosition = guideLine.PositionInBoundingBox(Vector2.Zero);

            previousMarker.Alpha = previous != null ? 1 : 0;
            previousMarker.Position = offset + (previous ?? Vector2.Zero);
            nextMarker.Alpha = next != null ? 1 : 0;
            nextMarker.Position = offset + (next ?? Vector2.Zero);

            Alpha = 1;
        }
    }
}
