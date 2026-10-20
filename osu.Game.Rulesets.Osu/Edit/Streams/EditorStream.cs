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

        private const int max_circles = 512;

        /// <summary>
        /// The path of the stream, with its start position and time. This is never part of the beatmap.
        /// </summary>
        public readonly Slider Path = new Slider();

        /// <summary>
        /// The beat divisor which determines the time between two consecutive circles.
        /// </summary>
        public int BeatDivisor = 4;

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

        public void SetDensity(PathControlPoint point, double density) => densities[point] = Math.Clamp(Math.Round(density, 2), MIN_DENSITY, MAX_DENSITY);

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
            var result = new List<(double, Vector2)>();

            double length = Path.Path.Distance;
            double baseSpacing = 100 * sliderMultiplier / Math.Max(1, BeatDivisor);
            var stops = getDensityStops(length);

            double distance = 0;
            double time = Path.StartTime;

            while (result.Count < max_circles)
            {
                result.Add((time, Path.Position + Path.Path.PositionAt(length > 0 ? Math.Min(1, distance / length) : 0)));

                distance = step(stops, distance, baseSpacing);

                // small leniency so that a stream which exactly fits its path doesn't lose its last circle to rounding errors.
                if (length <= 0 || distance > length + 0.5)
                    break;

                time += controlPointInfo.TimingPointAt(time).BeatLength / Math.Max(1, BeatDivisor);
            }

            return result;
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

        /// <summary>
        /// Advances <paramref name="distance"/> by the spacing of one circle.
        /// A step which crosses an anchor only uses the density before it for the part up to the anchor,
        /// so that the spacing changes right at the anchor rather than one circle after it.
        /// </summary>
        private static double step(List<(double Distance, double Density, bool Accelerates)> stops, double distance, double baseSpacing)
        {
            // the part of the step which is still to be covered.
            double remaining = 1;

            while (remaining > 0)
            {
                double spacing = baseSpacing * densityAt(stops, distance);
                double nextAnchor = stops.Select(s => s.Distance).Where(d => d > distance + 0.001).DefaultIfEmpty(double.PositiveInfinity).Min();

                if (spacing <= 0 || distance + spacing * remaining <= nextAnchor)
                    return distance + spacing * remaining;

                remaining -= (nextAnchor - distance) / spacing;
                distance = nextAnchor;
            }

            return distance;
        }

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

            return string.Join(',', format(Path.Position.X), format(Path.Position.Y), format(Path.StartTime), BeatDivisor.ToString(CultureInfo.InvariantCulture), pointsString, densitiesString, format_version);
        }

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
