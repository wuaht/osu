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
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// A visualizer in the style of NoCopyrightSounds: a title on the left and a breathing ring of dots on the right.
    /// </summary>
    public partial class NcsVisualizer : VisualizerDrawable
    {
        /// <summary>
        /// The horizontal position of the centre of the ring, relative to the width of the screen.
        /// </summary>
        private const float ring_position = 0.7f;

        /// <summary>
        /// The size of the ring, relative to the smaller dimension of the screen.
        /// </summary>
        private const float ring_size = 0.34f;

        private const double colour_fade_duration = 800;

        public override float BackgroundDim => 0.15f;

        public override float BackgroundZoom => 0.02f;

        private readonly Bindable<NcsTitle> titleSetting = new Bindable<NcsTitle>();

        private readonly Container textContainer;
        private readonly OsuSpriteText brandText;
        private readonly TruncatingSpriteText trackText;

        private readonly CircularContainer glow;
        private readonly DotRing ring;

        public NcsVisualizer(SpectrumAnalyser analyser)
            : base(analyser)
        {
            InternalChildren = new Drawable[]
            {
                glow = new CircularContainer
                {
                    RelativePositionAxes = Axes.X,
                    X = ring_position,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Masking = true,
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Colour = Color4.White,
                        Radius = 80,
                        Hollow = true,
                    },
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        AlwaysPresent = true,
                    },
                },
                ring = new DotRing(analyser) { RelativeSizeAxes = Axes.Both },
                textContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = 0.5f,
                    Padding = new MarginPadding { Left = 100, Right = 40 },
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            brandText = new OsuSpriteText
                            {
                                Font = OsuFont.Torus.With(size: 150, weight: FontWeight.Bold),
                                Shadow = false,
                            },
                            trackText = new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                // align with the left edge of the glyphs above, which have a side bearing at their size.
                                Margin = new MarginPadding { Left = 8 },
                                Font = OsuFont.Torus.With(size: 30, weight: FontWeight.SemiBold),
                                Shadow = false,
                            },
                        }
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopVisualizerNcsTitle, titleSetting);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            titleSetting.BindValueChanged(e =>
            {
                brandText.Text = e.NewValue == NcsTitle.Ncs ? @"NCS" : @"slop!";
                brandText.FadeInFromZero(300, Easing.OutQuint);
            }, true);
        }

        public override void ApplyMetadata(BeatmapMetadata metadata)
        {
            base.ApplyMetadata(metadata);

            trackText.Text = LocalisableString.Interpolate($"{new RomanisableString(metadata.ArtistUnicode, metadata.Artist)} - {new RomanisableString(metadata.TitleUnicode, metadata.Title)}");
        }

        public override void ApplyPalette(BackgroundPalette palette, int seed)
        {
            base.ApplyPalette(palette, seed);

            textContainer.FadeColour(palette.PrefersDarkText(BackgroundDim) ? Color4.Black : Color4.White, colour_fade_duration, Easing.OutQuint);

            Color4 accent = palette.ChooseAccentColour(seed);

            ring.FadeColour(accent, colour_fade_duration, Easing.OutQuint);
            glow.FadeEdgeEffectTo(accent.Opacity(0.35f), colour_fade_duration, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();

            float size = ring_size * Math.Min(DrawWidth, DrawHeight);

            ring.Centre = new Vector2(ring_position * DrawWidth, DrawHeight / 2);
            ring.RingSize = size;

            glow.Size = new Vector2(size * 1.3f * (1 + Analyser.Bass * 0.15f));
            glow.Alpha = 0.5f + Analyser.Bass * 0.5f;

            // a subtle pulse, enough to feel the beat without distracting from reading.
            textContainer.Scale = new Vector2(1 + Analyser.Beat * 0.015f);
        }

        /// <summary>
        /// Layers of dots on a circle, pushed outwards by the spectrum and displaced by noise.
        /// Dots are tinted by <see cref="Drawable.Colour"/>.
        /// </summary>
        private partial class DotRing : Drawable
        {
            private const int layer_count = 3;
            private const int dots_per_layer = 300;

            /// <summary>
            /// The radius of dots when silent, relative to <see cref="RingSize"/>.
            /// </summary>
            private const float min_radius = 0.675f;

            /// <summary>
            /// The radius of dots at full amplitude, relative to <see cref="RingSize"/>.
            /// </summary>
            private const float max_radius = 0.9f;

            /// <summary>
            /// How far noise displaces dots, relative to <see cref="RingSize"/>.
            /// </summary>
            private const float noise_displacement = 0.05f;

            /// <summary>
            /// How fast the noise evolves, per millisecond.
            /// </summary>
            private const float noise_speed = 0.00035f;

            /// <summary>
            /// How far each layer lags behind the previous one, in milliseconds.
            /// </summary>
            private const double layer_lag = 45;

            /// <summary>
            /// The position of the centre of the ring.
            /// </summary>
            public Vector2 Centre;

            /// <summary>
            /// The size of the ring, in pixels.
            /// </summary>
            public float RingSize;

            private readonly SpectrumAnalyser analyser;

            /// <summary>
            /// The time-smoothed band levels per layer.
            /// </summary>
            private readonly float[][] layerLevels;

            /// <summary>
            /// The spatially smoothed band levels of the layer being computed.
            /// </summary>
            private readonly float[] smoothed;

            private readonly Dot[] dots = new Dot[layer_count * dots_per_layer];

            private IShader shader = null!;
            private Texture texture = null!;

            public DotRing(SpectrumAnalyser analyser)
            {
                this.analyser = analyser;

                layerLevels = new float[layer_count][];
                for (int i = 0; i < layer_count; i++)
                    layerLevels[i] = new float[analyser.BandCount];

                smoothed = new float[analyser.BandCount];
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

                var bands = analyser.Bands;
                float time = (float)Time.Current * noise_speed;

                for (int layer = 0; layer < layer_count; layer++)
                {
                    float[] levels = layerLevels[layer];
                    float factor = layer == 0 ? 1 : (float)(1 - Math.Exp(-Time.Elapsed / (layer * layer_lag)));

                    for (int i = 0; i < levels.Length; i++)
                        levels[i] += (bands[i] - levels[i]) * factor;

                    for (int i = 0; i < levels.Length; i++)
                        smoothed[i] = 0.25f * levels[Math.Max(0, i - 1)] + 0.5f * levels[i] + 0.25f * levels[Math.Min(levels.Length - 1, i + 1)];

                    for (int i = 0; i < dots_per_layer; i++)
                    {
                        float angle = MathF.Tau * i / dots_per_layer;

                        // mirrored left to right, with the bass at the top.
                        float bandPosition = MathF.Abs(MathF.PI - angle) / MathF.PI;
                        bandPosition = (1 - bandPosition) * (smoothed.Length - 1);

                        int band = Math.Min((int)bandPosition, smoothed.Length - 2);
                        float level = float.Lerp(smoothed[band], smoothed[band + 1], bandPosition - band);
                        float amplitude = Math.Clamp(level * 0.8f + analyser.Bass * 0.35f, 0, 1);

                        var direction = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));

                        // sampled on a circle in noise space, so the displacement is seamless around the ring.
                        float noise = PerlinNoise.Sample(direction.X * 1.6f + 10, direction.Y * 1.6f + 10, time + layer * 4.7f);

                        float radius = float.Lerp(min_radius, max_radius, amplitude) + noise * noise_displacement * (0.5f + amplitude);

                        dots[layer * dots_per_layer + i] = new Dot
                        {
                            Position = direction * radius * RingSize / 2,
                            Radius = 1.6f + amplitude * 1.6f,
                            // later layers lag behind and are fainter, forming a trail.
                            Alpha = 0.95f - layer * 0.25f,
                        };
                    }
                }

                Invalidate(Invalidation.DrawNode);
            }

            protected override DrawNode CreateDrawNode() => new DotRingDrawNode(this);

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                texture?.Dispose();
            }

            private struct Dot
            {
                public Vector2 Position;
                public float Radius;
                public float Alpha;
            }

            private class DotRingDrawNode : DrawNode
            {
                protected new DotRing Source => (DotRing)base.Source;

                private IShader shader = null!;
                private Texture texture = null!;
                private Vector2 centre;

                private readonly Dot[] dots = new Dot[layer_count * dots_per_layer];

                private IVertexBatch<TexturedVertex2D>? vertexBatch;

                public DotRingDrawNode(DotRing source)
                    : base(source)
                {
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    shader = Source.shader;
                    texture = Source.texture;
                    centre = Source.Centre;

                    Source.dots.AsSpan().CopyTo(dots);
                }

                protected override void Draw(IRenderer renderer)
                {
                    base.Draw(renderer);

                    if (!texture.Available)
                        return;

                    vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(dots.Length, 2);

                    shader.Bind();

                    // drawn back to front, so the most responsive layer is on top.
                    for (int i = dots.Length - 1; i >= 0; i--)
                    {
                        var dot = dots[i];
                        var position = centre + dot.Position;
                        float r = dot.Radius;

                        var quad = new Quad(
                            Vector2Extensions.Transform(position + new Vector2(-r, -r), DrawInfo.Matrix),
                            Vector2Extensions.Transform(position + new Vector2(r, -r), DrawInfo.Matrix),
                            Vector2Extensions.Transform(position + new Vector2(-r, r), DrawInfo.Matrix),
                            Vector2Extensions.Transform(position + new Vector2(r, r), DrawInfo.Matrix));

                        ColourInfo colour = DrawColourInfo.Colour;
                        colour.ApplyChild(Color4.White.Opacity(dot.Alpha));

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
