// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Numerics;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Replaces the menu cursor with the system cursor, which is not affected by the game's latency.
    /// The animations of the menu cursor (rotating while dragging, shrinking and glowing on click) are mirrored onto the system cursor,
    /// while the menu cursor itself keeps running them invisibly.
    /// </summary>
    public partial class SystemCursorDisplay : Component
    {
        /// <summary>
        /// Whether the system cursor is used in place of the menu cursor.
        /// </summary>
        public IBindable<bool> Active => active;

        private readonly BindableBool active = new BindableBool();

        private readonly MenuCursorContainer menuCursor;

        private Bindable<MenuCursorStyle> cursorStyle = null!;

        private IWindow? window;

        private Image<Rgba32>? cursorImage;
        private Point cursorHotspot;

        private Vector3 highlightColour;

        private bool systemCursorVisible;
        private (int rotation, int scale, int highlight)? lastRenderedState;

        public SystemCursorDisplay(MenuCursorContainer menuCursor)
        {
            this.menuCursor = menuCursor;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, GameHost host, OsuColour colours)
        {
            cursorStyle = config.GetBindable<MenuCursorStyle>(OsuSetting.SlopMenuCursorStyle);
            window = host.Window;

            highlightColour = new Vector3(colours.Pink.R, colours.Pink.G, colours.Pink.B);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            cursorStyle.BindValueChanged(style =>
            {
                // Only desktop windows support custom system cursors.
                active.Value = style.NewValue == MenuCursorStyle.System && window != null && RuntimeInfo.IsDesktop;

                if (active.Value)
                {
                    cursorImage ??= SystemCursorImage.Load(out cursorHotspot);
                }
                else
                {
                    setSystemCursorVisible(false);
                    window?.SetCursorImage(null, default);
                    lastRenderedState = null;
                }
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (!active.Value)
                return;

            var cursor = menuCursor.AnimatedCursor;

            // The system cursor can't fade, so it is hidden once the menu cursor is mostly faded out (e.g. during gameplay or when idle).
            bool visible = menuCursor.State.Value == Visibility.Visible && cursor.Alpha >= 0.5f;

            setSystemCursorVisible(visible);

            if (!visible)
                return;

            // Quantised to avoid recreating the system cursor for imperceptible changes.
            var state = (
                rotation: (int)MathF.Round(cursor.Rotation),
                scale: (int)MathF.Round(cursor.Scale.X * 100),
                highlight: (int)MathF.Round(cursor.AdditiveLayer.Alpha * 10));

            if (state == lastRenderedState)
                return;

            lastRenderedState = state;

            using (var image = SystemCursorImage.Render(cursorImage!, cursorHotspot,
                       state.rotation, state.scale / 100f, state.highlight / 10f * 0.6f, highlightColour, out var hotspot))
            {
                window!.SetCursorImage(image, new System.Drawing.Point(hotspot.X, hotspot.Y));
            }
        }

        private void setSystemCursorVisible(bool visible)
        {
            if (systemCursorVisible == visible || window == null)
                return;

            systemCursorVisible = visible;

            // The game hides the system cursor by default (see OsuGame.SetHost), in favour of the menu cursor.
            if (visible)
                window.CursorState &= ~CursorState.Hidden;
            else
                window.CursorState |= CursorState.Hidden;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (window?.CursorState.HasFlagFast(CursorState.Hidden) == false && systemCursorVisible)
                window.CursorState |= CursorState.Hidden;

            cursorImage?.Dispose();
        }
    }
}
