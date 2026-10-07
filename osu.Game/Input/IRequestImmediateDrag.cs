// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Input
{
    /// <summary>
    /// A drawable which can request that a left mouse drag starting on it begins immediately on mouse movement,
    /// rather than only after the mouse has moved a minimum distance.
    /// </summary>
    /// <remarks>
    /// Only takes effect for the drawable which handled the mouse down event.
    /// </remarks>
    public interface IRequestImmediateDrag
    {
        /// <summary>
        /// Whether a drag starting on this drawable should begin immediately.
        /// </summary>
        bool RequestsImmediateDrag { get; }
    }
}
