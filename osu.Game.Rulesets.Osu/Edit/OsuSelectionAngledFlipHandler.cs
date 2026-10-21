// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit;
using osu.Game.Utils;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Mirrors the selection over a line with an arbitrary angle (like osu!stable's angled flip).
    /// </summary>
    public partial class OsuSelectionAngledFlipHandler : Component
    {
        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private readonly BindableList<HitObject> selectedItems = new BindableList<HitObject>();

        /// <summary>
        /// Whether there are any objects which can be flipped.
        /// </summary>
        public readonly BindableBool CanFlip = new BindableBool();

        /// <summary>
        /// Whether a flip operation is currently in progress.
        /// </summary>
        public readonly BindableBool OperationInProgress = new BindableBool();

        /// <summary>
        /// The line which the selection is currently mirrored over, or <c>null</c> if no operation is in progress.
        /// </summary>
        public readonly Bindable<AngledFlipLine?> PreviewLine = new Bindable<AngledFlipLine?>();

        private OsuHitObject[]? objectsInFlip;
        private Vector2 selectionCentre;
        private Dictionary<OsuHitObject, Vector2>? originalPositions;
        private Dictionary<IHasPath, Vector2[]>? originalPathControlPointPositions;

        [BackgroundDependencyLoader]
        private void load(EditorBeatmap editorBeatmap)
        {
            selectedItems.BindTo(editorBeatmap.SelectedHitObjects);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedItems.BindCollectionChanged((_, _) => CanFlip.Value = selectedMovableObjects.Any(), true);
        }

        public void Begin()
        {
            if (OperationInProgress.Value)
                throw new InvalidOperationException($"Cannot {nameof(Begin)} a flip operation while another is in progress!");

            OperationInProgress.Value = true;

            changeHandler?.BeginChange();

            objectsInFlip = selectedMovableObjects.ToArray();
            selectionCentre = objectsInFlip.Length > 0 ? GeometryUtils.GetSurroundingQuad(objectsInFlip).Centre : OsuPlayfield.BASE_SIZE / 2;
            originalPositions = objectsInFlip.ToDictionary(obj => obj, obj => obj.Position);
            originalPathControlPointPositions = objectsInFlip.OfType<IHasPath>().ToDictionary(
                obj => obj,
                obj => obj.Path.ControlPoints.Select(point => point.Position).ToArray());
        }

        /// <summary>
        /// Mirrors the selection, as it was when <see cref="Begin"/> was called, over a line.
        /// </summary>
        /// <param name="degrees">The angle of the line in degrees, where positive values are clockwise from the horizontal.</param>
        /// <param name="origin">A point on the line, or <c>null</c> to use the centre of the selection.</param>
        public void Update(float degrees, Vector2? origin = null)
        {
            if (!OperationInProgress.Value)
                throw new InvalidOperationException($"Cannot {nameof(Update)} a flip operation without calling {nameof(Begin)} first!");

            Debug.Assert(objectsInFlip != null && originalPositions != null && originalPathControlPointPositions != null);

            Vector2 actualOrigin = origin ?? selectionCentre;

            foreach (var ho in objectsInFlip)
            {
                // the flipped position might be outside the playfield bounds if the line doesn't go through the playfield centre.
                ho.Position = Vector2.Clamp(GetFlippedPosition(originalPositions[ho], actualOrigin, degrees), Vector2.Zero, OsuPlayfield.BASE_SIZE);

                if (ho is IHasPath withPath)
                {
                    var originalPath = originalPathControlPointPositions[withPath];

                    for (int i = 0; i < withPath.Path.ControlPoints.Count; ++i)
                        withPath.Path.ControlPoints[i].Position = GetFlippedPosition(originalPath[i], Vector2.Zero, degrees);
                }
            }

            PreviewLine.Value = new AngledFlipLine(actualOrigin, degrees);
        }

        public void Commit()
        {
            if (!OperationInProgress.Value)
                throw new InvalidOperationException($"Cannot {nameof(Commit)} a flip operation without calling {nameof(Begin)} first!");

            changeHandler?.EndChange();

            OperationInProgress.Value = false;
            PreviewLine.Value = null;

            objectsInFlip = null;
            originalPositions = null;
            originalPathControlPointPositions = null;
        }

        /// <summary>
        /// Mirrors a position over a line.
        /// </summary>
        /// <param name="position">The position to mirror.</param>
        /// <param name="origin">A point on the line.</param>
        /// <param name="degrees">The angle of the line in degrees, where positive values are clockwise from the horizontal (as the y axis points downwards).</param>
        public static Vector2 GetFlippedPosition(Vector2 position, Vector2 origin, float degrees)
        {
            float radians = MathHelper.DegreesToRadians(degrees);
            Vector2 direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians));

            Vector2 relative = position - origin;
            return origin + 2 * Vector2.Dot(relative, direction) * direction - relative;
        }

        private IEnumerable<OsuHitObject> selectedMovableObjects => selectedItems.OfType<OsuHitObject>()
                                                                                 .Where(h => h is not Spinner);
    }

    /// <param name="Origin">A point on the line.</param>
    /// <param name="Degrees">The angle of the line in degrees, clockwise from the horizontal.</param>
    public record struct AngledFlipLine(Vector2 Origin, float Degrees);
}
