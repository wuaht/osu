// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osuTK;

namespace osu.Game.Rulesets.Fposu.Tests
{
    [TestFixture]
    [Category("slop")]
    public class FposuScreenMeshTest
    {
        private const float aspect_ratio = 9 / 16f;

        [Test]
        public void TestFlatScreen()
        {
            var mesh = new FposuScreenMesh(0.5f, false, aspect_ratio);

            Assert.That(mesh.CircumLength, Is.EqualTo(1).Within(1e-5));
            Assert.That(mesh.HalfHeight, Is.EqualTo(0.5f * aspect_ratio).Within(1e-5));

            // the screen spans from -0.5 to 0.5 at the given distance (slightly moved back like in McOsu).
            assertEqual(mesh.GetPosition(new Vector2(0, 0)), new Vector3(-0.5f, mesh.HalfHeight, -0.5015f));
            assertEqual(mesh.GetPosition(new Vector2(1, 1)), new Vector3(0.5f, -mesh.HalfHeight, -0.5015f));
            assertEqual(mesh.GetPosition(new Vector2(0.5f, 0.5f)), new Vector3(0, 0, -0.5015f));
        }

        [Test]
        public void TestCurvedScreen()
        {
            var mesh = new FposuScreenMesh(0.5f, true, aspect_ratio);

            // like McOsu, the screen is curved along the circle through its edges.
            float radius = new Vector2(0.5f, 0.5f).Length;

            for (int i = 0; i <= FposuScreenMesh.FACE_COUNT; i++)
            {
                Vector3 position = mesh.GetPosition(new Vector2((float)i / FposuScreenMesh.FACE_COUNT, 0));
                Assert.That(new Vector2(position.X, position.Z + 0.0015f).Length, Is.EqualTo(radius).Within(1e-5), $"column {i} is not on the circle");
            }

            // the edges span a quarter circle, which the faces approximate.
            float expectedLength = FposuScreenMesh.FACE_COUNT * 2 * radius * MathF.Sin(MathF.PI / 4 / FposuScreenMesh.FACE_COUNT);
            Assert.That(mesh.CircumLength, Is.EqualTo(expectedLength).Within(1e-5));

            // the height is scaled along with the width, so that the 2D game is not stretched.
            Assert.That(mesh.HalfHeight, Is.EqualTo(0.5f * aspect_ratio * expectedLength).Within(1e-5));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestIntersectCentre(bool curved)
        {
            var mesh = new FposuScreenMesh(0.5f, curved, aspect_ratio);

            Vector2? uv = mesh.Intersect(new Vector3(0, 0, -1));

            Assert.That(uv, Is.Not.Null);
            Assert.That(uv!.Value.X, Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(uv.Value.Y, Is.EqualTo(0.5f).Within(1e-5));
        }

        [TestCase(false, 0.5f)]
        [TestCase(true, 0.5f)]
        [TestCase(true, 0.2f)]
        [TestCase(false, 1.5f)]
        public void TestIntersectionMatchesPosition(bool curved, float distance)
        {
            var mesh = new FposuScreenMesh(distance, curved, aspect_ratio);

            for (float u = 0.01f; u < 1; u += 0.07f)
            {
                for (float v = 0.01f; v < 1; v += 0.09f)
                {
                    Vector2? uv = mesh.Intersect(mesh.GetPosition(new Vector2(u, v)).Normalized());

                    Assert.That(uv, Is.Not.Null, $"({u}, {v}) not hit");
                    Assert.That(uv!.Value.X, Is.EqualTo(u).Within(1e-4), $"({u}, {v})");
                    Assert.That(uv.Value.Y, Is.EqualTo(v).Within(1e-4), $"({u}, {v})");
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNoIntersectionWhenLookingAway(bool curved)
        {
            var mesh = new FposuScreenMesh(0.5f, curved, aspect_ratio);

            // behind the camera.
            Assert.That(mesh.Intersect(new Vector3(0, 0, 1)), Is.Null);

            // beside the screen.
            Assert.That(mesh.Intersect(new Vector3(1, 0, -0.1f)), Is.Null);

            // above the screen.
            Assert.That(mesh.Intersect(new Vector3(0, 1, -0.2f)), Is.Null);
        }

        private static void assertEqual(Vector3 actual, Vector3 expected)
            => Assert.That((actual - expected).Length, Is.LessThan(1e-5f), $"Expected {expected}, but got {actual}.");
    }
}
