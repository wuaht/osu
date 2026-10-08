// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    /// <summary>
    /// Displays the waveform of a track in one of the <see cref="EditorWaveformStyle"/>s, based on <see cref="EditorWaveformData"/>.
    /// </summary>
    /// <remarks>
    /// The full width of this drawable represents the time from 0 to <see cref="TrackLength"/>, matching the timeline.
    /// Each block is drawn at the exact time of its samples, and the peak of all samples within a pixel is always displayed.
    /// </remarks>
    public partial class EditorWaveformGraph : Drawable
    {
        private static readonly Color4 low_colour = new Color4(1f, 0.3f, 0.3f, 1f);
        private static readonly Color4 mid_colour = new Color4(0.35f, 0.9f, 0.35f, 1f);
        private static readonly Color4 high_colour = new Color4(0.35f, 0.47f, 1f, 1f);

        private IShader shader = null!;
        private Texture texture = null!;

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders, IRenderer renderer)
        {
            shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            texture = renderer.WhitePixel;
        }

        private EditorWaveformStyle style = EditorWaveformStyle.Simple;

        public EditorWaveformStyle Style
        {
            get => style;
            set
            {
                if (style == value)
                    return;

                style = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private double trackLength;

        /// <summary>
        /// The length of the track in milliseconds, which corresponds to the full width of this drawable.
        /// </summary>
        public double TrackLength
        {
            get => trackLength;
            set
            {
                if (trackLength == value)
                    return;

                trackLength = value;
                Invalidate(Invalidation.DrawNode);
            }
        }

        private EditorWaveformData? data;

        private CancellationTokenSource? analysisCancellation;

        /// <summary>
        /// Analyses the given audio file in the background and displays it once done.
        /// </summary>
        /// <param name="stream">The audio file, or <see langword="null"/> if there is none. Disposed when done.</param>
        public void LoadAudio(Stream? stream)
        {
            analysisCancellation?.Cancel();
            analysisCancellation = new CancellationTokenSource();

            var token = analysisCancellation.Token;

            data = null;
            Invalidate(Invalidation.DrawNode);

            if (stream == null)
                return;

            // Not cancelled via the token directly, so that the stream is always disposed by the analysis.
            Task.Run(() =>
            {
                EditorWaveformData? result;

                try
                {
                    result = EditorWaveformData.Analyse(stream, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                Schedule(() =>
                {
                    if (token.IsCancellationRequested)
                        return;

                    data = result;
                    Invalidate(Invalidation.DrawNode);
                });
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Logger.Error(t.Exception, "Failed to analyse the waveform");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        protected override DrawNode CreateDrawNode() => new EditorWaveformDrawNode(this);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            analysisCancellation?.Cancel();
        }

        private class EditorWaveformDrawNode : DrawNode
        {
            protected new EditorWaveformGraph Source => (EditorWaveformGraph)base.Source;

            private IShader shader = null!;
            private Texture texture = null!;
            private EditorWaveformData? data;
            private EditorWaveformStyle style;
            private double trackLength;
            private Vector2 drawSize;

            private IVertexBatch<TexturedVertex2D>? vertexBatch;

            public EditorWaveformDrawNode(EditorWaveformGraph source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                shader = Source.shader;
                texture = Source.texture;
                data = Source.data;
                style = Source.style;
                trackLength = Source.trackLength;
                drawSize = Source.DrawSize;
            }

            protected override void Draw(IRenderer renderer)
            {
                base.Draw(renderer);

                if (data == null || trackLength <= 0 || drawSize.X <= 0 || drawSize.Y <= 0 || texture?.Available != true)
                    return;

                vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(1000, 10);

                // The size of a screen pixel in local space.
                Vector2 pixelSize = DrawInfo.MatrixInverse.ExtractScale().Xy;

                double localWidthPerMs = drawSize.X / trackLength;

                // Use the finest level whose blocks are at least a pixel wide, so that there's at most one block per pixel.
                // Coarser levels contain the peaks of the finer ones, and their blocks still start at exact sample times.
                var level = data.Levels[0];

                foreach (var candidate in data.Levels)
                {
                    level = candidate;

                    if (data.FrameToTime(candidate.FramesPerBlock) * localWidthPerMs >= pixelSize.X)
                        break;
                }

                // Only draw blocks which are visible.
                RectangleF visibleRectangle = (Quad.FromRectangle(renderer.CurrentMaskingInfo.ScreenSpaceAABB) * DrawInfo.MatrixInverse).AABBFloat;

                double blockDuration = data.FrameToTime(level.FramesPerBlock);
                int startBlock = (int)Math.Clamp(Math.Floor(visibleRectangle.Left / localWidthPerMs / blockDuration) - 1, 0, level.Count);
                int endBlock = (int)Math.Clamp(Math.Ceiling(visibleRectangle.Right / localWidthPerMs / blockDuration) + 1, 0, level.Count);

                float centre = drawSize.Y / 2;

                // Even silence is displayed as a thin line, so that the extent of the track remains visible.
                float minimumHeight = pixelSize.Y;

                ColourInfo white = applyDrawColour(Color4.White);
                ColourInfo low = applyDrawColour(low_colour);
                ColourInfo mid = applyDrawColour(mid_colour);
                ColourInfo high = applyDrawColour(high_colour);

                shader.Bind();
                texture.Bind();

                for (int i = startBlock; i < endBlock; i++)
                {
                    long startFrame = (long)i * level.FramesPerBlock;
                    long endFrame = Math.Min(startFrame + level.FramesPerBlock, data.FrameCount);

                    float left = (float)(data.FrameToTime(startFrame) * localWidthPerMs);
                    float right = (float)(data.FrameToTime(endFrame) * localWidthPerMs);

                    // The signed sample range of the block. When zoomed in, this shows the actual shape of the signal.
                    float max = level.Max[i] / data.PeakMax;
                    float min = level.Min[i] / data.PeakMax;
                    float peak = Math.Max(max, -min);

                    switch (style)
                    {
                        case EditorWaveformStyle.Simple:
                            drawRange(left, right, max, min, white);
                            break;

                        case EditorWaveformStyle.ThreeBand:
                        {
                            // Each band is normalised on its own, so that a dominant band extends beyond the full waveform.
                            float lowHeight = level.Low[i] / data.LowMax;
                            float midHeight = level.Mid[i] / data.MidMax;
                            float highHeight = level.High[i] / data.HighMax;

                            // Draw the largest band first, so that all of them remain visible.
                            drawBandsSorted(left, right, peak, (lowHeight, low), (midHeight, mid), (highHeight, high));
                            drawRange(left, right, max, min, white);
                            break;
                        }

                        case EditorWaveformStyle.Spectral:
                        {
                            float lowHeight = level.Low[i] / data.LowMax;
                            float midHeight = level.Mid[i] / data.MidMax;
                            float highHeight = level.High[i] / data.HighMax;
                            float strongest = Math.Max(lowHeight, Math.Max(midHeight, highHeight));

                            Color4 colour = strongest > 0
                                ? new Color4(lowHeight / strongest, midHeight / strongest, highHeight / strongest, 1)
                                : Color4.White;

                            drawRange(left, right, max, min, applyDrawColour(colour));
                            break;
                        }
                    }
                }

                shader.Unbind();

                void drawBandsSorted(float x0, float x1, float peakHeight, (float height, ColourInfo colour) a, (float height, ColourInfo colour) b, (float height, ColourInfo colour) c)
                {
                    if (a.height < b.height) (a, b) = (b, a);
                    if (b.height < c.height) (b, c) = (c, b);
                    if (a.height < b.height) (a, b) = (b, a);

                    // Bands which don't extend beyond the full waveform are covered by it entirely.
                    // The bands are only a visual aid and drawn symmetrically.
                    if (a.height > peakHeight) drawRange(x0, x1, a.height, -a.height, a.colour);
                    if (b.height > peakHeight) drawRange(x0, x1, b.height, -b.height, b.colour);
                    if (c.height > peakHeight) drawRange(x0, x1, c.height, -c.height, c.colour);
                }

                ColourInfo applyDrawColour(Color4 colour)
                {
                    ColourInfo result = DrawColourInfo.Colour;
                    result.ApplyChild(colour);
                    return result;
                }

                void drawRange(float x0, float x1, float top, float bottom, ColourInfo colour)
                {
                    float y0 = centre - Math.Clamp(top, -1, 1) * centre;
                    float y1 = centre - Math.Clamp(bottom, -1, 1) * centre;

                    if (y1 - y0 < minimumHeight)
                    {
                        float middle = (y0 + y1) / 2;
                        y0 = middle - minimumHeight / 2;
                        y1 = middle + minimumHeight / 2;
                    }

                    Quad quad = new Quad(
                        new Vector2(x0, y0),
                        new Vector2(x1, y0),
                        new Vector2(x0, y1),
                        new Vector2(x1, y1)) * DrawInfo.Matrix;

                    if (quad.Size.X == 0 || quad.Size.Y == 0)
                        return;

                    renderer.DrawQuad(texture, quad, colour, null, vertexBatch!.AddAction);
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                vertexBatch?.Dispose();
            }
        }
    }
}
