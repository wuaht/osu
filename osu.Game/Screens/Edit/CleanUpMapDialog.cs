// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens.Edit.Resnapping;
using osuTK;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Options for <see cref="MapCleaner"/>, replacing upstream's "Snap all notes to current snap divisor".
    /// </summary>
    public partial class CleanUpMapDialog : DangerousActionDialog
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private readonly Bindable<string> divisors = new Bindable<string>(BeatDivisorList.DEFAULT);
        private readonly BindableBool resnapObjects = new BindableBool(true);
        private readonly BindableBool resnapBookmarks = new BindableBool();
        private readonly BindableBool removeHitsounds = new BindableBool();
        private readonly BindableBool removeMuting = new BindableBool();
        private readonly BindableBool muteUnclickableHitsounds = new BindableBool();

        private readonly FormCheckBox muteUnclickableCheckBox;

        /// <param name="cleanUpAction">Invoked with the chosen options. <see cref="MapCleanerOptions.MuteWithSoftC999"/> is always <c>false</c> and filled in by the caller.</param>
        public CleanUpMapDialog(Action<MapCleanerOptions> cleanUpAction)
        {
            HeaderText = SlopEditorStrings.CleanUpMap;
            BodyText = SlopEditorStrings.CleanUpMapBody;

            DangerousAction = () =>
            {
                if (!BeatDivisorList.TryParse(divisors.Value, out double[] parsedDivisors))
                    BeatDivisorList.TryParse(BeatDivisorList.DEFAULT, out parsedDivisors);

                cleanUpAction(new MapCleanerOptions(parsedDivisors, resnapObjects.Value, resnapBookmarks.Value, removeHitsounds.Value, removeMuting.Value,
                    muteUnclickableHitsounds.Value && !removeMuting.Value, false));
            };

            FormTextBox divisorTextBox;

            MainContent.Child = new ReverseChildIDFillFlowContainer<Drawable>
            {
                Margin = new MarginPadding { Top = 20 },
                Padding = new MarginPadding { Horizontal = 20 },
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(7),
                Children = new Drawable[]
                {
                    divisorTextBox = new FormTextBox
                    {
                        Caption = SlopEditorStrings.BeatDivisors,
                        HintText = SlopEditorStrings.BeatDivisorsHint,
                        Current = divisors,
                    },
                    new FormCheckBox
                    {
                        Caption = SlopEditorStrings.CleanUpResnapObjects,
                        Current = resnapObjects,
                    },
                    new FormCheckBox
                    {
                        Caption = SlopEditorStrings.CleanUpResnapBookmarks,
                        Current = resnapBookmarks,
                    },
                    new FormCheckBox
                    {
                        Caption = SlopEditorStrings.CleanUpRemoveHitsounds,
                        Current = removeHitsounds,
                    },
                    new FormCheckBox
                    {
                        Caption = SlopEditorStrings.CleanUpRemoveMuting,
                        Current = removeMuting,
                    },
                    muteUnclickableCheckBox = new FormCheckBox
                    {
                        Caption = SlopEditorStrings.CleanUpMuteUnclickableHitsounds,
                        HintText = SlopEditorStrings.CleanUpMuteUnclickableHitsoundsHint,
                        Current = muteUnclickableHitsounds,
                    },
                }
            };

            divisorTextBox.OnCommit += (_, _) =>
            {
                if (!BeatDivisorList.TryParse(divisors.Value, out _))
                    divisors.SetDefault();
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            removeMuting.BindValueChanged(e => muteUnclickableCheckBox.Alpha = e.NewValue ? 0.5f : 1, true);
        }
    }
}
