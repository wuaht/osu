// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Edit
{
    [Cached]
    public partial class OsuHitObjectComposer : HitObjectComposer<OsuHitObject, OsuAction>
    {
        public OsuHitObjectComposer(Ruleset ruleset)
            : base(ruleset)
        {
        }

        public override Bindable<TernaryState> SelectionNewComboState { get; } = new Bindable<TernaryState>();

        protected override DrawableRuleset<OsuHitObject> CreateDrawableRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods)
            => new DrawableOsuEditorRuleset(ruleset, beatmap, mods);

        protected override IReadOnlyList<CompositionTool<OsuAction>> CompositionTools => new CompositionTool<OsuAction>[]
        {
            new HitCircleCompositionTool(),
            new SliderCompositionTool(),
            new SpinnerCompositionTool(),
            new GridFromPointsTool()
        };

        private readonly Bindable<TernaryState> rectangularGridSnapToggle = new Bindable<TernaryState>();

        protected override Drawable CreateHitObjectInspector() => new OsuHitObjectInspector(DistanceSnapProvider);

        protected override IEnumerable<Drawable> CreateTernaryButtons()
            => base.CreateTernaryButtons()
                   .Append(new DrawableTernaryButton<OsuAction>
                   {
                       Current = rectangularGridSnapToggle,
                       Description = "Grid Snap",
                       CreateIcon = () => new SpriteIcon { Icon = OsuIcon.EditorGridSnap },
                       Action = OsuAction.EditorToggleGridSnap,
                       Hotkey = HotkeyForAction(OsuAction.EditorToggleGridSnap)
                   })
                   .Concat(DistanceSnapProvider.CreateTernaryButtons());

        private BindableList<HitObject> selectedHitObjects;

        private Bindable<HitObject> placementObject;

        [Cached(typeof(IDistanceSnapProvider))]
        public readonly OsuDistanceSnapProvider DistanceSnapProvider = new OsuDistanceSnapProvider();

        [Cached]
        private readonly OsuSliderVelocityToolboxGroup sliderVelocityToolboxGroup = new OsuSliderVelocityToolboxGroup();

        [Cached]
        protected readonly OsuGridToolboxGroup OsuGridToolboxGroup = new OsuGridToolboxGroup();

        [Cached]
        protected readonly FreehandSliderToolboxGroup FreehandSliderToolboxGroup = new FreehandSliderToolboxGroup();

        private Bindable<bool> visualSpacingSnap;
        private Bindable<bool> blanketSnap;
        private Bindable<bool> lineSnap;

        private PatternSnapGuideOverlay patternSnapGuides;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            visualSpacingSnap = config.GetBindable<bool>(OsuSetting.SlopEditorVisualSpacingSnap);
            blanketSnap = config.GetBindable<bool>(OsuSetting.SlopEditorBlanketSnap);
            lineSnap = config.GetBindable<bool>(OsuSetting.SlopEditorLineSnap);

            AddInternal(DistanceSnapProvider);
            DistanceSnapProvider.AttachToToolbox(RightToolbox);

            // Give a bit of breathing room around the playfield content.
            PlayfieldContentContainer.Padding = new MarginPadding(10);

            // above the playfield, so that guide lines aren't hidden behind slider bodies.
            PlayfieldContentContainer.Add(patternSnapGuides = new PatternSnapGuideOverlay());
            PlayfieldContentContainer.Add(new OffscreenObjectOverlay(Playfield));

            LayerBelowRuleset.Add(
                distanceSnapGridContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both
                }
            );

            selectedHitObjects = EditorBeatmap.SelectedHitObjects.GetBoundCopy();
            selectedHitObjects.CollectionChanged += (_, _) => updateDistanceSnapGrid();

            placementObject = EditorBeatmap.PlacementObject.GetBoundCopy();
            placementObject.ValueChanged += _ => updateDistanceSnapGrid();
            DistanceSnapProvider.DistanceSnapToggle.ValueChanged += _ => updateDistanceSnapGrid();

            // we may be entering the screen with a selection already active
            updateDistanceSnapGrid();

            OsuGridToolboxGroup.GridType.BindValueChanged(updatePositionSnapGrid, true);

            RightToolbox.AddRange(new Drawable[]
                {
                    new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Child = sliderVelocityToolboxGroup,
                    },
                    OsuGridToolboxGroup,
                    new TransformToolboxGroup
                    {
                        RotationHandler = BlueprintContainer.SelectionHandler.RotationHandler,
                        ScaleHandler = (OsuSelectionScaleHandler)BlueprintContainer.SelectionHandler.ScaleHandler,
                        GridToolbox = OsuGridToolboxGroup,
                    },
                    new GenerateToolboxGroup(),
                    FreehandSliderToolboxGroup
                }
            );
        }

        private void updatePositionSnapGrid(ValueChangedEvent<PositionSnapGridType> obj)
        {
            if (positionSnapGrid != null)
                LayerBelowRuleset.Remove(positionSnapGrid, true);

            switch (obj.NewValue)
            {
                case PositionSnapGridType.Square:
                    var rectangularPositionSnapGrid = new RectangularPositionSnapGrid();

                    rectangularPositionSnapGrid.Spacing.BindTo(OsuGridToolboxGroup.SpacingVector);
                    rectangularPositionSnapGrid.GridLineRotation.BindTo(OsuGridToolboxGroup.GridLinesRotation);

                    positionSnapGrid = rectangularPositionSnapGrid;
                    break;

                case PositionSnapGridType.Triangle:
                    var triangularPositionSnapGrid = new TriangularPositionSnapGrid();

                    triangularPositionSnapGrid.Spacing.BindTo(OsuGridToolboxGroup.GridLineSpacing);
                    triangularPositionSnapGrid.GridLineRotation.BindTo(OsuGridToolboxGroup.GridLinesRotation);

                    positionSnapGrid = triangularPositionSnapGrid;
                    break;

                case PositionSnapGridType.Circle:
                    var circularPositionSnapGrid = new CircularPositionSnapGrid();

                    circularPositionSnapGrid.Spacing.BindTo(OsuGridToolboxGroup.GridLineSpacing);

                    positionSnapGrid = circularPositionSnapGrid;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(OsuGridToolboxGroup.GridType), OsuGridToolboxGroup.GridType, "Unsupported grid type.");
            }

            // Bind the start position to the toolbox sliders.
            positionSnapGrid.StartPosition.BindTo(OsuGridToolboxGroup.StartPosition);

            positionSnapGrid.RelativeSizeAxes = Axes.Both;
            LayerBelowRuleset.Add(positionSnapGrid);
        }

        protected override ComposeBlueprintContainer CreateBlueprintContainer()
            => new OsuBlueprintContainer(this);

        public override string ConvertSelectionToString()
            => string.Join(',', selectedHitObjects.Cast<OsuHitObject>().OrderBy(h => h.StartTime)
                                                  .Select(h => (h.IndexInCurrentCombo + 1).ToString(CultureInfo.InvariantCulture)));

        // 1,2,3,4 ...
        private static readonly Regex selection_regex = new Regex(@"^\d+(,\d+)*$", RegexOptions.Compiled);

        public override void SelectFromTimestamp(double timestamp, string objectDescription)
        {
            if (!selection_regex.IsMatch(objectDescription))
                return;

            List<OsuHitObject> remainingHitObjects = EditorBeatmap.HitObjects.Cast<OsuHitObject>().Where(h => h.StartTime >= timestamp).ToList();
            string[] splitDescription = objectDescription.Split(',');

            for (int i = 0; i < splitDescription.Length; i++)
            {
                if (!int.TryParse(splitDescription[i], out int combo) || combo < 1)
                    continue;

                OsuHitObject current = remainingHitObjects.FirstOrDefault(h => h.IndexInCurrentCombo + 1 == combo);

                if (current == null)
                    continue;

                EditorBeatmap.SelectedHitObjects.Add(current);

                if (i < splitDescription.Length - 1)
                    remainingHitObjects = remainingHitObjects.Where(h => h != current && h.StartTime >= current.StartTime).ToList();
            }
        }

        private DistanceSnapGrid distanceSnapGrid;
        private Container distanceSnapGridContainer;

        private readonly Cached distanceSnapGridCache = new Cached();
        private double? lastDistanceSnapGridTime;

        private PositionSnapGrid positionSnapGrid;

        protected override void Update()
        {
            base.Update();

            if (!(BlueprintContainer.CurrentTool is SelectTool))
            {
                if (EditorClock.CurrentTime != lastDistanceSnapGridTime)
                {
                    distanceSnapGridCache.Invalidate();
                    lastDistanceSnapGridTime = EditorClock.CurrentTime;
                }

                if (!distanceSnapGridCache.IsValid)
                    updateDistanceSnapGrid();
            }
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            // done after children are updated, as the placement blueprint snaps in its update.
            patternSnapGuides.Display(getDisplayedPatternSnapPoints(), Playfield);

            currentFrame++;
        }

        [CanBeNull]
        public SnapResult TrySnapToNearbyObjects(Vector2 screenSpacePosition, double? fallbackTime = null)
        {
            if (!snapToVisibleBlueprints(screenSpacePosition, out var snapResult))
                return null;

            if (DistanceSnapProvider.DistanceSnapToggle.Value != TernaryState.True || distanceSnapGrid == null)
                return snapResult;

            // In the case of snapping to nearby objects, a time value is not provided.
            // This matches the stable editor (which also uses current time), but with the introduction of time-snapping distance snap
            // this could result in unexpected behaviour when distance snapping is turned on and a user attempts to place an object that is
            // BOTH on a valid distance snap ring, and also at the same position as a previous object.
            //
            // We want to ensure that in this particular case, the time-snapping component of distance snap is still applied.
            // The easiest way to ensure this is to attempt application of distance snap after a nearby object is found, and copy over
            // the time value if the proposed positions are roughly the same.
            (Vector2 distanceSnappedPosition, double distanceSnappedTime) = distanceSnapGrid.GetSnappedPosition(distanceSnapGrid.ToLocalSpace(snapResult.ScreenSpacePosition));
            snapResult.Time = Precision.AlmostEquals(distanceSnapGrid.ToScreenSpace(distanceSnappedPosition), snapResult.ScreenSpacePosition, 1)
                ? distanceSnappedTime
                : fallbackTime;

            return snapResult;
        }

        [CanBeNull]
        public SnapResult TrySnapToDistanceGrid(Vector2 screenSpacePosition, double? fixedTime = null)
        {
            if (DistanceSnapProvider.DistanceSnapToggle.Value != TernaryState.True || distanceSnapGrid?.IsLoaded != true)
                return null;

            var playfield = PlayfieldAtScreenSpacePosition(screenSpacePosition);
            (Vector2 pos, double time) = distanceSnapGrid.GetSnappedPosition(distanceSnapGrid.ToLocalSpace(screenSpacePosition), fixedTime);

            if (pos.X < 0 || pos.X > OsuPlayfield.BASE_SIZE.X || pos.Y < 0 || pos.Y > OsuPlayfield.BASE_SIZE.Y)
                return null;

            return new SnapResult(distanceSnapGrid.ToScreenSpace(pos), time, playfield);
        }

        [CanBeNull]
        public SnapResult TrySnapToPositionGrid(Vector2 screenSpacePosition, double? fallbackTime = null)
        {
            if (rectangularGridSnapToggle.Value != TernaryState.True)
                return null;

            Vector2 pos = positionSnapGrid.GetSnappedPosition(positionSnapGrid.ToLocalSpace(screenSpacePosition));

            // A grid which doesn't perfectly fit the playfield can produce a position that is outside of the playfield.
            // We need to clamp the position to the playfield bounds to ensure that the snapped position is always in bounds.
            pos = Vector2.Clamp(pos, Vector2.Zero, OsuPlayfield.BASE_SIZE);

            var playfield = PlayfieldAtScreenSpacePosition(screenSpacePosition);
            return new SnapResult(positionSnapGrid.ToScreenSpace(pos), fallbackTime, playfield);
        }

        private bool snapToVisibleBlueprints(Vector2 screenSpacePosition, out SnapResult snapResult)
        {
            // check other on-screen objects for snapping/stacking
            var blueprints = BlueprintContainer.SelectionBlueprints.AliveChildren;

            var playfield = PlayfieldAtScreenSpacePosition(screenSpacePosition);

            float snapRadius =
                playfield.GamefieldToScreenSpace(new Vector2(OsuHitObject.OBJECT_RADIUS * 0.10f)).X -
                playfield.GamefieldToScreenSpace(Vector2.Zero).X;

            foreach (var b in blueprints)
            {
                if (b.IsSelected)
                    continue;

                var snapPositions = b.ScreenSpaceSnapPoints;

                if (!snapPositions.Any())
                    continue;

                var closestSnapPosition = snapPositions.MinBy(p => Vector2.Distance(p, screenSpacePosition));

                if (Vector2.Distance(closestSnapPosition, screenSpacePosition) < snapRadius)
                {
                    // if the snap target is a stacked object, snap to its unstacked position rather than its stacked position.
                    // this is intended to make working with stacks easier (because thanks to this, you can drag an object to any
                    // of the items on the stack to add an object to it, rather than having to drag to the position of the *first* object on it at all times).
                    if (b.Item is OsuHitObject osuObject && osuObject.StackOffset != Vector2.Zero)
                        closestSnapPosition = b.ToScreenSpace(b.ToLocalSpace(closestSnapPosition) - osuObject.StackOffset);

                    // only return distance portion, since time is not really valid
                    snapResult = new SnapResult(closestSnapPosition, null, playfield);
                    return true;
                }
            }

            if (snapToPatterns(screenSpacePosition, playfield, snapRadius, out snapResult))
                return true;

            snapResult = null;
            return false;
        }

        #region Pattern snapping

        /// <summary>
        /// Incremented every frame, to invalidate the per-frame state of pattern snapping.
        /// </summary>
        private long currentFrame;

        private long patternSnapFrame = -1;

        /// <summary>
        /// The pattern snap points of the current frame along with their screen space positions, or <c>null</c> if not yet calculated in this frame.
        /// </summary>
        private List<(PatternSnapPoint point, Vector2 screenSpacePosition)> patternSnapPoints;

        /// <summary>
        /// The pattern snap points which snapping was last performed to.
        /// Kept until snapping is performed again, so that guide lines remain visible while the mouse doesn't move.
        /// </summary>
        private readonly List<PatternSnapPoint> snappedPatternSnapPoints = new List<PatternSnapPoint>();

        private bool snapToPatterns(Vector2 screenSpacePosition, Playfield playfield, float snapRadius, out SnapResult snapResult)
        {
            snapResult = null;

            // the first snap of a frame replaces the previous frame's results.
            if (patternSnapFrame != currentFrame)
            {
                patternSnapFrame = currentFrame;
                patternSnapPoints = null;
                snappedPatternSnapPoints.Clear();
            }

            patternSnapPoints ??= calculatePatternSnapPoints(playfield);

            PatternSnapPoint closestPoint = null;
            Vector2 closestPosition = default;
            float closestDistance = snapRadius;

            foreach (var (point, pointScreenSpacePosition) in patternSnapPoints)
            {
                float distance = Vector2.Distance(pointScreenSpacePosition, screenSpacePosition);

                if (distance < closestDistance)
                {
                    closestPoint = point;
                    closestPosition = pointScreenSpacePosition;
                    closestDistance = distance;
                }
            }

            if (closestPoint == null)
                return false;

            if (!snappedPatternSnapPoints.Contains(closestPoint))
                snappedPatternSnapPoints.Add(closestPoint);

            snapResult = new SnapResult(closestPosition, null, playfield);
            return true;
        }

        private List<(PatternSnapPoint, Vector2)> calculatePatternSnapPoints(Playfield playfield)
        {
            var points = new List<PatternSnapPoint>();

            if (!visualSpacingSnap.Value && !blanketSnap.Value && !lineSnap.Value)
                return new List<(PatternSnapPoint, Vector2)>();

            var placementObject = BlueprintContainer.CurrentHitObjectPlacement?.HitObject;

            // same objects as considered by regular object snapping.
            var objects = BlueprintContainer.SelectionBlueprints.AliveChildren
                                            .Where(b => !b.IsSelected && b.Item is OsuHitObject && b.Item != placementObject && b.Item != EditorBeatmap.PlacementObject.Value)
                                            .Select(b => (OsuHitObject)b.Item)
                                            .ToList();

            if (visualSpacingSnap.Value)
                PatternSnapping.AddVisualSpacingSnapPoints(objects, points);

            if (blanketSnap.Value)
                PatternSnapping.AddBlanketSnapPoints(objects, points);

            if (lineSnap.Value)
                PatternSnapping.AddLineSnapPoints(objects, points);

            return points.Select(p => (p, playfield.GamefieldToScreenSpace(p.Position))).ToList();
        }

        /// <summary>
        /// Returns the pattern snap points which an object that is currently being moved or placed is snapped to.
        /// </summary>
        private IReadOnlyList<PatternSnapPoint> getDisplayedPatternSnapPoints()
        {
            if (snappedPatternSnapPoints.Count == 0)
                return Array.Empty<PatternSnapPoint>();

            var placementObject = BlueprintContainer.CurrentHitObjectPlacement?.HitObject as OsuHitObject;

            bool interacting = InputManager.CurrentState.Mouse.Buttons.HasAnyButtonPressed || (placementObject != null && CursorInPlacementArea);

            if (!interacting)
                return Array.Empty<PatternSnapPoint>();

            // a snap result may have been discarded (e.g. in favour of grid snap), so only display points which an object is actually located at.
            var objects = EditorBeatmap.SelectedHitObjects.OfType<OsuHitObject>();

            if (placementObject != null)
                objects = objects.Append(placementObject);

            var positions = objects.SelectMany(getSnappablePositions).ToList();

            return snappedPatternSnapPoints.Where(p => positions.Any(pos => Vector2.Distance(pos, p.Position) < 1)).ToList();
        }

        private static IEnumerable<Vector2> getSnappablePositions(OsuHitObject hitObject)
        {
            // snap results ignore stacking, but movement is based on the stacked position, so both are considered.
            yield return hitObject.Position;
            yield return hitObject.StackedPosition;

            if (hitObject is Slider slider)
            {
                foreach (var controlPoint in slider.Path.ControlPoints)
                {
                    yield return slider.Position + controlPoint.Position;
                    yield return slider.StackedPosition + controlPoint.Position;
                }

                Vector2 tail = slider.Path.PositionAt(1);
                yield return slider.Position + tail;
                yield return slider.StackedPosition + tail;
            }
        }

        #endregion

        private void updateDistanceSnapGrid()
        {
            distanceSnapGridContainer.Clear();
            distanceSnapGridCache.Invalidate();
            distanceSnapGrid = null;

            if (DistanceSnapProvider.DistanceSnapToggle.Value != TernaryState.True)
                return;

            switch (BlueprintContainer.CurrentTool)
            {
                case SelectTool:
                    if (!EditorBeatmap.SelectedHitObjects.Any())
                        return;

                    distanceSnapGrid = createDistanceSnapGrid(EditorBeatmap.SelectedHitObjects);
                    break;

                default:
                    if (!CursorInPlacementArea)
                        return;

                    distanceSnapGrid = createDistanceSnapGrid(Enumerable.Empty<HitObject>());
                    break;
            }

            if (distanceSnapGrid != null)
            {
                distanceSnapGridContainer.Add(distanceSnapGrid);
                distanceSnapGridCache.Validate();
            }
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            // Why is this logic here and not in `OsuSelectionHandler`?
            // Because we only want to handle this toggle after all other right-click handling completes.
            //
            // Consider that input is handled from the most nested child first:
            //
            // ComposeScreen
            //  |- OsuContextMenuContainer                 // right click for context
            //     |- TimelineBlueprintContainer
            //        |- TimelineSelectionHandler
            //     |- (Osu)HitObjectComposer               // right click for toggle new combo
            //        |- (Osu)EditorBlueprintContainer     // right click for select
            //           |- (Osu)EditorSelectionHandler    // right click for delete
            if (e.Button == MouseButton.Right)
            {
                var osuSelectionHandler = (OsuSelectionHandler)BlueprintContainer.SelectionHandler;

                if (!osuSelectionHandler.SelectedItems.Any())
                {
                    SelectionNewComboState!.Value = SelectionNewComboState.Value == TernaryState.False ? TernaryState.True : TernaryState.False;
                    return true;
                }
            }

            return base.OnMouseDown(e);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return false;

            handleToggleViaKey(e);
            return base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            handleToggleViaKey(e);
            base.OnKeyUp(e);
        }

        private bool gridSnapMomentary;

        private void handleToggleViaKey(KeyboardEvent key)
        {
            bool shiftPressed = key.ShiftPressed;

            if (shiftPressed != gridSnapMomentary)
            {
                gridSnapMomentary = shiftPressed;
                rectangularGridSnapToggle.Value = rectangularGridSnapToggle.Value == TernaryState.False ? TernaryState.True : TernaryState.False;
            }

            DistanceSnapProvider.HandleToggleViaKey(key);
        }

        private DistanceSnapGrid createDistanceSnapGrid(IEnumerable<HitObject> selectedHitObjects)
        {
            if (BlueprintContainer.CurrentTool is SpinnerCompositionTool)
                return null;

            var objects = selectedHitObjects.ToList();

            if (objects.Count == 0)
                // use accurate time value to give more instantaneous feedback to the user.
                return createGrid(h => h.StartTime <= EditorClock.CurrentTimeAccurate);

            double minTime = objects.Min(h => h.StartTime);
            return createGrid(h => h.StartTime < minTime, objects.Count + 1);
        }

        /// <summary>
        /// Creates a grid from the last <see cref="HitObject"/> matching a predicate to a target <see cref="HitObject"/>.
        /// </summary>
        /// <param name="sourceSelector">A predicate that matches <see cref="HitObject"/>s where the grid can start from.
        /// Only the last <see cref="HitObject"/> matching the predicate is used.</param>
        /// <param name="targetOffset">An offset from the <see cref="HitObject"/> selected via <paramref name="sourceSelector"/> at which the grid should stop.</param>
        /// <returns>The <see cref="OsuDistanceSnapGrid"/> from a selected <see cref="HitObject"/> to a target <see cref="HitObject"/>.</returns>
        private OsuDistanceSnapGrid createGrid(Func<HitObject, bool> sourceSelector, int targetOffset = 1)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetOffset);

            int positionSourceObjectIndex = -1;
            IHasSliderVelocity sliderVelocitySource = null;

            for (int i = 0; i < EditorBeatmap.HitObjects.Count; i++)
            {
                if (!sourceSelector(EditorBeatmap.HitObjects[i]))
                    break;

                positionSourceObjectIndex = i;

                if (EditorBeatmap.HitObjects[i] is IHasSliderVelocity hasSliderVelocity)
                    sliderVelocitySource = hasSliderVelocity;
            }

            if (positionSourceObjectIndex == -1)
                return null;

            HitObject sourceObject = EditorBeatmap.HitObjects[positionSourceObjectIndex];

            int targetIndex = positionSourceObjectIndex + targetOffset;
            HitObject targetObject = null;

            // Keep advancing the target object while its start time falls before the end time of the source object
            while (true)
            {
                if (targetIndex >= EditorBeatmap.HitObjects.Count)
                    break;

                if (EditorBeatmap.HitObjects[targetIndex].StartTime >= sourceObject.GetEndTime())
                {
                    targetObject = EditorBeatmap.HitObjects[targetIndex];
                    break;
                }

                targetIndex++;
            }

            if (sourceObject is Spinner)
                return null;

            return new OsuDistanceSnapGrid((OsuHitObject)sourceObject, (OsuHitObject)targetObject, sliderVelocitySource);
        }
    }
}
