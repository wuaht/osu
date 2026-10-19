// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders.Components
{
    /// <summary>
    /// A visualisation of the lines between <see cref="PathControlPointPiece{T}"/>s.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="OsuHitObject"/> which this <see cref="PathControlPointConnection{T}"/> visualises.</typeparam>
    public partial class PathControlPointConnection<T> : CompositeDrawable where T : OsuHitObject, IHasPath
    {
        private static readonly Color4 catmull_colour = new Color4(5, 255, 162, 255);
        private static readonly Color4 bezier_colour = new Color4(255, 255, 0, 255);
        private static readonly Color4 linear_colour = new Color4(237, 17, 33, 255);
        private static readonly Color4 perfect_curve_colour = new Color4(102, 68, 204, 255);

        private readonly T hitObject;

        private IBindable<Vector2> hitObjectPosition;
        private IBindable<int> pathVersion;
        private IBindable<int> stackHeight;

        private readonly Bindable<SliderCurveColourStyle> colourStyle = new Bindable<SliderCurveColourStyle>();

        public PathControlPointConnection(T hitObject)
        {
            this.hitObject = hitObject;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager config)
        {
            config?.BindWith(OsuSetting.SlopEditorSliderCurveColour, colourStyle);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitObjectPosition = hitObject.PositionBindable.GetBoundCopy();
            hitObjectPosition.BindValueChanged(_ => Scheduler.AddOnce(updateConnectingPath));

            pathVersion = hitObject.Path.Version.GetBoundCopy();
            pathVersion.BindValueChanged(_ => Scheduler.AddOnce(updateConnectingPath));

            stackHeight = hitObject.StackHeightBindable.GetBoundCopy();
            stackHeight.BindValueChanged(_ => updateConnectingPath());

            colourStyle.BindValueChanged(_ => Scheduler.AddOnce(updateConnectingPath));

            updateConnectingPath();
        }

        /// <summary>
        /// Updates the paths connecting the control points.
        /// </summary>
        private void updateConnectingPath()
        {
            ClearInternal();

            var controlPoints = hitObject.Path.ControlPoints;

            if (controlPoints.Count == 0)
                return;

            if (colourStyle.Value != SliderCurveColourStyle.Line)
            {
                addPath(controlPoints, 0, controlPoints.Count - 1, Color4.White);
                return;
            }

            // each segment is coloured by its curve type.
            int segmentStart = 0;

            for (int i = 1; i < controlPoints.Count; i++)
            {
                if (controlPoints[i].Type == null && i < controlPoints.Count - 1)
                    continue;

                addPath(controlPoints, segmentStart, i, getSegmentColour(controlPoints[segmentStart].Type, i - segmentStart + 1));
                segmentStart = i;
            }
        }

        private void addPath(IReadOnlyList<PathControlPoint> controlPoints, int start, int end, Color4 colour)
        {
            var path = new SmoothPath
            {
                PathRadius = 1,
                Colour = colour,
                Position = hitObject.StackedPosition,
            };

            for (int i = start; i <= end; i++)
                path.AddVertex(controlPoints[i].Position);

            path.OriginPosition = path.PositionInBoundingBox(Vector2.Zero);

            AddInternal(path);
        }

        private static Color4 getSegmentColour(PathType? type, int pointCount)
        {
            switch (type?.Type)
            {
                case SplineType.Catmull:
                    return catmull_colour;

                case SplineType.Linear:
                    return linear_colour;

                case SplineType.PerfectCurve:
                    return pointCount == 3 ? perfect_curve_colour : bezier_colour;

                default:
                    return bezier_colour;
            }
        }
    }
}
