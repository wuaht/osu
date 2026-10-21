// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Handles the hotkeys which toggle how the compose screen is displayed and how its hotkeys behave.
    /// </summary>
    public partial class ComposeDisplayActions : Component, IKeyBindingHandler<GlobalAction>
    {
        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        private Bindable<bool> zoomOut = null!;
        private Bindable<bool> showGrid = null!;
        private Bindable<bool> rotateAroundSelection = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            zoomOut = config.GetBindable<bool>(OsuSetting.SlopEditorZoomOut);
            showGrid = config.GetBindable<bool>(OsuSetting.SlopEditorShowGrid);
            rotateAroundSelection = config.GetBindable<bool>(OsuSetting.SlopEditorRotateAroundSelection);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.EditorToggleZoomOut:
                    zoomOut.Value = !zoomOut.Value;
                    display(SlopEditorStrings.ZoomOut, zoomOut.Value ? CommonStrings.Enabled : CommonStrings.Disabled, e.Action);
                    return true;

                case GlobalAction.EditorToggleGridDisplay:
                    showGrid.Value = !showGrid.Value;
                    display(SlopEditorStrings.GridDisplay, showGrid.Value ? CommonStrings.Enabled : CommonStrings.Disabled, e.Action);
                    return true;

                case GlobalAction.EditorToggleRotationHotkeyOrigin:
                    rotateAroundSelection.Value = !rotateAroundSelection.Value;
                    display(SlopEditorStrings.RotationHotkeyOrigin, rotateAroundSelection.Value ? SlopEditorStrings.SelectionOrigin : SlopEditorStrings.PlayfieldOrigin, e.Action);
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        private void display(LocalisableString description, LocalisableString value, GlobalAction action)
            => onScreenDisplay?.Display(new ActionToast(description, value, action));

        private partial class ActionToast : Toast
        {
            private readonly GlobalAction action;

            public ActionToast(LocalisableString description, LocalisableString value, GlobalAction action)
                : base(description, value)
            {
                this.action = action;
            }

            [BackgroundDependencyLoader]
            private void load(RealmKeyBindingStore keyBindingStore)
            {
                ExtraText = keyBindingStore.GetBindingsStringFor(action);
            }
        }
    }
}
