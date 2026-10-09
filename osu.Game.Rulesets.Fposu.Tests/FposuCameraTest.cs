// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osuTK;

namespace osu.Game.Rulesets.Fposu.Tests
{
    [TestFixture]
    [Category("slop")]
    public class FposuCameraTest
    {
        [Test]
        public void TestInitialOrientation()
        {
            var camera = new FposuCamera();

            assertEqual(camera.Forward, new Vector3(0, 0, -1));
            assertEqual(camera.Right, new Vector3(1, 0, 0));
            assertEqual(camera.Up, new Vector3(0, 1, 0));
        }

        [Test]
        public void TestRotation()
        {
            var camera = new FposuCamera();

            camera.Rotate(90, 0);
            assertEqual(camera.Forward, new Vector3(1, 0, 0));

            camera.Rotate(-90, 30);
            assertEqual(camera.Forward, new Vector3(0, MathF.Sin(MathF.PI / 6), -MathF.Cos(MathF.PI / 6)));
            assertEqual(camera.Up, new Vector3(0, MathF.Cos(MathF.PI / 6), MathF.Sin(MathF.PI / 6)));
        }

        [Test]
        public void TestPitchIsClamped()
        {
            var camera = new FposuCamera();

            camera.Rotate(0, 200);
            Assert.That(camera.Pitch, Is.EqualTo(FposuCamera.MAX_PITCH));

            camera.Rotate(0, -500);
            Assert.That(camera.Pitch, Is.EqualTo(-FposuCamera.MAX_PITCH));
        }

        [Test]
        public void TestYawWraps()
        {
            var camera = new FposuCamera();

            camera.Rotate(350, 0);
            Assert.That(camera.Yaw, Is.EqualTo(-10).Within(0.001));

            camera.Rotate(-380, 0);
            Assert.That(camera.Yaw, Is.EqualTo(-30).Within(0.001));
        }

        [TestCase(0, 0)]
        [TestCase(45, 10)]
        [TestCase(-120, -60)]
        [TestCase(179, 80)]
        public void TestLookAt(float yaw, float pitch)
        {
            var reference = new FposuCamera();
            reference.Rotate(yaw, pitch);

            var camera = new FposuCamera();
            camera.LookAt(reference.Forward * 3);

            Assert.That(camera.Yaw, Is.EqualTo(yaw).Within(0.01));
            Assert.That(camera.Pitch, Is.EqualTo(pitch).Within(0.01));
        }

        [Test]
        public void TestSensitivity()
        {
            // moving the mouse by cm/360 turns the camera fully.
            const int dpi = 800;
            const float cm_per_360 = 25;

            float countsForFullTurn = cm_per_360 / 2.54f * dpi;

            Assert.That(FposuCamera.DegreesPerCount(dpi, cm_per_360) * countsForFullTurn, Is.EqualTo(360).Within(0.01));
        }

        private static void assertEqual(Vector3 actual, Vector3 expected)
            => Assert.That((actual - expected).Length, Is.LessThan(1e-5f), $"Expected {expected}, but got {actual}.");
    }
}
