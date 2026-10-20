// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play
{
    public partial class PauseOverlay : GameplayMenuOverlay
    {
        public override LocalisableString Header => GameplayMenuOverlayStrings.PausedHeader;

        /// <summary>
        /// Saves the score up to the current point in time, so that its replay can be saved or exported while paused.
        /// </summary>
        public Func<Task<ScoreInfo>>? SaveReplay { get; init; }

        protected override void PopIn()
        {
            // the score keeps progressing between pauses, so each pause gets a fresh button rather than one tracking a previously saved score.
            if (SaveReplay != null)
            {
                FooterContent.Child = new SaveFailedScoreButton(SaveReplay)
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Width = 300,
                    Margin = new MarginPadding { Bottom = 10 },
                };
            }

            base.PopIn();
        }

        protected override Action BackAction => () =>
        {
            if (Buttons.Any())
                Buttons.First().TriggerClick();
            else
                OnResume?.Invoke();
        };

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            switch (e.Action)
            {
                case GlobalAction.PauseGameplay:
                    InternalButtons.First().TriggerClick();
                    return true;
            }

            return base.OnPressed(e);
        }
    }
}
