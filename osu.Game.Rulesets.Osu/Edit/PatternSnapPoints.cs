// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// A position derived from the positions of other objects, which objects can be snapped to in order to complete a pattern.
    /// </summary>
    /// <param name="Position">The position to snap to, in gamefield (osu!pixel) space.</param>
    public abstract record PatternSnapPoint(Vector2 Position)
    {
        /// <summary>
        /// Creates the guide lines visualising the pattern which is completed by an object at <see cref="Position"/>.
        /// </summary>
        public abstract IEnumerable<PatternSnapGuideLine> CreateGuideLines();
    }

    /// <summary>
    /// A line visualising a <see cref="PatternSnapPoint"/>.
    /// </summary>
    /// <param name="Vertices">The vertices of the line, in gamefield (osu!pixel) space.</param>
    /// <param name="Alpha">The opacity of the line.</param>
    public readonly record struct PatternSnapGuideLine(Vector2[] Vertices, float Alpha);

    /// <summary>
    /// The third corner of an equilateral triangle formed with two other objects.
    /// </summary>
    public record VisualSpacingSnapPoint(Vector2 Position, Vector2 First, Vector2 Second, float ObjectRadius) : PatternSnapPoint(Position)
    {
        public override IEnumerable<PatternSnapGuideLine> CreateGuideLines()
        {
            // the edges of the triangle are drawn between the edges of the objects rather than their centres.
            foreach (var (from, to) in new[] { (First, Second), (Second, Position), (Position, First) })
            {
                Vector2 direction = to - from;
                float length = direction.Length;

                if (length - 2 * ObjectRadius < 1)
                    continue;

                direction /= length;

                yield return new PatternSnapGuideLine(new[] { from + direction * ObjectRadius, to - direction * ObjectRadius }, PatternSnapping.GUIDE_LINE_ALPHA);
            }
        }
    }

    /// <summary>
    /// A position on a straight line of equally spaced objects, either continuing the line or filling a gap in it.
    /// </summary>
    /// <param name="Position">The position on the line, in gamefield (osu!pixel) space.</param>
    /// <param name="Line">All positions of the line in order, including <paramref name="Position"/>.</param>
    /// <param name="ObjectRadius">The radius of the objects, which guide lines are drawn between the edges of.</param>
    public record LineSnapPoint(Vector2 Position, Vector2[] Line, float ObjectRadius) : PatternSnapPoint(Position)
    {
        public override IEnumerable<PatternSnapGuideLine> CreateGuideLines()
        {
            Vector2 step = Line[1] - Line[0];
            float spacing = step.Length;

            // overlapping objects leave no space for lines between them, so the line is drawn through their centres instead.
            if (spacing - 2 * ObjectRadius < 1)
            {
                yield return new PatternSnapGuideLine(new[] { Line[0], Line[^1] }, PatternSnapping.GUIDE_LINE_ALPHA * 0.35f);
                yield break;
            }

            Vector2 direction = step / spacing;

            // the lines are drawn between the edges of the objects rather than their centres.
            for (int i = 0; i < Line.Length - 1; i++)
                yield return new PatternSnapGuideLine(new[] { Line[i] + direction * ObjectRadius, Line[i + 1] - direction * ObjectRadius }, PatternSnapping.GUIDE_LINE_ALPHA);
        }
    }

    /// <summary>
    /// The centre of a circular arc of a slider. An object placed there is perfectly blanketed by the slider.
    /// </summary>
    public record BlanketSnapPoint(Vector2 Position, CircularArcProperties Arc) : PatternSnapPoint(Position)
    {
        public override IEnumerable<PatternSnapGuideLine> CreateGuideLines()
        {
            // the full circle hints at the shape of the blanket.
            yield return new PatternSnapGuideLine(CreateArc(Position, Arc.Radius, 0, 2 * Math.PI, 1), PatternSnapping.GUIDE_LINE_ALPHA * 0.35f);

            // the arc of the slider.
            yield return new PatternSnapGuideLine(CreateArc(Position, Arc.Radius, Arc.ThetaStart, Arc.ThetaRange, Arc.Direction), PatternSnapping.GUIDE_LINE_ALPHA);
        }

        /// <summary>
        /// Creates the vertices of a circular arc.
        /// </summary>
        public static Vector2[] CreateArc(Vector2 centre, float radius, double thetaStart, double thetaRange, double direction)
        {
            const double max_step = Math.PI / 90;

            int segments = Math.Max(1, (int)Math.Ceiling(thetaRange / max_step));
            var vertices = new Vector2[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                double theta = thetaStart + direction * thetaRange * i / segments;
                vertices[i] = centre + new Vector2((float)Math.Cos(theta), (float)Math.Sin(theta)) * radius;
            }

            return vertices;
        }
    }

    /// <summary>
    /// The centre of a circular arc of a slider, which the centre of a circular arc of another slider is snapped to, such that one slider blankets the other.
    /// </summary>
    /// <param name="Position">The shared centre of both arcs, in gamefield (osu!pixel) space.</param>
    /// <param name="Arc">The arc of the slider which is snapped to.</param>
    /// <param name="SnappedArc">The arc of the slider which is snapped, with its centre at <paramref name="Position"/>.</param>
    public record SliderBlanketSnapPoint(Vector2 Position, CircularArcProperties Arc, CircularArcProperties SnappedArc) : PatternSnapPoint(Position)
    {
        public override IEnumerable<PatternSnapGuideLine> CreateGuideLines()
        {
            // the full circles hint at the shape of the blanket.
            yield return new PatternSnapGuideLine(BlanketSnapPoint.CreateArc(Position, Arc.Radius, 0, 2 * Math.PI, 1), PatternSnapping.GUIDE_LINE_ALPHA * 0.35f);
            yield return new PatternSnapGuideLine(BlanketSnapPoint.CreateArc(Position, SnappedArc.Radius, 0, 2 * Math.PI, 1), PatternSnapping.GUIDE_LINE_ALPHA * 0.35f);

            // the arcs of both sliders.
            yield return new PatternSnapGuideLine(BlanketSnapPoint.CreateArc(Position, Arc.Radius, Arc.ThetaStart, Arc.ThetaRange, Arc.Direction), PatternSnapping.GUIDE_LINE_ALPHA);
            yield return new PatternSnapGuideLine(BlanketSnapPoint.CreateArc(Position, SnappedArc.Radius, SnappedArc.ThetaStart, SnappedArc.ThetaRange, SnappedArc.Direction),
                PatternSnapping.GUIDE_LINE_ALPHA);
        }
    }

    public static class PatternSnapping
    {
        /// <summary>
        /// The colour of guide lines.
        /// </summary>
        public static readonly Color4 GUIDE_LINE_COLOUR = Color4.Yellow;

        /// <summary>
        /// The opacity of guide lines.
        /// </summary>
        public const float GUIDE_LINE_ALPHA = 0.75f;

        /// <summary>
        /// The minimum distance between two objects, in multiples of the object radius, for them to form a triangle.
        /// Closer objects overlap, which is not visual spacing.
        /// </summary>
        private const float min_visual_spacing_distance = 2;

        /// <summary>
        /// The maximum distance between two objects, in multiples of the object radius, for them to form a triangle.
        /// </summary>
        private const float max_visual_spacing_distance = 5;

        /// <summary>
        /// The minimum distance between two consecutive objects of a line, in multiples of the object radius.
        /// Closer objects are rather stacked than forming a line.
        /// </summary>
        private const float min_line_spacing = 0.5f;

        /// <summary>
        /// The maximum distance between two consecutive objects of a line, in multiples of the object radius.
        /// </summary>
        private const float max_line_spacing = 5;

        /// <summary>
        /// The maximum distance in osu!pixels at which positions are considered equal when looking for objects forming a line.
        /// </summary>
        private const float line_position_tolerance = 1;

        /// <summary>
        /// Adds the third corners of all equilateral triangles which can be formed with two of the given objects.
        /// </summary>
        /// <param name="objects">The objects to form triangles with.</param>
        /// <param name="output">The list to add the snap points to.</param>
        public static void AddVisualSpacingSnapPoints(IReadOnlyList<OsuHitObject> objects, List<PatternSnapPoint> output)
        {
            var positions = getPositions(objects);

            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = i + 1; j < positions.Count; j++)
                {
                    var a = positions[i];
                    var b = positions[j];

                    // a slider's head and tail don't form a pattern with each other.
                    if (a.owner == b.owner)
                        continue;

                    float radius = (float)objects[a.owner].Radius;
                    float distance = Vector2.Distance(a.position, b.position);

                    if (distance < radius * min_visual_spacing_distance - 0.5f || distance > radius * max_visual_spacing_distance)
                        continue;

                    var offset = b.position - a.position;

                    foreach (float angle in new[] { MathF.PI / 3, -MathF.PI / 3 })
                    {
                        Vector2 position = a.position + rotate(offset, angle);

                        if (isInPlayfield(position))
                            output.Add(new VisualSpacingSnapPoint(position, a.position, b.position, radius));
                    }
                }
            }
        }

        /// <summary>
        /// Adds the positions on straight lines of equally spaced objects which aren't occupied yet.
        /// Any two objects form a line, which can be continued indefinitely in both directions by placing further objects at the snap points.
        /// Additionally, the position halfway between two objects completes a line of three objects.
        /// </summary>
        /// <param name="objects">The objects to form lines with.</param>
        /// <param name="output">The list to add the snap points to.</param>
        public static void AddLineSnapPoints(IReadOnlyList<OsuHitObject> objects, List<PatternSnapPoint> output)
        {
            var positions = getPositions(objects);
            var added = new List<LineSnapPoint>();

            // ordered pairs, so that each line is continued in both directions.
            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = 0; j < positions.Count; j++)
                {
                    var a = positions[i];
                    var b = positions[j];

                    // a slider's head and tail don't form a pattern with each other.
                    if (a.owner == b.owner)
                        continue;

                    float radius = (float)objects[a.owner].Radius;
                    Vector2 offset = b.position - a.position;
                    float distance = offset.Length;
                    Vector2 midpoint = (a.position + b.position) / 2;

                    // the gap between two objects is filled with an object halfway between them.
                    // the distance is limited to that of consecutive objects of a line, as most pairs of objects aren't meant to form a line with each other.
                    if (i < j && distance >= 2 * radius * min_line_spacing && distance <= radius * max_line_spacing)
                        tryAdd(midpoint, offset / 2, radius);

                    if (distance < radius * min_line_spacing || distance > radius * max_line_spacing)
                        continue;

                    // objects which aren't next to each other on a line would continue it with a multiple of its spacing.
                    if (containsPosition(positions, midpoint))
                        continue;

                    tryAdd(b.position + offset, offset, radius);
                }
            }

            output.AddRange(added);

            void tryAdd(Vector2 position, Vector2 step, float radius)
            {
                if (!isInPlayfield(position) || containsPosition(positions, position))
                    return;

                var line = new List<Vector2> { position };

                // include all objects which are part of the line, for the guide lines.
                while (line.Count <= positions.Count && containsPosition(positions, line[0] - step))
                    line.Insert(0, line[0] - step);

                while (line.Count <= positions.Count && containsPosition(positions, line[^1] + step))
                    line.Add(line[^1] + step);

                // the same line may be found from multiple pairs of its objects.
                foreach (var existing in added)
                {
                    if (!isSamePosition(existing.Position, position))
                        continue;

                    if ((isSamePosition(existing.Line[0], line[0]) && isSamePosition(existing.Line[^1], line[^1]))
                        || (isSamePosition(existing.Line[0], line[^1]) && isSamePosition(existing.Line[^1], line[0])))
                        return;
                }

                added.Add(new LineSnapPoint(position, line.ToArray(), radius));
            }
        }

        /// <summary>
        /// Adds the centres of all circular arcs of the given sliders.
        /// </summary>
        /// <param name="objects">The objects to find the arcs of sliders in.</param>
        /// <param name="output">The list to add the snap points to.</param>
        public static void AddBlanketSnapPoints(IReadOnlyList<OsuHitObject> objects, List<PatternSnapPoint> output)
        {
            foreach (var hitObject in objects)
            {
                if (hitObject is not Slider slider)
                    continue;

                foreach (var arc in GetArcs(slider))
                {
                    if (isInPlayfield(arc.Centre))
                        output.Add(new BlanketSnapPoint(arc.Centre, arc));
                }
            }
        }

        /// <summary>
        /// Returns the circular arcs of a slider, with their centres in gamefield (osu!pixel) space.
        /// </summary>
        public static IEnumerable<CircularArcProperties> GetArcs(Slider slider)
        {
            var controlPoints = slider.Path.ControlPoints;

            // segments are determined in the same way as in SliderPath.calculatePath(), so that only arcs which are actually displayed as such are considered.
            int start = 0;

            for (int i = 0; i < controlPoints.Count; i++)
            {
                if (controlPoints[i].Type == null && i < controlPoints.Count - 1)
                    continue;

                if (i - start + 1 == 3 && controlPoints[start].Type?.Type == SplineType.PerfectCurve)
                {
                    var arc = new CircularArcProperties(new[] { controlPoints[start].Position, controlPoints[start + 1].Position, controlPoints[i].Position });

                    // the arc is defined relative to the slider's position.
                    if (isDisplayedAsArc(arc))
                        yield return new CircularArcProperties(arc.ThetaStart, arc.ThetaRange, arc.Direction, arc.Radius, slider.Position + arc.Centre);
                }

                start = i;
            }
        }

        /// <summary>
        /// Whether a perfect curve segment is displayed as a circular arc, matching the checks in SliderPath.calculateSubPath().
        /// Otherwise it falls back to a bezier curve.
        /// </summary>
        private static bool isDisplayedAsArc(CircularArcProperties arc)
        {
            if (!arc.IsValid)
                return false;

            int subPoints = (2f * arc.Radius <= 0.1f) ? 2 : Math.Max(2, (int)Math.Ceiling(arc.ThetaRange / (2.0 * Math.Acos(1f - (0.1f / arc.Radius)))));
            return subPoints < 1000;
        }

        /// <summary>
        /// Returns the positions of all objects which can form patterns, along with the index of the object they belong to.
        /// </summary>
        private static List<(Vector2 position, int owner)> getPositions(IReadOnlyList<OsuHitObject> objects)
        {
            var positions = new List<(Vector2 position, int owner)>();

            for (int i = 0; i < objects.Count; i++)
            {
                var hitObject = objects[i];

                if (hitObject is Spinner)
                    continue;

                positions.Add((hitObject.Position, i));

                if (hitObject is Slider slider)
                    positions.Add((slider.Position + slider.Path.PositionAt(1), i));
            }

            return positions;
        }

        private static bool containsPosition(List<(Vector2 position, int owner)> positions, Vector2 position)
        {
            foreach (var p in positions)
            {
                if (isSamePosition(p.position, position))
                    return true;
            }

            return false;
        }

        private static bool isSamePosition(Vector2 a, Vector2 b)
            => Vector2.DistanceSquared(a, b) <= line_position_tolerance * line_position_tolerance;

        private static bool isInPlayfield(Vector2 position)
            => position.X >= 0 && position.Y >= 0 && position.X <= OsuPlayfield.BASE_SIZE.X && position.Y <= OsuPlayfield.BASE_SIZE.Y;

        private static Vector2 rotate(Vector2 vector, float angle)
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);

            return new Vector2(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos);
        }
    }
}
