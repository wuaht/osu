// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Screens.Play.HUD;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Configuration;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning
{
    public partial class LegacyKeyCounterDisplay : KeyCounterDisplay
    {
        private static readonly Colour4 active_colour_top = Colour4.FromHex(@"#ffde00");
        private static readonly Colour4 active_colour_bottom = Colour4.FromHex(@"#f8009e");

        private static readonly OsuSetting[] active_colour_settings =
        {
            OsuSetting.SlopKeyOverlayColourK1,
            OsuSetting.SlopKeyOverlayColourK2,
            OsuSetting.SlopKeyOverlayColourM1,
            OsuSetting.SlopKeyOverlayColourM2,
        };

        private readonly Bindable<string>[] activeColours = new Bindable<string>[active_colour_settings.Length];

        protected override FillFlowContainer<KeyCounter> KeyFlow { get; }

        private readonly Sprite backgroundSprite;

        public LegacyKeyCounterDisplay()
        {
            AutoSizeAxes = Axes.Both;

            AddRange(new Drawable[]
            {
                backgroundSprite = new Sprite
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopLeft,
                    Scale = new Vector2(1.05f, 1),
                    X = 1,
                    Rotation = 90,
                },
                KeyFlow = new FillFlowContainer<KeyCounter>
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Y = 7.35f,
                    Spacing = new Vector2(1.2f),
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Both,
                },
            });
        }

        [Resolved]
        private ISkinSource source { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            for (int i = 0; i < active_colour_settings.Length; i++)
                activeColours[i] = config.GetBindable<string>(active_colour_settings[i]);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            KeyTextColor = source.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(SkinConfiguration.LegacySetting.InputOverlayText))?.Value ?? Color4.Black;

            Texture? backgroundTexture = source.GetTexture(@"inputoverlay-background");

            if (backgroundTexture != null)
                backgroundSprite.Texture = backgroundTexture;

            foreach (var colour in activeColours)
                colour.BindValueChanged(_ => updateActiveColours());

            updateActiveColours();
        }

        private void updateActiveColours()
        {
            for (int i = 0; i < KeyFlow.Count; ++i)
            {
                Colour4 fallback = i < 2 ? active_colour_top : active_colour_bottom;

                // the keys after the fourth one keep the colour of the mouse buttons.
                ((LegacyKeyCounter)KeyFlow[i]).ActiveColour = SlopColours.Parse(activeColours[System.Math.Min(i, activeColours.Length - 1)].Value, fallback);
            }
        }

        protected override KeyCounter CreateCounter(InputTrigger trigger) => new LegacyKeyCounter(trigger)
        {
            TextColour = keyTextColor,
        };

        private Colour4 keyTextColor = Colour4.White;

        public Colour4 KeyTextColor
        {
            get => keyTextColor;
            set
            {
                if (value != keyTextColor)
                {
                    keyTextColor = value;
                    foreach (var child in KeyFlow.Cast<LegacyKeyCounter>())
                        child.TextColour = value;
                }
            }
        }
    }
}
