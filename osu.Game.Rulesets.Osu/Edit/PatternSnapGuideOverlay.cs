// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Displays the guide lines of the <see cref="PatternSnapPoint"/>s which objects are currently snapped to.
    /// </summary>
    public partial class PatternSnapGuideOverlay : CompositeDrawable
    {
        private const float line_thickness = 3f;

        private PatternSnapPoint[] snapPoints = Array.Empty<PatternSnapPoint>();

        private readonly List<(SmoothPath path, Vector2[] vertices)> lines = new List<(SmoothPath, Vector2[])>();

        private Playfield? playfield;

        public PatternSnapGuideOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Displays the guide lines of the given snap points.
        /// </summary>
        /// <param name="points">The snap points to display the guide lines of.</param>
        /// <param name="playfield">The playfield the snap points are positioned in.</param>
        public void Display(IReadOnlyList<PatternSnapPoint> points, Playfield playfield)
        {
            this.playfield = playfield;

            if (points.SequenceEqual(snapPoints))
                return;

            snapPoints = points.ToArray();

            ClearInternal();
            lines.Clear();

            foreach (var point in snapPoints)
            {
                foreach (var line in point.CreateGuideLines())
                {
                    var path = new SmoothPath
                    {
                        PathRadius = line_thickness / 2,
                        Colour = PatternSnapping.GUIDE_LINE_COLOUR,
                        Alpha = line.Alpha,
                    };

                    AddInternal(path);
                    lines.Add((path, line.Vertices));
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (playfield == null)
                return;

            // the lines are positioned every frame as the playfield may move or be resized.
            foreach (var (path, vertices) in lines)
            {
                path.Vertices = vertices.Select(v => ToLocalSpace(playfield.GamefieldToScreenSpace(v))).ToList();
                path.OriginPosition = path.PositionInBoundingBox(Vector2.Zero);
            }
        }
    }
}
