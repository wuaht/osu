// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// A snapshot of the hitsounds of a <see cref="HitsoundBody"/>, independent of any hit objects.
    /// </summary>
    /// <param name="StartTime">The start time of the slider.</param>
    /// <param name="NormalBank">The bank of "sliderslide".</param>
    /// <param name="WhistleBank">The bank of "sliderwhistle", or <c>null</c> if the body has no whistle.</param>
    /// <param name="Volume">The volume.</param>
    /// <param name="CustomIndex">The custom sample index.</param>
    public sealed record HitsoundBodyState(double StartTime, string? NormalBank, string? WhistleBank, int Volume, int CustomIndex);
}
