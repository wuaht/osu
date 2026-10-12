// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The state of the hitsound editor (the hitsounds of the beatmap, the selection and the lane settings), and all operations which modify hitsounds.
    /// </summary>
    /// <remarks>
    /// All modifications go through <see cref="EditorBeatmap"/> and are undoable.
    /// </remarks>
    public partial class HitsoundEditor : Component
    {
        /// <summary>
        /// How many milliseconds apart a pasted hitsound may be from an existing hitsound to be applied to it.
        /// </summary>
        public const double PASTE_LENIENCY = 2;

        /// <summary>
        /// Whether the beatmap is a hitsound difficulty, whose objects only exist to play hitsounds.
        /// In this mode, objects are created and deleted as needed, such that every combination of hitsounds can be played.
        /// </summary>
        public readonly BindableBool HitsoundDifficultyMode = new BindableBool();

        /// <summary>
        /// Whether lanes which aren't played anywhere in the beatmap are hidden.
        /// </summary>
        public readonly BindableBool HideUnusedLanes = new BindableBool();

        /// <summary>
        /// The lanes which aren't played back.
        /// </summary>
        public readonly BindableList<HitsoundLane> MutedLanes = new BindableList<HitsoundLane>();

        /// <summary>
        /// The lanes whose sub-lanes (one for each custom sample set) are displayed below them.
        /// </summary>
        public readonly BindableList<HitsoundLane> ExpandedLanes = new BindableList<HitsoundLane>();

        /// <summary>
        /// Invoked once per frame at most, after the hitsounds of the beatmap changed.
        /// </summary>
        public event Action? MapChanged;

        public event Action? SelectionChanged;

        /// <summary>
        /// The lanes which are displayed, which are all lanes unless <see cref="HideUnusedLanes"/> is enabled.
        /// </summary>
        public IReadOnlyList<HitsoundLane> VisibleLanes { get; private set; } = HitsoundLane.ALL;

        public event Action? VisibleLanesChanged;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        /// <summary>
        /// The hitnormal bank and volume which most hitsounds of the beatmap use.
        /// New hitsounds of additions are played along with a hitnormal of this bank, new hitsounds without a hitsound before them start with this volume,
        /// and volumes are reset to it.
        /// </summary>
        public (string bank, int volume) GetBaseHitsounds()
        {
            // a hitsound difficulty which was just created without objects starts with the bank and volume chosen when creating it.
            if (Map.Columns.Count == 0 && startingHitsounds.TryGetValue(editorBeatmap.BeatmapInfo.ID, out var starting))
                return starting;

            return GetBaseHitsounds(Map.Columns);
        }

        /// <summary>
        /// The bank and volume which hitsound difficulties without objects start with, by the ID of their beatmap.
        /// Static, such that they are kept when the editor switches to the new difficulty.
        /// </summary>
        private static readonly Dictionary<Guid, (string bank, int volume)> startingHitsounds = new Dictionary<Guid, (string bank, int volume)>();

        /// <summary>
        /// Sets the bank and volume which a hitsound difficulty starts with while it doesn't have any hitsounds.
        /// </summary>
        public static void SetStartingHitsounds(BeatmapInfo beatmap, string bank, int volume) => startingHitsounds[beatmap.ID] = (bank, volume);

        public static (string bank, int volume) GetBaseHitsounds(IReadOnlyCollection<HitsoundColumn> columns)
        {
            if (columns.Count == 0)
                return (HitSampleInfo.BANK_NORMAL, 100);

            string bank = columns.Select(c => c.NormalBank).OfType<string>()
                                 .GroupBy(b => b)
                                 .OrderByDescending(g => g.Count())
                                 .Select(g => g.Key)
                                 .FirstOrDefault() ?? HitSampleInfo.BANK_NORMAL;

            int volume = columns.GroupBy(c => c.Volume)
                                .OrderByDescending(g => g.Count())
                                .ThenByDescending(g => g.Key)
                                .First().Key;

            return (bank, volume);
        }

        private HitsoundMap? map;
        private bool mapChangePending;

        private readonly HashSet<int> selectedKeys = new HashSet<int>();

        /// <summary>
        /// The lanes of selected hitsounds which are only selected in some lanes (by selecting a rectangle of lanes), by their keys.
        /// Hitsounds without an entry are selected in all lanes.
        /// </summary>
        private readonly Dictionary<int, HashSet<HitsoundLane>> selectedLanes = new Dictionary<int, HashSet<HitsoundLane>>();

        /// <summary>
        /// Objects which have been created in <see cref="HitsoundDifficultyMode"/> only to play an addition which no other object of their hitsound could play.
        /// Their hitnormal isn't wanted, so they are deleted once they don't play any additions anymore.
        /// </summary>
        private readonly HashSet<HitObject> additionCarriers = new HashSet<HitObject>();

        /// <summary>
        /// The key of the hitsound which was last clicked to select it, from which shift-clicking selects a range.
        /// </summary>
        private int? selectionAnchor;

        /// <summary>
        /// The hitsounds of the beatmap. Recreated lazily after the beatmap changes.
        /// </summary>
        public HitsoundMap Map => map ??= HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);

        private int rulesetId => editorBeatmap.BeatmapInfo.Ruleset.OnlineID;

        /// <summary>
        /// The bank of the hitnormals which are played along with new additions, or <c>null</c> to use the bank which most hitsounds use.
        /// Stored for the beatmap set (see <see cref="HitsoundPreferences.GetDefaultNormalBank"/>).
        /// </summary>
        public readonly Bindable<string?> DefaultNormalBank = new Bindable<string?>();

        /// <summary>
        /// The bank of the hitnormals which are played along with new additions (see <see cref="DefaultNormalBank"/>).
        /// </summary>
        public string GetDefaultNormalBank() => DefaultNormalBank.Value ?? GetBaseHitsounds().bank;

        [Resolved]
        private OsuConfigManager? config { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            var beatmapInfo = editorBeatmap.BeatmapInfo;

            HitsoundDifficultyMode.Value = HitsoundPreferences.IsHitsoundDifficulty(config, beatmapInfo);
            DefaultNormalBank.Value = HitsoundPreferences.GetDefaultNormalBank(config, beatmapInfo.BeatmapSet);

            if (config != null)
            {
                // remembered such that other difficulties can find the hitsound difficulty (see HitsoundDifficultyAutoApplier).
                HitsoundDifficultyMode.BindValueChanged(m => HitsoundPreferences.SetHitsoundDifficulty(config, beatmapInfo, m.NewValue));

                DefaultNormalBank.BindValueChanged(b =>
                {
                    if (beatmapInfo.BeatmapSet != null)
                        HitsoundPreferences.SetDefaultNormalBank(config, beatmapInfo.BeatmapSet, b.NewValue);
                });
            }

            editorBeatmap.HitObjectAdded += onHitObjectChanged;
            editorBeatmap.HitObjectRemoved += onHitObjectChanged;
            editorBeatmap.HitObjectUpdated += onHitObjectChanged;

            if (editorBeatmap.BeatmapSkin != null)
                editorBeatmap.BeatmapSkin.BeatmapSkinChanged += onBeatmapSkinChanged;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            HideUnusedLanes.BindValueChanged(_ => updateVisibleLanes(true), true);
            // deferred, as lanes are usually expanded by a button of a lane header, which is recreated when the visible lanes change.
            ExpandedLanes.BindCollectionChanged((_, _) => Scheduler.AddOnce(updateVisibleLanes, false));
        }

        // the event isn't invoked on the update thread.
        private void onBeatmapSkinChanged() => Schedule(() =>
        {
            availableCustomIndices = null;
            updateVisibleLanes(false);
        });

        protected override void Update()
        {
            base.Update();

            if (!mapChangePending)
                return;

            mapChangePending = false;

            // hitsounds whose objects have been removed or moved can't be selected anymore.
            int selectedCount = selectedKeys.Count;
            selectedKeys.RemoveWhere(k => Map.GetColumn(k) == null);

            foreach (int key in selectedLanes.Keys.Where(k => !selectedKeys.Contains(k)).ToArray())
                selectedLanes.Remove(key);

            MapChanged?.Invoke();
            updateVisibleLanes(false);

            if (selectedKeys.Count != selectedCount)
                SelectionChanged?.Invoke();
        }

        /// <param name="reset">
        /// Whether lanes which aren't used anymore should be hidden.
        /// Otherwise, lanes are only added, such that lanes don't disappear while their hitsounds are being edited.
        /// </param>
        private void updateVisibleLanes(bool reset)
        {
            IReadOnlyList<HitsoundLane> parents = HitsoundLane.ALL;

            if (HideUnusedLanes.Value)
            {
                parents = HitsoundLane.ALL.Where(l => Map.UsesLane(l) || (!reset && VisibleLanes.Contains(l))).ToList();

                // there always needs to be a lane to create hitsounds on.
                if (parents.Count == 0)
                    parents = HitsoundLane.ALL.Where(l => !l.IsAddition).ToList();
            }

            var lanes = new List<HitsoundLane>();

            foreach (var parent in parents)
            {
                lanes.Add(parent);

                if (!ExpandedLanes.Contains(parent))
                    continue;

                var indices = new SortedSet<int>(GetSubLaneIndices(parent));

                // like lanes, sub-lanes don't disappear while their hitsounds are being edited.
                if (!reset)
                    indices.UnionWith(VisibleLanes.Where(l => l.IsSubLane && l.Parent == parent).Select(l => l.CustomIndex!.Value));

                lanes.AddRange(indices.Select(parent.WithCustomIndex));
            }

            if (lanes.SequenceEqual(VisibleLanes))
                return;

            VisibleLanes = lanes;
            VisibleLanesChanged?.Invoke();
        }

        private void onHitObjectChanged(HitObject _) => invalidateMap();

        #region Sub-lanes

        /// <summary>
        /// The custom sample indices for which the beatmap has a sample file, by lane.
        /// </summary>
        private Dictionary<HitsoundLane, SortedSet<int>>? availableCustomIndices;

        /// <summary>
        /// The custom sample indices of the sub-lanes of a lane: the ones the beatmap has a sample file for, and the ones its hitsounds use.
        /// Empty if the lane's sample is only played from the skin, as there's nothing to distinguish then.
        /// </summary>
        public IReadOnlyList<int> GetSubLaneIndices(HitsoundLane lane)
        {
            lane = lane.Parent;

            var indices = new SortedSet<int>(Map.GetUsedCustomIndices(lane));

            if (getAvailableCustomIndices().TryGetValue(lane, out var available))
                indices.UnionWith(available);

            return indices.Any(i => i > 0) ? indices.ToList() : Array.Empty<int>();
        }

        public void ToggleExpanded(HitsoundLane lane)
        {
            lane = lane.Parent;

            if (!ExpandedLanes.Remove(lane))
                ExpandedLanes.Add(lane);
        }

        private Dictionary<HitsoundLane, SortedSet<int>> getAvailableCustomIndices()
        {
            if (availableCustomIndices != null)
                return availableCustomIndices;

            availableCustomIndices = new Dictionary<HitsoundLane, SortedSet<int>>();

            if (editorBeatmap.BeatmapSkin == null)
                return availableCustomIndices;

            foreach (var sampleSet in editorBeatmap.BeatmapSkin.GetAvailableSampleSets())
            {
                int index = sampleSet.SampleSetIndex;

                foreach (var lane in HitsoundLane.ALL)
                {
                    // e.g. "soft-hitwhistle.wav" for the beatmap's default samples, and "soft-hitwhistle2.wav" for custom index 2.
                    string name = index >= 2 ? $@"{lane.Bank}-{lane.Sample}{index}" : $@"{lane.Bank}-{lane.Sample}";

                    if (!sampleSet.Filenames.Any(f => Path.GetFileNameWithoutExtension(f) == name))
                        continue;

                    if (!availableCustomIndices.TryGetValue(lane, out var indices))
                        availableCustomIndices[lane] = indices = new SortedSet<int>();

                    indices.Add(index);
                }
            }

            return availableCustomIndices;
        }

        #endregion

        private void invalidateMap()
        {
            map = null;
            mapChangePending = true;
        }

        /// <summary>
        /// Whether a difficulty name suggests a hitsound difficulty (e.g. "Hitsounds" or "HS").
        /// </summary>
        public static bool IsHitsoundDifficultyName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return name.Contains(@"hitsound", StringComparison.OrdinalIgnoreCase)
                   || Regex.IsMatch(name, @"(^|[^a-z0-9])hs([^a-z0-9]|$)", RegexOptions.IgnoreCase);
        }

        #region Selection

        public IReadOnlyCollection<int> SelectedKeys => selectedKeys;

        public bool IsSelected(int key) => selectedKeys.Contains(key);

        /// <summary>
        /// The lanes in which a selected hitsound is selected, or <c>null</c> if it's selected in all lanes (or not selected).
        /// </summary>
        public IReadOnlyCollection<HitsoundLane>? GetSelectedLanes(int key) => selectedLanes.GetValueOrDefault(key);

        /// <summary>
        /// The selected columns, ordered by time.
        /// </summary>
        public IReadOnlyList<HitsoundColumn> SelectedColumns => selectedKeys.Count == 0
            ? Array.Empty<HitsoundColumn>()
            : Map.Columns.Where(c => selectedKeys.Contains(c.Key)).ToList();

        /// <summary>
        /// The columns which keyboard shortcuts and the selection tools act on: the selected columns, or the column at the given time if nothing is selected.
        /// </summary>
        /// <param name="currentTime">The current time of the editor.</param>
        public IReadOnlyList<HitsoundColumn> GetTargetColumns(double currentTime)
        {
            if (selectedKeys.Count > 0)
                return SelectedColumns;

            var column = Map.FindClosestColumn(currentTime, PASTE_LENIENCY);
            return column != null ? new[] { column } : Array.Empty<HitsoundColumn>();
        }

        /// <param name="keys">The keys of the hitsounds to select.</param>
        /// <param name="additive">Whether to keep the current selection.</param>
        /// <param name="lanes">The lanes in which to select the hitsounds, or <c>null</c> to select them in all lanes.</param>
        public void SetSelection(IEnumerable<int> keys, bool additive = false, IEnumerable<HitsoundLane>? lanes = null)
        {
            if (!additive)
            {
                selectedKeys.Clear();
                selectedLanes.Clear();
            }

            var laneList = lanes?.ToList();

            foreach (int key in keys)
            {
                bool wasSelected = !selectedKeys.Add(key);

                if (laneList == null)
                    selectedLanes.Remove(key);
                else if (!wasSelected)
                    selectedLanes[key] = new HashSet<HitsoundLane>(laneList);
                else if (selectedLanes.TryGetValue(key, out var existing))
                    existing.UnionWith(laneList);

                // hitsounds which were already selected in all lanes stay so.
            }

            SelectionChanged?.Invoke();
        }

        public void ToggleSelection(int key)
        {
            selectedLanes.Remove(key);

            if (!selectedKeys.Remove(key))
                selectedKeys.Add(key);

            selectionAnchor = key;
            SelectionChanged?.Invoke();
        }

        /// <summary>
        /// Selects only the given hitsound, which becomes the start of ranges selected by <see cref="SelectRangeTo"/>.
        /// </summary>
        public void SelectSingle(int key)
        {
            selectionAnchor = key;
            SetSelection(new[] { key });
        }

        /// <summary>
        /// Adds all hitsounds between the hitsound which was last clicked and the given one to the selection.
        /// </summary>
        public void SelectRangeTo(int key)
        {
            var anchor = selectionAnchor is int anchorKey ? Map.GetColumn(anchorKey) : null;
            var target = Map.GetColumn(key);

            if (anchor == null || target == null)
            {
                selectionAnchor = key;
                SetSelection(new[] { key }, additive: true);
                return;
            }

            double start = Math.Min(anchor.Time, target.Time);
            double end = Math.Max(anchor.Time, target.Time);

            SetSelection(Map.Columns.Where(c => c.Time >= start && c.Time <= end).Select(c => c.Key), additive: true);
        }

        /// <summary>
        /// The custom sample index which previews of lanes use: the one of the targeted hitsounds, or of the last hitsound before the given time.
        /// </summary>
        public int GetPreviewCustomIndex(double currentTime)
        {
            var targets = GetTargetColumns(currentTime);

            if (targets.Count > 0)
                return targets[0].CustomIndex;

            int index = Map.IndexOfFirstColumnAtOrAfter(currentTime) - 1;

            if (index >= 0)
                return Map.Columns[index].CustomIndex;

            return Map.Columns.Count > 0 ? Map.Columns[0].CustomIndex : 0;
        }

        public void SelectAll() => SetSelection(Map.Columns.Select(c => c.Key));

        public void ClearSelection()
        {
            if (selectedKeys.Count == 0)
                return;

            selectedKeys.Clear();
            selectedLanes.Clear();
            SelectionChanged?.Invoke();
        }

        #endregion

        #region Strokes

        private bool strokeActive;

        /// <summary>
        /// Starts a stroke (e.g. painting along a lane), which combines all modifications until <see cref="EndStroke"/> into a single undo step.
        /// </summary>
        public void BeginStroke()
        {
            if (strokeActive)
                return;

            strokeActive = true;
            editorBeatmap.BeginChange();
        }

        public void EndStroke()
        {
            if (!strokeActive)
                return;

            strokeActive = false;
            editorBeatmap.EndChange();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (editorBeatmap.IsNotNull())
            {
                EndStroke();

                editorBeatmap.HitObjectAdded -= onHitObjectChanged;
                editorBeatmap.HitObjectRemoved -= onHitObjectChanged;
                editorBeatmap.HitObjectUpdated -= onHitObjectChanged;

                if (editorBeatmap.BeatmapSkin != null)
                    editorBeatmap.BeatmapSkin.BeatmapSkinChanged -= onBeatmapSkinChanged;
            }

            base.Dispose(isDisposing);
        }

        #endregion

        #region Lane operations

        /// <summary>
        /// Turns the sample of a lane on or off for a column.
        /// </summary>
        /// <remarks>
        /// As every hitsound plays a hitnormal, turning on a hitnormal lane changes the bank of the hitnormal,
        /// and turning one off only does something in <see cref="HitsoundDifficultyMode"/>, where it deletes the objects.
        /// Turning on a sub-lane plays the sample from its custom sample set, see <see cref="setSubLane"/>.
        /// </remarks>
        public void SetLane(HitsoundColumn column, HitsoundLane lane, bool active) => perform(changed => setLane(column, lane, active, changed));

        /// <summary>
        /// Turns off the samples of the given lanes for each column (see <see cref="SetLane"/>), in a single change.
        /// </summary>
        public void RemoveLanes(IReadOnlyList<(HitsoundColumn column, IReadOnlyCollection<HitsoundLane> lanes)> items) => perform(changed =>
        {
            foreach (var (column, lanes) in items)
            {
                // additions first, as turning off hitnormals deletes the objects in hitsound difficulty mode.
                foreach (var lane in lanes.OrderBy(l => l.IsAddition ? 0 : 1))
                    setLane(column, lane, false, changed);
            }
        });

        /// <summary>
        /// Creates a hitsound at a time without one, which is only possible in <see cref="HitsoundDifficultyMode"/>.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <param name="lane">The lane whose sample the hitsound should play in addition to the hitnormal.</param>
        /// <returns>Whether the hitsound has been created.</returns>
        public bool CreateColumn(double time, HitsoundLane lane)
        {
            if (!HitsoundDifficultyMode.Value || Map.FindClosestColumn(time, HitsoundMap.COLUMN_MERGE_DISTANCE) != null)
                return false;

            // continue the hitsounds before, as the volume and custom index are usually the same for a while.
            int previousIndex = Map.IndexOfFirstColumnAtOrAfter(time) - 1;
            var previous = previousIndex >= 0 ? Map.Columns[previousIndex] : null;

            var baseHitsounds = GetBaseHitsounds();

            // the hitnormal bank rarely changes within a song, so additions are played along with a hitnormal of the default bank.
            string normalBank = lane.IsAddition ? GetDefaultNormalBank() : lane.Bank;
            int volume = previous?.Volume ?? baseHitsounds.volume;
            int customIndex = lane.CustomIndex ?? previous?.CustomIndex ?? 0;

            bool created = false;

            perform(_ => created = createObjects(time, 1, samples =>
            {
                samples = HitsoundSamples.WithoutAdditions(HitsoundSamples.WithNormalBank(samples, normalBank, false));

                if (lane.IsAddition)
                    samples = HitsoundSamples.WithAddition(samples, lane.Sample, lane.Bank);

                return HitsoundSamples.WithCustomIndex(HitsoundSamples.WithVolume(samples, volume), customIndex);
            }) > 0);

            return created;
        }

        /// <summary>
        /// Turns the sample of a lane on or off for a slider body.
        /// Only hitnormal lanes (which set the bank of "sliderslide") and whistle lanes ("sliderwhistle") are supported.
        /// </summary>
        public void SetBodyLane(HitsoundBody body, HitsoundLane lane, bool active)
        {
            if (!HitsoundBody.SupportsLane(lane))
                return;

            perform(changed =>
            {
                var samples = body.Samples.ToList();

                if (!lane.IsAddition)
                {
                    if (!active)
                        return;

                    samples = HitsoundSamples.WithNormalBank(samples, lane.Bank, false);
                }
                else if (active)
                    samples = HitsoundSamples.WithAddition(samples, lane.Sample, lane.Bank);
                else if (HitsoundSamples.Has(samples, lane))
                    samples = HitsoundSamples.WithoutAddition(samples, lane.Sample);

                // the body plays all of its samples from the same custom sample set.
                if (active && lane.CustomIndex is int customIndex)
                    samples = HitsoundSamples.WithCustomIndex(samples, customIndex);

                if (HitsoundSamples.AreEquivalent(samples, body.Samples))
                    return;

                body.SetSamples(samples);
                changed.Add(body.HitObject);
            });
        }

        private void setLane(HitsoundColumn column, HitsoundLane lane, bool active, HashSet<HitObject> changed)
        {
            var targets = editableTargets(column);

            if (targets.Count == 0)
                return;

            if (lane.CustomIndex is int customIndex)
            {
                setSubLane(column, lane, customIndex, active, targets, changed);
                return;
            }

            if (!lane.IsAddition)
            {
                if (active)
                {
                    foreach (var target in targets)
                        setSamples(target, HitsoundSamples.WithNormalBank(target.Samples, lane.Bank, false), changed);
                }
                else if (HitsoundDifficultyMode.Value && column.Has(lane))
                    deleteColumn(column, changed);

                return;
            }

            if (active)
            {
                if (targets.Any(t => HitsoundSamples.Has(t.Samples, lane)))
                    return;

                // all additions of a target use the same bank, so the addition can only be added to a target with additions of the same bank, or none.
                var target = targets.FirstOrDefault(t => HitsoundSamples.GetAdditionBank(t.Samples) == lane.Bank)
                             ?? targets.FirstOrDefault(t => HitsoundSamples.GetAdditionBank(t.Samples) == null);

                if (target != null)
                    setSamples(target, HitsoundSamples.WithAddition(target.Samples, lane.Sample, lane.Bank), changed);
                else
                {
                    // without a free target, the additions of the first target change their bank, which is the closest possible to what was asked for.
                    // no object is stacked to play the addition instead, as hitsound difficulties mustn't contain objects at the same time.
                    setSamples(targets[0], HitsoundSamples.WithAddition(targets[0].Samples, lane.Sample, lane.Bank), changed);
                }
            }
            else
            {
                foreach (var target in targets.Where(t => HitsoundSamples.Has(t.Samples, lane)))
                    setSamples(target, HitsoundSamples.WithoutAddition(target.Samples, lane.Sample), changed);

                if (HitsoundDifficultyMode.Value)
                    removeRedundantObjects(column, changed);
            }
        }

        /// <summary>
        /// Turns the sample of a sub-lane on or off. As the custom sample index applies to all samples of a target, the sample is preferably played by a target which
        /// already uses the custom sample index. Otherwise, the custom sample index of the hitsound changes instead, as there's no other way to play the sample
        /// (objects aren't stacked to play it, as hitsound difficulties mustn't contain objects at the same time).
        /// </summary>
        private void setSubLane(HitsoundColumn column, HitsoundLane lane, int customIndex, bool active, List<HitsoundTarget> targets, HashSet<HitObject> changed)
        {
            if (!active)
            {
                var playing = targets.Where(t => HitsoundSamples.Has(t.Samples, lane)).ToList();

                if (lane.IsAddition)
                {
                    foreach (var target in playing)
                        setSamples(target, HitsoundSamples.WithoutAddition(target.Samples, lane.Sample), changed);

                    if (HitsoundDifficultyMode.Value)
                        removeRedundantObjects(column, changed);
                }
                else if (HitsoundDifficultyMode.Value)
                {
                    // only the objects which play the hitnormal from this custom sample set are deleted.
                    foreach (var target in playing)
                        deleteTarget(target, changed);
                }

                return;
            }

            if (targets.Any(t => HitsoundSamples.Has(t.Samples, lane)))
                return;

            var sameIndex = targets.Where(t => HitsoundSamples.GetCustomIndex(t.Samples) == customIndex).ToList();

            if (!lane.IsAddition)
            {
                if (sameIndex.Count > 0)
                {
                    // the hitnormal of the object is wanted now, so it isn't deleted with its additions anymore.
                    additionCarriers.Remove(sameIndex[0].HitObject);
                    setSamples(sameIndex[0], HitsoundSamples.WithNormalBank(sameIndex[0].Samples, lane.Bank, false), changed);
                }
                else
                {
                    foreach (var target in targets)
                        setSamples(target, HitsoundSamples.WithCustomIndex(HitsoundSamples.WithNormalBank(target.Samples, lane.Bank, false), customIndex), changed);
                }

                return;
            }

            // all additions of a target use the same bank, so the addition can only be added to a target with additions of the same bank, or none.
            var free = sameIndex.FirstOrDefault(t => HitsoundSamples.GetAdditionBank(t.Samples) == lane.Bank)
                       ?? sameIndex.FirstOrDefault(t => HitsoundSamples.GetAdditionBank(t.Samples) == null);

            if (free != null)
            {
                setSamples(free, HitsoundSamples.WithAddition(free.Samples, lane.Sample, lane.Bank), changed);
                return;
            }

            // the target which plays the sample from another custom sample set switches to this one, otherwise the target which can play the addition.
            var switched = targets.FirstOrDefault(t => HitsoundSamples.Has(t.Samples, lane.Parent))
                           ?? targets.FirstOrDefault(t => HitsoundSamples.GetAdditionBank(t.Samples) == lane.Bank)
                           ?? targets.FirstOrDefault(t => HitsoundSamples.GetAdditionBank(t.Samples) == null)
                           ?? targets[0];

            setSamples(switched, HitsoundSamples.WithCustomIndex(HitsoundSamples.WithAddition(switched.Samples, lane.Sample, lane.Bank), customIndex), changed);
        }

        #endregion

        #region Selection operations

        /// <summary>
        /// Toggles an addition: if all columns have it, it is removed from all of them, otherwise it is added to the columns which don't have it,
        /// using the bank of their other additions (or of their hitnormal).
        /// </summary>
        public void ToggleAddition(IReadOnlyList<HitsoundColumn> columns, string name)
        {
            if (columns.Count == 0)
                return;

            bool allHave = columns.All(c => c.Targets.Any(t => t.Samples.Any(s => s.Name == name)));

            perform(changed =>
            {
                foreach (var column in columns)
                {
                    var targets = editableTargets(column);

                    if (targets.Count == 0)
                        continue;

                    if (allHave)
                    {
                        foreach (var target in targets)
                            setSamples(target, HitsoundSamples.WithoutAddition(target.Samples, name), changed);

                        if (HitsoundDifficultyMode.Value)
                            removeRedundantObjects(column, changed);
                    }
                    else if (targets.All(t => t.Samples.All(s => s.Name != name)))
                    {
                        var target = targets.FirstOrDefault(t => HitsoundSamples.GetAdditionBank(t.Samples) != null) ?? targets[0];
                        string bank = HitsoundSamples.GetAdditionBank(target.Samples) ?? HitsoundSamples.GetNormalBank(target.Samples) ?? HitSampleInfo.BANK_NORMAL;

                        setSamples(target, HitsoundSamples.WithAddition(target.Samples, name, bank), changed);
                    }
                }
            });
        }

        /// <summary>
        /// The samples of the selection which are moved by <see cref="MoveSelectionLanes"/>: the samples which the selected hitsounds play
        /// in the lanes they are selected in (or in all visible lanes if they are selected as a whole).
        /// </summary>
        private List<(HitsoundColumn column, List<int> laneIndices)> getMovableSelection()
        {
            var lanes = VisibleLanes;
            var result = new List<(HitsoundColumn, List<int>)>();

            foreach (var column in SelectedColumns)
            {
                var selected = GetSelectedLanes(column.Key);
                var indices = new List<int>();

                for (int i = 0; i < lanes.Count; i++)
                {
                    var lane = lanes[i];

                    if ((selected != null && !selected.Contains(lane)) || !column.Has(lane))
                        continue;

                    // a sub-lane is moved along with its lane.
                    if (lane.IsSubLane && (selected == null || selected.Contains(lane.Parent)) && column.Has(lane.Parent))
                        continue;

                    indices.Add(i);
                }

                if (indices.Count > 0)
                    result.Add((column, indices));
            }

            return result;
        }

        /// <summary>
        /// The lanes the selected samples would be moved to by a number of visible lanes, or <c>null</c> if they can't be moved that way.
        /// </summary>
        /// <param name="offset">The number of visible lanes.</param>
        /// <param name="allowNormalToAddition">
        /// Whether hitnormals may be moved into addition lanes. As every hitsound plays a hitnormal, this adds the addition but the hitnormal stays.
        /// When additions are moved as well, the hitnormals stay where they are instead, as moving would otherwise look like copying.
        /// </param>
        private List<(HitsoundColumn column, List<(HitsoundLane from, HitsoundLane to)> moves)>? getOffsetMoves(int offset, bool allowNormalToAddition)
        {
            if (offset == 0)
                return null;

            var lanes = VisibleLanes;
            var items = getMovableSelection();

            if (items.Count == 0 || items.Any(item => item.laneIndices.Any(i => i + offset < 0 || i + offset >= lanes.Count)))
                return null;

            var result = items.Select(item => (item.column, moves: item.laneIndices.Select(i => (from: lanes[i], to: lanes[i + offset])).ToList())).ToList();

            if (result.Any(r => r.moves.Any(m => !m.from.IsAddition && m.to.IsAddition)))
            {
                if (!allowNormalToAddition)
                    return null;

                bool onlyHitnormals = result.All(r => r.moves.All(m => !m.from.IsAddition));

                if (!onlyHitnormals)
                {
                    foreach (var r in result)
                        r.moves.RemoveAll(m => !m.from.IsAddition && m.to.IsAddition);

                    result.RemoveAll(r => r.moves.Count == 0);

                    if (result.Count == 0)
                        return null;
                }
            }

            return result;
        }

        /// <summary>
        /// The lanes the selected samples would be moved to when moving them to another bank, keeping their sample (e.g. from normal-whistle to soft-whistle),
        /// or <c>null</c> if there is no such bank for some of them.
        /// </summary>
        /// <param name="direction">The number of banks to move by: negative for previous banks, positive for next ones (in the order the banks are displayed).</param>
        private List<(HitsoundColumn column, List<(HitsoundLane from, HitsoundLane to)> moves)>? getBankMoves(int direction)
        {
            if (direction == 0)
                return null;

            var lanes = VisibleLanes;
            var items = getMovableSelection();

            if (items.Count == 0)
                return null;

            var result = new List<(HitsoundColumn, List<(HitsoundLane, HitsoundLane)>)>();

            foreach (var (column, laneIndices) in items)
            {
                var moves = new List<(HitsoundLane, HitsoundLane)>();

                foreach (int i in laneIndices)
                {
                    var lane = lanes[i];
                    int bankIndex = Array.IndexOf(HitsoundLane.BANKS, lane.Bank) + direction;

                    if (bankIndex < 0 || bankIndex >= HitsoundLane.BANKS.Length)
                        return null;

                    // the lane may be hidden (e.g. as unused), in which case it appears once the sample is moved there.
                    moves.Add((lane, lane with { Bank = HitsoundLane.BANKS[bankIndex] }));
                }

                result.Add((column, moves));
            }

            return result;
        }

        /// <summary>
        /// Whether the samples of the selection can be moved by a number of visible lanes (see <see cref="MoveSelectionLanes"/>).
        /// </summary>
        public bool CanMoveSelectionLanes(int offset, bool allowNormalToAddition = false) => getOffsetMoves(offset, allowNormalToAddition) != null;

        /// <summary>
        /// The offset closest to <paramref name="wanted"/> (but not further from 0) by which the samples of the selection can be moved, or 0 if there is none.
        /// </summary>
        public int ClampSelectionLaneOffset(int wanted, bool allowNormalToAddition = false)
        {
            for (int offset = wanted; offset != 0; offset -= Math.Sign(offset))
            {
                if (CanMoveSelectionLanes(offset, allowNormalToAddition))
                    return offset;
            }

            return 0;
        }

        /// <summary>
        /// Moves the samples of the selection by a number of visible lanes, e.g. from normal-whistle to normal-finish.
        /// Hitnormals change their bank (and custom sample index for sub-lanes), additions are removed from their lane and added in the new one.
        /// Additions moved into a hitnormal lane set the bank of the hitnormal. Hitnormals can only be moved into an addition lane on their own
        /// (<paramref name="allowNormalToAddition"/>), which adds the addition but keeps the hitnormal, as every hitsound plays one
        /// (in <see cref="HitsoundDifficultyMode"/>, its object only carries the addition from then on).
        /// The selection follows the moved samples.
        /// </summary>
        /// <returns>Whether the samples have been moved.</returns>
        public bool MoveSelectionLanes(int offset, bool allowNormalToAddition = false)
        {
            var moves = getOffsetMoves(offset, allowNormalToAddition);
            return moves != null && moveSelection(moves);
        }

        /// <summary>
        /// The number of banks closest to <paramref name="wanted"/> (but not further from 0) by which the samples of the selection can be moved, or 0 if there is none.
        /// </summary>
        public int ClampSelectionBankDelta(int wanted)
        {
            for (int delta = wanted; delta != 0; delta -= Math.Sign(delta))
            {
                if (getBankMoves(delta) != null)
                    return delta;
            }

            return 0;
        }

        /// <summary>
        /// Moves the samples of the selection to another bank, keeping their sample (e.g. from normal-whistle to soft-whistle).
        /// </summary>
        /// <param name="direction">The number of banks to move by: negative for previous banks, positive for next ones (in the order the banks are displayed).</param>
        /// <returns>Whether the samples have been moved.</returns>
        public bool MoveSelectionBanks(int direction)
        {
            var moves = getBankMoves(direction);
            return moves != null && moveSelection(moves);
        }

        /// <summary>
        /// The lane which a sample in a visible lane is moved to by <see cref="MoveSelectionBanks"/>, or <c>null</c> if there is no such bank.
        /// </summary>
        public HitsoundLane? GetBankMovedLane(int laneIndex, int direction)
        {
            if (laneIndex < 0 || laneIndex >= VisibleLanes.Count)
                return null;

            var lane = VisibleLanes[laneIndex];
            int bankIndex = Array.IndexOf(HitsoundLane.BANKS, lane.Bank) + direction;

            return bankIndex >= 0 && bankIndex < HitsoundLane.BANKS.Length ? lane with { Bank = HitsoundLane.BANKS[bankIndex] } : null;
        }

        private bool moveSelection(List<(HitsoundColumn column, List<(HitsoundLane from, HitsoundLane to)> moves)> items)
        {
            perform(changed =>
            {
                foreach (var (column, moves) in items)
                {
                    var targets = editableTargets(column);

                    // all moves are decided on the samples before moving, such that samples aren't moved twice (e.g. from normal to soft, and then from soft to drum).
                    var original = targets.ToDictionary(t => t, t => t.Samples.ToList());

                    // the additions which are added in their new lanes once all samples have been moved out of their old lanes.
                    var added = new List<HitsoundLane>();

                    foreach (var target in targets)
                    {
                        var samples = target.Samples.ToList();

                        var normalMove = moves.FirstOrDefault(m => !m.from.IsAddition && HitsoundSamples.Has(original[target], m.from));

                        if (normalMove != default)
                        {
                            if (normalMove.to.IsAddition)
                            {
                                // every hitsound plays a hitnormal, so moving one into an addition lane adds the addition, and the hitnormal stays.
                                // in hitsound difficulty mode, the hitnormal isn't wanted anymore, so the object is deleted once it doesn't play any additions.
                                if (HitsoundDifficultyMode.Value)
                                    additionCarriers.Add(target.HitObject);

                                added.Add(normalMove.to);
                            }
                            else
                                samples = withNormal(samples, normalMove.to);
                        }

                        foreach (var (from, to) in moves.Where(m => m.from.IsAddition && HitsoundSamples.Has(original[target], m.from)))
                        {
                            samples = HitsoundSamples.WithoutAddition(samples, from.Sample);

                            if (to.IsAddition)
                                added.Add(to);
                            else
                            {
                                // the addition becomes the hitnormal of the object, which is wanted now.
                                samples = withNormal(samples, to);
                                additionCarriers.Remove(target.HitObject);
                            }
                        }

                        setSamples(target, samples, changed);
                    }

                    foreach (var lane in added.Distinct())
                        setLane(column, lane, true, changed);

                    if (HitsoundDifficultyMode.Value)
                        removeRedundantObjects(column, changed);
                }
            });

            // the selection follows the samples into their new lanes.
            foreach (var (column, moves) in items)
            {
                if (!selectedLanes.TryGetValue(column.Key, out var selected))
                    continue;

                var moved = moves.ToDictionary(m => m.from, m => m.to);
                selectedLanes[column.Key] = selected.Select(l => moved.GetValueOrDefault(l, l)).ToHashSet();
            }

            SelectionChanged?.Invoke();
            return true;

            static List<HitSampleInfo> withNormal(List<HitSampleInfo> samples, HitsoundLane lane)
            {
                samples = HitsoundSamples.WithNormalBank(samples, lane.Bank, false);
                return lane.CustomIndex is int customIndex ? HitsoundSamples.WithCustomIndex(samples, customIndex) : samples;
            }
        }

        /// <summary>
        /// The visible lane which is <paramref name="offset"/> lanes from the visible lane at <paramref name="laneIndex"/>, or <c>null</c> if there is none.
        /// </summary>
        public HitsoundLane? GetMovedLane(int laneIndex, int offset)
        {
            int target = laneIndex + offset;
            return target >= 0 && target < VisibleLanes.Count ? VisibleLanes[target] : null;
        }

        public void SetNormalBank(IReadOnlyList<HitsoundColumn> columns, string bank)
            => performOnTargets(columns, samples => HitsoundSamples.WithNormalBank(samples, bank, false));

        /// <param name="columns">The columns.</param>
        /// <param name="bank">The bank, or <c>null</c> to make the additions use the bank of the hitnormal.</param>
        public void SetAdditionBank(IReadOnlyList<HitsoundColumn> columns, string? bank)
            => performOnTargets(columns, samples => HitsoundSamples.WithAdditionBank(samples, bank));

        public void SetVolume(IReadOnlyList<HitsoundColumn> columns, int volume)
            => SetVolumes(columns.Select(c => (c, volume)).ToList());

        public void SetVolumes(IReadOnlyList<(HitsoundColumn column, int volume)> volumes)
        {
            if (volumes.Count == 0)
                return;

            perform(changed =>
            {
                foreach (var (column, volume) in volumes)
                {
                    int clamped = Math.Clamp(volume, 5, 100);

                    foreach (var target in column.Targets)
                        setSamples(target, HitsoundSamples.WithVolume(target.Samples, clamped), changed);
                }
            });
        }

        /// <summary>
        /// Changes the volumes of the columns linearly (by time) from the volume of the first column to the volume of the last.
        /// </summary>
        public void RampVolume(IReadOnlyList<HitsoundColumn> columns)
        {
            if (columns.Count < 3)
                return;

            var first = columns[0];
            var last = columns[^1];
            double duration = last.Time - first.Time;

            if (duration <= 0)
                return;

            SetVolumes(columns.Select(c => (c, (int)Math.Round(first.Volume + (last.Volume - first.Volume) * (c.Time - first.Time) / duration))).ToList());
        }

        public void SetCustomIndex(IReadOnlyList<HitsoundColumn> columns, int index)
            => performOnTargets(columns, samples => HitsoundSamples.WithCustomIndex(samples, index));

        /// <summary>
        /// Removes all additions of the columns.
        /// </summary>
        public void ClearAdditions(IReadOnlyList<HitsoundColumn> columns)
        {
            if (columns.Count == 0)
                return;

            perform(changed =>
            {
                foreach (var column in columns)
                {
                    foreach (var target in editableTargets(column))
                        setSamples(target, HitsoundSamples.WithoutAdditions(target.Samples), changed);

                    if (HitsoundDifficultyMode.Value)
                        removeRedundantObjects(column, changed);
                }
            });
        }

        /// <summary>
        /// Deletes the objects of the columns. Parts of objects with duration (e.g. slider ends) can't be deleted, and lose their additions instead.
        /// </summary>
        public void DeleteColumns(IReadOnlyList<HitsoundColumn> columns)
        {
            if (columns.Count == 0)
                return;

            perform(changed =>
            {
                foreach (var column in columns)
                    deleteColumn(column, changed);
            });
        }

        #endregion

        #region Patterns

        /// <summary>
        /// Applies hitsounds to the columns at their times. In <see cref="HitsoundDifficultyMode"/>, an object is created where there is none.
        /// </summary>
        /// <param name="states">The hitsounds, ordered by time.</param>
        /// <returns>The number of hitsounds which couldn't be applied, as there were no (or not enough) objects at their time.</returns>
        public int ApplyStates(IReadOnlyList<HitsoundColumnState> states)
        {
            if (states.Count == 0)
                return 0;

            int dropped = 0;

            perform(changed =>
            {
                if (HitsoundDifficultyMode.Value)
                    createMissingObjects(states);

                var result = HitsoundCopier.Copy(states, Array.Empty<HitsoundBodyState>(), HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId), new HitsoundCopyOptions
                {
                    Leniency = PASTE_LENIENCY,
                    CopySliderBodies = false,
                    OverwriteUnmatched = false,
                });

                changed.UnionWith(result.ChangedHitObjects);

                // hitsounds without any column at their time don't count as copied by the copier, so are counted here.
                var map = HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);
                dropped = result.DroppedHitsounds + states.Count(s => map.FindClosestColumn(s.Time, PASTE_LENIENCY) == null);
            });

            return dropped;
        }

        /// <summary>
        /// Pastes copied hitsounds. Hitsounds which were only selected in some lanes when copying only add the additions of these lanes,
        /// keeping the hitnormals of the hitsounds they are pasted onto. Where there are no hitsounds, they are created with the copied hitnormals.
        /// </summary>
        /// <returns>The number of hitsounds which couldn't be pasted, as there were not enough (or no) hit objects at their time.</returns>
        public int Paste(IReadOnlyList<HitsoundClipboardEntry> entries)
        {
            var replaced = new List<HitsoundColumnState>();
            var merged = new List<(HitsoundColumn column, IReadOnlyCollection<HitsoundLane> lanes)>();

            foreach (var entry in entries)
            {
                if (entry.Lanes == null)
                {
                    replaced.Add(entry.State);
                    continue;
                }

                if (Map.FindClosestColumn(entry.State.Time, PASTE_LENIENCY) is HitsoundColumn column)
                    // the selected lanes also include lanes in which the copied hitsound had no sample, which must not be turned on.
                    merged.Add((column, entry.Lanes.Where(l => l.IsAddition && hasAddition(entry.State, l)).ToList()));
                else
                    replaced.Add(entry.State with { AdditionGroups = filterAdditions(entry.State.AdditionGroups, entry.Lanes) });
            }

            editorBeatmap.BeginChange();

            if (merged.Count > 0)
            {
                perform(changed =>
                {
                    foreach (var (column, lanes) in merged)
                    {
                        foreach (var lane in lanes)
                            setLane(column, lane, true, changed);
                    }
                });
            }

            int dropped = ApplyStates(replaced);

            editorBeatmap.EndChange();

            return dropped;

            static bool hasAddition(HitsoundColumnState state, HitsoundLane lane)
                => (lane.CustomIndex == null || lane.CustomIndex == state.CustomIndex)
                   && state.AdditionGroups.Any(g => g.Bank == lane.Bank && g.Names.Contains(lane.Sample));

            static IReadOnlyList<HitsoundAdditionGroup> filterAdditions(IReadOnlyList<HitsoundAdditionGroup> groups, IReadOnlyCollection<HitsoundLane> lanes)
                => groups.Select(g => g with { Names = g.Names.Where(n => lanes.Any(l => l.Bank == g.Bank && l.Sample == n)).ToList() })
                         .Where(g => g.Names.Count > 0)
                         .ToList();
        }

        /// <summary>
        /// Copies hitsounds onto the hit objects of this beatmap.
        /// </summary>
        public HitsoundCopyResult Import(IReadOnlyList<HitsoundColumnState> source, IReadOnlyList<HitsoundBodyState> sourceBodies, HitsoundCopyOptions options)
        {
            HitsoundCopyResult result = null!;

            perform(changed =>
            {
                result = HitsoundCopier.Copy(source, sourceBodies, HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId), options);
                changed.UnionWith(result.ChangedHitObjects);
            });

            return result;
        }

        #endregion

        #region Hitsound patterns

        [Resolved(canBeNull: true)]
        private HitsoundPatterns? patterns { get; set; }

        private static readonly HitsoundCopyOptions pattern_copy_options = new HitsoundCopyOptions
        {
            Leniency = PASTE_LENIENCY,
            CopySliderBodies = false,
            PreserveMutedVolumes = false,
            OverwriteUnmatched = true,
        };

        /// <summary>
        /// The hitsounds at a place where a pattern is used, with times relative to its start.
        /// </summary>
        public IReadOnlyList<HitsoundColumnState> GetPatternStates(HitsoundPatternOccurrence occurrence)
            => getPatternStates(Map, occurrence.StartTime, occurrence.Pattern.Length);

        /// <summary>
        /// The hitsounds of a pattern: the ones at the first place where it is used, or the last known ones if it isn't used anywhere.
        /// </summary>
        public IReadOnlyList<HitsoundColumnState> GetPatternStates(HitsoundPattern pattern)
        {
            var occurrence = patterns?.GetOccurrences(pattern).FirstOrDefault();
            return occurrence != null ? GetPatternStates(occurrence) : pattern.States;
        }

        /// <summary>
        /// The columns at a place where a pattern is used.
        /// </summary>
        public IReadOnlyList<HitsoundColumn> GetPatternColumns(HitsoundPatternOccurrence occurrence)
            => getColumnsInRange(Map, occurrence.StartTime, occurrence.Pattern.Length).ToList();

        /// <summary>
        /// The length of a pattern created from hitsounds: the whole beats from the first hitsound to after the last one.
        /// </summary>
        public double GetPatternLength(IReadOnlyList<HitsoundColumn> columns)
        {
            double start = columns[0].Time;
            double span = columns[^1].Time - start;
            double beatLength = editorBeatmap.ControlPointInfo.TimingPointAt(start).BeatLength;

            return (Math.Floor((span + PASTE_LENIENCY) / beatLength) + 1) * beatLength;
        }

        /// <summary>
        /// Creates a pattern from hitsounds, and uses it at their place.
        /// The pattern covers whole beats from the first hitsound on (see <see cref="GetPatternLength"/>), including hitsounds which aren't given.
        /// If the hitsounds are inside a place where another pattern is used, the new pattern becomes a sub-pattern of it.
        /// </summary>
        /// <returns>The pattern, or <c>null</c> if the hitsounds partly overlap a place where another pattern is used.</returns>
        public HitsoundPattern? CreatePattern(IReadOnlyList<HitsoundColumn> columns)
        {
            if (patterns == null || columns.Count == 0)
                return null;

            double start = columns[0].Time;
            double length = GetPatternLength(columns);

            editorBeatmap.BeginChange();

            try
            {
                return createPattern(start, length);
            }
            finally
            {
                editorBeatmap.EndChange();
            }
        }

        private HitsoundPattern? createPattern(double start, double length)
        {
            Debug.Assert(patterns != null);

            if (!patterns.Overlaps(start, start + length))
            {
                var pattern = patterns.Create(length, getPatternStates(Map, start, length));
                patterns.AddInstance(pattern, start);
                return pattern;
            }

            if (patterns.FindContainingOccurrence(start, start + length) is not HitsoundPatternOccurrence parent
                || !patterns.FitsSubPattern(parent.Pattern, start - parent.StartTime, length))
                return null;

            // the hitsounds are the same at all places of the containing pattern, so the sub-pattern is used there already.
            var subPattern = patterns.Create(length, getPatternStates(Map, start, length));
            patterns.AddSubPattern(parent.Pattern, subPattern, start - parent.StartTime);

            return subPattern;
        }

        /// <summary>
        /// Uses a pattern at a time, replacing the hitsounds there.
        /// If the time is inside a place where another pattern is used, the pattern becomes a sub-pattern of it, and is placed in all of its places.
        /// </summary>
        /// <returns>
        /// The number of hitsounds of the pattern which couldn't be placed, as there were not enough (or no) hit objects at their time,
        /// or <c>null</c> if the pattern would partly overlap a place where a pattern is already used, or be placed inside itself.
        /// </returns>
        public int? PlacePattern(HitsoundPattern pattern, double time)
        {
            if (patterns == null)
                return null;

            var states = GetPatternStates(pattern);
            HitsoundPatternOccurrence? parent = null;

            if (patterns.Overlaps(time, time + pattern.Length))
            {
                parent = patterns.FindContainingOccurrence(time, time + pattern.Length);

                if (parent == null || !patterns.CanPlaceSubPattern(parent.Pattern, pattern, time - parent.StartTime))
                    return null;
            }

            int dropped = 0;

            // a single transaction including the change of the patterns, such that the changed hitsounds aren't copied to other places (see HitsoundPatternSync).
            editorBeatmap.BeginChange();

            try
            {
                perform(changed =>
                {
                    if (parent == null)
                    {
                        dropped = applyPattern(states, time, pattern.Length, changed);
                        return;
                    }

                    // as a sub-pattern, the pattern is used in all places of the containing pattern.
                    double offset = time - parent.StartTime;

                    foreach (var place in patterns.GetOccurrences(parent.Pattern).ToList())
                    {
                        int placeDropped = applyPattern(states, place.StartTime + offset, pattern.Length, changed);

                        if (place == parent)
                            dropped = placeDropped;
                    }
                });

                if (parent == null)
                    patterns.AddInstance(pattern, time);
                else
                    patterns.AddSubPattern(parent.Pattern, pattern, time - parent.StartTime);
            }
            finally
            {
                editorBeatmap.EndChange();
            }

            return dropped;
        }

        /// <summary>
        /// Makes a place use a copy of its pattern, such that its hitsounds can be changed without changing the other places (see <see cref="HitsoundPatterns.MakeUnique"/>).
        /// </summary>
        public HitsoundPattern? MakePatternUnique(HitsoundPatternOccurrence occurrence)
        {
            editorBeatmap.BeginChange();

            try
            {
                return patterns?.MakeUnique(occurrence, GetPatternStates(occurrence));
            }
            finally
            {
                editorBeatmap.EndChange();
            }
        }

        /// <summary>
        /// Moves a place where a pattern is used, along with its hitsounds, to another time.
        /// The hitsounds at the old place are removed (in <see cref="HitsoundDifficultyMode"/> by deleting the objects, otherwise by removing their additions).
        /// For a sub-pattern, it is moved in all places where the pattern containing it is used.
        /// </summary>
        /// <returns>Whether the place has been moved, which isn't possible if it would overlap other places.</returns>
        public bool MovePatternOccurrence(HitsoundPatternOccurrence occurrence, double startTime)
        {
            if (patterns == null || Math.Abs(startTime - occurrence.StartTime) < 1 || !patterns.CanMove(occurrence, startTime))
                return false;

            var states = GetPatternStates(occurrence);
            var places = getPlacesOf(occurrence);
            double delta = startTime - occurrence.StartTime;
            double length = occurrence.Pattern.Length;

            // a single transaction including the change of the patterns, such that the changed hitsounds aren't copied to other places (see HitsoundPatternSync).
            editorBeatmap.BeginChange();

            try
            {
                perform(changed =>
                {
                    // all old places are cleared first, as the new places may overlap them.
                    foreach (double place in places)
                        clearRange(place, length, changed);

                    foreach (double place in places)
                        applyPattern(states, place + delta, length, changed);
                });

                patterns.Move(occurrence, startTime);
            }
            finally
            {
                editorBeatmap.EndChange();
            }

            return true;
        }

        /// <summary>
        /// Stops using a pattern at a place, and removes its hitsounds there (in <see cref="HitsoundDifficultyMode"/> by deleting the objects, otherwise by removing their additions).
        /// For a sub-pattern, it is removed from all places where the pattern containing it is used.
        /// </summary>
        /// <param name="occurrence">The place.</param>
        /// <param name="keepHitsounds">Whether to keep the hitsounds, only unlinking them from the pattern.</param>
        public void RemovePatternOccurrence(HitsoundPatternOccurrence occurrence, bool keepHitsounds)
        {
            if (patterns == null)
                return;

            // a single transaction including the change of the patterns, such that the removed hitsounds aren't copied to other places (see HitsoundPatternSync).
            editorBeatmap.BeginChange();

            try
            {
                if (!keepHitsounds)
                {
                    var places = getPlacesOf(occurrence);

                    perform(changed =>
                    {
                        foreach (double place in places)
                            clearRange(place, occurrence.Pattern.Length, changed);
                    });
                }

                patterns.Remove(occurrence);
            }
            finally
            {
                editorBeatmap.EndChange();
            }
        }

        /// <summary>
        /// The start times of all places which change along with a place: the place itself, or for a sub-pattern, its places in all places of the pattern containing it.
        /// </summary>
        private List<double> getPlacesOf(HitsoundPatternOccurrence occurrence)
        {
            if (patterns == null || occurrence.Parent == null || occurrence.SubPattern == null)
                return new List<double> { occurrence.StartTime };

            return patterns.GetOccurrences(occurrence.Parent).Select(p => p.StartTime + occurrence.SubPattern.Offset).ToList();
        }

        /// <summary>
        /// Removes the hitsounds of a time range: in <see cref="HitsoundDifficultyMode"/> by deleting the objects, otherwise by removing their additions.
        /// </summary>
        private void clearRange(double startTime, double length, HashSet<HitObject> changed)
        {
            var current = HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);

            foreach (var column in getColumnsInRange(current, startTime, length).ToList())
            {
                if (HitsoundDifficultyMode.Value)
                    deleteColumn(column, changed);
                else
                {
                    foreach (var target in editableTargets(column))
                        setSamples(target, HitsoundSamples.WithoutAdditions(target.Samples), changed);
                }
            }
        }

        /// <summary>
        /// The current hitsounds of all places where patterns are used, to find the places which a change changes (see <see cref="SyncPatterns"/>).
        /// </summary>
        public List<(HitsoundPatternOccurrence occurrence, IReadOnlyList<HitsoundColumnState> states)>? CapturePatternStates()
        {
            if (patterns == null || patterns.Occurrences.Count == 0)
                return null;

            var current = HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);

            return patterns.Occurrences.Select(o => (o, getPatternStates(current, o.StartTime, o.Pattern.Length))).ToList();
        }

        /// <summary>
        /// Copies the hitsounds of places where a pattern is used which have been changed (anywhere in the editor) to the other places where it is used.
        /// Sub-patterns are copied first, such that the patterns containing them are already the same everywhere when they are copied.
        /// If several places of the same pattern have been changed, the innermost (then the first) one wins.
        /// </summary>
        /// <param name="before">The hitsounds of the places before the change.</param>
        /// <param name="changed">The objects which have been changed.</param>
        public void SyncPatterns(List<(HitsoundPatternOccurrence occurrence, IReadOnlyList<HitsoundColumnState> states)> before)
            => perform(changed => syncPatterns(before, changed));

        private void syncPatterns(List<(HitsoundPatternOccurrence occurrence, IReadOnlyList<HitsoundColumnState> states)> before, HashSet<HitObject> changed)
        {
            Debug.Assert(patterns != null);

            var current = HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);

            var changedPlaces = before.Where(b => !HitsoundPatternStates.AreEqual(getPatternStates(current, b.occurrence.StartTime, b.occurrence.Pattern.Length), b.states))
                                      .Select(b => b.occurrence)
                                      .OrderByDescending(o => o.Depth)
                                      .ToList();

            var synced = new HashSet<Guid>();

            foreach (var place in changedPlaces)
            {
                if (!synced.Add(place.Pattern.ID))
                    continue;

                // other places may have been changed by copying sub-patterns before.
                var states = getPatternStates(HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId), place.StartTime, place.Pattern.Length);
                patterns.SetStates(place.Pattern, states);

                foreach (var other in patterns.GetOccurrences(place.Pattern).Where(o => o != place).ToList())
                    applyPattern(states, other.StartTime, place.Pattern.Length, changed);
            }
        }

        /// <summary>
        /// Replaces the hitsounds of a time range by the hitsounds of a pattern.
        /// In <see cref="HitsoundDifficultyMode"/>, objects are created and deleted as needed.
        /// </summary>
        /// <param name="states">The hitsounds of the pattern, with times relative to its start.</param>
        /// <param name="startTime">The start of the time range.</param>
        /// <param name="length">The length of the time range.</param>
        /// <param name="changed">The objects which have been changed.</param>
        /// <returns>The number of hitsounds which couldn't be applied, as there were not enough (or no) hit objects at their time.</returns>
        private int applyPattern(IReadOnlyList<HitsoundColumnState> states, double startTime, double length, HashSet<HitObject> changed)
        {
            var absolute = states.Select(s => s.WithTime(s.Time + startTime)).ToList();

            if (HitsoundDifficultyMode.Value)
            {
                var current = HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);

                foreach (var column in getColumnsInRange(current, startTime, length).ToList())
                {
                    if (!absolute.Any(s => Math.Abs(s.Time - column.Time) <= PASTE_LENIENCY))
                        deleteColumn(column, changed);
                }

                createMissingObjects(absolute);
            }

            var map = HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);
            var columns = getColumnsInRange(map, startTime, length).ToList();
            var result = HitsoundCopier.Copy(absolute, Array.Empty<HitsoundBodyState>(), columns, Array.Empty<HitsoundBody>(), pattern_copy_options);

            changed.UnionWith(result.ChangedHitObjects);

            // the copier only copies a summary of the hitsounds, which loses e.g. additions played from another custom sample set than the hitnormal.
            // where the exact samples are known, they are copied as well.
            foreach (var column in columns)
            {
                if (absolute.FirstOrDefault(s => Math.Abs(s.Time - column.Time) <= PASTE_LENIENCY) is not HitsoundColumnState { TargetSamples: { Count: > 0 } targetSamples })
                    continue;

                var targets = editableTargets(column);

                // the exact samples can't be reproduced by fewer targets, in which case the copied summary (which counts the hitsounds it had to drop) stays.
                if (targetSamples.Count > targets.Count)
                    continue;

                for (int i = 0; i < targets.Count; i++)
                {
                    if (i < targetSamples.Count)
                        setSamples(targets[i], targetSamples[i].ToList(), changed);
                    else if (HitsoundDifficultyMode.Value && targets[i].Kind == HitsoundTargetKind.Object)
                        deleteTarget(targets[i], changed);
                    else
                        setSamples(targets[i], HitsoundSamples.WithoutAdditions(targetSamples[0]), changed);
                }
            }

            // hitsounds without any column at their time don't count as copied by the copier, so are counted here.
            return result.DroppedHitsounds + absolute.Count(s => map.FindClosestColumn(s.Time, PASTE_LENIENCY) == null);
        }

        private static IReadOnlyList<HitsoundColumnState> getPatternStates(HitsoundMap map, double startTime, double length)
            => getColumnsInRange(map, startTime, length).Select(c => c.GetState().WithTime(c.Time - startTime)).ToList();

        /// <summary>
        /// The columns from <paramref name="startTime"/> to before <paramref name="startTime"/> + <paramref name="length"/>.
        /// </summary>
        private static IEnumerable<HitsoundColumn> getColumnsInRange(HitsoundMap map, double startTime, double length)
            => map.GetColumnsInRange(startTime - PASTE_LENIENCY, startTime + length - PASTE_LENIENCY)
                  .Where(c => c.Time < startTime + length - PASTE_LENIENCY);

        #endregion

        #region Helpers

        private void perform(Action<HashSet<HitObject>> action)
        {
            var changed = new HashSet<HitObject>();

            editorBeatmap.BeginChange();

            try
            {
                action(changed);

                foreach (var h in changed)
                {
                    // objects may have been removed by the action.
                    if (editorBeatmap.HitObjects.Contains(h))
                        editorBeatmap.Update(h);
                }
            }
            finally
            {
                editorBeatmap.EndChange();
                invalidateMap();
            }
        }

        private void performOnTargets(IReadOnlyList<HitsoundColumn> columns, Func<List<HitSampleInfo>, List<HitSampleInfo>> modify)
        {
            if (columns.Count == 0)
                return;

            perform(changed =>
            {
                foreach (var column in columns)
                {
                    foreach (var target in editableTargets(column))
                        setSamples(target, modify(target.Samples.ToList()), changed);
                }
            });
        }

        /// <summary>
        /// The targets whose hitsounds can be edited, which excludes targets playing sample files directly (keysounds).
        /// </summary>
        private static List<HitsoundTarget> editableTargets(HitsoundColumn column)
            => column.Targets.Where(t => !t.Samples.Any(HitsoundSamples.IsFileSample)).ToList();

        private static void setSamples(HitsoundTarget target, List<HitSampleInfo> samples, HashSet<HitObject> changed)
        {
            if (HitsoundSamples.AreEquivalent(samples, target.Samples))
                return;

            target.SetSamples(samples);
            changed.Add(target.HitObject);
        }

        private void deleteColumn(HitsoundColumn column, HashSet<HitObject> changed)
        {
            foreach (var target in column.Targets)
                deleteTarget(target, changed);
        }

        private void deleteTarget(HitsoundTarget target, HashSet<HitObject> changed)
        {
            // objects with duration which play their hitsound at the end (e.g. spinners) aren't deleted, as they are more than a hitsound.
            if (target.Kind == HitsoundTargetKind.Object || target.Kind == HitsoundTargetKind.HoldStart)
            {
                editorBeatmap.Remove(target.HitObject);
                changed.Remove(target.HitObject);
            }
            else
                setSamples(target, HitsoundSamples.WithoutAdditions(target.Samples), changed);
        }

        /// <summary>
        /// Removes stacked objects which play nothing but a hitnormal which another object of the column already plays (from the same custom sample set).
        /// </summary>
        private void removeRedundantObjects(HitsoundColumn column, HashSet<HitObject> changed)
        {
            var kept = new List<HitsoundTarget>();

            // objects carrying additions are checked last, such that the objects they were created next to are kept instead.
            foreach (var target in column.Targets.OrderBy(t => additionCarriers.Contains(t.HitObject)))
            {
                bool playsOnlyHitnormal = target.Kind == HitsoundTargetKind.Object
                                          && !target.Samples.Any(HitsoundSamples.IsAddition)
                                          && !target.Samples.Any(HitsoundSamples.IsFileSample);

                bool redundant = playsOnlyHitnormal
                                 && ((additionCarriers.Contains(target.HitObject) && kept.Count > 0)
                                     || kept.Any(k => HitsoundSamples.GetNormalBank(k.Samples) == HitsoundSamples.GetNormalBank(target.Samples)
                                                      && HitsoundSamples.GetCustomIndex(k.Samples) == HitsoundSamples.GetCustomIndex(target.Samples)));

                if (redundant)
                {
                    editorBeatmap.Remove(target.HitObject);
                    changed.Remove(target.HitObject);
                    additionCarriers.Remove(target.HitObject);
                }
                else
                    kept.Add(target);
            }
        }

        /// <summary>
        /// Creates an object at the time of each hitsound which there is no column for yet.
        /// Objects aren't stacked where a column exists, as hitsound difficulties mustn't contain objects at the same time.
        /// </summary>
        private void createMissingObjects(IEnumerable<HitsoundColumnState> states)
        {
            var current = HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);

            foreach (var state in states)
            {
                if (current.FindClosestColumn(state.Time, PASTE_LENIENCY) == null)
                    createObjects(state.Time, 1, s => s);
            }
        }

        /// <summary>
        /// Creates objects at a time and adds them to the beatmap.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <param name="count">The number of objects.</param>
        /// <param name="createSamples">Creates the samples of each object from the default samples.</param>
        /// <returns>The number of created objects.</returns>
        private int createObjects(double time, int count, Func<List<HitSampleInfo>, List<HitSampleInfo>> createSamples)
        {
            var ruleset = editorBeatmap.BeatmapInfo.Ruleset;
            var placements = new List<(double, int)>();

            if (ruleset.OnlineID == HitsoundObjectFactory.OSU_MANIA_RULESET_ID)
            {
                // objects at the same time have to be in different columns.
                var occupied = editorBeatmap.HitObjects
                                            .Where(h => h.StartTime <= time + PASTE_LENIENCY && h.GetEndTime() >= time - PASTE_LENIENCY)
                                            .OfType<IHasColumn>()
                                            .Select(h => h.Column)
                                            .ToHashSet();

                int keyCount = Math.Max(1, (int)Math.Round(editorBeatmap.Difficulty.CircleSize));

                for (int column = 0; column < keyCount && placements.Count < count; column++)
                {
                    if (!occupied.Contains(column))
                        placements.Add((time, column));
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                    placements.Add((time, i));
            }

            var hitObjects = HitsoundObjectFactory.Create(ruleset, editorBeatmap.Difficulty, placements);

            foreach (var h in hitObjects)
            {
                h.Samples = createSamples(h.Samples.ToList());
                editorBeatmap.Add(h);
            }

            return hitObjects.Count;
        }

        #endregion
    }
}
