// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Copies the hitsounds of the hitsound difficulty of the beatmap set onto hit objects while they are placed or moved in another difficulty,
    /// if enabled by <see cref="OsuSetting.SlopEditorAutoApplyHitsoundDifficulty"/>.
    /// </summary>
    /// <remarks>
    /// The hitsounds are copied in the same change as the placement or move, such that undoing it doesn't leave the copied hitsounds behind.
    /// Hit objects which aren't moved keep their hitsounds, such that hitsounds which were changed by hand aren't overwritten.
    /// </remarks>
    public partial class HitsoundDifficultyAutoApplier : Component
    {
        private static readonly HitsoundCopyOptions options = new HitsoundCopyOptions
        {
            // hit objects without a hitsound at their time keep what they were placed with (see apply).
            OverwriteUnmatched = false,
        };

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        private readonly Bindable<bool> enabled = new Bindable<bool>();

        private OsuConfigManager config = null!;

        private IReadOnlyList<HitsoundColumnState>? source;
        private IReadOnlyList<HitsoundBodyState>? sourceBodies;

        /// <summary>
        /// The start and end times of the hit objects when their hitsounds were last copied (or when the editor was loaded),
        /// such that hitsounds are only copied again when they are moved.
        /// </summary>
        private readonly Dictionary<HitObject, (double start, double end)> lastTimes = new Dictionary<HitObject, (double, double)>();

        /// <summary>
        /// The hit objects which had a hitsound at their time when their hitsounds were last copied.
        /// When they are moved away from it, their additions are removed, as they were copied from there.
        /// </summary>
        private readonly HashSet<HitObject> matched = new HashSet<HitObject>();

        /// <summary>
        /// Hit objects added before the source finished loading that need hitsounds applied once it does.
        /// </summary>
        private readonly List<HitObject> pendingApplication = new List<HitObject>();

        /// <summary>
        /// Pasted hit objects which haven't had hitsounds applied yet.
        /// Unlike placed hit objects, their additions are removed when the hitsound difficulty has no hitsound at their time,
        /// as they were copied from somewhere else rather than chosen for this time.
        /// </summary>
        private readonly HashSet<HitObject> pasted = new HashSet<HitObject>();

        /// <summary>
        /// Marks hit objects which are about to be added as pasted.
        /// </summary>
        public void MarkPasted(IEnumerable<HitObject> hitObjects) => pasted.UnionWith(hitObjects);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            this.config = config;
            config.BindWith(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, enabled);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            foreach (var h in editorBeatmap.HitObjects)
                lastTimes[h] = getTimes(h);

            editorBeatmap.HitObjectAdded += onHitObjectAdded;
            editorBeatmap.HitObjectUpdated += onHitObjectUpdated;
            editorBeatmap.HitObjectRemoved += onHitObjectRemoved;

            enabled.BindValueChanged(e =>
            {
                if (e.NewValue)
                    loadSource();
            }, true);
        }

        /// <summary>
        /// Loads the hitsounds of the hitsound difficulty of the beatmap set, if this isn't one itself.
        /// </summary>
        private void loadSource()
        {
            if (source != null || HitsoundPreferences.IsHitsoundDifficulty(config, editorBeatmap.BeatmapInfo))
                return;

            // the difficulty named like a hitsound difficulty, or for which hitsound difficulty mode was turned on.
            var others = editorBeatmap.BeatmapInfo.BeatmapSet?.Beatmaps.Where(b => !b.Equals(editorBeatmap.BeatmapInfo));
            var difficulty = others == null ? null : HitsoundPreferences.FindHitsoundDifficulty(config, others);

            if (difficulty == null || !HitsoundPreferences.ReceivesHitsounds(config, editorBeatmap.BeatmapInfo))
                return;

            Task.Run(() =>
            {
                // a fresh reference, in case the difficulty was saved since the editor was loaded.
                var info = beatmapManager.QueryBeatmap(b => b.ID == difficulty.ID) ?? difficulty;
                var playable = beatmapManager.GetWorkingBeatmap(info).GetPlayableBeatmap(info.Ruleset);
                var map = HitsoundMap.Create(playable.HitObjects, info.Ruleset.OnlineID);

                return (map.Columns.Select(c => c.GetState()).ToList(), map.Bodies.Select(b => b.GetState()).ToList());
            }).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Logger.Error(task.Exception, $@"Failed to load hitsounds of {difficulty.GetDisplayTitle()}");
                    return;
                }

                var (columns, bodies) = task.GetResultSafely();

                Schedule(() =>
                {
                    source = columns;
                    sourceBodies = bodies;

                    if (pendingApplication.Count > 0)
                    {
                        foreach (var h in pendingApplication)
                            apply(h, pasted.Remove(h));

                        pendingApplication.Clear();
                        pasted.Clear();
                        changeHandler?.SaveState();
                    }
                });
            });
        }

        private void onHitObjectAdded(HitObject hitObject)
        {
            if (source == null)
            {
                // pasted hit objects stay marked until the source finished loading.
                apply(hitObject, false);
                pendingApplication.Add(hitObject);
                return;
            }

            apply(hitObject, pasted.Remove(hitObject));
        }

        private void onHitObjectUpdated(HitObject hitObject)
        {
            // only moved hit objects get new hitsounds, as other changes may be changes of the hitsounds by hand.
            if (lastTimes.TryGetValue(hitObject, out var times) && times == getTimes(hitObject))
                return;

            apply(hitObject, matched.Contains(hitObject));
        }

        private void onHitObjectRemoved(HitObject hitObject)
        {
            lastTimes.Remove(hitObject);
            matched.Remove(hitObject);
            pendingApplication.Remove(hitObject);
            pasted.Remove(hitObject);
        }

        /// <param name="hitObject">The hit object to copy the hitsounds onto.</param>
        /// <param name="removeUnmatched">Whether to remove the additions of hitsounds which don't have a counterpart in the hitsound difficulty.</param>
        private void apply(HitObject hitObject, bool removeUnmatched)
        {
            lastTimes[hitObject] = getTimes(hitObject);

            // undoing and redoing restores the hitsounds as they were.
            if (changeHandler is EditorChangeHandler { IsRestoring: true })
                return;

            if (!enabled.Value || source == null || sourceBodies == null)
                return;

            var map = HitsoundMap.Create(hitObject.Yield(), editorBeatmap.BeatmapInfo.Ruleset.OnlineID);

            bool hasMatch = map.Columns.Any(c => source.Any(s => Math.Abs(s.Time - c.Time) <= options.Leniency));

            var result = HitsoundCopier.Copy(source, sourceBodies, map, removeUnmatched ? new HitsoundCopyOptions { OverwriteUnmatched = true } : options);

            if (hasMatch)
                matched.Add(hitObject);
            else
                matched.Remove(hitObject);

            // the samples are already changed, so they are part of the current change. updating only refreshes the drawables.
            foreach (var changed in result.ChangedHitObjects)
                editorBeatmap.Update(changed);
        }

        private static (double start, double end) getTimes(HitObject hitObject) => (hitObject.StartTime, hitObject.GetEndTime());

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorBeatmap.IsNotNull())
            {
                editorBeatmap.HitObjectAdded -= onHitObjectAdded;
                editorBeatmap.HitObjectUpdated -= onHitObjectUpdated;
                editorBeatmap.HitObjectRemoved -= onHitObjectRemoved;
            }
        }
    }
}
