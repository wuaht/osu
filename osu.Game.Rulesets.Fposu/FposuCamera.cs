// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Fposu
{
    /// <summary>
    /// A first person camera at the origin, which is turned by yaw and pitch (like the camera of McOsu's FPoSu).
    /// </summary>
    /// <remarks>
    /// The coordinate system is right-handed with Y pointing up. With no rotation, the camera looks along negative Z, at the centre of the <see cref="FposuScreenMesh"/>.
    /// </remarks>
    public class FposuCamera
    {
        /// <summary>
        /// The maximum pitch in degrees, in either direction (like cl_pitchup / cl_pitchdown of McOsu).
        /// </summary>
        public const float MAX_PITCH = 89;

        /// <summary>
        /// The horizontal rotation in degrees. Positive values turn the camera to the right.
        /// </summary>
        public float Yaw { get; private set; }

        /// <summary>
        /// The vertical rotation in degrees. Positive values turn the camera upwards.
        /// </summary>
        public float Pitch { get; private set; }

        /// <summary>
        /// The direction the camera looks in.
        /// </summary>
        public Vector3 Forward
        {
            get
            {
                float yaw = MathHelper.DegreesToRadians(Yaw);
                float pitch = MathHelper.DegreesToRadians(Pitch);

                return new Vector3(MathF.Sin(yaw) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch));
            }
        }

        /// <summary>
        /// The direction to the right of the camera.
        /// </summary>
        public Vector3 Right
        {
            get
            {
                float yaw = MathHelper.DegreesToRadians(Yaw);
                return new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
            }
        }

        /// <summary>
        /// The direction above the camera.
        /// </summary>
        public Vector3 Up => Vector3.Cross(Right, Forward);

        /// <summary>
        /// Turns the camera.
        /// </summary>
        /// <param name="yaw">The horizontal rotation in degrees. Positive values turn to the right.</param>
        /// <param name="pitch">The vertical rotation in degrees. Positive values turn upwards.</param>
        public void Rotate(float yaw, float pitch)
        {
            if (!float.IsFinite(yaw) || !float.IsFinite(pitch))
                return;

            Yaw = wrapYaw(Yaw + yaw);
            Pitch = Math.Clamp(Pitch + pitch, -MAX_PITCH, MAX_PITCH);
        }

        /// <summary>
        /// Turns the camera to look at a point.
        /// </summary>
        public void LookAt(Vector3 target)
        {
            float length = target.Length;

            // like McEngine, which ignores targets too close to the camera.
            if (length < 0.001f || !float.IsFinite(length))
                return;

            Yaw = wrapYaw(MathHelper.RadiansToDegrees(MathF.Atan2(target.X, -target.Z)));
            Pitch = Math.Clamp(MathHelper.RadiansToDegrees(MathF.Asin(Math.Clamp(target.Y / length, -1, 1))), -MAX_PITCH, MAX_PITCH);
        }

        /// <summary>
        /// Resets the camera to look at the centre of the screen.
        /// </summary>
        public void Reset()
        {
            Yaw = 0;
            Pitch = 0;
        }

        /// <summary>
        /// Returns the rotation in degrees per count of mouse movement, like in first person shooters.
        /// </summary>
        /// <param name="dpi">The counts per inch of the mouse.</param>
        /// <param name="cmPer360">The distance in centimetres for a full turn.</param>
        public static float DegreesPerCount(int dpi, float cmPer360)
        {
            double countsPer360 = cmPer360 * (dpi / 2.54);
            return countsPer360 > 0 ? (float)(360 / countsPer360) : 0;
        }

        private static float wrapYaw(float yaw)
        {
            yaw %= 360;

            if (yaw > 180)
                yaw -= 360;
            else if (yaw <= -180)
                yaw += 360;

            return yaw;
        }
    }
}
