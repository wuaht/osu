// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Fposu.UI
{
    /// <summary>
    /// Turns the <see cref="FposuCamera"/> with mouse movement and moves the cursor to where the centre of the view hits the <see cref="FposuScreen"/> (like McOsu's FPoSu).
    /// Buttons and keys are handled as usual.
    /// </summary>
    public partial class FposuInputManager : OsuInputManager
    {
        private readonly FposuCamera camera;
        private readonly Func<FposuScreen?> getScreen;

        private FposuMouseInput mouseInput = null!;

        /// <summary>
        /// The relative mouse movement which turns the camera.
        /// </summary>
        public FposuMouseInput MouseInput => mouseInput;

        private Bindable<int> mouseDpi = null!;
        private Bindable<float> cmPer360 = null!;
        private Bindable<bool> invertHorizontal = null!;
        private Bindable<bool> invertVertical = null!;
        private Bindable<bool> absoluteMode = null!;
        private Bindable<bool> mouseButtonsDisabled = null!;

        /// <summary>
        /// Whether the cursor position is determined by the camera rather than by the position of the mouse.
        /// </summary>
        public bool CameraControlsCursor { get; private set; }

        /// <param name="ruleset">The ruleset.</param>
        /// <param name="camera">The camera to turn.</param>
        /// <param name="getScreen">Retrieves the screen the game is displayed on (created after this input manager).</param>
        public FposuInputManager(RulesetInfo ruleset, FposuCamera camera, Func<FposuScreen?> getScreen)
            : base(ruleset)
        {
            this.camera = camera;
            this.getScreen = getScreen;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            AddInternal(mouseInput = new FposuMouseInput());

            mouseDpi = config.GetBindable<int>(OsuSetting.SlopFposuMouseDpi);
            cmPer360 = config.GetBindable<float>(OsuSetting.SlopFposuCmPer360);
            invertHorizontal = config.GetBindable<bool>(OsuSetting.SlopFposuInvertHorizontal);
            invertVertical = config.GetBindable<bool>(OsuSetting.SlopFposuInvertVertical);
            absoluteMode = config.GetBindable<bool>(OsuSetting.SlopFposuAbsoluteMode);
            mouseButtonsDisabled = config.GetBindable<bool>(OsuSetting.MouseDisableButtons);
        }

        protected override bool Handle(UIEvent e)
        {
            // the position of the mouse is ignored, as the cursor is moved by the camera instead.
            if (e is MouseMoveEvent && CameraControlsCursor)
                return false;

            // the skip button is displayed on the screen and so receives input through this input manager, which blocks mouse buttons while they are disabled in gameplay.
            // it stays clickable, like in osu!, where it is outside of the ruleset.
            if (e is MouseDownEvent && mouseButtonsDisabled.Value)
            {
                var skipButton = HoveredDrawables.OfType<OsuClickableContainer>().FirstOrDefault(d => d.FindClosestParent<SkipOverlay>() != null);

                if (skipButton != null)
                {
                    skipButton.TriggerClick();
                    return true;
                }
            }

            return base.Handle(e);
        }

        /// <summary>
        /// Whether the camera looks at the cursor instead of controlling it.
        /// </summary>
        private bool cameraFollowsCursor;

        protected override void Update()
        {
            // before the input of this frame is processed, so that it uses the new cursor position.
            updateCamera();

            base.Update();

            // after the input of this frame is processed, so that the camera follows the current cursor position.
            if (cameraFollowsCursor && getScreen() is FposuScreen screen && screen.IsLoaded)
                lookAtCursor(screen);
        }

        private void updateCamera()
        {
            // always consumed, so that movement while e.g. paused doesn't turn the camera later on.
            Vector2 movement = mouseInput.ConsumeMovement();

            CameraControlsCursor = false;
            cameraFollowsCursor = false;

            var screen = getScreen();

            if (screen?.IsLoaded != true)
                return;

            // replays and mods moving the cursor (e.g. autopilot): the camera looks at the cursor, like McOsu does for auto.
            if (ReplayInputHandler != null || !AllowUserCursorMovement)
            {
                cameraFollowsCursor = true;
                return;
            }

            // paused.
            if (!UseParentInput)
                return;

            // without relative mouse movement (e.g. tablets, or the cursor not being confined to the window), the camera looks at the cursor instead (McOsu's absolute mode).
            if (absoluteMode.Value || !mouseInput.IsAvailable)
            {
                cameraFollowsCursor = true;
                return;
            }

            CameraControlsCursor = true;

            float degreesPerCount = FposuCamera.DegreesPerCount(mouseDpi.Value, cmPer360.Value);

            // moving the mouse to the right turns the camera to the right, moving it down turns the camera downwards.
            camera.Rotate(
                movement.X * degreesPerCount * (invertHorizontal.Value ? -1 : 1),
                -movement.Y * degreesPerCount * (invertVertical.Value ? -1 : 1));

            // when not looking at the screen, the cursor is moved off the screen, so that nothing can be hit.
            // (McOsu moves it to the centre of the screen instead, which allows hitting objects there while looking elsewhere.)
            Vector2 uv = screen.Mesh.Intersect(camera.Forward) ?? new Vector2(-1);
            Vector2 position = screen.ToScreenSpaceFromUV(uv);

            if (position != CurrentState.Mouse.Position)
                new MousePositionAbsoluteInput { Position = position }.Apply(CurrentState, this);
        }

        private void lookAtCursor(FposuScreen screen)
        {
            Vector2 uv = screen.ToUVFromScreenSpace(CurrentState.Mouse.Position);
            camera.LookAt(screen.Mesh.GetPosition(uv));
        }
    }
}
