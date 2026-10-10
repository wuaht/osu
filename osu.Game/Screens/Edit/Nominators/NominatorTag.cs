// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// A small coloured pill with a label, e.g. for the groups of a nominator or whether they are open for requests.
    /// </summary>
    public partial class NominatorTag : CompositeDrawable, IHasTooltip
    {
        public LocalisableString TooltipText { get; set; }

        private readonly FillFlowContainer flow;

        public NominatorTag(LocalisableString text, Color4 colour, Drawable? icon = null, float textSize = 10)
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 4;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                    Alpha = 0.2f,
                },
                flow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    Padding = new MarginPadding { Horizontal = 5, Vertical = 2 },
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Colour = colour,
                            Font = OsuFont.Default.With(size: textSize, weight: FontWeight.Bold),
                        },
                    }
                },
            };

            if (icon != null)
            {
                icon.Colour = colour;
                flow.Insert(-1, icon);
            }
        }
    }
}
