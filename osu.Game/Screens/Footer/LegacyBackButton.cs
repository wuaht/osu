// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Footer
{
    /// <summary>
    /// stable's back button. Uses the skin's <c>menu-back</c> when provided, otherwise stable's default button built from <c>back-button-layer</c>.
    /// </summary>
    public partial class LegacyBackButton : BeatSyncedContainer
    {
        private const float stable_scale = 1.6f;

        private static readonly Color4 colour_idle = new Color4(238, 51, 153, 255);
        private static readonly Color4 colour_active = new Color4(187, 17, 119, 255);

        public Action? Action;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        // set when the skin provides menu-back.
        private Drawable? hoverOverlay;

        // set when using stable's default button.
        private Drawable? layerLeft;
        private Drawable? layerRight;
        private Drawable? icon;
        private OsuSpriteText? text;

        private bool expanded;

        public LegacyBackButton()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            AutoSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            updateArtwork();
        }

        private void onSkinChanged() => Schedule(updateArtwork);

        private void updateArtwork()
        {
            Clear();
            hoverOverlay = layerLeft = layerRight = icon = text = null;
            expanded = false;

            Drawable? createMenuBack() => skin.GetAnimation("menu-back", true, true, true)?.With(d =>
            {
                d.Anchor = Anchor.BottomLeft;
                d.Origin = Anchor.BottomLeft;
            });

            if (createMenuBack() is Drawable menuBack)
            {
                Add(menuBack);
                Add(hoverOverlay = createMenuBack()!.With(d =>
                {
                    d.Alpha = 1 / 255f;
                    d.Blending = BlendingParameters.Additive;
                }));
                return;
            }

            var layer = skin.GetTexture("back-button-layer");

            Children = new Drawable[]
            {
                layerRight = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomRight,
                    Texture = layer,
                    Colour = colour_idle,
                },
                layerLeft = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomRight,
                    Texture = layer,
                    Colour = colour_idle,
                },
                text = new OsuSpriteText
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.Centre,
                    Y = -21 * stable_scale,
                    Text = "back",
                    Font = OsuFont.GetFont(size: 15 * stable_scale * 0.8f),
                },
                icon = new SpriteIcon
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.Centre,
                    Y = -21 * stable_scale,
                    Size = new Vector2(18 * stable_scale * 0.6f),
                    Icon = FontAwesome.Solid.ChevronCircleLeft,
                },
            };

            // the text's width is only known after layout.
            ScheduleAfterChildren(() => contract(Easing.OutElastic, 0));
        }

        // stable widens the button for translations longer than "back".
        private float textAllowance => Math.Max(0, (text?.DrawWidth ?? 0) / stable_scale - 38);

        private void expand(Easing easing, double duration = 600)
        {
            expanded = true;
            moveLayers(40, 16.666666f, 86, easing, duration);
            layerLeft?.FadeColour(colour_active, duration, easing);
        }

        private void contract(Easing easing, double duration = 600)
        {
            expanded = false;
            moveLayers(22, 9.166666f, 60, easing, duration);
            layerLeft?.FadeColour(colour_idle, duration, easing);
        }

        private void moveLayers(float left, float iconX, float right, Easing easing, double duration)
        {
            right += textAllowance;

            layerLeft?.MoveToX(left * stable_scale, duration, easing);
            icon?.MoveToX(iconX * stable_scale, duration, easing);
            layerRight?.MoveToX(right * stable_scale, duration, easing);
            text?.MoveToX(((right - left) / 2.4f + left) * stable_scale, duration, easing);
        }

        protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
        {
            base.OnNewBeat(beatIndex, timingPoint, effectPoint, amplitudes);

            icon?.ScaleTo(expanded ? 1.5f : 0.9f).ScaleTo(1, timingPoint.BeatLength * 3, Easing.OutElastic);
        }

        private Drawable? inputTarget => hoverOverlay ?? layerRight;

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => inputTarget?.ReceivePositionalInputAt(screenSpacePos) == true;

        protected override bool OnHover(HoverEvent e)
        {
            skin.GetSample(new SampleInfo("back-button-hover", "menuclick"))?.Play();

            hoverOverlay?.FadeTo(0.4f, 250);

            if (layerRight != null)
                expand(Easing.OutElastic);

            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverOverlay?.FadeTo(1 / 255f, 250);

            if (layerRight != null)
                contract(Easing.OutElastic);
        }

        protected override bool OnClick(ClickEvent e)
        {
            skin.GetSample(new SampleInfo("back-button-click", "menuback"))?.Play();

            if (layerRight != null)
                contract(Easing.InOutBack, 400);

            Action?.Invoke();
            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }
    }
}
