// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// The cursor used in menus and the editor.
    /// </summary>
    public enum MenuCursorStyle
    {
        /// <summary>
        /// The regular menu cursor.
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MenuCursorStyleDefault))]
        Default,

        /// <summary>
        /// The cursor of the current skin (see <see cref="MenuSkinCursor"/>).
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MenuCursorStyleSkin))]
        Skin,

        /// <summary>
        /// The system cursor (see <see cref="SystemCursorDisplay"/>).
        /// </summary>
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.MenuCursorStyleSystem))]
        System,
    }
}
