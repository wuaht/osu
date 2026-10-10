// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Beatmaps;
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
        /// New hitsounds without a hitsound before them start with these, and volumes are reset to the volume.
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

        [BackgroundDependencyLoader]
        private void load()
        {
            HitsoundDifficultyMode.Value = IsHitsoundDifficultyName(editorBeatmap.BeatmapInfo.DifficultyName);

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

        public void SetSelection(IEnumerable<int> keys, bool additive = false)
        {
            if (!additive)
                selectedKeys.Clear();

            selectedKeys.UnionWith(keys);
            SelectionChanged?.Invoke();
        }

        public void ToggleSelection(int key)
        {
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
        /// Creates a hitsound at a time without one, which is only possible in <see cref="HitsoundDifficultyMode"/>.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <param name="lane">The lane whose sample the hitsound should play in addition to the hitnormal.</param>
        /// <returns>Whether the hitsound has been created.</returns>
        public bool CreateColumn(double time, HitsoundLane lane)
        {
            if (!HitsoundDifficultyMode.Value || Map.FindClosestColumn(time, HitsoundMap.COLUMN_MERGE_DISTANCE) != null)
                return false;

            // continue the hitsounds before, as the hitnormal bank, volume and custom index are usually the same for a while.
            int previousIndex = Map.IndexOfFirstColumnAtOrAfter(time) - 1;
            var previous = previousIndex >= 0 ? Map.Columns[previousIndex] : null;

            var baseHitsounds = previous == null ? GetBaseHitsounds() : default;

            string normalBank = lane.IsAddition ? previous?.NormalBank ?? baseHitsounds.bank : lane.Bank;
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
                else if (HitsoundDifficultyMode.Value)
                {
                    string normalBank = column.NormalBank ?? lane.Bank;

                    createObjects(column.Time, 1, samples => HitsoundSamples.WithCustomIndex(HitsoundSamples.WithVolume(
                        HitsoundSamples.WithAddition(HitsoundSamples.WithoutAdditions(HitsoundSamples.WithNormalBank(samples, normalBank, false)), lane.Sample, lane.Bank),
                        column.Volume), column.CustomIndex), additionCarrier: true);
                }
                else
                {
                    // without a free target, the additions of the first target change their bank, which is the closest possible to what was asked for.
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
        /// already uses the custom sample index. In <see cref="HitsoundDifficultyMode"/>, an object is created for it otherwise, so that the other samples stay the same.
        /// Outside of it, the custom sample index of the hitsound changes instead, as there's no other way to play the sample.
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
                else if (HitsoundDifficultyMode.Value)
                {
                    createObjects(column.Time, 1, samples => HitsoundSamples.WithCustomIndex(HitsoundSamples.WithVolume(
                        HitsoundSamples.WithoutAdditions(HitsoundSamples.WithNormalBank(samples, lane.Bank, false)), column.Volume), customIndex));
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

            if (HitsoundDifficultyMode.Value)
            {
                string normalBank = column.NormalBank ?? lane.Bank;

                createObjects(column.Time, 1, samples => HitsoundSamples.WithCustomIndex(HitsoundSamples.WithVolume(
                    HitsoundSamples.WithAddition(HitsoundSamples.WithoutAdditions(HitsoundSamples.WithNormalBank(samples, normalBank, false)), lane.Sample, lane.Bank),
                    column.Volume), customIndex), additionCarrier: true);

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
        /// Applies hitsounds to the columns at their times. In <see cref="HitsoundDifficultyMode"/>, objects are created as needed.
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
                {
                    var current = HitsoundMap.Create(editorBeatmap.HitObjects, rulesetId);

                    foreach (var state in states)
                    {
                        var column = current.FindClosestColumn(state.Time, PASTE_LENIENCY);
                        int missing = state.RequiredTargetCount - (column?.Targets.Count ?? 0);

                        if (missing > 0)
                            createObjects(column?.Time ?? state.Time, missing, s => s);
                    }
                }

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
        /// Creates objects at a time and adds them to the beatmap.
        /// </summary>
        /// <param name="time">The time.</param>
        /// <param name="count">The number of objects.</param>
        /// <param name="createSamples">Creates the samples of each object from the default samples.</param>
        /// <param name="additionCarrier">Whether the objects are only created to play additions (see <see cref="additionCarriers"/>).</param>
        /// <returns>The number of created objects.</returns>
        private int createObjects(double time, int count, Func<List<HitSampleInfo>, List<HitSampleInfo>> createSamples, bool additionCarrier = false)
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

                if (additionCarrier)
                    additionCarriers.Add(h);
            }

            return hitObjects.Count;
        }

        #endregion
    }
}
