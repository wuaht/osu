// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// stable's song select play button: the skin's <c>menu-osu</c> beating in the bottom right corner,
    /// with a larger copy that only shows on hover and starts the beatmap on click.
    /// </summary>
    public partial class LegacyPlayButton : CompositeDrawable
    {
        private const float beat_scale = 0.4f;
        private const float hover_scale = 0.55f;

        public Action? Action { get; set; }

        private readonly Sprite cookie;
        private readonly Sprite pulse;
        private readonly Sprite hover;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private SkinManager? skinManager { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        public LegacyPlayButton()
        {
            // stable's (40, 450) from the top right of its 640x480 space.
            Anchor = Anchor.BottomRight;
            Position = new Vector2(-40, -30) * LegacySongSelectComponent.STABLE_SCALE;

            InternalChildren = new Drawable[]
            {
                pulse = new Sprite { Origin = Anchor.Centre },
                cookie = new Sprite { Origin = Anchor.Centre, Scale = new Vector2(beat_scale) },
                hover = new Sprite { Origin = Anchor.Centre, Scale = new Vector2(hover_scale), Alpha = 0 },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += updateTexture;
            updateTexture();
        }

        private void updateTexture() => Schedule(() =>
        {
            Texture? texture = skin.GetTexture("menu-osu") ?? skinManager?.DefaultClassicSkin.GetTexture("menu-osu");
            cookie.Texture = pulse.Texture = hover.Texture = texture;
        });

        protected override void Update()
        {
            base.Update();

            // stable eases out quadratically over the beat.
            float linear = (float)beatProgress();
            float progress = linear * (2 - linear);

            cookie.Scale = new Vector2(Interpolation.ValueAt(progress, 0.45f, beat_scale, 0, 1));
            pulse.Scale = new Vector2(Interpolation.ValueAt(progress, 0.45f, 0.49f, 0, 1));
            pulse.Alpha = Interpolation.ValueAt(progress, 0.7f, 0, 0, 1);
        }

        /// <summary>
        /// How far the track is into the current beat, or into the current second when nothing is playing.
        /// </summary>
        private double beatProgress()
        {
            var working = beatmap.Value;
            var track = working.Track;

            if (track.IsRunning && working.BeatmapLoaded)
            {
                var timingPoint = working.Beatmap.ControlPointInfo.TimingPointAt(track.CurrentTime);
                double beatLength = timingPoint.BeatLength;

                if (beatLength > 0)
                {
                    double offset = (track.CurrentTime - timingPoint.Time) % beatLength;
                    return (offset < 0 ? offset + beatLength : offset) / beatLength;
                }
            }

            return Time.Current % 1000 / 1000;
        }

        // the hover copy is a circle, so only its round part reacts.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            if (hover.Texture == null)
                return false;

            Vector2 local = ToLocalSpace(screenSpacePos);
            return local.Length < hover.Texture.DisplayWidth * hover_scale / 2;
        }

        protected override bool OnHover(HoverEvent e)
        {
            hover.FadeIn(100);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) => hover.FadeOut(100);

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e)
        {
            hover.ScaleTo(0.6f).ScaleTo(2, 800, Easing.Out).Then().ScaleTo(hover_scale);
            hover.FadeTo(0.011f, 100);
            Action?.Invoke();
            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= updateTexture;
        }
    }
}
