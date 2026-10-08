// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Components.Timelines.Summary.Parts
{
    /// <summary>
    /// Displays the difficulty strain of the beatmap over time (like osu!stable).
    /// The part before the current time is white, the part after it grey.
    /// </summary>
    public partial class StrainPart : CompositeDrawable
    {
        private const float graph_alpha = 0.5f;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorBeatmapDifficulty? beatmapDifficulty { get; set; }

        private readonly IBindable<EditorBeatmapDifficultyInfo?> difficultyInfo = new Bindable<EditorBeatmapDifficultyInfo?>();

        private Bindable<bool> showStrains = null!;

        private Container pastContainer = null!;
        private Container futureContainer = null!;
        private BarGraph pastGraph = null!;
        private BarGraph futureGraph = null!;

        /// <summary>
        /// The length of time covered by the graph's sections.
        /// </summary>
        private double graphDuration;

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
                        Child = pastGraph = createGraph(Color4.White),
                    },
                    futureContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Y,
                        Masking = true,
                        Child = futureGraph = createGraph(OsuColour.Gray(0.5f)),
                    },
                }
            };
        }

        private static BarGraph createGraph(Color4 colour) => new BarGraph
        {
            RelativeSizeAxes = Axes.Y,
            Direction = BarDirection.BottomToTop,
            MaxValue = 1,
            Colour = colour,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (beatmapDifficulty != null)
                difficultyInfo.BindTo(beatmapDifficulty.Info);

            difficultyInfo.BindValueChanged(info =>
            {
                float[] strains = info.NewValue?.Strains ?? Array.Empty<float>();

                graphDuration = strains.Length * EditorBeatmapDifficulty.STRAIN_SECTION_LENGTH;
                pastGraph.Values = strains;
                futureGraph.Values = strains;
            }, true);

            showStrains.BindValueChanged(show => this.FadeTo(show.NewValue ? 1 : 0, 200, Easing.OutQuint), true);
        }

        protected override void Update()
        {
            base.Update();

            double trackLength = editorClock.TrackLength;

            if (trackLength <= 0)
                return;

            float graphWidth = (float)(graphDuration / trackLength * DrawWidth);
            float currentX = Math.Clamp((float)(editorClock.CurrentTime / trackLength * DrawWidth), 0, DrawWidth);

            pastGraph.Width = graphWidth;
            futureGraph.Width = graphWidth;

            pastContainer.Width = currentX;

            futureContainer.X = currentX;
            futureContainer.Width = DrawWidth - currentX;
            futureGraph.X = -currentX;
        }
    }
}
