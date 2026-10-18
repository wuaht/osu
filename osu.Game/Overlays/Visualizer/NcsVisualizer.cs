// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// A visualizer in the style of NoCopyrightSounds: a title on the left and a rotating, audio reactive sphere of dots on the right.
    /// </summary>
    /// <remarks>
    /// Modelled after the "NCS Spectrum" After Effects template, which renders a layered particle sphere with Trapcode Form.
    /// </remarks>
    public partial class NcsVisualizer : VisualizerDrawable
    {
        /// <summary>
        /// The horizontal position of the centre of the sphere, relative to the width of the screen.
        /// </summary>
        private const float sphere_position = 0.7f;

        /// <summary>
        /// The radius of the sphere, relative to the height of the screen.
        /// </summary>
        private const float sphere_radius = 0.2f;

        /// <summary>
        /// The horizontal position of the centre of the text, relative to the width of the screen.
        /// </summary>
        private const float text_position = 0.25f;

        /// <summary>
        /// The screen height the text is designed at. It scales with the screen beyond that.
        /// </summary>
        private const float design_height = 1080;

        private const double colour_fade_duration = 800;

        public override float BackgroundDim => 0.15f;

        public override float BackgroundZoom => 0.015f;

        private readonly Bindable<NcsTitle> titleSetting = new Bindable<NcsTitle>();

        private readonly Container textContainer;
        private readonly OsuSpriteText brandText;
        private readonly Container trackBar;
        private readonly Box trackBarBackground;
        private readonly TruncatingSpriteText trackText;

        private readonly DotSphere sphere;

        public NcsVisualizer(SpectrumAnalyser analyser)
            : base(analyser)
        {
            InternalChildren = new Drawable[]
            {
                sphere = new DotSphere(analyser) { RelativeSizeAxes = Axes.Both },
                textContainer = new Container
                {
                    RelativePositionAxes = Axes.X,
                    X = text_position,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 12),
                        Children = new Drawable[]
                        {
                            brandText = new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Font = OsuFont.Torus.With(size: 190, weight: FontWeight.Bold),
                                Spacing = new Vector2(-6, 0),
                                Shadow = false,
                            },
                            trackBar = new Container
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                AutoSizeAxes = Axes.Both,
                                Children = new Drawable[]
                                {
                                    trackBarBackground = new Box { RelativeSizeAxes = Axes.Both },
                                    trackText = new TruncatingSpriteText
                                    {
                                        Margin = new MarginPadding { Horizontal = 18, Vertical = 6 },
                                        MaxWidth = 640,
                                        Font = OsuFont.Torus.With(size: 26, weight: FontWeight.Bold),
                                        Spacing = new Vector2(3, 0),
                                        Shadow = false,
                                    },
                                },
                            },
                        },
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

            LocalisableString track = LocalisableString.Interpolate($"{new RomanisableString(metadata.ArtistUnicode, metadata.Artist)} - {new RomanisableString(metadata.TitleUnicode, metadata.Title)}");
            trackText.Text = track.ToUpper();
        }

        public override void ApplyPalette(BackgroundPalette palette, int seed)
        {
            base.ApplyPalette(palette, seed);

            bool darkText = palette.PrefersDarkText(BackgroundDim);
            Color4 textColour = darkText ? Color4.Black : Color4.White;

            // the track is written in the background colour, cut out of a bar in the text colour.
            brandText.FadeColour(textColour, colour_fade_duration, Easing.OutQuint);
            trackBarBackground.FadeColour(textColour, colour_fade_duration, Easing.OutQuint);
            trackText.FadeColour(darkText ? Color4.White : Color4.Black, colour_fade_duration, Easing.OutQuint);

            sphere.TransformTo(nameof(DotSphere.AccentColour), palette.ChooseAccentColour(seed), colour_fade_duration, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();

            sphere.Centre = new Vector2(sphere_position * DrawWidth, DrawHeight / 2);
            // keeps a margin to the edge of narrow screens, where the height alone would make the sphere too large.
            sphere.Radius = Math.Min(sphere_radius * DrawHeight, (1 - sphere_position) * DrawWidth * 0.7f);

            // the text scales with the screen, but never grows past the left half.
            float scale = Math.Min(DrawHeight / design_height, DrawWidth * text_position * 1.8f / Math.Max(1, textContainer.DrawWidth));

            // a subtle pulse, enough to feel the beat without distracting from reading.
            textContainer.Scale = new Vector2(scale * (1 + Analyser.Beat * 0.01f));
        }

        /// <summary>
        /// Concentric shells of dots forming a sphere, which rotates slowly in perspective and is pushed outwards by the spectrum.
        /// </summary>
        private partial class DotSphere : Drawable
        {
            /// <summary>
            /// The radii of the shells, relative to <see cref="Radius"/>, from the outside in.
            /// </summary>
            private static readonly float[] shell_radii = { 1, 0.82f, 0.64f };

            /// <summary>
            /// The opacity of the shells, from the outside in.
            /// </summary>
            private static readonly float[] shell_opacity = { 1, 0.55f, 0.3f };

            /// <summary>
            /// The number of rings of latitude on the outermost shell. Inner shells have proportionally fewer.
            /// </summary>
            private const int latitude_count = 56;

            /// <summary>
            /// The distance of the camera from the centre of the sphere, relative to <see cref="Radius"/>.
            /// </summary>
            private const float camera_distance = 3.5f;

            /// <summary>
            /// The rotation around the vertical axis, in radians per millisecond.
            /// </summary>
            private const float rotation_speed = 0.00012f;

            /// <summary>
            /// How far the sphere is tilted towards the camera, in radians.
            /// </summary>
            private const float tilt = 0.4f;

            /// <summary>
            /// How far the spectrum pushes dots outwards at full amplitude, relative to <see cref="Radius"/>.
            /// </summary>
            private const float spectrum_displacement = 0.2f;

            /// <summary>
            /// Shapes the response to the spectrum, so quiet bands barely move the dots.
            /// </summary>
            private const float response_curve = 1.5f;

            /// <summary>
            /// How far noise displaces dots, relative to <see cref="Radius"/>.
            /// </summary>
            private const float noise_displacement = 0.035f;

            /// <summary>
            /// How fast the noise evolves, per millisecond.
            /// </summary>
            private const float noise_speed = 0.0003f;

            /// <summary>
            /// The radius of a dot at the centre of the sphere, in pixels.
            /// </summary>
            private const float dot_radius = 1.7f;

            /// <summary>
            /// The radius of the glow around each dot, relative to the dot.
            /// </summary>
            private const float dot_glow_size = 5;

            /// <summary>
            /// The position of the centre of the sphere.
            /// </summary>
            public Vector2 Centre;

            /// <summary>
            /// The radius of the outermost shell, in pixels.
            /// </summary>
            public float Radius;

            /// <summary>
            /// The colour of the glow. Dots facing the camera are lighter.
            /// </summary>
            public Color4 AccentColour { get; set; } = Color4.White;

            private readonly SpectrumAnalyser analyser;

            private readonly Vector3[] points;
            private readonly int[] pointShells;

            /// <summary>
            /// The position of each point in the spectrum, as a fractional band index.
            /// </summary>
            private readonly float[] pointBands;

            private readonly Dot[] dots;

            /// <summary>
            /// The spatially smoothed band levels.
            /// </summary>
            private readonly float[] smoothed;

            private IShader shader = null!;
            private Texture dotTexture = null!;
            private Texture glowTexture = null!;

            public DotSphere(SpectrumAnalyser analyser)
            {
                this.analyser = analyser;

                smoothed = new float[analyser.BandCount];

                var pointList = new List<Vector3>();
                var shellList = new List<int>();

                for (int shell = 0; shell < shell_radii.Length; shell++)
                {
                    int latitudes = Math.Max(8, (int)MathF.Round(latitude_count * shell_radii[shell]));

                    for (int i = 0; i < latitudes; i++)
                    {
                        // excludes the poles themselves, where a ring would collapse into a single bright point.
                        float polar = MathF.PI * (i + 0.5f) / latitudes;
                        float ringRadius = MathF.Sin(polar);
                        float y = MathF.Cos(polar);

                        // keeps the spacing between dots equal along every ring.
                        int count = Math.Max(3, (int)MathF.Round(latitudes * 2 * ringRadius));
                        // staggers alternate rings, so the dots don't line up in visible meridians.
                        float offset = (i % 2) * 0.5f;

                        for (int j = 0; j < count; j++)
                        {
                            float azimuth = MathF.Tau * (j + offset) / count;

                            pointList.Add(new Vector3(ringRadius * MathF.Cos(azimuth), y, ringRadius * MathF.Sin(azimuth)));
                            shellList.Add(shell);
                        }
                    }
                }

                points = pointList.ToArray();
                pointShells = shellList.ToArray();
                pointBands = new float[points.Length];

                // mirrored around the equator, with the bass at the equator and the treble at the poles.
                for (int i = 0; i < points.Length; i++)
                    pointBands[i] = MathF.Abs(points[i].Y) * (analyser.BandCount - 1);

                dots = new Dot[points.Length];
            }

            [BackgroundDependencyLoader]
            private void load(IRenderer renderer, ShaderManager shaders)
            {
                dotTexture = SoftDotTexture.Create(renderer);
                glowTexture = SoftDotTexture.CreateGlow(renderer);
                shader = shaders.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            }

            protected override void Update()
            {
                base.Update();

                var bands = analyser.Bands;

                for (int i = 0; i < smoothed.Length; i++)
                    smoothed[i] = 0.25f * bands[Math.Max(0, i - 1)] + 0.5f * bands[i] + 0.25f * bands[Math.Min(bands.Length - 1, i + 1)];

                float time = (float)Time.Current;
                float noiseTime = time * noise_speed;

                float rotation = time * rotation_speed;
                float cosRotation = MathF.Cos(rotation);
                float sinRotation = MathF.Sin(rotation);
                float cosTilt = MathF.Cos(tilt);
                float sinTilt = MathF.Sin(tilt);

                // the whole sphere breathes with the bass.
                float pulse = 1 + analyser.Bass * 0.06f + analyser.Beat * 0.02f;

                for (int i = 0; i < points.Length; i++)
                {
                    var point = points[i];
                    int shell = pointShells[i];

                    float bandPosition = pointBands[i];
                    int band = Math.Min((int)bandPosition, smoothed.Length - 2);
                    float level = Math.Clamp(float.Lerp(smoothed[band], smoothed[band + 1], bandPosition - band), 0, 1);

                    // inner shells react less, so the sphere keeps its shape.
                    float reaction = MathF.Pow(level, response_curve) * spectrum_displacement * shell_radii[shell];
                    float noise = PerlinNoise.Sample(point.X * 1.8f + shell * 7.3f, point.Y * 1.8f, point.Z * 1.8f + noiseTime);

                    float distance = shell_radii[shell] * pulse + reaction + noise * noise_displacement * (1 + level * 2);
                    var position = point * distance;

                    // rotates around the vertical axis, then tilts towards the camera.
                    float x = position.X * cosRotation + position.Z * sinRotation;
                    float z = -position.X * sinRotation + position.Z * cosRotation;
                    float y = position.Y * cosTilt - z * sinTilt;
                    z = position.Y * sinTilt + z * cosTilt;

                    float perspective = camera_distance / (camera_distance - z);

                    // dots facing the camera are larger and brighter.
                    float facing = Math.Clamp((z + 1) / 2, 0, 1);

                    dots[i] = new Dot
                    {
                        Position = new Vector2(x, -y) * Radius * perspective,
                        Radius = dot_radius * perspective * (0.7f + facing * 0.5f + level * 0.6f),
                        Alpha = shell_opacity[shell] * (0.15f + facing * 0.85f),
                        Lightness = facing,
                    };
                }

                Invalidate(Invalidation.DrawNode);
            }

            protected override DrawNode CreateDrawNode() => new DotSphereDrawNode(this);

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                dotTexture?.Dispose();
                glowTexture?.Dispose();
            }

            private struct Dot
            {
                public Vector2 Position;
                public float Radius;
                public float Alpha;

                /// <summary>
                /// How far the dot is lightened towards white.
                /// </summary>
                public float Lightness;
            }

            private class DotSphereDrawNode : DrawNode
            {
                protected new DotSphere Source => (DotSphere)base.Source;

                /// <summary>
                /// The radius of the halo behind the sphere, relative to the sphere.
                /// </summary>
                private const float halo_size = 1.9f;

                private IShader shader = null!;
                private Texture dotTexture = null!;
                private Texture glowTexture = null!;
                private Vector2 centre;
                private float radius;
                private float bass;
                private Color4 accent;

                private readonly Dot[] dots;

                private IVertexBatch<TexturedVertex2D>? vertexBatch;

                public DotSphereDrawNode(DotSphere source)
                    : base(source)
                {
                    dots = new Dot[source.dots.Length];
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    shader = Source.shader;
                    dotTexture = Source.dotTexture;
                    glowTexture = Source.glowTexture;
                    centre = Source.Centre;
                    radius = Source.Radius;
                    bass = Source.analyser.Bass;
                    accent = Source.AccentColour;

                    Source.dots.AsSpan().CopyTo(dots);
                }

                protected override void Draw(IRenderer renderer)
                {
                    base.Draw(renderer);

                    if (!dotTexture.Available || !glowTexture.Available)
                        return;

                    // the batch is drawn in several parts if it fills up.
                    vertexBatch ??= renderer.CreateQuadBatch<TexturedVertex2D>(8192, 4);

                    shader.Bind();

                    // additive, so overlapping dots and glows bloom like the light of the original.
                    renderer.SetBlend(BlendingParameters.Additive);

                    drawSprite(renderer, glowTexture, centre, radius * halo_size, accent.Opacity(0.12f + bass * 0.1f));

                    foreach (var dot in dots)
                        drawSprite(renderer, glowTexture, centre + dot.Position, dot.Radius * dot_glow_size, accent.Opacity(dot.Alpha * 0.07f));

                    foreach (var dot in dots)
                    {
                        var colour = Interpolation.ValueAt(dot.Lightness * 0.65f, accent, Color4.White, 0, 1);
                        drawSprite(renderer, dotTexture, centre + dot.Position, dot.Radius, colour.Opacity(dot.Alpha));
                    }

                    renderer.SetBlend(DrawColourInfo.Blending);

                    shader.Unbind();
                }

                private void drawSprite(IRenderer renderer, Texture texture, Vector2 position, float size, Color4 tint)
                {
                    var quad = new Quad(
                        Vector2Extensions.Transform(position + new Vector2(-size, -size), DrawInfo.Matrix),
                        Vector2Extensions.Transform(position + new Vector2(size, -size), DrawInfo.Matrix),
                        Vector2Extensions.Transform(position + new Vector2(-size, size), DrawInfo.Matrix),
                        Vector2Extensions.Transform(position + new Vector2(size, size), DrawInfo.Matrix));

                    ColourInfo colour = DrawColourInfo.Colour;
                    colour.ApplyChild(tint);

                    renderer.DrawQuad(texture, quad, colour, vertexAction: vertexBatch!.AddAction);
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
