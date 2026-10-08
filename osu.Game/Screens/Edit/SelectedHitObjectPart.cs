// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// A part of a selected <see cref="IHasRepeats"/> hit object (e.g. a slider's head, body or tail), which hitsound changes should be limited to.
    /// </summary>
    /// <param name="HitObject">The hit object which the part belongs to.</param>
    /// <param name="NodeIndex">
    /// The index into <see cref="IHasRepeats.NodeSamples"/> of the selected node (head, repeats and tail),
    /// or <c>null</c> if the body (<see cref="HitObject.Samples"/>) is selected.
    /// </param>
    public record SelectedHitObjectPart(HitObject HitObject, int? NodeIndex)
    {
        /// <summary>
        /// Whether this part is still valid for its hit object (e.g. a node may no longer exist after the repeat count was reduced).
        /// </summary>
        public bool IsValid => NodeIndex == null || (HitObject is IHasRepeats hasRepeats && NodeIndex.Value >= 0 && NodeIndex.Value < hasRepeats.NodeSamples.Count);
    }
}
