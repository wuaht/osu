// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Skinning;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    /// <summary>
    /// The gameplay cursor, including its trail, for display in menus (see <see cref="Ruleset.CreateMenuCursor"/>).
    /// </summary>
    public partial class OsuMenuCursor : VisibilityContainer
    {
        private readonly OsuRuleset ruleset;
        private readonly MenuOsuCursorContainer cursorContainer;

        public OsuMenuCursor(OsuRuleset ruleset)
        {
            this.ruleset = ruleset;

            RelativeSizeAxes = Axes.Both;

            // Matches the structure of gameplay: the skin transformations of the ruleset, and the scaling of the playfield.
            InternalChild = new RulesetSkinProvidingContainer(ruleset, new Beatmap(), null)
            {
                RelativeSizeAxes = Axes.Both,
                Child = new OsuPlayfieldAdjustmentContainer
                {
                    Child = cursorContainer = new MenuOsuCursorContainer(),
                },
            };
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // Provides the ruleset settings (e.g. whether the cursor trail is shown), which are available to the cursor in gameplay.
            if (parent.Get<IRulesetConfigCache>()?.GetConfigFor(ruleset) is OsuRulesetConfigManager config)
                dependencies.Cache(config);

            return dependencies;
        }

        protected override void PopIn() => cursorContainer.Show();

        protected override void PopOut() => cursorContainer.Hide();

        private partial class MenuOsuCursorContainer : OsuCursorContainer
        {
            // In gameplay, the cursor reacts to the gameplay keys. In menus, it reacts to the mouse buttons instead.
            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (e.Button == MouseButton.Left || e.Button == MouseButton.Right)
                    ActiveCursor.Expand();

                return false;
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                if (!e.HasAnyButtonPressed)
                    ActiveCursor.Contract();
            }

            protected override void PopIn()
            {
                base.PopIn();
                this.FadeIn(300, Easing.OutQuint);
            }

            // In gameplay, the cursor stays slightly visible when hidden, which is not wanted in menus.
            protected override void PopOut()
            {
                base.PopOut();
                this.FadeOut(450, Easing.OutQuint);
            }
        }
    }
}
