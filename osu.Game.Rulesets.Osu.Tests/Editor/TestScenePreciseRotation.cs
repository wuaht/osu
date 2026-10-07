// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Cursor;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Screens.Edit.Components.RadioButtons;
using osu.Game.Tests.Beatmaps;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    public partial class TestScenePreciseRotation : TestSceneOsuEditor
    {
        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new TestBeatmap(ruleset);

        [Test]
        public void TestHotkeyHandling()
        {
            AddStep("deselect everything", () => EditorBeatmap.SelectedHitObjects.Clear());
            AddStep("press rotate hotkey", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.R);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("no popover present", getPopover, () => Is.Null);

            AddStep("select single circle",
                () => EditorBeatmap.SelectedHitObjects.Add(EditorBeatmap.HitObjects.OfType<HitCircle>().First()));
            AddStep("press rotate hotkey", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.R);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("popover present", getPopover, () => Is.Not.Null);
            AddAssert("only playfield centre origin rotation available", () =>
            {
                var popover = getPopover();
                var buttons = popover.ChildrenOfType<EditorRadioButton>();
                return buttons.Any(btn => btn.Text == "Selection centre" && !btn.Enabled.Value)
                       && buttons.Any(btn => btn.Text == "Playfield centre" && btn.Enabled.Value);
            });
            AddStep("press rotate hotkey", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.R);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("no popover present", getPopover, () => Is.Null);

            AddStep("select first three objects", () =>
            {
                EditorBeatmap.SelectedHitObjects.Clear();
                EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects.Take(3));
            });
            AddStep("press rotate hotkey", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.R);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("popover present", getPopover, () => Is.Not.Null);
            AddAssert("both origin rotation available", () =>
            {
                var popover = getPopover();
                var buttons = popover.ChildrenOfType<EditorRadioButton>();
                return buttons.Any(btn => btn.Text == "Selection centre" && btn.Enabled.Value)
                       && buttons.Any(btn => btn.Text == "Playfield centre" && btn.Enabled.Value);
            });
            AddStep("press rotate hotkey", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.R);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("no popover present", getPopover, () => Is.Null);

            PreciseRotationPopover? getPopover() => this.ChildrenOfType<PreciseRotationPopover>().SingleOrDefault();
        }

        [Test]
        public void TestRotateCorrectness()
        {
            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(new HitObject[]
                {
                    new HitCircle { Position = new Vector2(100) },
                    new HitCircle { Position = new Vector2(200) },
                });
            });
            AddStep("select both circles", () => EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects));
            AddStep("press rotate hotkey", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.R);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("popover present", getPopover, () => Is.Not.Null);

            AddStep("rotate by 180deg", () => getPopover().ChildrenOfType<TextBox>().Single().Current.Value = "180");
            AddAssert("first object rotated 180deg around playfield centre",
                () => EditorBeatmap.HitObjects.OfType<HitCircle>().ElementAt(0).Position,
                () => Is.EqualTo(OsuPlayfield.BASE_SIZE - new Vector2(100)));
            AddAssert("second object rotated 180deg around playfield centre",
                () => EditorBeatmap.HitObjects.OfType<HitCircle>().ElementAt(1).Position,
                () => Is.EqualTo(OsuPlayfield.BASE_SIZE - new Vector2(200)));

            AddStep("change rotation origin", () => getPopover().ChildrenOfType<EditorRadioButton>().ElementAt(2).TriggerClick());
            AddAssert("first object rotated 90deg around selection centre",
                () => EditorBeatmap.HitObjects.OfType<HitCircle>().ElementAt(0).Position, () => Is.EqualTo(new Vector2(200, 200)));
            AddAssert("second object rotated 90deg around selection centre",
                () => EditorBeatmap.HitObjects.OfType<HitCircle>().ElementAt(1).Position, () => Is.EqualTo(new Vector2(100, 100)));

            PreciseRotationPopover? getPopover() => this.ChildrenOfType<PreciseRotationPopover>().SingleOrDefault();
        }

        [Test]
        public void TestScrollRotation()
        {
            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(new HitObject[]
                {
                    new HitCircle { StartTime = 0, Position = new Vector2(200, 192) },
                    new HitCircle { StartTime = 500, Position = new Vector2(300, 192) },
                });
            });
            AddStep("select both circles", () => EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects));
            AddStep("move mouse to playfield", () => InputManager.MoveMouseTo(this.ChildrenOfType<OsuPlayfield>().Single()));

            AddStep("scroll up with ctrl+shift", () => scrollWithModifiers(1, false));
            assertRightCircleAngle(5);

            AddStep("scroll down twice with ctrl+shift", () => scrollWithModifiers(-2, false));
            assertRightCircleAngle(-5);

            AddStep("scroll up with ctrl+shift+alt", () => scrollWithModifiers(1, true));
            assertRightCircleAngle(-4);

            AddStep("scroll down with ctrl+shift+alt", () => scrollWithModifiers(-1, true));
            assertRightCircleAngle(-5);

            AddStep("undo", () => Editor.Undo());
            assertRightCircleAngle(-4);

            void scrollWithModifiers(float amount, bool alt)
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.ShiftLeft);
                if (alt)
                    InputManager.PressKey(Key.AltLeft);

                InputManager.ScrollVerticalBy(amount);

                if (alt)
                    InputManager.ReleaseKey(Key.AltLeft);
                InputManager.ReleaseKey(Key.ShiftLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            }

            // positive angles are clockwise on screen (y axis pointing down).
            void assertRightCircleAngle(float degrees)
            {
                AddAssert($"right circle rotated to {degrees}deg", () =>
                {
                    var position = EditorBeatmap.HitObjects.OfType<HitCircle>().ElementAt(1).Position;
                    float radians = MathHelper.DegreesToRadians(degrees);
                    var expected = new Vector2(250 + 50 * MathF.Cos(radians), 192 + 50 * MathF.Sin(radians));
                    return Precision.AlmostEquals(position, expected, 0.01f);
                });
            }
        }

        [Test]
        public void TestScrollRotationGestureIsSingleUndoStepAndShowsTooltip()
        {
            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(new HitObject[]
                {
                    new HitCircle { StartTime = 0, Position = new Vector2(200, 192) },
                    new HitCircle { StartTime = 500, Position = new Vector2(300, 192) },
                });
            });
            AddStep("select both circles", () => EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects));
            AddStep("move mouse to playfield", () => InputManager.MoveMouseTo(this.ChildrenOfType<OsuPlayfield>().Single()));

            AddStep("hold ctrl+shift", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.ShiftLeft);
            });
            AddStep("scroll up three times", () =>
            {
                for (int i = 0; i < 3; i++)
                    InputManager.ScrollVerticalBy(1);
            });
            AddStep("hold alt", () => InputManager.PressKey(Key.AltLeft));
            AddStep("scroll down twice", () =>
            {
                for (int i = 0; i < 2; i++)
                    InputManager.ScrollVerticalBy(-1);
            });
            AddStep("release alt", () => InputManager.ReleaseKey(Key.AltLeft));

            AddUntilStep("tooltip shows total rotation", () =>
            {
                var tooltip = getScrollRotationTooltip();
                return tooltip.State.Value == Visibility.Visible
                       && tooltip.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString() == "13°");
            });

            AddStep("release ctrl+shift", () =>
            {
                InputManager.ReleaseKey(Key.ShiftLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("tooltip hidden", () => getScrollRotationTooltip().State.Value, () => Is.EqualTo(Visibility.Hidden));
            AddAssert("right circle rotated", () => EditorBeatmap.HitObjects.OfType<HitCircle>().ElementAt(1).Position, () => Is.Not.EqualTo(new Vector2(300, 192)));

            AddStep("undo once", () => Editor.Undo());
            AddAssert("left circle back at original position",
                () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<HitCircle>().ElementAt(0).Position, new Vector2(200, 192), 0.01f));
            AddAssert("right circle back at original position",
                () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<HitCircle>().ElementAt(1).Position, new Vector2(300, 192), 0.01f));

            OsuTooltipContainer.OsuTooltip getScrollRotationTooltip()
                => this.ChildrenOfType<OsuSelectionHandler>().Single().ChildrenOfType<OsuTooltipContainer.OsuTooltip>().Single();
        }
    }
}
