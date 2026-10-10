// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Cursor;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Tests.Editor;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Tests.Beatmaps;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneScrollRotation : TestSceneOsuEditor
    {
        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new TestBeatmap(ruleset);

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

        [Test]
        public void TestSingleSliderRotatesAroundHead()
        {
            AddStep("rotate around object starts", () => config.SetValue(OsuSetting.SlopEditorRotateAroundObjectStarts, true));
            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.Add(createSlider(new Vector2(200, 192)));
            });
            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects));
            AddStep("move mouse to playfield", () => InputManager.MoveMouseTo(this.ChildrenOfType<OsuPlayfield>().Single()));

            AddStep("scroll up with ctrl+shift", () => scrollWithCtrlShift(1));

            AddAssert("head unchanged", () => Precision.AlmostEquals(slider().Position, new Vector2(200, 192), 0.01f));
            AddAssert("tail rotated around head", () => Precision.AlmostEquals(slider().Position + slider().Path.PositionAt(1), rotated(new Vector2(300, 192), new Vector2(200, 192), 5), 0.01f));
        }

        [Test]
        public void TestRotationIgnoresSliderBodies()
        {
            AddStep("rotate around object starts", () => config.SetValue(OsuSetting.SlopEditorRotateAroundObjectStarts, true));
            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.AddRange(new HitObject[]
                {
                    new HitCircle { StartTime = 0, Position = new Vector2(100, 192) },
                    createSlider(new Vector2(200, 192), 500),
                });
            });
            AddStep("select all", () => EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects));
            AddStep("move mouse to playfield", () => InputManager.MoveMouseTo(this.ChildrenOfType<OsuPlayfield>().Single()));

            AddStep("scroll up with ctrl+shift", () => scrollWithCtrlShift(1));

            // the centre of the circle and the slider head, rather than of the selection including the slider body.
            AddAssert("rotated around circle and slider head", () => Precision.AlmostEquals(slider().Position, rotated(new Vector2(200, 192), new Vector2(150, 192), 5), 0.01f));
        }

        [Test]
        public void TestRotationAroundWholeSelectionWhenDisabled()
        {
            AddStep("rotate around selection", () => config.SetValue(OsuSetting.SlopEditorRotateAroundObjectStarts, false));
            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.Add(createSlider(new Vector2(200, 192)));
            });
            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects));
            AddStep("move mouse to playfield", () => InputManager.MoveMouseTo(this.ChildrenOfType<OsuPlayfield>().Single()));

            AddStep("scroll up with ctrl+shift", () => scrollWithCtrlShift(1));

            AddAssert("head rotated around centre of slider", () => Precision.AlmostEquals(slider().Position, rotated(new Vector2(200, 192), new Vector2(250, 192), 5), 0.01f));
            AddStep("restore setting", () => config.SetValue(OsuSetting.SlopEditorRotateAroundObjectStarts, true));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestRotationOriginDisplayed(bool aroundObjectStarts)
        {
            AddStep("set rotation origin", () => config.SetValue(OsuSetting.SlopEditorRotateAroundObjectStarts, aroundObjectStarts));
            AddStep("replace objects", () =>
            {
                EditorBeatmap.Clear();
                EditorBeatmap.Add(createSlider(new Vector2(200, 192)));
            });
            AddStep("select slider", () => EditorBeatmap.SelectedHitObjects.AddRange(EditorBeatmap.HitObjects));
            AddStep("move mouse to playfield", () => InputManager.MoveMouseTo(this.ChildrenOfType<OsuPlayfield>().Single()));

            AddAssert("origin hidden", () => rotationOrigin().Alpha, () => Is.Zero);

            AddStep("hold ctrl+shift", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.PressKey(Key.ShiftLeft);
            });
            AddUntilStep("origin displayed", () => rotationOrigin().Alpha, () => Is.EqualTo(1));
            AddAssert("origin at pivot", () =>
            {
                var playfield = this.ChildrenOfType<OsuPlayfield>().Single();
                Vector2 pivot = aroundObjectStarts ? new Vector2(200, 192) : new Vector2(250, 192);
                return Precision.AlmostEquals(rotationOrigin().ScreenSpaceDrawQuad.Centre, playfield.GamefieldToScreenSpace(pivot), 0.5f);
            });

            AddStep("scroll up", () => InputManager.ScrollVerticalBy(1));
            AddAssert("origin still displayed", () => rotationOrigin().Alpha, () => Is.EqualTo(1));

            AddStep("release ctrl+shift", () =>
            {
                InputManager.ReleaseKey(Key.ShiftLeft);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddUntilStep("origin hidden", () => rotationOrigin().Alpha, () => Is.Zero);
            AddStep("restore setting", () => config.SetValue(OsuSetting.SlopEditorRotateAroundObjectStarts, true));

            Drawable rotationOrigin() => this.ChildrenOfType<OsuSelectionHandler>().Single().ScrollRotationOrigin;
        }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private Slider slider() => EditorBeatmap.HitObjects.OfType<Slider>().Single();

        private static Slider createSlider(Vector2 position, double startTime = 0) => new Slider
        {
            StartTime = startTime,
            Position = position,
            Path = new SliderPath(new[]
            {
                new PathControlPoint(Vector2.Zero, PathType.LINEAR),
                new PathControlPoint(new Vector2(100, 0)),
            }),
        };

        private void scrollWithCtrlShift(float amount)
        {
            InputManager.PressKey(Key.ControlLeft);
            InputManager.PressKey(Key.ShiftLeft);
            InputManager.ScrollVerticalBy(amount);
            InputManager.ReleaseKey(Key.ShiftLeft);
            InputManager.ReleaseKey(Key.ControlLeft);
        }

        // positive angles are clockwise on screen (y axis pointing down).
        private static Vector2 rotated(Vector2 point, Vector2 origin, float degrees)
        {
            float radians = MathHelper.DegreesToRadians(degrees);
            Vector2 offset = point - origin;

            return origin + new Vector2(
                offset.X * MathF.Cos(radians) - offset.Y * MathF.Sin(radians),
                offset.X * MathF.Sin(radians) + offset.Y * MathF.Cos(radians));
        }
    }
}
