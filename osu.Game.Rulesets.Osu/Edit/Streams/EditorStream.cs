// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.Streams
{
    /// <summary>
    /// An editor-only stream object: a path with a density for each of its anchors, along which hit circles are laid out.
    /// The hit circles are regular objects of the beatmap, so this is only required to keep the stream editable.
    /// </summary>
    public class EditorStream
    {
        public const double MIN_DENSITY = 0.1;
        public const double MAX_DENSITY = 10;

        internal const int MAX_CIRCLES = 512;

        /// <summary>
        /// The path of the stream, with its start position and time. This is never part of the beatmap.
        /// </summary>
        public readonly Slider Path = new Slider();

        /// <summary>
        /// The beat divisor which determines the time between two consecutive circles.
        /// </summary>
        public int BeatDivisor = 4;

        /// <summary>
        /// The number of circles left out at the start and end of the path, after the stream was cut.
        /// </summary>
        public int TrimStart, TrimEnd;

        /// <summary>
        /// The circles of the stream, in order.
        /// </summary>
        public readonly List<HitCircle> Circles = new List<HitCircle>();

        /// <summary>
        /// The times and positions <see cref="Circles"/> had when the stream last laid them out or accepted their changes.
        /// </summary>
        internal (double Time, Vector2 Position)[] Snapshot = Array.Empty<(double, Vector2)>();

        private readonly Dictionary<PathControlPoint, double> densities = new Dictionary<PathControlPoint, double>();

        private readonly HashSet<PathControlPoint> accelerating = new HashSet<PathControlPoint>();

        /// <summary>
        /// Whether <paramref name="point"/> lies on the path and therefore carries a density.
        /// This is the case for the first and last anchor, and every anchor which starts a new segment.
        /// </summary>
        public bool HasDensity(PathControlPoint point)
        {
            var points = Path.Path.ControlPoints;
            return point.Type != null || (points.Count > 0 && (point == points[0] || point == points[^1]));
        }

        /// <summary>
        /// The density of the circles at <paramref name="point"/>, as a multiple of the regular distance snap spacing.
        /// Anchors without a density of their own continue the density of the previous one.
        /// </summary>
        public double GetDensity(PathControlPoint point)
        {
            if (densities.TryGetValue(point, out double density))
                return density;

            double previous = 1;

            foreach (var p in Path.Path.ControlPoints)
            {
                if (p == point)
                    break;

                if (densities.TryGetValue(p, out double d))
                    previous = d;
            }

            return previous;
        }

        public void SetDensity(PathControlPoint point, double density)
        {
            // anchors without a density of their own would otherwise follow the new density, changing the sections after this one too.
            foreach (var p in Path.Path.ControlPoints.Where(p => p != point && HasDensity(p) && !densities.ContainsKey(p)).ToArray())
                densities[p] = GetDensity(p);

            densities[point] = Math.Clamp(Math.Round(density, 2), MIN_DENSITY, MAX_DENSITY);
        }

        /// <summary>
        /// The next anchor after <paramref name="point"/> which carries a density, or <c>null</c> if <paramref name="point"/> is the last one.
        /// </summary>
        public PathControlPoint? GetNextDensityAnchor(PathControlPoint point)
            => Path.Path.ControlPoints.SkipWhile(p => p != point).Skip(1).FirstOrDefault(HasDensity);

        /// <summary>
        /// Whether the density changes gradually from <paramref name="point"/> to the next anchor which carries a density.
        /// Otherwise, the whole segment starting at <paramref name="point"/> uses its density.
        /// </summary>
        public bool GetAccelerates(PathControlPoint point) => accelerating.Contains(point);

        public void SetAccelerates(PathControlPoint point, bool accelerates)
        {
            if (accelerates)
                accelerating.Add(point);
            else
                accelerating.Remove(point);
        }

        /// <summary>
        /// Whether the density of <paramref name="point"/> affects the layout.
        /// The density of the last anchor is only used as the target of an accelerating segment before it.
        /// </summary>
        public bool UsesDensity(PathControlPoint point)
        {
            if (!HasDensity(point))
                return false;

            var anchors = Path.Path.ControlPoints.Where(HasDensity).ToList();

            if (anchors.Count < 2 || point != anchors[^1])
                return true;

            return GetAccelerates(anchors[^2]);
        }

        /// <summary>
        /// Moves, rotates, scales and mirrors the whole stream. The densities are scaled along, so that the circles stay at the same relative positions.
        /// </summary>
        /// <param name="transform">Maps a position to its transformed position.</param>
        /// <param name="scale">The factor by which distances are scaled.</param>
        public void Transform(Func<Vector2, Vector2> transform, float scale)
        {
            Vector2 newPosition = transform(Path.Position);

            foreach (var point in Path.Path.ControlPoints)
                point.Position = transform(Path.Position + point.Position) - newPosition;

            Path.Position = newPosition;

            if (scale != 1)
            {
                foreach (var point in Path.Path.ControlPoints.Where(HasDensity).ToArray())
                    SetDensity(point, GetDensity(point) * scale);
            }
        }

        /// <summary>
        /// Computes the times and positions of the stream's circles.
        /// </summary>
        public List<(double Time, Vector2 Position)> Generate(ControlPointInfo controlPointInfo, double sliderMultiplier)
        {
            var result = generateUntrimmed(controlPointInfo, sliderMultiplier, out _);
            int start = trimmedStart(result.Count);
            return result.GetRange(start, trimmedEnd(result.Count) - start);
        }

        // at least one circle is kept, so that editing the path of a cut stream never deletes it.
        private int trimmedStart(int count) => Math.Min(TrimStart, count - 1);

        private int trimmedEnd(int count) => Math.Max(trimmedStart(count) + 1, count - TrimEnd);

        /// <summary>
        /// Creates a separate stream which covers only the circles from <paramref name="first"/> to <paramref name="last"/> (indices into <see cref="Generate"/>).
        /// Its path is cut at those circles, and the densities are carried over so that the circles stay in place.
        /// </summary>
        /// <returns>The part, or <c>null</c> if its path would be empty.</returns>
        public EditorStream? CreatePart(int first, int last, ControlPointInfo controlPointInfo, double sliderMultiplier)
        {
            var full = generateUntrimmed(controlPointInfo, sliderMultiplier, out var distances);
            int offset = trimmedStart(full.Count);

            if (first < 0 || offset + last >= full.Count)
                return null;

            double length = Path.Path.Distance;
            double from = distances[offset + first];
            double to = distances[offset + last];

            if (to - from < 0.01)
                return null;

            var anchors = GetDensityAnchors();
            var points = Path.Path.ControlPoints.Select(p => new PathControlPoint(p.Position, p.Type)).ToList();

            // the end is cut first, so that "from" is still measured along the original path.
            if (to < length - 0.01)
                points = cut(points, anchors, to, true);

            points = cut(points, anchors, from, false);

            Vector2 start = points[0].Position;

            foreach (var p in points)
                p.Position -= start;

            points[0].Type ??= PathType.LINEAR;

            var part = new EditorStream { BeatDivisor = BeatDivisor };
            part.Path.Position = Path.Position + start;
            part.Path.StartTime = full[offset + first].Time;
            part.Path.Path.ControlPoints.AddRange(points);

            var stops = getDensityStops(length);

            foreach (var (point, distance) in part.GetDensityAnchors())
            {
                double originalDistance = from + distance;

                // same offset as in generateUntrimmed, for cuts at a circle which lies on an anchor.
                part.densities[point] = Math.Clamp(Math.Round(densityAt(stops, originalDistance + 0.01), 2), MIN_DENSITY, MAX_DENSITY);

                // an anchor inside an accelerating segment keeps accelerating towards the original segment's end.
                int segment = stops.FindLastIndex(s => s.Distance <= originalDistance + 0.01);
                if (segment >= 0 && segment < stops.Count - 1 && stops[segment].Accelerates)
                    part.accelerating.Add(point);
            }

            return part;
        }

        /// <summary>
        /// Cuts <paramref name="points"/> at <paramref name="distance"/>, keeping the part before (<paramref name="keepHead"/>) or after it.
        /// Cuts at an anchor of the original path slice the control points, so that no degenerate segment is left.
        /// </summary>
        private static List<PathControlPoint> cut(List<PathControlPoint> points, List<(PathControlPoint Point, double Distance)> anchors, double distance, bool keepHead)
        {
            if (distance <= 0.01)
                return points;

            int anchor = anchors.FindIndex(a => Math.Abs(a.Distance - distance) <= 0.01);

            if (anchor >= 0)
            {
                // anchors are matched by position, as the points were copied.
                Vector2 position = anchors[anchor].Point.Position;
                int index = points.FindIndex(p => p.Type != null && p.Position == position);

                if (index == -1 && anchor == anchors.Count - 1)
                    index = points.Count - 1;

                if (index >= 0)
                {
                    var sliced = keepHead ? points.Take(index + 1).ToList() : points.Skip(index).ToList();
                    var result = sliced.Select(p => new PathControlPoint(p.Position, p.Type)).ToList();

                    if (keepHead)
                        result[^1].Type = null;

                    return result;
                }
            }

            var (head, tail) = SliderPathSplitter.Split(new SliderPath(points.ToArray()), distance);
            return (keepHead ? head : tail).ToList();
        }

        private List<(double Time, Vector2 Position)> generateUntrimmed(ControlPointInfo controlPointInfo, double sliderMultiplier, out List<double> distances)
        {
            var result = new List<(double, Vector2)>();

            double length = Path.Path.Distance;
            double baseSpacing = 100 * sliderMultiplier / Math.Max(1, BeatDivisor);
            var stops = getDensityStops(length);

            distances = new List<double> { 0 };

            // every circle is exactly one distance snap (times the density) away from the previous one, independent of where the anchors lie.
            // the small offset keeps a circle which lands on an anchor from using the previous segment's density due to floating point inaccuracies.
            while (distances.Count < MAX_CIRCLES)
            {
                double? next = nextDistance(distances[^1], baseSpacing * densityAt(stops, distances[^1] + 0.01), length);

                if (next == null)
                    break;

                distances.Add(next.Value);
            }

            double time = Path.StartTime;

            foreach (double distance in distances)
            {
                if (result.Count > 0)
                    time = NextCircleTime(controlPointInfo, time);

                result.Add((time, Path.Position + Path.Path.PositionAt(length > 0 ? Math.Min(1, distance / length) : 0)));
            }

            return result;
        }

        /// <summary>
        /// The time of the circle following the one at <paramref name="time"/>.
        /// </summary>
        internal double NextCircleTime(ControlPointInfo controlPointInfo, double time) => time + controlPointInfo.TimingPointAt(time).BeatLength / Math.Max(1, BeatDivisor);

        /// <summary>
        /// Finds the first distance along the path after <paramref name="from"/> whose straight distance to the position at <paramref name="from"/> is <paramref name="spacing"/>,
        /// as distance snapping measures straight distances rather than distances along the path.
        /// </summary>
        /// <returns>The distance, or <c>null</c> if the rest of the path is too short.</returns>
        private double? nextDistance(double from, double spacing, double length)
        {
            if (spacing <= 0 || length <= 0)
                return null;

            Vector2 origin = positionAt(from);
            double step = Math.Min(spacing / 8, 2);
            double low = from;

            while (true)
            {
                double high = Math.Min(low + step, length);

                if (Vector2.Distance(positionAt(high), origin) >= spacing)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        double mid = (low + high) / 2;

                        if (Vector2.Distance(positionAt(mid), origin) >= spacing)
                            high = mid;
                        else
                            low = mid;
                    }

                    return high;
                }

                if (high >= length)
                {
                    // floating point inaccuracies would otherwise drop a circle which lies exactly on the end of the path (e.g. of a cut part).
                    return Vector2.Distance(positionAt(length), origin) >= spacing - 0.05 ? length : null;
                }

                low = high;
            }

            Vector2 positionAt(double distance) => Path.Path.PositionAt(distance / length);
        }

        /// <summary>
        /// The distance of <paramref name="position"/> to the path.
        /// </summary>
        internal float DistanceToPath(Vector2 position)
        {
            var vertices = new List<Vector2>();
            Path.Path.GetPathToProgress(vertices, 0, 1);

            Vector2 local = position - Path.Position;

            if (vertices.Count == 0)
                return Vector2.Distance(local, Vector2.Zero);

            float min = Vector2.Distance(local, vertices[0]);

            for (int i = 1; i < vertices.Count; i++)
            {
                Vector2 a = vertices[i - 1];
                Vector2 ab = vertices[i] - a;
                float t = ab.LengthSquared > 0 ? Math.Clamp(Vector2.Dot(local - a, ab) / ab.LengthSquared, 0, 1) : 0;

                min = Math.Min(min, Vector2.Distance(local, a + ab * t));
            }

            return min;
        }

        /// <summary>
        /// Returns the distance along the path of every anchor which carries a density.
        /// </summary>
        public List<(PathControlPoint Point, double Distance)> GetDensityAnchors()
        {
            var points = Path.Path.ControlPoints;
            var anchors = points.Where(HasDensity).ToList();
            double[] segmentEnds = Path.Path.GetSegmentEnds().ToArray();
            double length = Path.Path.Distance;

            var result = new List<(PathControlPoint, double)>();

            for (int i = 0; i < anchors.Count; i++)
                result.Add((anchors[i], i == 0 ? 0 : segmentEnds.ElementAtOrDefault(i - 1) * length));

            return result;
        }

        private List<(double Distance, double Density, bool Accelerates)> getDensityStops(double length)
            => GetDensityAnchors().Select(a => (Math.Min(a.Distance, length), GetDensity(a.Point), GetAccelerates(a.Point))).ToList();

        private static double densityAt(List<(double Distance, double Density, bool Accelerates)> stops, double distance)
        {
            if (stops.Count == 0)
                return 1;

            if (distance <= stops[0].Distance)
                return stops[0].Density;

            for (int i = 1; i < stops.Count; i++)
            {
                if (distance >= stops[i].Distance && i < stops.Count - 1)
                    continue;

                var (fromDistance, fromDensity, accelerates) = stops[i - 1];
                var (toDistance, toDensity, _) = stops[i];

                // segments keep the density of the anchor which starts them, unless they accelerate towards the next one.
                if (!accelerates)
                    return fromDensity;

                if (toDistance - fromDistance <= 0)
                    return toDensity;

                distance = Math.Min(distance, toDistance);

                return fromDensity + (toDensity - fromDensity) * (distance - fromDistance) / (toDistance - fromDistance);
            }

            return stops[^1].Density;
        }

        #region Serialisation

        /// <summary>
        /// Serialises the stream to a line of the <c>[SlopStreams]</c> section.
        /// Format: <c>x,y,startTime,divisor,points,densities,version</c>, where <c>points</c> are <c>type:x:y</c> relative to the start position, separated by <c>|</c>,
        /// and <c>densities</c> are the densities of the respective points, separated by <c>|</c> and empty for points without a density.
        /// A density followed by <c>~</c> accelerates towards the next one.
        /// Lines without a version (written before segments had constant densities) accelerate between all anchors.
        /// </summary>
        public string Serialise()
        {
            var points = Path.Path.ControlPoints;

            string pointsString = string.Join('|', points.Select(p => $"{serialiseType(p.Type)}:{format(p.Position.X)}:{format(p.Position.Y)}"));
            string densitiesString = string.Join('|', points.Select(p => HasDensity(p) ? format(GetDensity(p)) + (GetAccelerates(p) ? "~" : string.Empty) : string.Empty));

            string line = string.Join(',', format(Path.Position.X), format(Path.Position.Y), format(Path.StartTime), BeatDivisor.ToString(CultureInfo.InvariantCulture), pointsString, densitiesString, format_version);

            if (TrimStart != 0 || TrimEnd != 0)
                line += FormattableString.Invariant($",{TrimStart},{TrimEnd}");

            return line;
        }

        /// <summary>
        /// Creates an independent copy of the stream, without its circles.
        /// </summary>
        public EditorStream? Clone() => Parse(Serialise());

        /// <summary>
        /// Parses a line of the <c>[SlopStreams]</c> section. Returns <c>null</c> if the line is invalid.
        /// </summary>
        public static EditorStream? Parse(string line)
        {
            try
            {
                string[] parts = line.Split(',');

                if (parts.Length < 6)
                    return null;

                var stream = new EditorStream { BeatDivisor = Math.Max(1, int.Parse(parts[3], CultureInfo.InvariantCulture)) };

                stream.Path.Position = new Vector2(parseFloat(parts[0]), parseFloat(parts[1]));
                stream.Path.StartTime = double.Parse(parts[2], CultureInfo.InvariantCulture);

                string[] points = parts[4].Split('|');
                string[] pointDensities = parts[5].Split('|');
                bool legacy = parts.Length < 7;

                if (parts.Length >= 9)
                {
                    stream.TrimStart = Math.Max(0, int.Parse(parts[7], CultureInfo.InvariantCulture));
                    stream.TrimEnd = Math.Max(0, int.Parse(parts[8], CultureInfo.InvariantCulture));
                }

                for (int i = 0; i < points.Length; i++)
                {
                    string[] pointParts = points[i].Split(':');

                    if (pointParts.Length < 3)
                        return null;

                    var point = new PathControlPoint(new Vector2(parseFloat(pointParts[1]), parseFloat(pointParts[2])), parseType(pointParts[0]));
                    stream.Path.Path.ControlPoints.Add(point);

                    if (i < pointDensities.Length && pointDensities[i].Length > 0)
                    {
                        string density = pointDensities[i];
                        bool accelerates = legacy || density.EndsWith('~');

                        stream.SetDensity(point, double.Parse(density.TrimEnd('~'), CultureInfo.InvariantCulture));
                        stream.SetAccelerates(point, accelerates);
                    }
                }

                if (stream.Path.Path.ControlPoints.Count < 2)
                    return null;

                // the first anchor always needs a type.
                stream.Path.Path.ControlPoints[0].Type ??= PathType.LINEAR;

                return stream;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        private const string format_version = "2";

        private static string format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static float parseFloat(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        private static string serialiseType(PathType? type)
        {
            if (type == null)
                return string.Empty;

            switch (type.Value.Type)
            {
                case SplineType.Linear:
                    return "L";

                case SplineType.PerfectCurve:
                    return "P";

                case SplineType.Catmull:
                    return "C";

                case SplineType.BSpline:
                    return type.Value.Degree == null ? "B" : $"B{type.Value.Degree.Value.ToString(CultureInfo.InvariantCulture)}";

                default:
                    return "B";
            }
        }

        private static PathType? parseType(string value)
        {
            switch (value)
            {
                case "":
                    return null;

                case "L":
                    return PathType.LINEAR;

                case "P":
                    return PathType.PERFECT_CURVE;

                case "C":
                    return PathType.CATMULL;

                case "B":
                    return PathType.BEZIER;

                default:
                    if (value.StartsWith('B') && int.TryParse(value.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int degree))
                        return PathType.BSpline(degree);

                    return PathType.BEZIER;
            }
        }

        #endregion
    }
}
