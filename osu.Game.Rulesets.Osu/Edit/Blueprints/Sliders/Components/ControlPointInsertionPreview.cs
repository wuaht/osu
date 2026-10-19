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
        public static int GetInsertionIndex(IReadOnlyList<PathControlPoint> controlPoints, Vector2 position)
        {
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
        /// Shows the preview for a control point inserted at <paramref name="cursorPosition"/>.
        /// </summary>
        /// <param name="cursorPosition">The cursor position, in the parent's (playfield) space.</param>
        public void UpdatePreview(Vector2 cursorPosition)
        {
            var controlPoints = slider.Path.ControlPoints;

            if (controlPoints.Count < 2)
            {
                Alpha = 0;
                return;
            }

            Vector2 position = cursorPosition - slider.Position;
            int insertionIndex = GetInsertionIndex(controlPoints, position);

            var newControlPoints = controlPoints.Select(p => new PathControlPoint(p.Position, p.Type)).ToList();
            newControlPoints.Insert(insertionIndex, new PathControlPoint(position));

            var newPath = new SliderPath(newControlPoints.ToArray());
            newPath.ExpectedDistance.Value = distanceSnapProvider?.FindSnappedDistance((float)newPath.CalculatedDistance, slider.StartTime, slider) ?? newPath.CalculatedDistance;

            vertices.Clear();
            newPath.GetPathToProgress(vertices, 0, 1);

            Vector2 offset = slider.StackedPosition;

            body.PathRadius = (float)slider.Radius;
            body.SetVertices(vertices);
            body.Position = offset;
            body.OriginPosition = body.PathOffset;

            Vector2 previous = controlPoints[insertionIndex - 1].Position;
            Vector2 next = controlPoints[insertionIndex].Position;

            guideLine.ClearVertices();
            guideLine.AddVertex(previous);
            guideLine.AddVertex(position);
            guideLine.AddVertex(next);
            guideLine.Position = offset;
            guideLine.OriginPosition = guideLine.PositionInBoundingBox(Vector2.Zero);

            previousMarker.Position = offset + previous;
            nextMarker.Position = offset + next;

            Alpha = 1;
        }
    }
}
