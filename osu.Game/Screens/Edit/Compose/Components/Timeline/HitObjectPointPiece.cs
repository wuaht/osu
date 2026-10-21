// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    public partial class HitObjectPointPiece : CircularContainer
    {
        protected OsuSpriteText Label { get; private set; }

        protected Container LabelContainer { get; private set; }

        /// <summary>
        /// Contains drawables displayed in front of the label, inside the piece.
        /// </summary>
        public Container PrefixContainer { get; } = new Container
        {
            AutoSizeAxes = Axes.Both,
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            AutoSizeAxes = Axes.Both;

            Color4 colour = GetRepresentingColour(colours);

            InternalChildren = new Drawable[]
            {
                LabelContainer = new Container
                {
                    AutoSizeAxes = Axes.X,
                    Height = 16,
                    Masking = true,
                    CornerRadius = 8,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            Colour = colour,
                            RelativeSizeAxes = Axes.Both,
                        },
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Children = new Drawable[]
                            {
                                PrefixContainer,
                                Label = new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Padding = new MarginPadding(5),
                                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold),
                                    Colour = colours.B5,
                                }
                            }
                        }
                    }
                },
            };
        }

        protected virtual Color4 GetRepresentingColour(OsuColour colours)
        {
            return colours.Yellow;
        }
    }
}
