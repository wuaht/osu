// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Fposu.UI
{
    /// <summary>
    /// Renders its children (the 2D game) into a frame buffer, which is displayed on a <see cref="FposuScreenMesh"/> as seen by a <see cref="FposuCamera"/>.
    /// </summary>
    /// <remarks>
    /// The children are laid out and receive input as if this was a regular container. Only how they are displayed differs.
    /// </remarks>
    public partial class FposuScreen : Container, IBufferedDrawable
    {
        /// <summary>
        /// The number of columns each face of the mesh is drawn as, and the number of rows of the mesh.
        /// The texture is mapped linearly within each cell, so cells have to be small to keep the perspective distortion negligible.
        /// </summary>
        private const int columns_per_face = 4;

        private const int rows = 32;

        private const int columns = FposuScreenMesh.FACE_COUNT * columns_per_face;

        /// <summary>
        /// Cells with any corner further than this angle in degrees from the view direction aren't drawn, as their projection becomes unstable.
        /// No part of the view is affected, as this is beyond the corners of the view at the maximum field of view.
        /// </summary>
        private const float max_cell_angle = 85;

        /// <summary>
        /// Half of the size of the background cube (like McOsu's fposu_cube_size).
        /// </summary>
        private const float cube_size = 500;

        private const int cube_grid_lines = 10;

        private static readonly Color4 environment_colour = new Color4(12, 12, 16, 255);
        private static readonly Color4 cube_line_colour = new Color4(255, 255, 255, 28);

        public readonly FposuCamera Camera;

        /// <summary>
        /// The current mesh, which depends on the settings and the aspect ratio of this container.
        /// </summary>
        public FposuScreenMesh Mesh { get; private set; } = new FposuScreenMesh(0.5f, true, 9 / 16f);

        public IShader TextureShader { get; private set; } = null!;

        public Color4 BackgroundColour => new Color4(0, 0, 0, 0);

        public DrawColourInfo? FrameBufferDrawColour => base.DrawColourInfo;

        public Vector2 FrameBufferScale => Vector2.One;

        // like BufferedContainer: children shouldn't receive the colour, as it's applied when the frame buffer is drawn.
        public override DrawColourInfo DrawColourInfo
        {
            get
            {
                var blending = Blending;
                blending.ApplyDefaultToInherited();

                return new DrawColourInfo(Color4.White, blending);
            }
        }

        private readonly BufferedDrawNodeSharedData sharedData = new BufferedDrawNodeSharedData();

        private Bindable<float> fov = null!;
        private Bindable<float> distance = null!;
        private Bindable<bool> curved = null!;
        private Bindable<bool> backgroundCube = null!;
        private Bindable<bool> skybox = null!;

        /// <summary>
        /// The default skybox texture, which is created once as it doesn't change.
        /// </summary>
        private static readonly Lazy<SixLabors.ImageSharp.Image<Rgba32>> default_skybox_image = new Lazy<SixLabors.ImageSharp.Image<Rgba32>>(() => FposuSkybox.CreateDefaultTexture(default_skybox_face_size));

        private const int default_skybox_face_size = 512;

        private Texture? defaultSkyboxTexture;

        /// <summary>
        /// The skybox texture of the skin, if it provides one.
        /// </summary>
        private Texture? skinSkyboxTexture;

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        /// <summary>
        /// The positions of the corners of all cells of the mesh, row by row.
        /// </summary>
        private Vector3[] meshPoints = Array.Empty<Vector3>();

        public FposuScreen(FposuCamera camera)
        {
            Camera = camera;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders, OsuConfigManager config, GameHost host)
        {
            TextureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);

            fov = config.GetBindable<float>(OsuSetting.SlopFposuFov);
            distance = config.GetBindable<float>(OsuSetting.SlopFposuDistance);
            curved = config.GetBindable<bool>(OsuSetting.SlopFposuCurved);
            backgroundCube = config.GetBindable<bool>(OsuSetting.SlopFposuBackgroundCube);
            skybox = config.GetBindable<bool>(OsuSetting.SlopFposuSkybox);

            var image = default_skybox_image.Value;

            defaultSkyboxTexture = host.Renderer.CreateTexture(image.Width, image.Height);
            // the upload takes ownership of the image.
            defaultSkyboxTexture.SetData(new TextureUpload(image.Clone()));

            updateSkinSkybox();
            skinSource.SourceChanged += onSkinChanged;
        }

        private void onSkinChanged() => Schedule(updateSkinSkybox);

        private void updateSkinSkybox() => skinSkyboxTexture = skinSource.GetTexture(@"skybox");

        /// <summary>
        /// The skybox texture which is displayed, or <c>null</c> if no skybox is displayed.
        /// </summary>
        private Texture? currentSkyboxTexture => skybox.Value ? skinSkyboxTexture ?? defaultSkyboxTexture : null;

        /// <summary>
        /// Converts a position on the screen to screen space of the 2D game.
        /// </summary>
        public Vector2 ToScreenSpaceFromUV(Vector2 uv) => ToScreenSpace(uv * DrawSize);

        /// <summary>
        /// Converts a position in screen space of the 2D game to a position on the screen.
        /// </summary>
        public Vector2 ToUVFromScreenSpace(Vector2 screenSpacePosition) => DrawWidth > 0 && DrawHeight > 0 ? Vector2.Divide(ToLocalSpace(screenSpacePosition), DrawSize) : Vector2.Zero;

        protected override void Update()
        {
            base.Update();

            float aspectRatio = DrawWidth > 0 ? DrawHeight / DrawWidth : 9 / 16f;

            if (Mesh.Distance != distance.Value || Mesh.Curved != curved.Value || Mesh.AspectRatio != aspectRatio || meshPoints.Length == 0)
            {
                Mesh = new FposuScreenMesh(distance.Value, curved.Value, aspectRatio);

                var points = new Vector3[(columns + 1) * (rows + 1)];

                for (int row = 0; row <= rows; row++)
                {
                    for (int column = 0; column <= columns; column++)
                        points[row * (columns + 1) + column] = Mesh.GetPosition(new Vector2((float)column / columns, (float)row / rows));
                }

                meshPoints = points;
            }

            // the 2D game changes every frame.
            Invalidate(Invalidation.DrawNode);
        }

        protected override RectangleF ComputeChildMaskingBounds() => ScreenSpaceDrawQuad.AABBFloat; // like BufferedContainer, children should never be masked away.

        protected override DrawNode CreateDrawNode() => new FposuScreenDrawNode(this, sharedData);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            sharedData.Dispose();

            if (skinSource.IsNotNull())
                skinSource.SourceChanged -= onSkinChanged;

            defaultSkyboxTexture?.Dispose();
        }

        private class FposuScreenDrawNode : BufferedDrawNode, ICompositeDrawNode
        {
            protected new FposuScreen Source => (FposuScreen)base.Source;

            protected new CompositeDrawableDrawNode Child => (CompositeDrawableDrawNode)base.Child;

            private Vector3 forward;
            private Vector3 right;
            private Vector3 up;
            private float focalLength;
            private bool drawCube;
            private Texture? skyboxTexture;
            private Vector3[] meshPoints = Array.Empty<Vector3>();
            private Texture whitePixel = null!;

            // reused between frames to avoid allocations.
            private Vector2[] projected = Array.Empty<Vector2>();
            private bool[] valid = Array.Empty<bool>();

            public FposuScreenDrawNode(FposuScreen source, BufferedDrawNodeSharedData sharedData)
                : base(source, new CompositeDrawableDrawNode(source), sharedData)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                forward = Source.Camera.Forward;
                right = Source.Camera.Right;
                up = Source.Camera.Up;

                // the field of view is horizontal (like McOsu by default).
                focalLength = DrawRectangle.Width / 2 / MathF.Tan(MathHelper.DegreesToRadians(Source.fov.Value) / 2);

                drawCube = Source.backgroundCube.Value;
                skyboxTexture = Source.currentSkyboxTexture;
                meshPoints = Source.meshPoints;
            }

            public List<DrawNode>? Children
            {
                get => Child.Children;
                set => Child.Children = value;
            }

            public bool AddChildDrawNodes => RequiresRedraw;

            protected override void DrawContents(IRenderer renderer)
            {
                whitePixel = renderer.WhitePixel;

                // the environment replaces anything behind the game.
                renderer.DrawQuad(whitePixel, Quad.FromRectangle(DrawRectangle), environment_colour);

                // like McOsu, the skybox replaces the background cube.
                if (skyboxTexture != null)
                    drawSkybox(renderer, skyboxTexture);
                else if (drawCube)
                    drawBackgroundCube(renderer);

                drawMesh(renderer);
            }

            private void drawMesh(IRenderer renderer)
            {
                if (meshPoints.Length != (columns + 1) * (rows + 1))
                    return;

                Texture texture = SharedData.MainBuffer.Texture;

                if (projected.Length != meshPoints.Length)
                {
                    projected = new Vector2[meshPoints.Length];
                    valid = new bool[meshPoints.Length];
                }

                float minCos = MathF.Cos(MathHelper.DegreesToRadians(max_cell_angle));

                for (int i = 0; i < meshPoints.Length; i++)
                {
                    Vector3 point = meshPoints[i];
                    float depth = Vector3.Dot(point, forward);

                    valid[i] = depth > minCos * point.Length;

                    if (valid[i])
                        projected[i] = project(point, depth);
                }

                float cellWidth = texture.DisplayWidth / columns;
                float cellHeight = texture.DisplayHeight / rows;

                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int topLeft = row * (columns + 1) + column;
                        int topRight = topLeft + 1;
                        int bottomLeft = topLeft + columns + 1;
                        int bottomRight = bottomLeft + 1;

                        if (!valid[topLeft] || !valid[topRight] || !valid[bottomLeft] || !valid[bottomRight])
                            continue;

                        var quad = new Quad(projected[topLeft], projected[topRight], projected[bottomLeft], projected[bottomRight]);

                        // the texture coordinates are given separately from the texture rectangle, so that sampling isn't clamped to each cell (which would cause seams).
                        renderer.DrawQuad(texture, quad, DrawColourInfo.Colour, textureCoords: new RectangleF(column * cellWidth, row * cellHeight, cellWidth, cellHeight));
                    }
                }
            }

            /// <summary>
            /// The number of columns and rows each face of the skybox is drawn as, to keep the perspective distortion negligible.
            /// </summary>
            private const int skybox_subdivisions = 16;

            private const int skybox_points_per_row = skybox_subdivisions + 1;

            // reused between frames to avoid allocations.
            private readonly Vector2[] skyboxProjected = new Vector2[skybox_points_per_row * skybox_points_per_row];
            private readonly bool[] skyboxValid = new bool[skybox_points_per_row * skybox_points_per_row];

            private void drawSkybox(IRenderer renderer, Texture texture)
            {
                float minCos = MathF.Cos(MathHelper.DegreesToRadians(max_cell_angle));

                float faceWidth = texture.DisplayWidth / FposuSkybox.TEXTURE_COLUMNS;
                float faceHeight = texture.DisplayHeight / FposuSkybox.TEXTURE_ROWS;

                // half a texel is left out at the edges of the faces, so that neighbouring parts of the texture (which may not be adjacent on the cube) aren't sampled.
                float insetX = texture.Width > 0 ? texture.DisplayWidth / texture.Width / 2 : 0;
                float insetY = texture.Height > 0 ? texture.DisplayHeight / texture.Height / 2 : 0;

                float cellWidth = (faceWidth - 2 * insetX) / skybox_subdivisions;
                float cellHeight = (faceHeight - 2 * insetY) / skybox_subdivisions;

                foreach (var face in Enum.GetValues<FposuSkybox.Face>())
                {
                    for (int row = 0; row <= skybox_subdivisions; row++)
                    {
                        for (int column = 0; column <= skybox_subdivisions; column++)
                        {
                            Vector3 point = FposuSkybox.GetDirection(face, new Vector2((float)column / skybox_subdivisions, (float)row / skybox_subdivisions));
                            float depth = Vector3.Dot(point, forward);

                            int index = row * skybox_points_per_row + column;

                            skyboxValid[index] = depth > minCos * point.Length;

                            if (skyboxValid[index])
                                skyboxProjected[index] = project(point, depth);
                        }
                    }

                    var (faceColumn, faceRow) = FposuSkybox.GetTexturePosition(face);

                    float faceX = faceColumn * faceWidth + insetX;
                    float faceY = faceRow * faceHeight + insetY;

                    for (int row = 0; row < skybox_subdivisions; row++)
                    {
                        for (int column = 0; column < skybox_subdivisions; column++)
                        {
                            int topLeft = row * skybox_points_per_row + column;
                            int topRight = topLeft + 1;
                            int bottomLeft = topLeft + skybox_points_per_row;
                            int bottomRight = bottomLeft + 1;

                            if (!skyboxValid[topLeft] || !skyboxValid[topRight] || !skyboxValid[bottomLeft] || !skyboxValid[bottomRight])
                                continue;

                            var quad = new Quad(skyboxProjected[topLeft], skyboxProjected[topRight], skyboxProjected[bottomLeft], skyboxProjected[bottomRight]);

                            renderer.DrawQuad(texture, quad, Color4.White,
                                textureCoords: new RectangleF(faceX + column * cellWidth, faceY + row * cellHeight, cellWidth, cellHeight));
                        }
                    }
                }
            }

            private void drawBackgroundCube(IRenderer renderer)
            {
                const float step = 2 * cube_size / cube_grid_lines;

                // for each face, lines along both of its axes.
                for (int axis = 0; axis < 3; axis++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int i = 0; i <= cube_grid_lines; i++)
                        {
                            float offset = -cube_size + i * step;

                            drawLine(renderer, cubePoint(axis, side, offset, -cube_size, true), cubePoint(axis, side, offset, cube_size, true));
                            drawLine(renderer, cubePoint(axis, side, offset, -cube_size, false), cubePoint(axis, side, offset, cube_size, false));
                        }
                    }
                }
            }

            /// <summary>
            /// Returns a point on a face of the background cube.
            /// </summary>
            /// <param name="axis">The axis which the face is perpendicular to.</param>
            /// <param name="side">The side of the cube along <paramref name="axis"/>.</param>
            /// <param name="a">The position along one of the other axes.</param>
            /// <param name="b">The position along the remaining axis.</param>
            /// <param name="swap">Whether to swap the other axes.</param>
            private static Vector3 cubePoint(int axis, int side, float a, float b, bool swap)
            {
                if (swap)
                    (a, b) = (b, a);

                switch (axis)
                {
                    case 0:
                        return new Vector3(side * cube_size, a, b);

                    case 1:
                        return new Vector3(a, side * cube_size, b);

                    default:
                        return new Vector3(a, b, side * cube_size);
                }
            }

            private void drawLine(IRenderer renderer, Vector3 start, Vector3 end)
            {
                // clipped to the part in front of the camera.
                const float near = 1;

                float startDepth = Vector3.Dot(start, forward);
                float endDepth = Vector3.Dot(end, forward);

                if (startDepth < near && endDepth < near)
                    return;

                if (startDepth < near)
                {
                    start = Vector3.Lerp(start, end, (near - startDepth) / (endDepth - startDepth));
                    startDepth = near;
                }
                else if (endDepth < near)
                {
                    end = Vector3.Lerp(start, end, (near - startDepth) / (endDepth - startDepth));
                    endDepth = near;
                }

                Vector2 a = project(start, startDepth);
                Vector2 b = project(end, endDepth);

                Vector2 direction = b - a;
                float length = direction.Length;

                if (length < 0.5f || !float.IsFinite(length))
                    return;

                Vector2 normal = new Vector2(-direction.Y, direction.X) / length;

                renderer.DrawQuad(whitePixel, new Quad(a - normal, b - normal, a + normal, b + normal), cube_line_colour);
            }

            /// <summary>
            /// Projects a point in front of the camera to screen space.
            /// </summary>
            private Vector2 project(Vector3 point, float depth)
            {
                Vector2 centre = DrawRectangle.Centre;

                return new Vector2(
                    centre.X + focalLength * Vector3.Dot(point, right) / depth,
                    centre.Y - focalLength * Vector3.Dot(point, up) / depth);
            }
        }
    }
}
