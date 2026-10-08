// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Displays the gameplay cursor of the current skin, including its trail, in menus.
    /// The cursor is provided by the osu! ruleset (see <see cref="Ruleset.CreateMenuCursor"/>), such that it looks and behaves exactly like in gameplay.
    /// </summary>
    public partial class MenuSkinCursor : CompositeDrawable
    {
        /// <summary>
        /// Whether a gameplay cursor is available. If not, nothing is displayed.
        /// </summary>
        public IBindable<bool> Available => available;

        private readonly BindableBool available = new BindableBool();

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        private VisibilityContainer? cursor;

        private bool visible;

        public MenuSkinCursor()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Like osu!stable, the cursor of osu! is used regardless of the selected ruleset.
            cursor = rulesets.GetRuleset(@"osu")?.CreateInstance().CreateMenuCursor();

            if (cursor == null)
                return;

            AddInternal(cursor);
            cursor.State.Value = visible ? Visibility.Visible : Visibility.Hidden;

            available.Value = true;
        }

        /// <summary>
        /// Shows or hides the cursor.
        /// </summary>
        public void SetVisible(bool value)
        {
            visible = value;

            if (cursor != null)
                cursor.State.Value = value ? Visibility.Visible : Visibility.Hidden;
        }
    }
}
