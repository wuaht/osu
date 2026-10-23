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
    public partial class SyncTimingConfirmationDialog : DangerousActionDialog
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private readonly BindableBool syncBookmarks = new BindableBool(true);
        private readonly BindableBool syncPreviewPoint = new BindableBool(true);
        private readonly BindableBool syncTiming = new BindableBool();
        private readonly Bindable<TimingCopierResnapMode> timingMode = new Bindable<TimingCopierResnapMode>();
        private readonly Bindable<string> divisors = new Bindable<string>(BeatDivisorList.DEFAULT);

        private readonly FillFlowContainer<Drawable> timingOptions;

        public SyncTimingConfirmationDialog(Action<SyncTimingOptions> syncAction)
        {
            HeaderText = EditorDialogsStrings.SyncTimingConfirmationHeader;
            BodyText = EditorDialogsStrings.SyncTimingConfirmationBody;

            DangerousAction = () =>
            {
                if (!BeatDivisorList.TryParse(divisors.Value, out double[] parsedDivisors))
                    BeatDivisorList.TryParse(BeatDivisorList.DEFAULT, out parsedDivisors);

                syncAction(new SyncTimingOptions(syncBookmarks.Value, syncPreviewPoint.Value, syncTiming.Value, timingMode.Value, parsedDivisors));
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
                    new FormCheckBox
                    {
                        Caption = EditorDialogsStrings.SyncTimingOptionBookmarks,
                        Current = syncBookmarks,
                    },
                    new FormCheckBox
                    {
                        Caption = EditorDialogsStrings.SyncTimingOptionPreviewPoint,
                        Current = syncPreviewPoint,
                    },
                    new FormCheckBox
                    {
                        Caption = SlopEditorStrings.SyncTimingOptionTiming,
                        Current = syncTiming,
                    },
                    timingOptions = new ReverseChildIDFillFlowContainer<Drawable>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(7),
                        Children = new Drawable[]
                        {
                            new FormEnumDropdown<TimingCopierResnapMode>
                            {
                                Caption = SlopEditorStrings.SyncTimingObjectHandling,
                                Current = timingMode,
                            },
                            divisorTextBox = new FormTextBox
                            {
                                Caption = SlopEditorStrings.BeatDivisors,
                                HintText = SlopEditorStrings.BeatDivisorsHint,
                                Current = divisors,
                            },
                        }
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

            syncTiming.BindValueChanged(e => timingOptions.Alpha = e.NewValue ? 1 : 0, true);
        }
    }

    public record SyncTimingOptions(bool Bookmarks, bool PreviewPoint, bool Timing, TimingCopierResnapMode TimingMode, double[] Divisors);
}
