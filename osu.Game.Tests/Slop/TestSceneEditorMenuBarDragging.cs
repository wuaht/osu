// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit.Components.Menus;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneEditorMenuBarDragging : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new OsuRuleset();

        private EditorMenuBar menuBar => Editor.ChildrenOfType<EditorMenuBar>().First();

        private ScrollContainer<Drawable> barScrollContainer => menuBar.ChildrenOfType<ScrollContainer<Drawable>>().First();

        private Menu.DrawableMenuItem firstItem => menuBar.ChildrenOfType<Menu.DrawableMenuItem>().First();

        [Test]
        public void TestMenuBarCannotBeDragged()
        {
            Vector2 initialPosition = Vector2.Zero;

            AddStep("store item position", () => initialPosition = firstItem.ScreenSpaceDrawQuad.TopLeft);

            AddStep("drag the menu bar", () =>
            {
                InputManager.MoveMouseTo(firstItem);
                InputManager.PressButton(MouseButton.Left);
                InputManager.MoveMouseTo(firstItem, new Vector2(300, 0));
            });

            AddWaitStep("wait while dragging", 5);
            AddAssert("bar not scrolled", () => barScrollContainer.Current, () => Is.Zero);
            AddAssert("item didn't move", () => firstItem.ScreenSpaceDrawQuad.TopLeft, () => Is.EqualTo(initialPosition));

            AddStep("release", () => InputManager.ReleaseButton(MouseButton.Left));

            AddStep("scroll over the menu bar", () =>
            {
                InputManager.MoveMouseTo(firstItem);
                InputManager.ScrollHorizontalBy(-5);
                InputManager.ScrollVerticalBy(-5);
            });

            AddWaitStep("wait", 5);
            AddAssert("bar not scrolled", () => barScrollContainer.Current, () => Is.Zero);
        }
    }
}
