// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Rulesets.Objects
{
    public static class SliderPathExtensions
    {
        /// <summary>
        /// Snaps the provided <paramref name="hitObject"/>'s duration using the <paramref name="snapProvider"/>.
        /// </summary>
        public static void SnapTo<THitObject>(this THitObject hitObject, IDistanceSnapProvider? snapProvider)
            where THitObject : HitObject, IHasPath, IHasSliderVelocity
        {
            hitObject.Path.ExpectedDistance.Value = snapProvider?.FindSnappedSliderDistance((float)hitObject.Path.CalculatedDistance, hitObject.StartTime, hitObject) ?? hitObject.Path.CalculatedDistance;
        }

        /// <summary>
        /// Reverse the direction of this path.
        /// </summary>
        /// <param name="sliderPath">The <see cref="SliderPath"/>.</param>
        /// <param name="positionalOffset">The positional offset of the resulting path. It should be added to the start position of this path.</param>
        public static void Reverse(this SliderPath sliderPath, out Vector2 positionalOffset)
        {
            var controlPoints = sliderPath.ControlPoints;

            var inheritedLinearPoints = controlPoints.Where(p => sliderPath.PointsInSegment(p)[0].Type == PathType.LINEAR && p.Type == null).ToList();

            // Inherited points after a linear point, as well as the first control point if it inherited,
            // should be treated as linear points, so their types are temporarily changed to linear.
            inheritedLinearPoints.ForEach(p => p.Type = PathType.LINEAR);

            double[] segmentEnds = sliderPath.GetSegmentEnds().ToArray();

            // Remove segments after the end of the slider.
            for (int numSegmentsToRemove = segmentEnds.Count(se => se >= 1) - 1; numSegmentsToRemove > 0 && controlPoints.Count > 0;)
            {
                if (controlPoints.Last().Type is not null)
                {
                    numSegmentsToRemove--;
                    segmentEnds = segmentEnds[..^1];
                }

                controlPoints.RemoveAt(controlPoints.Count - 1);
            }

            // Restore original control point types.
            inheritedLinearPoints.ForEach(p => p.Type = null);

            // Recalculate middle perfect curve control points at the end of the slider path.
            if (controlPoints.Count >= 3 && controlPoints[^3].Type == PathType.PERFECT_CURVE && controlPoints[^2].Type == null && segmentEnds.Any())
            {
                double lastSegmentStart = segmentEnds.Length > 1 ? segmentEnds[^2] : 0;

                // we want to shorten the last perfect segment, preserving its shape, so that its end is consistent with the slider path's end.
                // therefore we also reposition the middle point of the segment to be ideally halfway through its arc.
                // the end of the segment is assumed to be at path position 1 at all times,
                // but the start of the segment cannot be assumed to be at 0 because multi-segment sliders exist.
                controlPoints[^2].Position = sliderPath.PositionAt((lastSegmentStart + 1) / 2);
            }

            sliderPath.reverseControlPoints(out positionalOffset);
        }

        /// <summary>
        /// Extends this path by a mirrored copy of itself, attached at its end.
        /// </summary>
        /// <param name="sliderPath">The <see cref="SliderPath"/>.</param>
        /// <param name="axis">
        /// The direction of the mirror axis, which passes through the path end.
        /// If <c>null</c>, the copy is rotated by 180° around the path end instead (point symmetry).
        /// </param>
        public static void Mirror(this SliderPath sliderPath, Vector2? axis)
        {
            double distance = sliderPath.Distance;

            if (distance <= 0 || axis?.LengthSquared == 0)
                return;

            // reflection across the axis is a reflection along its normal.
            Vector2 normal = axis is Vector2 a ? new Vector2(-a.Y, a.X).Normalized() : Vector2.Zero;

            // reversing twice trims the path to its actual end, the reversed state is the mirrored copy before transformation.
            var path = new SliderPath(sliderPath.ControlPoints.Select(p => new PathControlPoint(p.Position, p.Type)).ToArray(), sliderPath.ExpectedDistance.Value);
            path.Reverse(out Vector2 end);

            var copy = path.ControlPoints.Select(p => new PathControlPoint(end + transform(p.Position), p.Type)).ToArray();

            path.Reverse(out _);

            var points = path.ControlPoints.ToList();
            points[^1].Type = copy[0].Type;
            points.AddRange(copy.Skip(1));

            sliderPath.ControlPoints.Clear();
            sliderPath.ControlPoints.AddRange(points);
            sliderPath.ExpectedDistance.Value = distance * 2;

            Vector2 transform(Vector2 v) => axis == null ? -v : v - 2 * Vector2.Dot(v, normal) * normal;
        }

        /// <summary>
        /// Reverses the order of the provided <see cref="SliderPath"/>'s <see cref="PathControlPoint"/>s.
        /// </summary>
        /// <param name="sliderPath">The <see cref="SliderPath"/>.</param>
        /// <param name="positionalOffset">The positional offset of the resulting path. It should be added to the start position of this path.</param>
        private static void reverseControlPoints(this SliderPath sliderPath, out Vector2 positionalOffset)
        {
            var points = sliderPath.ControlPoints.ToArray();
            positionalOffset = sliderPath.PositionAt(1);

            sliderPath.ControlPoints.Clear();

            PathType? lastType = null;

            for (int i = 0; i < points.Length; i++)
            {
                var p = points[i];
                p.Position -= positionalOffset;

                // propagate types forwards to last null type
                if (i == points.Length - 1)
                {
                    p.Type = lastType;
                    p.Position = Vector2.Zero;
                }
                else if (p.Type != null)
                    (p.Type, lastType) = (lastType, p.Type);

                sliderPath.ControlPoints.Insert(0, p);
            }
        }
    }
}
