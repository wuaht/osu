// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Visualisation;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using osuTK;
using osuTK.Graphics;
using osuTK.Graphics.ES30;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    /// <summary>
    /// A cursor trail drawn as a continuous line of signed distance field segments.
    /// Segments are rendered in screen space with a max blend into an intermediate buffer, which is then drawn additively.
    /// </summary>
    [DrawVisualiserHidden]
    public partial class SdfCursorTrail : Drawable, IRequireHighFrequencyMousePosition
    {
        private const int max_parts = 2048;

        /// <summary>
        /// The duration it takes for a segment to fully fade out.
        /// </summary>
        private const double fade_duration = 500;

        /// <summary>
        /// The colour of newly added segments.
        /// </summary>
        public Colour4 PartColour { get; set; } = Colour4.White;

        /// <summary>
        /// Whether the centre of the trail should be blended towards white.
        /// </summary>
        public bool WhiteBlend { get; set; }

        public Vector2 CursorScale { get; set; } = Vector2.One;

        /// <summary>
        /// Whether the trail follows the mouse. If false, positions are added via <see cref="AddPosition"/> only.
        /// </summary>
        public bool FollowMouse { get; set; } = true;

        private readonly TrailPart[] parts = new TrailPart[max_parts];
        private readonly Queue<Vector2> addQueue = new Queue<Vector2>();

        private int currentIndex;
        private Vector2? lastPosition;
        private float? lastTime;

        private double timeOffset;
        private float fadeClock;

        private RectangleF rootRectangle;

        private IShader trailShader = null!;
        private IShader textureShader = null!;

        public SdfCursorTrail()
        {
            // as we are currently very dependent on having a running clock, let's make our own clock for the time being.
            Clock = new FramedClock();

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            trailShader = shaders.Load(@"SdfCursorTrail", @"SdfCursorTrail");
            textureShader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            timeOffset = Time.Current;
        }

        public override bool IsPresent => true;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            if (FollowMouse)
                AddPosition(e.ScreenSpaceMousePosition);

            return base.OnMouseMove(e);
        }

        /// <summary>
        /// Adds a trail position in screen space.
        /// </summary>
        public void AddPosition(Vector2 screenSpacePosition)
        {
            addQueue.Enqueue(screenSpacePosition);

            while (addQueue.Count > max_parts)
                addQueue.Dequeue();
        }

        protected override void Update()
        {
            base.Update();

            Color4 colour = PartColour.ToLinear();

            while (addQueue.Count > 0)
                add(addQueue.Dequeue(), colour);

            fadeClock = (float)((Time.Current - timeOffset) / fade_duration);

            // keep float precision of the stored times.
            if (fadeClock > 10000)
                resetTime();

            Drawable root = this;
            while (root.Parent != null)
                root = root.Parent;

            rootRectangle = root.ScreenSpaceDrawQuad.AABBFloat;

            Invalidate(Invalidation.DrawNode);
        }

        private void resetTime()
        {
            for (int i = 0; i < max_parts; i++)
            {
                parts[i].StartTime -= fadeClock;
                parts[i].EndTime -= fadeClock;
            }

            if (lastTime != null)
                lastTime -= fadeClock;

            timeOffset = Time.Current;
            fadeClock = 0;
        }

        private void add(Vector2 position, Color4 colour)
        {
            lastPosition ??= position;

            // the trail width is in screen pixels, independent of the resolution.
            float size = 16 * CursorScale.X + 1;
            float time = fadeClock + 1;

            lastTime ??= time;

            Vector2 from = lastPosition.Value;
            Vector2 direction;
            float length;

            if (position == from)
            {
                direction = new Vector2(size, 0);
                length = 0;
            }
            else
            {
                direction = position - from;
                length = direction.Length;
                direction *= size / length;
            }

            parts[currentIndex] = new TrailPart
            {
                From = from,
                To = position,
                Direction = direction,
                Extent = (size + length / 2) / size,
                OnePx = MathF.Sqrt(2) / size,
                StartTime = lastTime.Value,
                EndTime = time,
                Colour = colour,
                Valid = true,
            };

            currentIndex = (currentIndex + 1) % max_parts;

            lastPosition = position;
            lastTime = time;
        }

        protected override DrawNode CreateDrawNode() => new SdfTrailDrawNode(this);

        private struct TrailPart
        {
            public Vector2 From;
            public Vector2 To;
            public Vector2 Direction;
            public float Extent;
            public float OnePx;
            public float StartTime;
            public float EndTime;
            public Color4 Colour;
            public bool Valid;
        }

        private class SdfTrailDrawNode : DrawNode
        {
            protected new SdfCursorTrail Source => (SdfCursorTrail)base.Source;

            private IShader trailShader = null!;
            private IShader textureShader = null!;

            private readonly TrailPart[] parts = new TrailPart[max_parts];

            private float fadeClock;
            private float onePx;
            private float whiteBlend;
            private RectangleF rootRectangle;

            private IVertexBatch<SdfTrailVertex>? vertexBatch;
            private IFrameBuffer? frameBuffer;

            public SdfTrailDrawNode(SdfCursorTrail source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                trailShader = Source.trailShader;
                textureShader = Source.textureShader;
                fadeClock = Source.fadeClock;
                onePx = MathF.Sqrt(2) / (16 * Source.CursorScale.X + 1);
                whiteBlend = Source.WhiteBlend ? 1 : 0;
                rootRectangle = Source.rootRectangle;

                Source.parts.CopyTo(parts, 0);
            }

            private static readonly BlendingParameters max_blending = new BlendingParameters
            {
                Source = BlendingType.One,
                Destination = BlendingType.One,
                SourceAlpha = BlendingType.One,
                DestinationAlpha = BlendingType.One,
                RGBEquation = BlendingEquation.Max,
                AlphaEquation = BlendingEquation.Max,
            };

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (rootRectangle.Width < 1 || rootRectangle.Height < 1)
                    return;

                bool anyVisible = false;

                foreach (var part in parts)
                {
                    if (part.Valid && part.EndTime > fadeClock)
                    {
                        anyVisible = true;
                        break;
                    }
                }

                if (!anyVisible)
                    return;

                vertexBatch ??= renderer.CreateQuadBatch<SdfTrailVertex>(max_parts, 1);
                frameBuffer ??= renderer.CreateFrameBuffer();

                Vector2 size = new Vector2(MathF.Ceiling(rootRectangle.Width), MathF.Ceiling(rootRectangle.Height));
                frameBuffer.Size = size;

                RectangleI screenSpaceMaskingRect = new RectangleI((int)Math.Floor(rootRectangle.X), (int)Math.Floor(rootRectangle.Y), (int)size.X + 1, (int)size.Y + 1);

                renderer.PushMaskingInfo(new MaskingInfo
                {
                    ScreenSpaceAABB = screenSpaceMaskingRect,
                    MaskingRect = rootRectangle,
                    ToMaskingSpace = Matrix3.Identity,
                    BlendRange = 1,
                    AlphaExponent = 1,
                }, true);

                renderer.PushViewport(new RectangleI(0, 0, (int)size.X, (int)size.Y));
                renderer.PushScissor(new RectangleI(0, 0, (int)size.X, (int)size.Y));
                renderer.PushScissorOffset(screenSpaceMaskingRect.Location);

                frameBuffer.Bind();

                renderer.PushOrtho(rootRectangle);
                renderer.Clear(new ClearInfo(Color4.Transparent));
                renderer.SetBlend(max_blending);

                trailShader.Bind();

                foreach (var part in parts)
                {
                    if (!part.Valid || part.EndTime <= fadeClock)
                        continue;

                    addPart(part);
                }

                vertexBatch.Draw();
                trailShader.Unbind();

                renderer.PopOrtho();

                frameBuffer.Unbind();

                renderer.PopScissorOffset();
                renderer.PopViewport();
                renderer.PopScissor();
                renderer.PopMaskingInfo();

                renderer.SetBlend(BlendingParameters.Additive);

                textureShader.Bind();
                renderer.DrawFrameBuffer(frameBuffer, rootRectangle, DrawColourInfo.Colour);
                textureShader.Unbind();
            }

            private void addPart(TrailPart part)
            {
                Debug.Assert(vertexBatch != null);

                Vector2 perpendicular = new Vector2(-part.Direction.Y, part.Direction.X);

                float extent = 1 - part.Extent;
                float texX = part.Extent + part.OnePx;
                float texY = 1 + part.OnePx;
                float startTime = part.StartTime - fadeClock;
                float endTime = part.EndTime - fadeClock;

                vertexBatch.Add(createVertex(part.From - part.Direction - perpendicular, new Vector2(-texX, -texY), startTime));
                vertexBatch.Add(createVertex(part.From - part.Direction + perpendicular, new Vector2(-texX, texY), startTime));
                vertexBatch.Add(createVertex(part.To + part.Direction + perpendicular, new Vector2(texX, texY), endTime));
                vertexBatch.Add(createVertex(part.To + part.Direction - perpendicular, new Vector2(texX, -texY), endTime));

                SdfTrailVertex createVertex(Vector2 position, Vector2 texturePosition, float time) => new SdfTrailVertex
                {
                    Position = position,
                    Colour = part.Colour,
                    TexturePosition = texturePosition,
                    Time = time,
                    Extent = extent,
                    OnePx = onePx,
                    WhiteBlend = whiteBlend,
                };
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                vertexBatch?.Dispose();
                frameBuffer?.Dispose();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SdfTrailVertex : IEquatable<SdfTrailVertex>, IVertex
        {
            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 Position;

            [VertexMember(4, VertexAttribPointerType.Float)]
            public Color4 Colour;

            [VertexMember(2, VertexAttribPointerType.Float)]
            public Vector2 TexturePosition;

            [VertexMember(1, VertexAttribPointerType.Float)]
            public float Time;

            [VertexMember(1, VertexAttribPointerType.Float)]
            public float Extent;

            [VertexMember(1, VertexAttribPointerType.Float)]
            public float OnePx;

            [VertexMember(1, VertexAttribPointerType.Float)]
            public float WhiteBlend;

            public readonly bool Equals(SdfTrailVertex other)
            {
                return Position.Equals(other.Position)
                       && Colour.Equals(other.Colour)
                       && TexturePosition.Equals(other.TexturePosition)
                       && Time.Equals(other.Time)
                       && Extent.Equals(other.Extent)
                       && OnePx.Equals(other.OnePx)
                       && WhiteBlend.Equals(other.WhiteBlend);
            }
        }
    }
}
