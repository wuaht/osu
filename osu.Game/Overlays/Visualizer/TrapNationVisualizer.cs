// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// A visualizer in the style of the Trap Nation channel: a pulsing logo surrounded by a mirrored, layered spectrum, with particles flying past.
    /// </summary>
    public partial class TrapNationVisualizer : VisualizerDrawable
    {
        /// <summary>
        /// The diameter of the logo, relative to the smaller dimension of the screen.
        /// </summary>
        private const float logo_size = 0.32f;

        /// <summary>
        /// The size logos are designed at. They are scaled to their actual size.
        /// </summary>
        private const float logo_design_size = 300;

        /// <summary>
        /// How far the logo shakes on beats, in pixels.
        /// </summary>
        private const float logo_shake = 6;

        public override float BackgroundDim => 0.35f;

        public override float BackgroundZoom => 0.04f;

        public override float BackgroundShake => 10;

        private readonly SpectrumRing ring;
        private readonly CircularContainer logoContainer;

        private readonly Bindable<TrapNationLogo> logo = new Bindable<TrapNationLogo>();

        private TextureStore textures = null!;

        public TrapNationVisualizer(SpectrumAnalyser analyser)
            : base(analyser)
        {
            InternalChildren = new Drawable[]
            {
                new ParticleField(analyser) { RelativeSizeAxes = Axes.Both },
                ring = new SpectrumRing(analyser) { RelativeSizeAxes = Axes.Both },
                logoContainer = new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(logo_design_size),
                    Masking = true,
                    BorderColour = Color4.White,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Color4.Black.Opacity(0.4f),
                        Radius = 30,
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, TextureStore textures)
        {
            this.textures = textures;
            config.BindWith(OsuSetting.SlopVisualizerTrapNationLogo, logo);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            logo.BindValueChanged(e =>
            {
                bool trapNation = e.NewValue == TrapNationLogo.TrapNation;
                Drawable content = trapNation ? createTrapNationLogo() : createSlopLogo();

                // the slop! logo brings its own ring.
                logoContainer.BorderThickness = trapNation ? 16 : 0;

                logoContainer.Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                        },
                        content,
                    },
                };

                content.FadeInFromZero(300, Easing.OutQuint);
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            float diameter = logo_size * Math.Min(DrawWidth, DrawHeight) * (1 + Analyser.Bass * 0.12f + Analyser.Beat * 0.05f);

            // noise rather than random offsets, so the shake is jittery but still continuous.
            float time = (float)Time.Current * 0.02f;
            var shake = new Vector2(PerlinNoise.Sample(time, 0.5f, 0), PerlinNoise.Sample(0.5f, time, 7.3f)) * Analyser.Beat * logo_shake;

            logoContainer.Scale = new Vector2(diameter / logo_design_size);
            logoContainer.Position = shake;

            // starts slightly inside the logo, so the base of the spectrum is hidden under the logo's border.
            ring.Radius = diameter / 2 - 4;
            ring.CentreOffset = shake;
        }

        private Drawable createSlopLogo() => new Sprite
        {
            RelativeSizeAxes = Axes.Both,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            FillMode = FillMode.Fit,
            Texture = textures.Get(@"Icons/SlopLogo"),
        };

        private static Drawable createTrapNationLogo()
        {
            const float outer_width = 150;
            const float inner_width = 92;

            return new Container
            {
                RelativeSizeAxes = Axes.Both,
                // the triangles are positioned by their centroids, which sit low in their bounding boxes. move up a little for optical balance.
                Y = -8,
                Children = new[]
                {
                    createTriangle(outer_width, Color4.White),
                    createTriangle(inner_width, Color4.Black),
                },
            };

            static Drawable createTriangle(float width, Color4 colour)
            {
                float height = width * MathF.Sqrt(3) / 2;

                return new osu.Framework.Graphics.Shapes.Triangle
                {
                    Anchor = Anchor.Centre,
                    Size = new Vector2(width, height),
                    // the centroid, so that triangles of different sizes are concentric and their outline is equally thick.
                    OriginPosition = new Vector2(width / 2, height * 2 / 3),
                    Colour = colour,
                };
            }
        }

        /// <summary>
        /// The spectrum mirrored around the logo, with lagging coloured copies behind it.
        /// </summary>
        private partial class SpectrumRing : Drawable
        {
            /// <summary>
            /// The colours of the layers, from back to front.
            /// </summary>
            private static readonly Color4[] layer_colours =
            {
                Color4Extensions.FromHex(@"32cd32"),
                Color4Extensions.FromHex(@"3a5fcd"),
                Color4Extensions.FromHex(@"c71585"),
                Color4Extensions.FromHex(@"e0203c"),
                Color4Extensions.FromHex(@"ffb90f"),
                Color4.White,
            };

            /// <summary>
            /// How far each layer lags behind the one in front of it, in milliseconds.
            /// </summary>
            private const double layer_lag = 30;

            /// <summary>
            /// How much larger each layer is than the one in front of it, so lagging layers still peek out.
            /// </summary>
            private const float layer_growth = 0.07f;

            /// <summary>
            /// The maximum height of the spectrum, relative to <see cref="Radius"/>.
            /// </summary>
            private const float max_height = 0.75f;

            /// <summary>
            /// The height of the spectrum when silent, in pixels.
            /// </summary>
            private const float min_height = 3;

            /// <summary>
            /// The number of interpolated segments between each band.
            /// </summary>
            private const int subdivisions = 4;

            /// <summary>
            /// The inner radius of the spectrum.
            /// </summary>
            public float Radius;

            /// <summary>
            /// The offset of the centre of the spectrum from the centre of this drawable.
            /// </summary>
            public Vector2 CentreOffset;

            private readonly SpectrumAnalyser analyser;

            /// <summary>
            /// The level of every band per layer, from back to front.
            /// </summary>
            private readonly float[][] layers;

            private IShader shader = null!;
            private Texture texture = null!;

            public SpectrumRing(SpectrumAnalyser analyser)
            {
                this.analyser = analyser;

                layers = new float[layer_colours.Length][];
                for (int i = 0; i < layers.Length; i++)
                    layers[i] = new float[analyser.BandCount];
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer, ShaderManager shaders)
            {
                texture = renderer.WhitePixel;
                shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            }

            protected override void Update()
            {
                base.Update();

                var bands = analyser.Bands;

                for (int layer = 0; layer < layers.Length; layer++)
                {
                    int depth = layers.Length - 1 - layer;
                    float factor = depth == 0 ? 1 : (float)(1 - Math.Exp(-Time.Elapsed / (depth * layer_lag)));

                    float[] levels = layers[layer];
                    for (int i = 0; i < levels.Length; i++)
                        levels[i] += (bands[i] - levels[i]) * factor;
                }

                Invalidate(Invalidation.DrawNode);
            }

            protected override DrawNode CreateDrawNode() => new SpectrumRingDrawNode(this);

            private class SpectrumRingDrawNode : DrawNode
            {
                protected new SpectrumRing Source => (SpectrumRing)base.Source;

                private IShader shader = null!;
                private Texture texture = null!;

                private Vector2 centre;
                private float radius;

                private float[][] layers = Array.Empty<float[]>();

                /// <summary>
                /// The spatially smoothed levels of the layer being drawn.
                /// </summary>
                private float[] smoothed = Array.Empty<float>();

                /// <summary>
                /// The interpolated heights of the layer being drawn, from the top of the spectrum to the bottom.
                /// </summary>
                private float[] heights = Array.Empty<float>();

                private IVertexBatch<TexturedVertex2D>? vertexBatch;

                public SpectrumRingDrawNode(SpectrumRing source)
                    : base(source)
                {
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    shader = Source.shader;
                    texture = Source.texture;
                    centre = Source.DrawSize / 2 + Source.CentreOffset;
                    radius = Source.Radius;

                    if (layers.Length != Source.layers.Length)
                        layers = new float[Source.layers.Length][];

                    for (int i = 0; i < layers.Length; i++)
                    {
                        int bandCount = Source.layers[i].Length;

                        if (layers[i]?.Length != bandCount)
                            layers[i] = new float[bandCount];

                        Source.layers[i].AsSpan().CopyTo(layers[i]);
                    }
                }

                protected override void Draw(IRenderer renderer)
                {
                    base.Draw(renderer);

                    if (radius <= 0 || layers.Length == 0 || layers[0].Length < 2)
                        return;

                    int bandCount = layers[0].Length;
                    int segments = (bandCount - 1) * subdivisions;

                    if (smoothed.Length != bandCount)
                    {
                        smoothed = new float[bandCount];
                        heights = new float[segments + 1];
                    }

                    vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(segments * 2 * layer_colours.Length, 2);

                    shader.Bind();

                    Vector2 inflation = DrawInfo.MatrixInverse.ExtractScale().Xy;

                    for (int layer = 0; layer < layers.Length; layer++)
                    {
                        int depth = layers.Length - 1 - layer;
                        float scale = radius * max_height * (1 + depth * layer_growth);

                        computeHeights(layers[layer], scale);

                        ColourInfo colour = DrawColourInfo.Colour;
                        colour.ApplyChild(layer_colours[layer]);

                        for (int k = 0; k < segments; k++)
                        {
                            float angle1 = MathF.PI * k / segments;
                            float angle2 = MathF.PI * (k + 1) / segments;

                            // the right half, then the mirrored left half.
                            drawSegment(renderer, angle1, angle2, heights[k], heights[k + 1], 1, colour, inflation);
                            drawSegment(renderer, angle1, angle2, heights[k], heights[k + 1], -1, colour, inflation);
                        }
                    }

                    shader.Unbind();
                }

                /// <summary>
                /// Fills <see cref="heights"/> with smoothly interpolated heights for the given band levels.
                /// </summary>
                private void computeHeights(float[] levels, float scale)
                {
                    // a light blur removes the jaggedness of adjacent bands moving independently.
                    for (int i = 0; i < levels.Length; i++)
                        smoothed[i] = 0.25f * mirrored(levels, i - 1) + 0.5f * levels[i] + 0.25f * mirrored(levels, i + 1);

                    for (int k = 0; k < heights.Length; k++)
                    {
                        int i = k / subdivisions;
                        float t = (float)(k % subdivisions) / subdivisions;

                        float p0 = mirrored(smoothed, i - 1);
                        float p1 = mirrored(smoothed, i);
                        float p2 = mirrored(smoothed, i + 1);
                        float p3 = mirrored(smoothed, i + 2);

                        // catmull-rom spline through the band levels.
                        float level = 0.5f * (2 * p1
                                              + (p2 - p0) * t
                                              + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                              + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t);

                        heights[k] = min_height + Math.Max(0, level) * scale;
                    }
                }

                /// <summary>
                /// Reads a level, reflecting out of range indices back into range.
                /// As the spectrum is mirrored at the top and the bottom, this keeps it smooth across both joins.
                /// </summary>
                private static float mirrored(float[] values, int index)
                {
                    int last = values.Length - 1;

                    if (index < 0)
                        index = -index;
                    else if (index > last)
                        index = 2 * last - index;

                    return values[Math.Clamp(index, 0, last)];
                }

                private void drawSegment(IRenderer renderer, float angle1, float angle2, float height1, float height2, float side, ColourInfo colour, Vector2 inflation)
                {
                    // angles are measured clockwise from the top of the ring, with the left half mirrored.
                    var direction1 = new Vector2(side * MathF.Sin(angle1), -MathF.Cos(angle1));
                    var direction2 = new Vector2(side * MathF.Sin(angle2), -MathF.Cos(angle2));

                    var quad = new Quad(
                        Vector2Extensions.Transform(centre + direction1 * radius, DrawInfo.Matrix),
                        Vector2Extensions.Transform(centre + direction1 * (radius + height1), DrawInfo.Matrix),
                        Vector2Extensions.Transform(centre + direction2 * radius, DrawInfo.Matrix),
                        Vector2Extensions.Transform(centre + direction2 * (radius + height2), DrawInfo.Matrix));

                    // only smooth the outer edge. smoothing between segments would leave visible seams.
                    renderer.DrawQuad(texture, quad, colour, null, vertexBatch!.AddAction, new Vector2(inflation.X / Math.Max(height1, height2), 0));
                }

                protected override void Dispose(bool isDisposing)
                {
                    base.Dispose(isDisposing);

                    vertexBatch?.Dispose();
                }
            }
        }

        /// <summary>
        /// Soft particles flying outwards from the centre, faster the louder the music is.
        /// </summary>
        private partial class ParticleField : Drawable
        {
            private const int particle_count = 160;

            /// <summary>
            /// The speed at which particles fly when silent, in distance per millisecond.
            /// </summary>
            private const float base_speed = 0.00008f;

            /// <summary>
            /// The additional speed at full loudness, in distance per millisecond.
            /// </summary>
            private const float loudness_speed = 0.0007f;

            /// <summary>
            /// The additional speed on beats, in distance per millisecond.
            /// </summary>
            private const float beat_speed = 0.0005f;

            private readonly SpectrumAnalyser analyser;
            private readonly Particle[] particles = new Particle[particle_count];

            private IShader shader = null!;
            private Texture texture = null!;

            public ParticleField(SpectrumAnalyser analyser)
            {
                this.analyser = analyser;

                Blending = BlendingParameters.Additive;

                // spread over the whole screen initially, so they don't all start in the centre.
                for (int i = 0; i < particles.Length; i++)
                    particles[i] = Particle.Create(RNG.NextSingle());
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer, ShaderManager shaders)
            {
                texture = SoftDotTexture.Create(renderer);
                shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            }

            protected override void Update()
            {
                base.Update();

                float speed = base_speed + analyser.Loudness * loudness_speed + analyser.Beat * beat_speed;
                float elapsed = (float)Time.Elapsed;

                for (int i = 0; i < particles.Length; i++)
                {
                    ref var particle = ref particles[i];

                    // accelerate with distance for a sense of depth, as if flying towards the viewer.
                    particle.Distance += elapsed * speed * particle.Speed * (0.15f + particle.Distance);

                    if (particle.Distance > 1)
                        particle = Particle.Create(RNG.NextSingle(0.05f));
                }

                Invalidate(Invalidation.DrawNode);
            }

            protected override DrawNode CreateDrawNode() => new ParticleFieldDrawNode(this);

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                texture?.Dispose();
            }

            private struct Particle
            {
                public Vector2 Direction;

                /// <summary>
                /// The distance from the centre, relative to the distance to the corners of the screen.
                /// </summary>
                public float Distance;

                public float Speed;
                public float Size;
                public float Alpha;

                public static Particle Create(float distance)
                {
                    float angle = RNG.NextSingle(MathF.Tau);

                    return new Particle
                    {
                        Direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)),
                        Distance = distance,
                        Speed = RNG.NextSingle(0.6f, 1.4f),
                        Size = RNG.NextSingle(1.5f, 4.5f),
                        Alpha = RNG.NextSingle(0.25f, 0.8f),
                    };
                }
            }

            private class ParticleFieldDrawNode : DrawNode
            {
                protected new ParticleField Source => (ParticleField)base.Source;

                private IShader shader = null!;
                private Texture texture = null!;

                private Vector2 centre;
                private float maxDistance;

                private readonly Particle[] particles = new Particle[particle_count];

                private IVertexBatch<TexturedVertex2D>? vertexBatch;

                public ParticleFieldDrawNode(ParticleField source)
                    : base(source)
                {
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    shader = Source.shader;
                    texture = Source.texture;
                    centre = Source.DrawSize / 2;
                    maxDistance = centre.Length;

                    Source.particles.AsSpan().CopyTo(particles);
                }

                protected override void Draw(IRenderer renderer)
                {
                    base.Draw(renderer);

                    if (!texture.Available)
                        return;

                    vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(particle_count, 2);

                    shader.Bind();

                    foreach (var particle in particles)
                    {
                        // fade in when leaving the centre, as particles respawn there.
                        float alpha = particle.Alpha * Math.Clamp(particle.Distance / 0.15f, 0, 1);

                        if (alpha <= 0)
                            continue;

                        // grow with distance for a sense of depth.
                        float size = particle.Size * (0.4f + 1.2f * particle.Distance);
                        var position = centre + particle.Direction * particle.Distance * maxDistance;

                        var quad = new Quad(
                            Vector2Extensions.Transform(position + new Vector2(-size, -size), DrawInfo.Matrix),
                            Vector2Extensions.Transform(position + new Vector2(size, -size), DrawInfo.Matrix),
                            Vector2Extensions.Transform(position + new Vector2(-size, size), DrawInfo.Matrix),
                            Vector2Extensions.Transform(position + new Vector2(size, size), DrawInfo.Matrix));

                        ColourInfo colour = DrawColourInfo.Colour;
                        colour.ApplyChild(Color4.White.Opacity(alpha));

                        renderer.DrawQuad(texture, quad, colour, vertexAction: vertexBatch.AddAction);
                    }

                    shader.Unbind();
                }

                protected override void Dispose(bool isDisposing)
                {
                    base.Dispose(isDisposing);

                    vertexBatch?.Dispose();
                }
            }
        }
    }
}
