// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Components.Timelines.Summary.Parts
{
    /// <summary>
    /// Displays the difficulty strain of the beatmap over time (like osu!stable).
    /// The part before the current time is white, the part after it grey.
    /// </summary>
    public partial class StrainPart : CompositeDrawable
    {
        private const float graph_alpha = 0.8f;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmapDifficulty? beatmapDifficulty { get; set; }

        private readonly IBindable<EditorBeatmapDifficultyInfo?> difficultyInfo = new Bindable<EditorBeatmapDifficultyInfo?>();

        private Bindable<bool> showStrains = null!;

        private Container pastContainer = null!;
        private Container futureContainer = null!;
        private StrainGraph pastGraph = null!;
        private StrainGraph futureGraph = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            showStrains = config.GetBindable<bool>(OsuSetting.SlopEditorShowDifficultyStrains);

            Alpha = 0;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = graph_alpha,
                Children = new Drawable[]
                {
                    // the graph is displayed twice, each masked to the part before or after the current time.
                    pastContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        Masking = true,
                        Child = pastGraph = new StrainGraph { Colour = Color4.White },
                    },
                    futureContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        Masking = true,
                        Child = futureGraph = new StrainGraph { Colour = OsuColour.Gray(0.75f) },
                    },
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (beatmapDifficulty != null)
                difficultyInfo.BindTo(beatmapDifficulty.Info);

            difficultyInfo.BindValueChanged(info =>
            {
                float[] strains = info.NewValue?.Strains ?? Array.Empty<float>();
                double startTime = info.NewValue?.StrainsStartTime ?? 0;

                pastGraph.SetStrains(strains, startTime);
                futureGraph.SetStrains(strains, startTime);
            }, true);

            showStrains.BindValueChanged(show => this.FadeTo(show.NewValue ? 1 : 0, 200, Easing.OutQuint), true);
        }

        protected override void Update()
        {
            base.Update();

            double trackLength = editorClock.TrackLength;

            if (trackLength <= 0)
                return;

            // both graphs span the whole width, which represents the whole track like the other parts of the summary timeline.
            pastGraph.Width = DrawWidth;
            futureGraph.Width = DrawWidth;
            pastGraph.TrackLength = trackLength;
            futureGraph.TrackLength = trackLength;

            float currentX = Math.Clamp((float)(editorClock.CurrentTime / trackLength * DrawWidth), 0, DrawWidth);

            pastContainer.Width = currentX;

            futureContainer.X = currentX;
            futureContainer.Width = DrawWidth - currentX;
            futureGraph.X = -currentX;
        }

        /// <summary>
        /// Draws the strains as columns of one unit width, each displaying the highest strain within the time it covers.
        /// This way, each column represents exactly its position in the track, regardless of how many sections there are.
        /// </summary>
        public partial class StrainGraph : Drawable
        {
            private float[] strains = Array.Empty<float>();
            private double strainsStartTime;

            /// <param name="strains">The strains in sections of <see cref="EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH"/>.</param>
            /// <param name="startTime">The start time of the first section.</param>
            public void SetStrains(float[] strains, double startTime)
            {
                this.strains = strains;
                strainsStartTime = startTime;
                columnsValid = false;
            }

            private double trackLength;

            public double TrackLength
            {
                set
                {
                    if (trackLength == value)
                        return;

                    trackLength = value;
                    columnsValid = false;
                }
            }

            private readonly List<float> columns = new List<float>();

            /// <summary>
            /// The height of each column of one unit width, relative to the height of the graph.
            /// </summary>
            public IReadOnlyList<float> Columns => columns;

            private bool columnsValid;
            private float lastDrawWidth;

            private IShader shader = null!;
            private Texture texture = null!;

            public StrainGraph()
            {
                RelativeSizeAxes = Axes.Y;
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

                if (columnsValid && DrawWidth == lastDrawWidth)
                    return;

                lastDrawWidth = DrawWidth;
                columnsValid = true;

                updateColumns();
                Invalidate(Invalidation.DrawNode);
            }

            private void updateColumns()
            {
                columns.Clear();

                if (strains.Length == 0 || trackLength <= 0 || DrawWidth <= 0)
                    return;

                int columnCount = (int)Math.Ceiling(DrawWidth);
                double sectionLength = EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH;

                for (int c = 0; c < columnCount; c++)
                {
                    // relative to the start of the first section.
                    double start = c / DrawWidth * trackLength - strainsStartTime;
                    double end = (c + 1) / DrawWidth * trackLength - strainsStartTime;

                    int firstSection = Math.Max(0, (int)Math.Floor(start / sectionLength));
                    int lastSection = Math.Min(strains.Length - 1, (int)Math.Ceiling(end / sectionLength) - 1);

                    float value = 0;

                    for (int s = firstSection; s <= lastSection; s++)
                        value = Math.Max(value, strains[s]);

                    columns.Add(value);
                }
            }

            protected override DrawNode CreateDrawNode() => new StrainGraphDrawNode(this);

            private class StrainGraphDrawNode : DrawNode
            {
                protected new StrainGraph Source => (StrainGraph)base.Source;

                private IShader shader = null!;
                private Texture texture = null!;
                private Vector2 drawSize;

                private readonly List<float> columns = new List<float>();

                public StrainGraphDrawNode(StrainGraph source)
                    : base(source)
                {
                }

                public override void ApplyState()
                {
                    base.ApplyState();

                    shader = Source.shader;
                    texture = Source.texture;
                    drawSize = Source.DrawSize;

                    columns.Clear();
                    columns.AddRange(Source.columns);
                }

                protected override void Draw(IRenderer renderer)
                {
                    base.Draw(renderer);

                    shader.Bind();

                    for (int c = 0; c < columns.Count; c++)
                    {
                        float height = columns[c] * drawSize.Y;

                        if (height <= 0)
                            continue;

                        float right = Math.Min(c + 1, drawSize.X);

                        renderer.DrawQuad(texture, new Quad(
                            Vector2Extensions.Transform(new Vector2(c, drawSize.Y - height), DrawInfo.Matrix),
                            Vector2Extensions.Transform(new Vector2(right, drawSize.Y - height), DrawInfo.Matrix),
                            Vector2Extensions.Transform(new Vector2(c, drawSize.Y), DrawInfo.Matrix),
                            Vector2Extensions.Transform(new Vector2(right, drawSize.Y), DrawInfo.Matrix)
                        ), DrawColourInfo.Colour);
                    }

                    shader.Unbind();
                }
            }
        }
    }
}
