// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Screens.Menu;
using osu.Game.Skinning;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// The star fountains of kiai time in the main menu, shot with the applause of the skin when a nominator accepted.
    /// </summary>
    public partial class AcceptedFountains : CompositeDrawable
    {
        private StarFountain leftFountain = null!;
        private StarFountain rightFountain = null!;
        private StarFountainSounds sounds = null!;
        private SkinnableSound applause = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                leftFountain = new StarFountain
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    X = 250,
                },
                rightFountain = new StarFountain
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    X = -250,
                },
                sounds = new StarFountainSounds(),
                // the applause of legacy skins, and the one of the best rank otherwise.
                applause = new SkinnableSound(new SampleInfo(@"applause", @"Results/applause-s"))
                {
                    Volume = { Value = 0.8 },
                },
            };
        }

        public void Shoot()
        {
            int direction = RNG.Next(-1, 2);

            leftFountain.Shoot(-direction);
            rightFountain.Shoot(direction);

            // Don't play SFX when game is in background, as it can be a bit noisy.
            if (host.IsActive.Value)
            {
                sounds.Play();
                applause.Play();
            }
        }
    }
}
