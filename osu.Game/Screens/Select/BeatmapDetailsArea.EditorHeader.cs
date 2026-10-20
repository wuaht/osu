// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapDetailsArea
    {
        /// <summary>
        /// Replaces the details / ranking tabs in the beatmap editor song select, making it clear that beatmaps are opened for editing.
        /// </summary>
        public partial class EditorHeader : CompositeDrawable
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Left = SongSelect.WEDGE_CONTENT_MARGIN, Right = 5f },
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(8f, 0f),
                        Margin = new MarginPadding { Top = 2f },
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Icon = FontAwesome.Solid.PencilAlt,
                                Size = new Vector2(14),
                                Colour = colourProvider.Highlight1,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = SlopSongSelectStrings.EditorDetailsHeader,
                                Font = OsuFont.Style.Body.With(weight: FontWeight.Bold),
                                Colour = colourProvider.Highlight1,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = SlopSongSelectStrings.EditorDetailsHint,
                                Font = OsuFont.Style.Caption1,
                                Colour = colourProvider.Content2,
                            },
                        },
                    },
                };
            }

            protected override bool OnMouseDown(MouseDownEvent e) => true;
        }
    }
}
