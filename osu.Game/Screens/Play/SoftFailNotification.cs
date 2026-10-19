// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A large notification shown when failing without ending gameplay (see <see cref="Configuration.OsuSetting.SlopKillOnFail"/>).
    /// </summary>
    public partial class SoftFailNotification : CompositeDrawable
    {
        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = SlopSettingsStrings.SoftFailNotification,
                Font = OsuFont.Torus.With(size: 72, weight: FontWeight.Bold),
                Colour = colours.Red,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            this.FadeInFromZero(200).Then().Delay(1500).FadeOut(500).Expire();
        }
    }
}
