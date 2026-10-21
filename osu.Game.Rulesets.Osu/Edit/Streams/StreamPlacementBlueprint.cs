// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit.Blueprints.HitCircles.Components;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.Streams
{
    /// <summary>
    /// Places an <see cref="EditorStream"/>. The path is drawn exactly like the one of a slider.
    /// </summary>
    public partial class StreamPlacementBlueprint : SliderPlacementBlueprint
    {
        protected override bool ShowsPlacementObject => false;

        protected override bool SnapsLength => false;

        protected override bool ShowsSliderPieces => false;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private EditorStreamManager? streamManager { get; set; }

        [Resolved]
        private OsuSliderVelocityToolboxGroup? sliderVelocityToolbox { get; set; }

        private StreamPathLine pathLine = null!;
        private Container<HitCirclePiece> circlePieces = null!;

        private Container densityLabel = null!;
        private OsuSpriteText densityText = null!;

        private float circleScale = 1;

        /// <summary>
        /// The densities chosen by scrolling while placing, keyed by the anchors of the placement slider.
        /// Anchors without an entry continue the density of the previous one.
        /// </summary>
        private readonly Dictionary<PathControlPoint, double> anchorDensities = new Dictionary<PathControlPoint, double>();

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            Add(pathLine = new StreamPathLine { Depth = 2 });
            Add(circlePieces = new Container<HitCirclePiece> { RelativeSizeAxes = Axes.Both, Depth = 1 });
            Add(densityLabel = new Container
            {
                Origin = Anchor.TopCentre,
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 4,
                Depth = 0,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Black,
                        Alpha = 0.6f,
                    },
                    densityText = new OsuSpriteText
                    {
                        Margin = new MarginPadding { Horizontal = 4, Vertical = 1 },
                        Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        Colour = colours.Yellow,
                    },
                },
            });

            var template = new HitCircle();
            template.ApplyDefaults(editorBeatmap.ControlPointInfo, editorBeatmap.Difficulty);
            circleScale = template.Scale;
        }

        protected override void OnSliderUpdated()
        {
            base.OnSliderUpdated();

            if (!IsLoaded || PlacementActive == PlacementState.Finished)
            {
                pathLine.Alpha = 0;
                circlePieces.Alpha = 0;
                densityLabel.Alpha = 0;
                return;
            }

            circlePieces.Alpha = 1;

            List<Vector2> layout;

            if (PlacementActive == PlacementState.Waiting)
            {
                // like the other placement tools, show where the first circle is going to be placed.
                pathLine.Alpha = 0;
                densityLabel.Alpha = 0;
                layout = new List<Vector2> { HitObject.Position };
            }
            else
            {
                pathLine.Alpha = 1;
                densityLabel.Alpha = 1;

                pathLine.UpdateFrom(HitObject);

                var stream = createStream();
                layout = stream.Generate(editorBeatmap.ControlPointInfo, editorBeatmap.Difficulty.SliderMultiplier).Select(c => c.Position).ToList();

                var points = stream.Path.Path.ControlPoints;
                densityLabel.Position = HitObject.Position + points[^1].Position + new Vector2(0, circleScale * OsuHitObject.OBJECT_RADIUS * 0.6f + 8);
                densityText.Text = $"{stream.GetDensity(points[^1]).ToString("0.0#", CultureInfo.InvariantCulture)}x";
            }

            while (circlePieces.Count < layout.Count)
                circlePieces.Add(new HitCirclePiece());

            for (int i = 0; i < circlePieces.Count; i++)
            {
                var piece = circlePieces[i];

                if (i >= layout.Count)
                {
                    piece.Alpha = 0;
                    continue;
                }

                piece.Alpha = 1;
                piece.Position = layout[i];
                piece.Scale = new Vector2(circleScale);
            }
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            // before placement starts, scrolling seeks to the time to place the stream at.
            if (PlacementActive != PlacementState.Active || e.ControlPressed || e.AltPressed || e.SuperPressed)
                return base.OnScroll(e);

            var points = HitObject.Path.ControlPoints;

            if (points.Count == 0)
                return base.OnScroll(e);

            double step = e.ShiftPressed ? 0.01 : 0.1;
            var stream = createStream();
            double density = stream.GetDensity(stream.Path.Path.ControlPoints[^1]);

            // shift+scroll arrives as a horizontal scroll on some platforms.
            float delta = e.ScrollDelta.Y != 0 ? e.ScrollDelta.Y : e.ScrollDelta.X;
            int steps = Math.Sign(delta) * (int)Math.Max(1, Math.Round(Math.Abs(delta)));

            double newDensity = Math.Clamp(Math.Round(Math.Round(density / step) * step + steps * step, 2), EditorStream.MIN_DENSITY, EditorStream.MAX_DENSITY);

            // the density applies to the whole segment being drawn, from the anchor which starts it to the cursor,
            // so that its circles are evenly spaced instead of ramping from the previous density.
            int segmentStart = points.Count - 1;

            while (segmentStart > 0 && (segmentStart == points.Count - 1 || points[segmentStart].Type == null))
                segmentStart--;

            for (int i = segmentStart; i < points.Count; i++)
                anchorDensities[points[i]] = newDensity;

            OnSliderUpdated();
            return true;
        }

        protected override void CommitHitObject()
        {
            if (streamManager == null)
            {
                base.CommitHitObject();
                return;
            }

            streamManager.Add(createStream(), HitObject.Samples, HitObject.NewCombo);
        }

        private EditorStream createStream()
        {
            var stream = new EditorStream { BeatDivisor = editorBeatmap.BeatDivisor };

            stream.Path.StartTime = HitObject.StartTime;
            stream.Path.Position = HitObject.Position;
            foreach (var point in HitObject.Path.ControlPoints)
            {
                var copy = new PathControlPoint(point.Position, point.Type);
                stream.Path.Path.ControlPoints.Add(copy);

                if (anchorDensities.TryGetValue(point, out double density))
                    stream.SetDensity(copy, density);
                // the stream starts with the slider velocity of the toolbox unless a different density was chosen for its first anchor.
                // this is set before the following anchors are added, so that the ones without a density continue it.
                else if (point == HitObject.Path.ControlPoints[0])
                    stream.SetDensity(copy, sliderVelocityToolbox?.SliderVelocity.Value ?? 1);
            }

            return stream;
        }
    }
}
