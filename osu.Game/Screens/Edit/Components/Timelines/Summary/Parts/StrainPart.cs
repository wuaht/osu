// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osuTK;

namespace osu.Game.Screens.Edit.Components.Timelines.Summary.Parts
{
    /// <summary>
    /// Displays the difficulty strain of the beatmap over time (like osu!stable).
    /// The colours and the opacities of the parts before and after the current time can be customised.
    /// </summary>
    public partial class StrainPart : CompositeDrawable
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmapDifficulty? beatmapDifficulty { get; set; }

        private readonly IBindable<EditorBeatmapDifficultyInfo?> difficultyInfo = new Bindable<EditorBeatmapDifficultyInfo?>();

        private Bindable<bool> showStrains = null!;
        private Bindable<string> lowColour = null!;
        private Bindable<string> highColour = null!;
        private Bindable<int> activeAlpha = null!;
        private Bindable<int> inactiveAlpha = null!;

        private Container pastContainer = null!;
        private Container futureContainer = null!;
        private StrainGraph pastGraph = null!;
        private StrainGraph futureGraph = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            showStrains = config.GetBindable<bool>(OsuSetting.SlopEditorShowDifficultyStrains);
            lowColour = config.GetBindable<string>(OsuSetting.SlopStrainGraphLowColour);
            highColour = config.GetBindable<string>(OsuSetting.SlopStrainGraphHighColour);
            activeAlpha = config.GetBindable<int>(OsuSetting.SlopStrainGraphActiveAlpha);
            inactiveAlpha = config.GetBindable<int>(OsuSetting.SlopStrainGraphInactiveAlpha);

            Alpha = 0;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // the graph is displayed twice, each masked to the part before or after the current time.
                    pastContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        Masking = true,
                        Child = pastGraph = new StrainGraph(),
                    },
                    futureContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        Masking = true,
                        Child = futureGraph = new StrainGraph(),
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

            lowColour.BindValueChanged(_ => updateColours());
            highColour.BindValueChanged(_ => updateColours(), true);

            activeAlpha.BindValueChanged(a => pastContainer.Alpha = a.NewValue / 100f, true);
            inactiveAlpha.BindValueChanged(a => futureContainer.Alpha = a.NewValue / 100f, true);
        }

        private void updateColours()
        {
            Colour4 low = SlopColours.Parse(lowColour.Value, Colour4.White);
            Colour4 high = SlopColours.Parse(highColour.Value, Colour4.White);

            pastGraph.SetColours(low, high);
            futureGraph.SetColours(low, high);
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

            private Colour4 lowColour = Colour4.White;
            private Colour4 highColour = Colour4.White;

            /// <summary>
            /// Sets the colours of the columns, which are interpolated in the upper half of the strains (like osu!stable).
            /// </summary>
            public void SetColours(Colour4 low, Colour4 high)
            {
                lowColour = low;
                highColour = high;
                Invalidate(Invalidation.DrawNode);
            }

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
                private Colour4 lowColour;
                private Colour4 highColour;

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
                    lowColour = Source.lowColour;
                    highColour = Source.highColour;

                    columns.Clear();
                    columns.AddRange(Source.columns);
                }

                protected override void Draw(IRenderer renderer)
                {
                    base.Draw(renderer);

                    shader.Bind();

                    SRGBColour drawColour = DrawColourInfo.Colour.TopLeft;
                    float maxStrain = 0;

                    foreach (float c in columns)
                        maxStrain = Math.Max(maxStrain, c);

                    for (int c = 0; c < columns.Count; c++)
                    {
                        float height = columns[c] * drawSize.Y;

                        if (height <= 0)
                            continue;

                        float right = Math.Min(c + 1, drawSize.X);

                        float amount = maxStrain > 0 ? Math.Max(0, (columns[c] / maxStrain - 0.5f) * 2) : 0;
                        SRGBColour colour = drawColour * (SRGBColour)Interpolation.ValueAt(amount, lowColour, highColour, 0, 1);

                        renderer.DrawQuad(texture, new Quad(
                            Vector2Extensions.Transform(new Vector2(c, drawSize.Y - height), DrawInfo.Matrix),
                            Vector2Extensions.Transform(new Vector2(right, drawSize.Y - height), DrawInfo.Matrix),
                            Vector2Extensions.Transform(new Vector2(c, drawSize.Y), DrawInfo.Matrix),
                            Vector2Extensions.Transform(new Vector2(right, drawSize.Y), DrawInfo.Matrix)
                        ), colour);
                    }

                    shader.Unbind();
                }
            }
        }
    }
}
