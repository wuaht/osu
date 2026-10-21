// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit.Blueprints.Sliders;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Handles the hotkeys which change the path of sliders: curve types, bezier degree elevation, and random rotations and shapes.
    /// </summary>
    public partial class SliderPathActions : Component, IKeyBindingHandler<GlobalAction>
    {
        [Resolved]
        private OsuHitObjectComposer composer { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        private InputManager inputManager = null!;

        private readonly Random rng = new Random();

        protected override void LoadComplete()
        {
            base.LoadComplete();
            inputManager = GetContainingInputManager()!;
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            switch (e.Action)
            {
                case GlobalAction.EditorSwitchToPerfectCurve:
                    return !e.Repeat && changeCurveType(PathType.PERFECT_CURVE, e.Action);

                case GlobalAction.EditorSwitchToBezierCurve:
                    return !e.Repeat && changeCurveType(PathType.BEZIER, e.Action);

                case GlobalAction.EditorSwitchToLinearCurve:
                    return !e.Repeat && changeCurveType(PathType.LINEAR, e.Action);

                case GlobalAction.EditorSwitchToCatmullCurve:
                    return !e.Repeat && changeCurveType(PathType.CATMULL, e.Action);

                case GlobalAction.EditorIncreaseBezierDegree:
                    return increaseBezierDegree();

                case GlobalAction.EditorRandomSliderRotation:
                    return rotateSlidersRandomly();

                case GlobalAction.EditorRandomSliderShape:
                    return randomiseSliderShape();
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        /// <summary>
        /// Changes the curve type of the selected slider, or of newly placed sliders if no slider is selected.
        /// </summary>
        private bool changeCurveType(PathType type, GlobalAction action)
        {
            var selected = editorBeatmap.SelectedHitObjects;

            if (!selected.OfType<Slider>().Any())
            {
                // pressing the hotkey of the current type again goes back to choosing the type automatically.
                composer.PlacementCurveType = composer.PlacementCurveType == type ? null : type;
                onScreenDisplay?.Display(new CurveTypeToast(composer.PlacementCurveType?.Description ?? SlopEditorStrings.AutomaticCurve, action));
                return true;
            }

            if (selected.Count != 1)
                return false;

            var slider = (Slider)selected[0];
            var controlPoints = slider.Path.ControlPoints;

            if (type == PathType.PERFECT_CURVE && controlPoints.Count > 3)
                return false;

            var typedPoints = controlPoints.Where((p, i) => p.Type != null || i == 0).ToList();

            if (typedPoints.All(p => p.Type == type))
                return false;

            editorBeatmap.BeginChange();

            foreach (var point in typedPoints)
                point.Type = type;

            editorBeatmap.Update(slider);
            editorBeatmap.EndChange();

            onScreenDisplay?.Display(new CurveTypeToast(type.Description, action));
            return true;
        }

        /// <summary>
        /// Raises the degree of one bezier segment of the hovered slider by one without changing its shape,
        /// or converts a perfect curve slider to bezier.
        /// </summary>
        private bool increaseBezierDegree()
        {
            var blueprints = composer.BlueprintContainer.SelectionBlueprints.AliveChildren.OfType<SliderSelectionBlueprint>().ToList();

            var blueprint = blueprints.FirstOrDefault(b => b.IsHovered)
                            ?? blueprints.SingleOrDefault(b => b.IsSelected && b.SelectedControlPointIndex != null);

            if (blueprint == null)
                return false;

            var slider = blueprint.HitObject;
            var controlPoints = slider.Path.ControlPoints;

            if (controlPoints.Count == 3 && controlPoints[0].Type == PathType.PERFECT_CURVE && controlPoints.Skip(1).All(p => p.Type == null))
            {
                var converted = BezierConverter.ConvertToModernBezier(controlPoints);

                foreach (var point in converted)
                    point.Position = new Vector2(MathF.Round(point.Position.X), MathF.Round(point.Position.Y));

                removeConsecutiveDuplicates(converted);

                editorBeatmap.BeginChange();
                controlPoints.Clear();
                controlPoints.AddRange(converted);
                editorBeatmap.Update(slider);
                editorBeatmap.EndChange();
                return true;
            }

            int? index = blueprint.SelectedControlPointIndex ?? getHoveredSegmentStart(slider);

            if (index == null)
                return false;

            int segmentStart = index.Value;

            while (segmentStart > 0 && controlPoints[segmentStart].Type == null)
                segmentStart--;

            if (controlPoints[segmentStart].Type != PathType.BEZIER)
                return false;

            int segmentEnd = segmentStart + 1;

            while (segmentEnd < controlPoints.Count - 1 && controlPoints[segmentEnd].Type == null)
                segmentEnd++;

            if (segmentEnd >= controlPoints.Count)
                return false;

            var segment = controlPoints.Skip(segmentStart).Take(segmentEnd - segmentStart + 1).Select(p => p.Position).ToList();
            int count = segment.Count;

            editorBeatmap.BeginChange();

            // degree elevation: every inner point moves towards its predecessor, and the last point is duplicated.
            for (int i = 1; i < count; i++)
            {
                Vector2 position = Vector2.Lerp(segment[i], segment[i - 1], (float)i / count);
                position = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));

                if (position == controlPoints[segmentStart + i - 1].Position)
                    position.X++;

                controlPoints[segmentStart + i].Position = position;
            }

            var endType = controlPoints[segmentEnd].Type;
            controlPoints[segmentEnd].Type = null;
            controlPoints.Insert(segmentEnd + 1, new PathControlPoint(segment[^1], endType));

            editorBeatmap.Update(slider);
            editorBeatmap.EndChange();
            return true;
        }

        /// <summary>
        /// Returns the index of the first control point of the segment of <paramref name="slider"/> closest to the cursor,
        /// or <c>null</c> if the cursor isn't on the slider body.
        /// </summary>
        private int? getHoveredSegmentStart(Slider slider)
        {
            Vector2 cursor = composer.Playfield.ScreenSpaceToGamefield(inputManager.CurrentState.Mouse.Position) - slider.StackedPosition;
            var controlPoints = slider.Path.ControlPoints;

            int? closestSegment = null;
            float closestDistance = (float)slider.Radius;

            int start = 0;

            for (int i = 1; i < controlPoints.Count; i++)
            {
                if (controlPoints[i].Type == null && i < controlPoints.Count - 1)
                    continue;

                var segmentPath = new SliderPath(controlPoints.Skip(start).Take(i - start + 1)
                                                              .Select((p, j) => new PathControlPoint(p.Position, j == 0 ? controlPoints[start].Type ?? PathType.LINEAR : null))
                                                              .ToArray());

                var vertices = new List<Vector2>();
                segmentPath.GetPathToProgress(vertices, 0, 1);

                for (int j = 0; j < vertices.Count - 1; j++)
                {
                    float distance = distanceToLine(cursor, vertices[j], vertices[j + 1]);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestSegment = start;
                    }
                }

                start = i;
            }

            return closestSegment;
        }

        /// <summary>
        /// Rotates every selected slider around its head by a random angle.
        /// </summary>
        private bool rotateSlidersRandomly()
        {
            var sliders = editorBeatmap.SelectedHitObjects.OfType<Slider>().ToList();

            if (sliders.Count == 0)
                return false;

            editorBeatmap.BeginChange();

            foreach (var slider in sliders)
            {
                float angle = (float)(rng.NextDouble() * Math.PI * 2);
                float cos = MathF.Cos(angle);
                float sin = MathF.Sin(angle);

                var controlPoints = slider.Path.ControlPoints;
                Vector2 origin = controlPoints[0].Position;

                foreach (var point in controlPoints)
                {
                    Vector2 offset = point.Position - origin;
                    point.Position = origin + new Vector2(offset.X * cos - offset.Y * sin, offset.X * sin + offset.Y * cos);
                }

                editorBeatmap.Update(slider);
            }

            editorBeatmap.EndChange();
            return true;
        }

        /// <summary>
        /// Replaces the path of the selected slider with a random bezier path between its head and its last control point.
        /// </summary>
        private bool randomiseSliderShape()
        {
            if (editorBeatmap.SelectedHitObjects.Count != 1 || editorBeatmap.SelectedHitObjects[0] is not Slider slider)
                return false;

            var controlPoints = slider.Path.ControlPoints;

            if (controlPoints.Count < 2)
                return false;

            Vector2 origin = slider.Position;
            Vector2 head = origin + controlPoints[0].Position;
            Vector2 tail = origin + controlPoints[^1].Position;

            var positions = new List<Vector2> { head };
            positions.AddRange(RandomSliderShapes.Generate(head, tail, rng));
            positions.Add(tail);

            var newPoints = new List<PathControlPoint>();

            foreach (var position in positions)
            {
                var relative = position - origin;
                relative = new Vector2(MathF.Round(relative.X), MathF.Round(relative.Y));

                // a point which is repeated forms a red anchor.
                if (newPoints.Count > 0 && newPoints[^1].Position == relative)
                {
                    if (newPoints.Count > 1)
                        newPoints[^1].Type = PathType.BEZIER;
                    continue;
                }

                newPoints.Add(new PathControlPoint(relative));
            }

            newPoints[0].Type = PathType.BEZIER;

            if (newPoints.Count > 1)
                newPoints[^1].Type = null;

            // the tail must not be lost to rounding, otherwise the slider would end elsewhere.
            if (newPoints.Count == 1)
                newPoints.Add(new PathControlPoint(controlPoints[^1].Position));

            editorBeatmap.BeginChange();

            controlPoints.Clear();
            controlPoints.AddRange(newPoints);

            // the length follows the new shape instead of truncating it.
            slider.Path.ExpectedDistance.Value = null;

            editorBeatmap.Update(slider);
            editorBeatmap.EndChange();
            return true;
        }

        private static void removeConsecutiveDuplicates(List<PathControlPoint> points)
        {
            for (int i = points.Count - 1; i > 0; i--)
            {
                if (points[i].Position != points[i - 1].Position)
                    continue;

                points[i - 1].Type ??= points[i].Type;
                points.RemoveAt(i);
            }
        }

        private static float distanceToLine(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 line = end - start;
            float lengthSquared = line.LengthSquared;

            if (lengthSquared == 0)
                return Vector2.Distance(point, start);

            float t = Math.Clamp(Vector2.Dot(point - start, line) / lengthSquared, 0, 1);
            return Vector2.Distance(point, start + line * t);
        }

        private partial class CurveTypeToast : Toast
        {
            private readonly GlobalAction action;

            public CurveTypeToast(LocalisableString value, GlobalAction action)
                : base(SlopEditorStrings.SliderCurve, value)
            {
                this.action = action;
            }

            [BackgroundDependencyLoader]
            private void load(RealmKeyBindingStore keyBindingStore)
            {
                ExtraText = keyBindingStore.GetBindingsStringFor(action);
            }
        }
    }
}
