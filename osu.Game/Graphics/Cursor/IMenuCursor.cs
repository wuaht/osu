// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// A gameplay cursor provided by a ruleset for display in menus (see <see cref="Rulesets.Ruleset.CreateMenuCursor"/>).
    /// </summary>
    public interface IMenuCursor : IDrawable
    {
        /// <summary>
        /// Expands the cursor, when a button is pressed.
        /// </summary>
        void Expand();

        /// <summary>
        /// Contracts the cursor, when all buttons are released.
        /// </summary>
        void Contract();
    }
}
