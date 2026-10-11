// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Hitsounding;
using osuTK;

namespace osu.Game.Screens.Edit.Submission
{
    [LocalisableDescription(typeof(BeatmapSubmissionStrings), nameof(BeatmapSubmissionStrings.SubmissionSettings))]
    public partial class ScreenSubmissionSettings : WizardScreen
    {
        private readonly BindableBool loadInBrowserAfterSubmission = new BindableBool();

        public override LocalisableString? NextStepText => BeatmapSubmissionStrings.ConfirmSubmission;

        [Resolved]
        private BeatmapSubmissionSettings settings { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager configManager, OsuColour colours)
        {
            configManager.BindWith(OsuSetting.EditorSubmissionLoadInBrowserAfterSubmission, loadInBrowserAfterSubmission);

            Content.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    new FormEnumDropdown<BeatmapSubmissionTarget>
                    {
                        RelativeSizeAxes = Axes.X,
                        Caption = BeatmapSubmissionStrings.BeatmapSubmissionTargetCaption,
                        Current = settings.Target,
                    },
                    new FormCheckBox
                    {
                        Caption = BeatmapSubmissionStrings.NotifyOnDiscussionReplies,
                        Current = settings.NotifyOnDiscussionReplies,
                    },
                    new FormCheckBox
                    {
                        Caption = BeatmapSubmissionStrings.LoadInBrowserAfterSubmission,
                        Current = loadInBrowserAfterSubmission,
                    },
                    new OsuSpriteText
                    {
                        Text = SlopSubmissionStrings.UploadedDifficulties,
                        Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE, weight: FontWeight.Bold),
                        Margin = new MarginPadding { Top = 15 },
                    },
                    difficultyFlow = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Spacing = new Vector2(5),
                    },
                    new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE, weight: FontWeight.Bold))
                    {
                        RelativeSizeAxes = Axes.X,
                        Colour = colours.Orange1,
                        Text = BeatmapSubmissionStrings.LegacyExportDisclaimer,
                        Padding = new MarginPadding { Top = 20 }
                    },
                }
            });

            addDifficultyCheckboxes(configManager);

            switch (settings.LatestOnlineStateRequest?.CompletionState)
            {
                case APIRequestCompletionState.Completed:
                    setSubmissionTargetFromLatestOnlineState();
                    break;

                case APIRequestCompletionState.Waiting:
                    settings.Target.Disabled = true;
                    settings.LatestOnlineStateRequest.Success += _ => setSubmissionTargetFromLatestOnlineState();
                    break;
            }
        }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        private FillFlowContainer difficultyFlow = null!;

        private readonly List<(BeatmapInfo difficulty, BindableBool uploaded)> difficulties = new List<(BeatmapInfo, BindableBool)>();

        /// <summary>
        /// Adds a checkbox for each difficulty, to choose which difficulties are uploaded. Hitsound difficulties aren't by default (see <see cref="BeatmapSubmissionSettings.ExcludedBeatmaps"/>).
        /// </summary>
        private void addDifficultyCheckboxes(OsuConfigManager configManager)
        {
            foreach (var difficulty in beatmap.Value.BeatmapSetInfo.Beatmaps.OrderBy(b => b.Ruleset.OnlineID).ThenBy(b => b.StarRating))
            {
                var uploaded = new BindableBool(settings.ExcludedBeatmaps.All(b => b.ID != difficulty.ID));

                uploaded.BindValueChanged(u =>
                {
                    if (u.NewValue)
                        settings.ExcludedBeatmaps.RemoveAll(b => b.ID == difficulty.ID);
                    else if (settings.ExcludedBeatmaps.All(b => b.ID != difficulty.ID))
                        settings.ExcludedBeatmaps.Add(difficulty);

                    updateCheckboxes();
                });

                difficulties.Add((difficulty, uploaded));

                difficultyFlow.Add(new FormCheckBox
                {
                    Caption = HitsoundPreferences.IsHitsoundDifficulty(configManager, difficulty)
                        ? SlopSubmissionStrings.HitsoundDifficultyCaption(difficulty.DifficultyName)
                        : difficulty.DifficultyName,
                    Current = uploaded,
                });
            }

            updateCheckboxes();
        }

        private void updateCheckboxes()
        {
            // a beatmap set needs at least one difficulty, so the last uploaded difficulty can't be turned off.
            int uploadedCount = difficulties.Count(d => d.uploaded.Value);

            foreach (var (_, uploaded) in difficulties)
                uploaded.Disabled = uploaded.Value && uploadedCount == 1;
        }

        private void setSubmissionTargetFromLatestOnlineState()
        {
            Debug.Assert(settings.LatestOnlineStateRequest != null);
            settings.Target.Disabled = false;
            settings.Target.Value = settings.LatestOnlineStateRequest.Response?.Status >= BeatmapOnlineStatus.Pending ? BeatmapSubmissionTarget.Pending : BeatmapSubmissionTarget.WIP;
        }
    }
}
