// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Skinning;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// Displays the cursor of the current legacy skin in menus, matching the gameplay cursor of osu!.
    /// </summary>
    public partial class MenuSkinCursor : CompositeDrawable
    {
        private const double revolution_duration = 10000;

        private const float pressed_scale = 1.3f;
        private const float released_scale = 1f;

        /// <summary>
        /// Whether the current skin provides a cursor. If not, nothing is displayed.
        /// </summary>
        public IBindable<bool> Available => available;

        private readonly BindableBool available = new BindableBool();

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager skinManager { get; set; } = null!;

        private readonly IBindable<Skin> currentSkin = new Bindable<Skin>();

        private Sprite? cursorSprite;
        private bool expand;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            currentSkin.BindTo(skinManager.CurrentSkin);
            currentSkin.BindValueChanged(_ => Scheduler.AddOnce(recreate));
            skin.SourceChanged += onSourceChanged;

            recreate();
        }

        private void onSourceChanged() => Scheduler.AddOnce(recreate);

        private void recreate()
        {
            ClearInternal();
            cursorSprite = null;

            // Only legacy skins provide their cursor as textures. The cursors of other skins are drawn by the rulesets themselves.
            available.Value = currentSkin.Value is LegacySkin && skin.GetTexture(@"cursor") != null;

            if (!available.Value)
                return;

            bool centre = skin.GetConfig<LegacySkinCursorSetting, bool>(LegacySkinCursorSetting.CursorCentre)?.Value ?? true;
            bool spin = skin.GetConfig<LegacySkinCursorSetting, bool>(LegacySkinCursorSetting.CursorRotate)?.Value ?? true;
            expand = skin.GetConfig<LegacySkinCursorSetting, bool>(LegacySkinCursorSetting.CursorExpand)?.Value ?? true;

            InternalChildren = new Drawable[]
            {
                cursorSprite = new Sprite
                {
                    Texture = skin.GetTexture(@"cursor"),
                    Origin = centre ? Anchor.Centre : Anchor.TopLeft,
                },
                new Sprite
                {
                    Texture = skin.GetTexture(@"cursormiddle"),
                    Origin = centre ? Anchor.Centre : Anchor.TopLeft,
                },
            };

            if (spin)
                cursorSprite.Spin(revolution_duration, RotationDirection.Clockwise);
        }

        /// <summary>
        /// Expands the cursor, when a button is pressed.
        /// </summary>
        public void Expand()
        {
            if (expand)
                cursorSprite?.ScaleTo(released_scale).ScaleTo(pressed_scale, 100, Easing.Out);
        }

        /// <summary>
        /// Contracts the cursor, when all buttons are released.
        /// </summary>
        public void Contract() => cursorSprite?.ScaleTo(released_scale, 100, Easing.Out);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSourceChanged;
        }

        /// <summary>
        /// Legacy skin configuration of the cursor, matching the keys of skin.ini.
        /// </summary>
        private enum LegacySkinCursorSetting
        {
            CursorCentre,
            CursorExpand,
            CursorRotate,
        }
    }
}
