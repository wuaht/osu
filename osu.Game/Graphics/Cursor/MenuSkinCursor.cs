// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Displays the gameplay cursor of the current skin in menus.
    /// The cursor is provided by the osu! ruleset (see <see cref="Ruleset.CreateMenuCursor"/>), such that it looks and behaves exactly like in gameplay,
    /// for legacy skins as well as for skins whose cursor is drawn by the ruleset (e.g. argon).
    /// </summary>
    public partial class MenuSkinCursor : CompositeDrawable
    {
        /// <summary>
        /// Whether a skinned cursor is available. If not, nothing is displayed.
        /// </summary>
        public IBindable<bool> Available => available;

        private readonly BindableBool available = new BindableBool();

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        private IMenuCursor? cursor;

        private Container? scalingContainer;

        private MenuCursorContainer? menuCursorContainer;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Like osu!stable, the cursor of osu! is used regardless of the selected ruleset.
            Ruleset? ruleset = rulesets.GetRuleset(@"osu")?.CreateInstance();
            cursor = ruleset?.CreateMenuCursor();

            if (ruleset == null || cursor == null)
                return;

            // Applies the skin transformations of the ruleset, through which the cursor is looked up.
            AddInternal(scalingContainer = new Container
            {
                Child = new RulesetSkinProvidingContainer(ruleset, new Beatmap(), null)
                {
                    Child = (Drawable)cursor,
                },
            });

            menuCursorContainer = this.FindClosestParent<MenuCursorContainer>();

            available.Value = true;
        }

        protected override void Update()
        {
            base.Update();

            if (menuCursorContainer == null || scalingContainer == null)
                return;

            // In gameplay, the cursor is scaled along with the playfield (see OsuPlayfieldAdjustmentContainer).
            // Apply the same scale here, such that the cursor has the same size as in gameplay.
            Vector2 screenSize = menuCursorContainer.DrawSize;
            scalingContainer.Scale = new Vector2(MathF.Min(screenSize.X, screenSize.Y * 4 / 3) * playfield_size_adjust / playfield_width);
        }

        /// <summary>
        /// The portion of the screen taken up by the playfield in gameplay.
        /// </summary>
        private const float playfield_size_adjust = 0.8f;

        /// <summary>
        /// The width of the playfield in gameplay, in playfield coordinates.
        /// </summary>
        private const float playfield_width = 512;

        /// <summary>
        /// Expands the cursor, when a button is pressed.
        /// </summary>
        public void Expand() => cursor?.Expand();

        /// <summary>
        /// Contracts the cursor, when all buttons are released.
        /// </summary>
        public void Contract() => cursor?.Contract();
    }
}
