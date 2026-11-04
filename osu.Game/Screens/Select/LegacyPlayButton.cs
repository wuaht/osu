// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// stable's song select play button in the bottom right corner, showing the client's logo at the size of the skin's <c>menu-osu</c>.
    /// It grows on hover and starts the beatmap on click.
    /// </summary>
    public partial class LegacyPlayButton : CompositeDrawable
    {
        private const float beat_scale = 0.4f;
        private const float hover_scale = 0.55f;

        public Action? Action { get; set; }

        private readonly Container hoverContainer;
        private readonly OsuLogo logo;

        /// <summary>
        /// The width of the skin's <c>menu-osu</c>, which stable sizes its button from.
        /// </summary>
        private float textureWidth;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager? skinManager { get; set; }

        public LegacyPlayButton()
        {
            // stable's (40, 450) from the top right of its 640x480 space.
            Anchor = Anchor.BottomRight;
            Position = new Vector2(-40, -30) * LegacySongSelectComponent.STABLE_SCALE;

            InternalChild = hoverContainer = new LogoContainer
            {
                Child = logo = new OsuLogo(),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += updateSize;
            updateSize();
        }

        private void updateSize() => Schedule(() =>
        {
            Texture? texture = skinManager?.DefaultClassicSkin.GetTexture("menu-osu");
            textureWidth = texture?.DisplayWidth ?? 0;
        });

        protected override void Update()
        {
            base.Update();

            if (logo.DrawWidth > 0)
                logo.Scale = new Vector2(textureWidth * beat_scale / logo.DrawWidth);
        }

        // stable reacts within the hovered size of the round button.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => ToLocalSpace(screenSpacePos).Length < textureWidth * hover_scale / 2;

        protected override bool OnHover(HoverEvent e)
        {
            hoverContainer.ScaleTo(hover_scale / beat_scale, 500, Easing.OutElastic);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) => hoverContainer.ScaleTo(1, 500, Easing.OutElastic);

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e)
        {
            hoverContainer.ScaleTo(0.9f * hover_scale / beat_scale).ScaleTo(hover_scale / beat_scale, 500, Easing.OutElastic);
            Action?.Invoke();
            return true;
        }

        /// <summary>
        /// The logo keeps its own beat, but hover and clicks are handled by the button, so it stays silent and doesn't scale itself.
        /// </summary>
        private partial class LogoContainer : Container
        {
            public override bool PropagatePositionalInputSubTree => false;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= updateSize;
        }
    }
}
