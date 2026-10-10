// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Displays the hitsounds of a <see cref="HitsoundColumn"/> on the lanes: circles for hitnormals and diamonds for additions.
    /// </summary>
    public partial class HitsoundColumnPiece : CompositeDrawable
    {
        private const float width = 20;

        private readonly Box line;
        private readonly Box selectionHighlight;
        private readonly Container<Container> markers;

        public HitsoundColumnPiece()
        {
            RelativeSizeAxes = Axes.Y;
            Width = width;
            Origin = Anchor.TopCentre;

            InternalChildren = new Drawable[]
            {
                selectionHighlight = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
                line = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 1,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                },
                markers = new Container<Container> { RelativeSizeAxes = Axes.Both },
            };
        }

        public void Apply(HitsoundColumn column, IReadOnlyList<HitsoundLane> lanes, bool selected, IEnumerable<HitsoundLane> mutedLanes, float laneHeight)
        {
            Alpha = 1;

            selectionHighlight.Alpha = selected ? 0.2f : 0;

            // keysounds can't be edited, which is shown by a grey line.
            line.Colour = column.HasFileSamples ? Colour4.Gray : Colour4.White;
            line.Alpha = selected ? 0.6f : column.HasFileSamples ? 0.5f : 0.12f;

            while (markers.Count < lanes.Count)
            {
                markers.Add(new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    Child = new Box { RelativeSizeAxes = Axes.Both },
                });
            }

            float size = Math.Clamp(laneHeight * 0.55f, 6, 16);
            var muted = mutedLanes as ICollection<HitsoundLane> ?? mutedLanes.ToList();

            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];

                if (i >= lanes.Count || !column.Has(lanes[i]))
                {
                    marker.Alpha = 0;
                    continue;
                }

                var lane = lanes[i];
                var colour = HitsoundLane.GetBankColour(lane.Bank);

                marker.Alpha = muted.Contains(lane) || muted.Contains(lane.Parent) ? 0.3f : 1;
                marker.Y = (i + 0.5f) * laneHeight;
                marker.Child.Colour = colour;
                marker.BorderColour = ColourInfo.SingleColour(Colour4.White);
                marker.BorderThickness = selected ? 3 : 0;

                if (lane.IsAddition)
                {
                    marker.Size = new Vector2(size * 0.8f);
                    marker.Rotation = 45;
                    marker.CornerRadius = 2;
                }
                else
                {
                    marker.Size = new Vector2(size);
                    marker.Rotation = 0;
                    marker.CornerRadius = size / 2;
                }
            }
        }
    }
}
