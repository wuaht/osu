// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using osuTK;

namespace osu.Game.Rulesets.Fposu.Tests
{
    [TestFixture]
    [Category("slop")]
    public class FposuSkyboxTest
    {
        [Test]
        public void TestFacesSeenUpright()
        {
            // looking at the centre of each side face, its right edge is to the right of the camera and its top edge above it.
            foreach (float yaw in new[] { -90f, 0, 90, 180 })
            {
                var camera = new FposuCamera();
                camera.Rotate(yaw, 0);

                var (face, centre) = FposuSkybox.GetFacePosition(camera.Forward);

                Assert.That(centre.X, Is.EqualTo(0.5f).Within(1e-4), $"yaw {yaw}");
                Assert.That(centre.Y, Is.EqualTo(0.5f).Within(1e-4), $"yaw {yaw}");

                assertDirection(FposuSkybox.GetDirection(face, new Vector2(1, 0.5f)) - FposuSkybox.GetDirection(face, centre), camera.Right);
                assertDirection(FposuSkybox.GetDirection(face, new Vector2(0.5f, 0)) - FposuSkybox.GetDirection(face, centre), camera.Up);
            }
        }

        [Test]
        public void TestFrontFaceFacesInitialCamera()
        {
            var (face, _) = FposuSkybox.GetFacePosition(new FposuCamera().Forward);
            Assert.That(face, Is.EqualTo(FposuSkybox.Face.Front));
        }

        [Test]
        public void TestAdjacentFacesInTextureShareEdges()
        {
            // the middle row wraps around.
            assertSharedVerticalEdge(FposuSkybox.Face.Left, FposuSkybox.Face.Front);
            assertSharedVerticalEdge(FposuSkybox.Face.Front, FposuSkybox.Face.Right);
            assertSharedVerticalEdge(FposuSkybox.Face.Right, FposuSkybox.Face.Back);
            assertSharedVerticalEdge(FposuSkybox.Face.Back, FposuSkybox.Face.Left);

            // the top and bottom faces adjoin the front face.
            assertSharedHorizontalEdge(FposuSkybox.Face.Up, FposuSkybox.Face.Front);
            assertSharedHorizontalEdge(FposuSkybox.Face.Front, FposuSkybox.Face.Down);
        }

        [Test]
        public void TestFacePositionIsInverseOfDirection()
        {
            foreach (var face in Enum.GetValues<FposuSkybox.Face>())
            {
                for (float x = 0.05f; x < 1; x += 0.1f)
                {
                    for (float y = 0.05f; y < 1; y += 0.1f)
                    {
                        var (resultFace, position) = FposuSkybox.GetFacePosition(FposuSkybox.GetDirection(face, new Vector2(x, y)) * 3);

                        Assert.That(resultFace, Is.EqualTo(face));
                        Assert.That(position.X, Is.EqualTo(x).Within(1e-4));
                        Assert.That(position.Y, Is.EqualTo(y).Within(1e-4));
                    }
                }
            }
        }

        [Test]
        public void TestDefaultTexture()
        {
            const int face_size = 128;

            using (var image = FposuSkybox.CreateDefaultTexture(face_size))
            {
                Assert.That(image.Width, Is.EqualTo(face_size * FposuSkybox.TEXTURE_COLUMNS));
                Assert.That(image.Height, Is.EqualTo(face_size * FposuSkybox.TEXTURE_ROWS));

                // the medians ignore the stars.
                byte medianBlue(int x, int y, int size)
                {
                    var values = new List<byte>();

                    for (int i = 0; i < size; i++)
                    {
                        for (int j = 0; j < size; j++)
                            values.Add(image[x + i, y + j].B);
                    }

                    values.Sort();
                    return values[values.Count / 2];
                }

                // the centre of the up face, just above the horizon on the front face, and just below it.
                byte zenith = medianBlue(face_size + face_size / 2 - 8, face_size / 2 - 8, 16);
                byte horizon = medianBlue(face_size + face_size / 2 - 8, face_size + face_size / 2 - 4, 4);
                byte belowHorizon = medianBlue(face_size + face_size / 2 - 8, face_size + face_size / 2, 4);

                // the sky is lighter at the horizon than at the zenith, without a visible edge at the horizon.
                Assert.That(horizon, Is.GreaterThan(zenith + 10));
                Assert.That(Math.Abs(horizon - belowHorizon), Is.LessThan(10));
            }
        }

        private static void assertSharedVerticalEdge(FposuSkybox.Face left, FposuSkybox.Face right)
        {
            for (float y = 0; y <= 1; y += 0.25f)
                assertEqual(FposuSkybox.GetDirection(left, new Vector2(1, y)), FposuSkybox.GetDirection(right, new Vector2(0, y)));
        }

        private static void assertSharedHorizontalEdge(FposuSkybox.Face top, FposuSkybox.Face bottom)
        {
            for (float x = 0; x <= 1; x += 0.25f)
                assertEqual(FposuSkybox.GetDirection(top, new Vector2(x, 1)), FposuSkybox.GetDirection(bottom, new Vector2(x, 0)));
        }

        private static void assertDirection(Vector3 actual, Vector3 expected)
            => assertEqual(actual.Normalized(), expected.Normalized());

        private static void assertEqual(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-4));
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-4));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-4));
        }
    }
}
