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
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning;
using osu.Game.Storyboards;

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

        [Resolved]
        private GameHost host { get; set; } = null!;

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
        /// Forgets that the hitsound difficulty has been saved, e.g. as its hitsounds have just been copied to the other difficulties explicitly.
        /// </summary>
        public void CancelPending() => pending = false;

        /// <summary>
        /// The sync which is currently running, if any. Difficulties shouldn't be loaded before it has finished, as they may be about to change.
        /// </summary>
        public static Task? Running { get; private set; }

        /// <summary>
        /// Copies the saved hitsounds of the hitsound difficulty to the other difficulties, if it has been saved since they were last copied.
        /// The difficulties are loaded and the hitsounds copied in the background, and the difficulties are saved on the update thread afterwards, one per frame.
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

            Guid currentId = current.ID;
            string currentTitle = current.GetDisplayTitle();

            // this component is gone once the editor has exited, so the saves are scheduled on the game's update thread instead.
            CopyInBackground(beatmapManager, host.UpdateThread.Scheduler, () =>
            {
                // the saved hitsounds, as unsaved changes may have been discarded when leaving.
                var info = beatmapManager.QueryBeatmap(b => b.ID == currentId)
                           ?? throw new InvalidOperationException($@"{currentTitle} couldn't be found.");

                var playable = beatmapManager.GetWorkingBeatmap(info).GetPlayableBeatmap(info.Ruleset);
                var map = HitsoundMap.Create(playable.HitObjects, info.Ruleset.OnlineID);

                return (map.Columns.Select(c => c.GetState()).ToList(), map.Bodies.Select(b => b.GetState()).ToList());
            }, targets, new HitsoundCopyOptions(), null, result =>
            {
                // the failure has already been logged.
                if (result == null)
                    return;

                var (changed, dropped, failed) = result.Value;

                notify(SlopHitsoundEditorStrings.SyncResult(changed, targets.Count - failed), FontAwesome.Solid.Check);

                if (dropped > 0)
                    notify(SlopHitsoundEditorStrings.CopyDropped(dropped), FontAwesome.Solid.ExclamationTriangle);

                if (failed > 0)
                    notify(SlopHitsoundEditorStrings.CopyFailed(failed), FontAwesome.Solid.Times);
            });
        }

        /// <summary>
        /// Copies hitsounds onto other difficulties without blocking the update thread: the difficulties are loaded and the hitsounds copied in the background,
        /// and the difficulties are saved on the update thread afterwards, one per frame. <see cref="Running"/> is set until it has finished.
        /// </summary>
        /// <param name="beatmapManager">The beatmap manager.</param>
        /// <param name="scheduler">The scheduler of the update thread, which the saves and callbacks are run on.</param>
        /// <param name="getSource">Retrieves the hitsounds to copy. Called from a background thread.</param>
        /// <param name="difficulties">The difficulties to copy the hitsounds to.</param>
        /// <param name="options">Which parts of the hitsounds to copy.</param>
        /// <param name="onProgress">Called with the difficulty being processed, whether it is being saved (rather than loaded), and its index.</param>
        /// <param name="onCompleted">Called with the number of changed hitsounds, of hitsounds which couldn't be copied, and of difficulties which couldn't be copied to,
        /// or with null if the hitsounds to copy couldn't be retrieved.</param>
        public static void CopyInBackground(BeatmapManager beatmapManager, Scheduler scheduler,
                                            Func<(IReadOnlyList<HitsoundColumnState> columns, IReadOnlyList<HitsoundBodyState> bodies)> getSource,
                                            IReadOnlyList<BeatmapInfo> difficulties, HitsoundCopyOptions options,
                                            Action<BeatmapInfo, bool, int>? onProgress, Action<(int changed, int dropped, int failed)?> onCompleted)
        {
            var completion = new TaskCompletionSource();
            Running = completion.Task;

            Task.Run(() =>
            {
                try
                {
                    var (columns, bodies) = getSource();

                    var copies = PrepareCopies(beatmapManager, columns, bodies, difficulties, options, (difficulty, index) =>
                    {
                        if (onProgress != null)
                            scheduler.Add(() => onProgress(difficulty, false, index));
                    });

                    scheduler.Add(() => saveNext(copies, 0));
                }
                catch (Exception e)
                {
                    Logger.Error(e, @"Failed to retrieve the hitsounds to copy");

                    scheduler.Add(() =>
                    {
                        onCompleted(null);
                        completion.SetResult();
                    });
                }
            });

            void saveNext(List<PreparedCopy> copies, int index)
            {
                if (index < copies.Count)
                {
                    // the progress is shown for a frame before the save blocks the update thread.
                    onProgress?.Invoke(copies[index].Difficulty, true, index);

                    scheduler.Add(() =>
                    {
                        copies[index].Save(beatmapManager);
                        saveNext(copies, index + 1);
                    });
                    return;
                }

                try
                {
                    onCompleted((copies.Sum(c => c.ChangedHitsounds), copies.Sum(c => c.DroppedHitsounds), copies.Count(c => c.Failed)));
                }
                finally
                {
                    completion.SetResult();
                }
            }
        }

        private void notify(Framework.Localisation.LocalisableString text, IconUsage icon) => notifications?.Post(new SimpleNotification
        {
            Text = text,
            Icon = icon,
        });

        /// <summary>
        /// Loads the difficulties and copies the hitsounds onto them, without saving them yet. Can be called from a background thread.
        /// <paramref name="onDifficultyStarted"/> is called (on the calling thread) with each difficulty and its index before it is loaded.
        /// </summary>
        public static List<PreparedCopy> PrepareCopies(BeatmapManager beatmapManager, IReadOnlyList<HitsoundColumnState> source, IReadOnlyList<HitsoundBodyState> sourceBodies,
                                                       IReadOnlyList<BeatmapInfo> difficulties, HitsoundCopyOptions options, Action<BeatmapInfo, int>? onDifficultyStarted = null)
        {
            var copies = new List<PreparedCopy>();

            foreach (var difficulty in difficulties)
            {
                onDifficultyStarted?.Invoke(difficulty, copies.Count);

                var copy = new PreparedCopy(difficulty);
                copies.Add(copy);

                try
                {
                    var info = beatmapManager.QueryBeatmap(b => b.ID == difficulty.ID)
                               ?? throw new InvalidOperationException($@"{difficulty.GetDisplayTitle()} couldn't be found.");

                    var working = beatmapManager.GetWorkingBeatmap(info);
                    var playable = working.GetPlayableBeatmap(info.Ruleset);

                    // like in the editor, the samples are only defined by the hit objects. otherwise the legacy sample points of the file would be
                    // written back to it, and the encoder could skip the sample points of the copied hitsounds as redundant, reverting their volumes.
                    playable.ControlPointInfo = EditorBeatmap.ConvertControlPoints(playable.ControlPointInfo);

                    var result = HitsoundCopier.Copy(source, sourceBodies, HitsoundMap.Create(playable.HitObjects, info.Ruleset.OnlineID), options);

                    copy.DroppedHitsounds = result.DroppedHitsounds;

                    if (result.ChangedHitObjects.Count == 0)
                        continue;

                    // the samples of nested objects (which are written to the file as well) are created from the samples of their parents.
                    foreach (var h in result.ChangedHitObjects)
                        h.ApplyDefaults(playable.ControlPointInfo, playable.Difficulty);

                    copy.PendingSave = (info, playable, working.GetSkin(), working.Storyboard);
                    copy.PendingChangedHitsounds = result.ChangedHitsounds;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Failed to copy hitsounds to {difficulty.GetDisplayTitle()}");
                    copy.Failed = true;
                }
            }

            return copies;
        }

        /// <summary>
        /// A difficulty which the hitsounds have been copied onto, which still has to be saved (see <see cref="Save"/>).
        /// </summary>
        public class PreparedCopy
        {
            public readonly BeatmapInfo Difficulty;

            public int ChangedHitsounds { get; private set; }

            public int DroppedHitsounds { get; internal set; }

            public bool Failed { get; internal set; }

            internal (BeatmapInfo info, IBeatmap playable, ISkin skin, Storyboard storyboard)? PendingSave;

            internal int PendingChangedHitsounds;

            public PreparedCopy(BeatmapInfo difficulty)
            {
                Difficulty = difficulty;
            }

            /// <summary>
            /// Saves the difficulty, if its hitsounds changed. Has to be called from the update thread.
            /// </summary>
            public void Save(BeatmapManager beatmapManager)
            {
                if (PendingSave is not var (preparedInfo, playable, skin, storyboard))
                    return;

                PendingSave = null;

                try
                {
                    // the beatmap set of the reference fetched while preparing doesn't contain the files saved since (e.g. by the other copies),
                    // and saving it would write its file list back to the database, unlinking those difficulties from their files.
                    var info = beatmapManager.QueryBeatmap(b => b.ID == preparedInfo.ID)
                               ?? throw new InvalidOperationException($@"{Difficulty.GetDisplayTitle()} couldn't be found.");

                    beatmapManager.Save(info, playable, skin, storyboard);
                    ChangedHitsounds = PendingChangedHitsounds;
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Failed to copy hitsounds to {Difficulty.GetDisplayTitle()}");
                    Failed = true;
                }
            }
        }
    }
}
