// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Copies the hitsounds of the hitsound difficulty to the other difficulties of the beatmap set (except those which don't receive hitsounds,
    /// see <see cref="HitsoundPreferences.ReceivesHitsounds"/>) when leaving it after it has been saved, if enabled by <see cref="OsuSetting.SlopEditorAutoApplyHitsoundDifficulty"/>.
    /// </summary>
    /// <remarks>
    /// The hitsounds are copied when leaving the difficulty rather than when saving it, as the editor would have to be reloaded after each save:
    /// saving the hitsound difficulty again would otherwise write its outdated beatmap set (including the hashes of the other difficulties) back to the database.
    /// </remarks>
    public partial class HitsoundDifficultySync : Component
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private readonly Bindable<bool> enabled = new Bindable<bool>();

        /// <summary>
        /// Whether the hitsound difficulty has been saved since its hitsounds were last copied.
        /// </summary>
        private bool pending;

        [BackgroundDependencyLoader]
        private void load()
        {
            config.BindWith(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, enabled);
        }

        /// <summary>
        /// To be called when the difficulty has been saved.
        /// </summary>
        public void OnSaved()
        {
            if (enabled.Value && HitsoundPreferences.IsHitsoundDifficulty(config, editorBeatmap.BeatmapInfo))
                pending = true;
        }

        /// <summary>
        /// Copies the saved hitsounds of the hitsound difficulty to the other difficulties in the background, if it has been saved since they were last copied.
        /// To be called when leaving the difficulty.
        /// </summary>
        public void SyncIfPending()
        {
            if (!pending)
                return;

            pending = false;

            var current = editorBeatmap.BeatmapInfo;
            var targets = current.BeatmapSet?.Beatmaps.Where(b => !b.Equals(current) && HitsoundPreferences.ReceivesHitsounds(config, b)).ToList();

            if (targets == null || targets.Count == 0)
                return;

            // Capture values from the main thread before handing off to the background.
            Guid currentId = current.ID;
            string currentTitle = current.GetDisplayTitle();

            Task.Run(() =>
            {
                try
                {
                    // the saved hitsounds, as unsaved changes may have been discarded when leaving.
                    var info = beatmapManager.QueryBeatmap(b => b.ID == currentId)
                               ?? throw new InvalidOperationException($@"{currentTitle} couldn't be found.");

                    var playable = beatmapManager.GetWorkingBeatmap(info).GetPlayableBeatmap(info.Ruleset);
                    var map = HitsoundMap.Create(playable.HitObjects, info.Ruleset.OnlineID);

                    var (changed, dropped, failed) = CopyToDifficulties(beatmapManager, map.Columns.Select(c => c.GetState()).ToList(), map.Bodies.Select(b => b.GetState()).ToList(), targets,
                        new HitsoundCopyOptions());

                    notify(SlopHitsoundEditorStrings.SyncResult(changed, targets.Count - failed), FontAwesome.Solid.Check);

                    if (dropped > 0)
                        notify(SlopHitsoundEditorStrings.CopyDropped(dropped), FontAwesome.Solid.ExclamationTriangle);

                    if (failed > 0)
                        notify(SlopHitsoundEditorStrings.CopyFailed(failed), FontAwesome.Solid.Times);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Failed to copy the hitsounds of {currentTitle}");
                }
            });
        }

        private void notify(Framework.Localisation.LocalisableString text, IconUsage icon) => notifications?.Post(new SimpleNotification
        {
            Text = text,
            Icon = icon,
        });

        /// <summary>
        /// Copies hitsounds onto other difficulties and saves them.
        /// </summary>
        /// <remarks>
        /// If the difficulty being edited belongs to the same beatmap set, it has to be saved before, and the editor has to be reloaded afterwards,
        /// as saving it would otherwise write its outdated beatmap set (including the hashes of the other difficulties) back to the database.
        /// </remarks>
        /// <returns>The number of changed hitsounds, of hitsounds which couldn't be copied, and of difficulties which couldn't be copied to.</returns>
        public static (int changed, int dropped, int failed) CopyToDifficulties(BeatmapManager beatmapManager, IReadOnlyList<HitsoundColumnState> source,
                                                                                IReadOnlyList<HitsoundBodyState> sourceBodies, IReadOnlyList<BeatmapInfo> difficulties,
                                                                                HitsoundCopyOptions options)
        {
            int changedHitsounds = 0;
            int droppedHitsounds = 0;
            int failed = 0;

            foreach (var difficulty in difficulties)
            {
                try
                {
                    // fetch a fresh detached reference, as each save changes the beatmap set.
                    var info = beatmapManager.QueryBeatmap(b => b.ID == difficulty.ID)
                               ?? throw new InvalidOperationException($@"{difficulty.GetDisplayTitle()} couldn't be found.");

                    var working = beatmapManager.GetWorkingBeatmap(info);
                    var playable = working.GetPlayableBeatmap(info.Ruleset);

                    var result = HitsoundCopier.Copy(source, sourceBodies, HitsoundMap.Create(playable.HitObjects, info.Ruleset.OnlineID), options);

                    droppedHitsounds += result.DroppedHitsounds;

                    if (result.ChangedHitObjects.Count == 0)
                        continue;

                    // the samples of nested objects (which are written to the file as well) are created from the samples of their parents.
                    foreach (var h in result.ChangedHitObjects)
                        h.ApplyDefaults(playable.ControlPointInfo, playable.Difficulty);

                    beatmapManager.Save(info, playable, working.GetSkin(), working.Storyboard);
                    changedHitsounds += result.ChangedHitsounds;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Failed to copy hitsounds to {difficulty.GetDisplayTitle()}");
                    failed++;
                }
            }

            return (changedHitsounds, droppedHitsounds, failed);
        }
    }
}
