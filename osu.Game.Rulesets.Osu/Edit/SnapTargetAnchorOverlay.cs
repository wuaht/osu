// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit.Components;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Displays a ring around every control point of the selected sliders which objects can be snapped to (see <see cref="PathControlPoint.IsSnapTarget"/>),
    /// regardless of whether the control point itself is selected.
    /// </summary>
    public partial class SnapTargetAnchorOverlay : CompositeDrawable
    {
        /// <summary>
        /// The size of the control points in gamefield units, at a slider scale of 1 (see <see cref="Blueprints.Sliders.Components.PathControlPointPiece{T}"/>).
        /// </summary>
        private const float control_point_size = 8;

        /// <summary>
        /// The gap between the control points and the rings in pixels.
        /// </summary>
        private const float ring_gap = 1;

        /// <summary>
        /// The thickness of the rings in pixels.
        /// </summary>
        private const float ring_thickness = 2;

        private readonly Playfield playfield;
        private readonly Func<IEnumerable<Slider>> getSliders;

        private readonly List<EditorAnchorShapeContainer> rings = new List<EditorAnchorShapeContainer>();

        private readonly Bindable<EditorAnchorShape> anchorShape = new Bindable<EditorAnchorShape>();

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        /// <param name="playfield">The playfield the sliders are positioned in.</param>
        /// <param name="getSliders">The sliders whose control points are displayed.</param>
        public SnapTargetAnchorOverlay(Playfield playfield, Func<IEnumerable<Slider>> getSliders)
        {
            this.playfield = playfield;
            this.getSliders = getSliders;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopEditorAnchorShape, anchorShape);
        }

        protected override void Update()
        {
            base.Update();

            // positioned every frame, as sliders may be moved and the playfield may move or be resized.
            float gamefieldScale = Vector2.Distance(ToLocalSpace(playfield.GamefieldToScreenSpace(Vector2.Zero)), ToLocalSpace(playfield.GamefieldToScreenSpace(Vector2.UnitX)));

            int count = 0;

            foreach (var slider in getSliders())
            {
                foreach (var controlPoint in slider.Path.ControlPoints.Where(p => p.IsSnapTarget))
                {
                    var ring = getRing(count++);
                    float controlPointSize = control_point_size * slider.Scale * gamefieldScale;

                    ring.Shape = anchorShape.Value;
                    ring.Size = new Vector2(controlPointSize + 2 * (ring_gap + ring_thickness));
                    ring.BorderThickness = ring_thickness;
                    ring.Position = ToLocalSpace(playfield.GamefieldToScreenSpace(slider.StackedPosition + controlPoint.Position));
                    ring.Alpha = 1;
                }
            }

            for (int i = count; i < rings.Count; i++)
                rings[i].Alpha = 0;
        }

        private EditorAnchorShapeContainer getRing(int index)
        {
            if (index < rings.Count)
                return rings[index];

            var ring = new EditorAnchorShapeContainer(outline: true)
            {
                Origin = Anchor.Centre,
                Colour = colours.Green,
            };

            rings.Add(ring);
            AddInternal(ring);
            return ring;
        }
    }
}
