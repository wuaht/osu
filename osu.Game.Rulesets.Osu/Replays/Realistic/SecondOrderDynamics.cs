// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.Replays.Realistic
{
    /// <summary>
    /// A position which follows a target like a damped spring.
    /// </summary>
    /// <remarks>
    /// See https://www.youtube.com/watch?v=KPoeNZZ6H4s for an explanation of the parameters.
    /// </remarks>
    public class SecondOrderDynamics
    {
        public Vector2 Position { get; set; }

        public Vector2 Velocity { get; set; }

        private float k1;
        private float k2;
        private float k3;

        public SecondOrderDynamics(Vector2 position)
        {
            Position = position;
            Parameters = new DynamicsParameters(1, 1, 0);
        }

        public DynamicsParameters Parameters
        {
            set
            {
                float angularFrequency = 2 * MathF.PI * value.Frequency;

                k1 = value.Damping / (MathF.PI * value.Frequency);
                k2 = 1 / (angularFrequency * angularFrequency);
                k3 = value.Response * value.Damping / angularFrequency;
            }
        }

        /// <summary>
        /// Moves the position towards <paramref name="target"/>.
        /// </summary>
        /// <param name="dt">The elapsed time, in seconds.</param>
        /// <param name="target">The target position.</param>
        public void Update(float dt, Vector2 target)
        {
            // keeps the simulation stable for large time steps.
            float k2Stable = MathF.Max(k2, MathF.Max(dt * dt / 2 + dt * k1 / 2, dt * k1));

            Position += dt * Velocity;
            Velocity += dt * (target + k3 * Velocity - Position - k1 * Velocity) / k2Stable;
        }
    }

    /// <param name="Frequency">How fast the position reacts to target changes, in hertz.</param>
    /// <param name="Damping">How fast oscillations settle. Values below 1 overshoot the target.</param>
    /// <param name="Response">How the position initially reacts to target changes. Negative values anticipate the movement, values above 1 overshoot.</param>
    public readonly record struct DynamicsParameters(float Frequency, float Damping, float Response);
}
