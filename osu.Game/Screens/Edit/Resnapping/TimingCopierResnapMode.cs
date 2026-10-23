// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit.Resnapping
{
    public enum TimingCopierResnapMode
    {
        /// <summary>
        /// Objects keep their position in beats and move along with the new timing.
        /// </summary>
        [LocalisableDescription(typeof(SlopEditorStrings), nameof(SlopEditorStrings.TimingCopierPreserveBeatSpacing))]
        PreserveBeatSpacing,

        /// <summary>
        /// Objects keep their time and are snapped to the closest tick of the new timing.
        /// </summary>
        [LocalisableDescription(typeof(SlopEditorStrings), nameof(SlopEditorStrings.TimingCopierResnap))]
        Resnap,

        /// <summary>
        /// Objects are left untouched.
        /// </summary>
        [LocalisableDescription(typeof(SlopEditorStrings), nameof(SlopEditorStrings.TimingCopierKeepObjectsFixed))]
        KeepObjectsFixed,
    }
}
