// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// stable's song select play button in the bottom right corner, showing the client's logo at the size of the skin's <c>menu-osu</c>.
    /// Like stable, a beating logo (<c>s_Osu</c>) with a flash (<c>s_Osu1</c>) always sits there, and the larger button (<c>s_Osu2</c>) only fades in while hovered.
    /// </summary>
    public partial class LegacyPlayButton : CompositeDrawable
    {
        private const float hover_scale = 0.55f;

        public Action? Action { get; set; }

        /// <summary>
        /// stable's hovered button (<c>s_Osu2</c>), which song select draws above everything else, including the mode button.
        /// </summary>
        public Drawable HoverLogo => hoverContainer;

        private readonly BeatLayer beatLayer;
        private readonly Container hoverContainer;

        /// <summary>
        /// The width of the skin's <c>menu-osu</c>, which stable sizes its logos from.
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

            InternalChildren = new Drawable[]
            {
                beatLayer = new BeatLayer(this),
                hoverContainer = new LogoContainer(this)
                {
                    Alpha = 0,
                    Scale = new Vector2(hover_scale),
                },
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

        // stable reacts within the hovered size of the round button.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => ToLocalSpace(screenSpacePos).Length < textureWidth * hover_scale / 2;

        // stable's HoverEffect: a linear 100ms fade, reversed when the hover is lost.
        protected override bool OnHover(HoverEvent e)
        {
            hoverContainer.FadeIn(100);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) => hoverContainer.FadeOut(100);

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e)
        {
            // stable's start_OnClick: the button bursts from 0.6 to 2 and fades out, while the beating logos are moved away.
            hoverContainer.ScaleTo(0.6f).ScaleTo(2, 800, Easing.Out).Then().ScaleTo(hover_scale);
            hoverContainer.FadeIn().FadeTo(0.011f, 100).Then(700).FadeOut();
            beatLayer.FadeOut().Delay(800).FadeIn();
            Action?.Invoke();
            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= updateSize;
        }

        /// <summary>
        /// A client logo sized like stable's <c>menu-osu</c>. It keeps its own beat, but hover and clicks are handled by the button.
        /// </summary>
        private partial class LogoContainer : Container
        {
            private readonly LegacyPlayButton button;
            private readonly OsuLogo logo;

            public override bool PropagatePositionalInputSubTree => false;

            public LogoContainer(LegacyPlayButton button)
            {
                this.button = button;
                Child = logo = new OsuLogo();
            }

            protected override void Update()
            {
                base.Update();

                if (logo.DrawWidth > 0)
                    logo.Scale = new Vector2(button.textureWidth / logo.DrawWidth);
            }
        }

        /// <summary>
        /// stable's <c>s_Osu</c> and <c>s_Osu1</c>, eased over every beat of the playing beatmap (or every second without one).
        /// </summary>
        private partial class BeatLayer : BeatSyncedContainer
        {
            private readonly LogoContainer logo;
            private readonly LogoContainer flash;

            public BeatLayer(LegacyPlayButton button)
            {
                Children = new Drawable[]
                {
                    logo = new LogoContainer(button),
                    flash = new LogoContainer(button),
                };
            }

            protected override void Update()
            {
                base.Update();

                double beatLength = TimeSinceLastBeat + TimeUntilNextBeat;

                logo.Scale = new Vector2(Interpolation.ValueAt(TimeSinceLastBeat, 0.45f, 0.4f, 0, beatLength, Easing.OutQuad));
                flash.Scale = new Vector2(Interpolation.ValueAt(TimeSinceLastBeat, 0.45f, 0.49f, 0, beatLength, Easing.OutQuad));
                flash.Alpha = Interpolation.ValueAt(TimeSinceLastBeat, 0.7f, 0f, 0, beatLength, Easing.OutQuad);
            }
        }
    }
}
