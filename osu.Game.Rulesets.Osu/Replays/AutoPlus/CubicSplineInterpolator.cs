// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.AutoPlus
{
    /// <summary>
    /// Moves the cursor on a clamped cubic spline through the points.
    /// </summary>
    public class CubicSplineInterpolator : IAutoPlusInterpolator
    {
        private const float max_derivative = 5;

        private readonly Vector2 derivativeAtStart;
        private readonly Vector2 derivativeAtEnd;

        private readonly SplineInterpolator xInterpolator;
        private readonly SplineInterpolator yInterpolator;

        public int StartTime { get; }

        public int EndTime { get; }

        public bool Ignored => false;

        public CubicSplineInterpolator(List<PointWithTime> points, Vector2 derivativeAtStart, Vector2 derivativeAtEnd)
        {
            if (points.Count < 2)
                throw new ArgumentException("Cannot interpolate with less than 2 points.", nameof(points));

            StartTime = points[0].Time;
            EndTime = points[^1].Time;

            if (StartTime == EndTime)
                throw new ArgumentException("StartTime must be lower than EndTime.", nameof(points));

            this.derivativeAtStart = clampDerivative(derivativeAtStart);
            this.derivativeAtEnd = clampDerivative(derivativeAtEnd);

            double[] keys = points.Select(p => (double)p.Time).ToArray();

            xInterpolator = new SplineInterpolator(keys, points.Select(p => (double)p.Position.X).ToArray(), this.derivativeAtStart.X, this.derivativeAtEnd.X);
            yInterpolator = new SplineInterpolator(keys, points.Select(p => (double)p.Position.Y).ToArray(), this.derivativeAtStart.Y, this.derivativeAtEnd.Y);
        }

        private static Vector2 clampDerivative(Vector2 derivative)
        {
            float length = derivative.Length;
            return length > max_derivative ? derivative * (max_derivative / length) : derivative;
        }

        public Vector2 PositionAtTime(int time) => new Vector2((float)xInterpolator.GetValue(time), (float)yInterpolator.GetValue(time));

        public Vector2 DerivativeAtStartTime() => derivativeAtStart;

        public Vector2 DerivativeAtEndTime() => derivativeAtEnd;

        private class SplineInterpolator
        {
            private readonly double[] keys;
            private readonly double[] values;
            private readonly double[] h;
            private readonly double[] a;

            public SplineInterpolator(double[] keys, double[] values, double startDerivative, double endDerivative)
            {
                int n = keys.Length;

                this.keys = keys;
                this.values = values;
                h = new double[n];

                for (int i = 1; i < n; i++)
                    h[i] = keys[i] - keys[i - 1];

                double[] sub = new double[n];
                double[] diag = new double[n];
                double[] sup = new double[n];
                double[] b = new double[n];

                diag[0] = 2;
                sup[0] = 1;
                b[0] = 6 * ((values[1] - values[0]) / h[1] - startDerivative) / h[1];

                sub[n - 1] = 1;
                diag[n - 1] = 2;
                sup[n - 1] = 0;
                b[n - 1] = 6 * (endDerivative - (values[n - 1] - values[n - 2]) / h[n - 1]) / h[n - 1];

                for (int i = 1; i <= n - 2; i++)
                {
                    sub[i] = h[i] / 6;
                    diag[i] = (h[i] + h[i + 1]) / 3;
                    sup[i] = h[i + 1] / 6;
                    b[i] = (values[i + 1] - values[i]) / h[i + 1] - (values[i] - values[i - 1]) / h[i];
                }

                solveTridiagonal(sub, diag, sup, b, n);
                a = b;
            }

            private static void solveTridiagonal(double[] sub, double[] diag, double[] sup, double[] b, int n)
            {
                for (int i = 1; i < n; i++)
                {
                    double m = sub[i] / diag[i - 1];
                    diag[i] -= m * sup[i - 1];
                    b[i] -= m * b[i - 1];
                }

                b[n - 1] /= diag[n - 1];

                for (int i = n - 2; i >= 0; i--)
                    b[i] = (b[i] - sup[i] * b[i + 1]) / diag[i];
            }

            public double GetValue(double key)
            {
                int index = Array.BinarySearch(keys, key);
                if (index < 0)
                    index = ~index;

                if (index == 0)
                    return values[0];

                if (index >= keys.Length)
                    return values[^1];

                double t = key - keys[index - 1];
                double s = h[index] - t;

                return ((-a[index - 1] / 6 * (s + h[index]) * t + values[index - 1]) * s + (-a[index] / 6 * (t + h[index]) * s + values[index]) * t) / h[index];
            }
        }
    }
}
