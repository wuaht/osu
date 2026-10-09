// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.OfflineProfiles;

namespace osu.Game.Overlays.OfflineProfiles
{
    /// <summary>
    /// A grey pill which marks a user as an <see cref="OfflineProfile"/>.
    /// </summary>
    public partial class OfflineProfilePill : CircularContainer
    {
        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            AutoSizeAxes = Axes.X;
            Height = 20;
            Masking = true;

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Gray5,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Margin = new MarginPadding { Horizontal = 8 },
                    Text = OfflineProfileStrings.OfflineProfile.ToUpper(),
                    Font = OsuFont.GetFont(size: 11, weight: FontWeight.Bold),
                    Colour = OsuColour.Gray(0.85f),
                },
            };
        }
    }
}
