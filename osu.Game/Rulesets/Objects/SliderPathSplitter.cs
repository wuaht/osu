// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Rulesets.Objects
{
    /// <summary>
    /// Splits a <see cref="SliderPath"/> into two paths which together follow the same shape.
    /// </summary>
    public static class SliderPathSplitter
    {
        /// <summary>
        /// Splits <paramref name="path"/> at <paramref name="distance"/> along it.
        /// </summary>
        /// <remarks>
        /// Linear and perfect curve segments keep their type. Any other segment containing the split is converted to bezier anchors.
        /// </remarks>
        /// <param name="path">The path to split.</param>
        /// <param name="distance">The distance along the path at which to split. Must be within (0, <see cref="SliderPath.Distance"/>).</param>
        /// <returns>The control points of both parts, relative to the start of <paramref name="path"/>.</returns>
        public static (PathControlPoint[] head, PathControlPoint[] tail) Split(SliderPath path, double distance)
        {
            var segments = getSegments(path.ControlPoints);

            var head = new List<Segment>();
            var tail = new List<Segment>();

            double segmentStart = 0;
            bool split = false;

            foreach (var segment in segments)
            {
                if (split)
                {
                    tail.Add(segment);
                    continue;
                }

                double length = new SliderPath(segment.ToControlPoints()).CalculatedDistance;

                if (distance >= segmentStart + length)
                {
                    head.Add(segment);
                    segmentStart += length;
                    continue;
                }

                var (left, right) = splitSegment(segment, distance - segmentStart, length);
                head.AddRange(left);
                tail.AddRange(right);
                split = true;
            }

            if (!split)
            {
                // the split is in the linear extension past the last control point.
                Vector2 splitPosition = path.PositionAt(distance / path.Distance);
                tail.Add(new Segment(PathType.LINEAR, new List<Vector2> { splitPosition, path.PositionAt(1) }));
            }

            return (join(head), join(tail));
        }

        private static List<Segment> getSegments(IList<PathControlPoint> controlPoints)
        {
            var segments = new List<Segment>();
            int start = 0;

            for (int i = 1; i < controlPoints.Count; i++)
            {
                if (controlPoints[i].Type == null && i < controlPoints.Count - 1)
                    continue;

                segments.Add(new Segment(controlPoints[start].Type ?? PathType.LINEAR, controlPoints.Skip(start).Take(i - start + 1).Select(p => p.Position).ToList()));
                start = i;
            }

            return segments;
        }

        private static (List<Segment> left, List<Segment> right) splitSegment(Segment segment, double distance, double length)
        {
            if (segment.Type == PathType.LINEAR)
                return splitLinear(segment, distance);

            if (segment.Type == PathType.PERFECT_CURVE && segment.Points.Count == 3)
            {
                var arc = new CircularArcProperties(segment.Points.ToArray());

                if (arc.IsValid)
                    return splitArc(segment, arc, distance);
            }

            // anything else is split as a series of beziers.
            var beziers = getSegments(BezierConverter.ConvertToModernBezier(segment.ToControlPoints()));

            var left = new List<Segment>();
            var right = new List<Segment>();

            double bezierStart = 0;
            bool split = false;

            foreach (var bezier in beziers)
            {
                if (split)
                {
                    right.Add(bezier);
                    continue;
                }

                double bezierLength = new SliderPath(bezier.ToControlPoints()).CalculatedDistance;

                if (distance >= bezierStart + bezierLength && bezier != beziers[^1])
                {
                    left.Add(bezier);
                    bezierStart += bezierLength;
                    continue;
                }

                double t = findBezierParameter(bezier.Points, Math.Clamp((distance - bezierStart) / bezierLength, 0, 1));
                var (leftPoints, rightPoints) = splitBezier(bezier.Points, t);

                left.Add(new Segment(PathType.BEZIER, leftPoints));
                right.Add(new Segment(PathType.BEZIER, rightPoints));
                split = true;
            }

            return (left, right);
        }

        private static (List<Segment> left, List<Segment> right) splitLinear(Segment segment, double distance)
        {
            var points = segment.Points;
            double travelled = 0;

            for (int i = 1; i < points.Count; i++)
            {
                float lineLength = Vector2.Distance(points[i - 1], points[i]);

                if (travelled + lineLength < distance && i < points.Count - 1)
                {
                    travelled += lineLength;
                    continue;
                }

                Vector2 splitPosition = lineLength > 0
                    ? Vector2.Lerp(points[i - 1], points[i], (float)Math.Clamp((distance - travelled) / lineLength, 0, 1))
                    : points[i];

                var left = points.Take(i).Append(splitPosition).ToList();
                var right = points.Skip(i).Prepend(splitPosition).ToList();

                return (new List<Segment> { new Segment(PathType.LINEAR, left) }, new List<Segment> { new Segment(PathType.LINEAR, right) });
            }

            throw new InvalidOperationException("A linear segment must have at least two points.");
        }

        private static (List<Segment> left, List<Segment> right) splitArc(Segment segment, CircularArcProperties arc, double distance)
        {
            double splitTheta = arc.ThetaStart + arc.Direction * distance / arc.Radius;

            Vector2 pointAt(double theta) => arc.Centre + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * arc.Radius;

            Vector2 splitPosition = pointAt(splitTheta);

            var left = new List<Vector2> { segment.Points[0], pointAt((arc.ThetaStart + splitTheta) / 2), splitPosition };
            var right = new List<Vector2> { splitPosition, pointAt((splitTheta + arc.ThetaEnd) / 2), segment.Points[2] };

            return (new List<Segment> { new Segment(PathType.PERFECT_CURVE, left) }, new List<Segment> { new Segment(PathType.PERFECT_CURVE, right) });
        }

        /// <summary>
        /// Finds the bezier parameter at which the given proportion of the curve's length has been travelled.
        /// </summary>
        private static double findBezierParameter(List<Vector2> points, double proportion)
        {
            const int steps = 1000;

            double[] lengths = new double[steps + 1];
            Vector2 previous = points[0];

            for (int i = 1; i <= steps; i++)
            {
                Vector2 current = evaluateBezier(points, (double)i / steps);
                lengths[i] = lengths[i - 1] + Vector2.Distance(previous, current);
                previous = current;
            }

            double target = proportion * lengths[steps];

            for (int i = 1; i <= steps; i++)
            {
                if (lengths[i] < target)
                    continue;

                double stepLength = lengths[i] - lengths[i - 1];
                double stepProportion = stepLength > 0 ? (target - lengths[i - 1]) / stepLength : 0;

                return (i - 1 + stepProportion) / steps;
            }

            return 1;
        }

        private static Vector2 evaluateBezier(List<Vector2> points, double t) => splitBezier(points, t).left[^1];

        /// <summary>
        /// Splits a bezier curve at <paramref name="t"/> using De Casteljau's algorithm.
        /// </summary>
        private static (List<Vector2> left, List<Vector2> right) splitBezier(List<Vector2> points, double t)
        {
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            var current = points.ToArray();

            for (int level = current.Length; level > 0; level--)
            {
                left.Add(current[0]);
                right.Insert(0, current[level - 1]);

                for (int i = 0; i < level - 1; i++)
                    current[i] = Vector2.Lerp(current[i], current[i + 1], (float)t);
            }

            return (left, right);
        }

        private static PathControlPoint[] join(List<Segment> segments)
        {
            var result = new List<PathControlPoint>();

            foreach (var segment in segments)
            {
                result.Add(new PathControlPoint(segment.Points[0], segment.Type));
                result.AddRange(segment.Points.Skip(1).SkipLast(1).Select(p => new PathControlPoint(p)));
            }

            result.Add(new PathControlPoint(segments[^1].Points[^1]));

            return result.ToArray();
        }

        private class Segment
        {
            public readonly PathType Type;
            public readonly List<Vector2> Points;

            public Segment(PathType type, List<Vector2> points)
            {
                Type = type;
                Points = points;
            }

            public PathControlPoint[] ToControlPoints() =>
                Points.Select((p, i) => new PathControlPoint(p, i == 0 ? Type : null)).ToArray();
        }
    }
}
