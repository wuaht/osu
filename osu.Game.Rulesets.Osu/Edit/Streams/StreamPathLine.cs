// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.Streams
{
    /// <summary>
    /// A thin line along the path of a stream.
    /// </summary>
    public partial class StreamPathLine : CompositeDrawable
    {
        private readonly SmoothPath path;

        private readonly List<Vector2> vertices = new List<Vector2>();

        public StreamPathLine()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = path = new SmoothPath
            {
                PathRadius = 1.5f,
                Alpha = 0.6f,
            };
        }

        public void UpdateFrom(Slider slider)
        {
            vertices.Clear();
            slider.Path.GetPathToProgress(vertices, 0, 1);

            path.Vertices = vertices.Count > 1 ? vertices.ToArray() : new[] { Vector2.Zero, Vector2.Zero };
            path.Position = slider.Position;
            path.OriginPosition = path.PositionInBoundingBox(Vector2.Zero);
        }
    }
}
