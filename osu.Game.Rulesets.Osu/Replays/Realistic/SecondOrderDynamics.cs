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

        private Vector2? previousTarget;

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
            if (dt <= 0)
                return;

            Vector2 targetVelocity = previousTarget.HasValue ? (target - previousTarget.Value) / dt : Vector2.Zero;
            previousTarget = target;

            // keeps the simulation stable for large time steps.
            float k2Stable = MathF.Max(k2, MathF.Max(dt * dt / 2 + dt * k1 / 2, dt * k1));

            Position += dt * Velocity;
            Velocity += dt * (target + k3 * targetVelocity - Position - k1 * Velocity) / k2Stable;
        }

        /// <summary>
        /// Forgets the previous target, so that a jump to a new target doesn't give the position a sudden kick.
        /// </summary>
        public void ResetTarget() => previousTarget = null;

        /// <summary>
        /// Moves the position onto the edge of the given circle if it is outside of it, and stops any movement away from the circle.
        /// </summary>
        public void ConstrainToCircle(Vector2 centre, float radius)
        {
            Vector2 offset = Position - centre;
            float distance = offset.Length;

            if (distance <= radius)
                return;

            Vector2 normal = offset / distance;
            Position = centre + normal * radius;

            float outwardSpeed = Vector2.Dot(Velocity, normal);
            if (outwardSpeed > 0)
                Velocity -= normal * outwardSpeed;
        }
    }

    /// <param name="Frequency">How fast the position reacts to target changes, in hertz.</param>
    /// <param name="Damping">How fast oscillations settle. Values below 1 overshoot the target.</param>
    /// <param name="Response">How the position initially reacts to target changes. Negative values anticipate the movement, values above 1 overshoot.</param>
    public readonly record struct DynamicsParameters(float Frequency, float Damping, float Response);
}
