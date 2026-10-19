// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Screens.Menu
{
    /// <summary>
    /// An intro which goes to the main menu as soon as it has loaded, see <see cref="Configuration.OsuSetting.SlopSkipIntro"/>.
    /// </summary>
    public partial class IntroSkip : IntroScreen
    {
        protected override string BeatmapHash => "3c8b1fcc9434dbb29e2fb613d3b9eada9d7bb6c125ceb32396c3b53437280c83";

        protected override string BeatmapFile => "circles.osz";

        private bool waitingForMenu;

        public IntroSkip(Func<MainMenu>? createNextScreen = null)
            : base(createNextScreen)
        {
        }

        protected override void LogoArriving(OsuLogo logo, bool resuming)
        {
            base.LogoArriving(logo, resuming);

            if (!resuming)
            {
                StartTrack();
                FadeInBackground();
                PrepareMenuLoad();
                waitingForMenu = true;
            }
        }

        protected override void Update()
        {
            base.Update();

            if (waitingForMenu && IsMenuLoaded)
            {
                waitingForMenu = false;
                LoadMenu();
            }
        }
    }
}
