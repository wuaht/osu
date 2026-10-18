// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Argon
{
    public partial class ArgonCursor : SkinnableCursor
    {
        private static readonly ColourInfo border_colour = ColourInfo.GradientVertical(Colour4.FromHex("FC618F"), Colour4.FromHex("BB1A41"));
        private static readonly Color4 fill_colour = Colour4.FromHex("FC618F").Darken(0.6f);
        private static readonly Color4 glow_colour = new Color4(171, 255, 255, 100);

        private readonly CircularContainer border;
        private readonly Box fill;
        private readonly Circle centre;

        public ArgonCursor()
        {
            RelativeSizeAxes = Axes.Both;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            InternalChildren = new[]
            {
                ExpandTarget = border = new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 6,
                    BorderColour = border_colour,
                    Children = new Drawable[]
                    {
                        fill = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0.4f,
                            Colour = fill_colour,
                        },
                        new CircularContainer
                        {
                            Origin = Anchor.Centre,
                            Anchor = Anchor.Centre,
                            RelativeSizeAxes = Axes.Both,
                            Masking = true,
                            BorderThickness = 2,
                            BorderColour = Color4.White.Opacity(0.8f),
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Alpha = 0,
                                    AlwaysPresent = true,
                                },
                            },
                        },
                    },
                },
                centre = new Circle
                {
                    Origin = Anchor.Centre,
                    Anchor = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Scale = new Vector2(0.2f),
                    Colour = new Color4(255, 255, 255, 255),
                    EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Glow,
                        Radius = 20,
                        Colour = glow_colour,
                    },
                },
            };
        }

        public override void ApplyColourEffect(Colour4? colour)
        {
            if (colour == null)
            {
                border.BorderColour = border_colour;
                fill.Colour = fill_colour;
                setGlow(glow_colour);
                return;
            }

            Colour4 c = colour.Value;

            // colour the parts directly instead of tinting, as the default colours would darken the effect colour.
            border.BorderColour = c;
            fill.Colour = c.Darken(0.3f);
            setGlow(c.Opacity(100 / 255f));
        }

        private void setGlow(Color4 colour) => centre.EdgeEffect = new EdgeEffectParameters
        {
            Type = EdgeEffectType.Glow,
            Radius = 20,
            Colour = colour,
        };
    }
}
