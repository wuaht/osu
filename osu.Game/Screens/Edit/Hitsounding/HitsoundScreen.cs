// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Screens.Edit.Components;
using osu.Game.Utils;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// The hitsound editor: a timeline with a lane for each sample, on which the hitsounds of the whole beatmap are edited.
    /// Hitsounds can also be copied between difficulties, and combined into a hitsound difficulty.
    /// </summary>
    [Cached]
    public partial class HitsoundScreen : EditorScreen, IKeyBindingHandler<GlobalAction>, IKeyBindingHandler<PlatformAction>
    {
        /// <summary>
        /// The copied hitsounds, with times relative to the first hitsound.
        /// Static, such that hitsounds can be pasted into other difficulties.
        /// </summary>
        private static IReadOnlyList<HitsoundColumnState>? clipboard;

        [Cached]
        private readonly HitsoundEditor hitsoundEditor = new HitsoundEditor();

        [Cached]
        private readonly HitsoundTimeline timeline = new HitsoundTimeline();

        [Cached]
        private readonly HitsoundPlayback playback = new HitsoundPlayback();

        private HitsoundTopBar topBar = null!;
        private Container gridContainer = null!;
        private HitsoundToolbox toolbox = null!;

        [Resolved]
        private EditorClock clock { get; set; } = null!;

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        [Resolved]
        private Editor? editor { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        public HitsoundScreen()
            : base(EditorScreenMode.Hitsound)
        {
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // names of sub-lanes are shared by all difficulties of the beatmap set.
            var editorBeatmap = parent.Get<EditorBeatmap>();
            var metadata = editorBeatmap.BeatmapInfo.Metadata;

            dependencies.Cache(new HitsoundLaneNames(parent.Get<Storage>(), editorBeatmap.BeatmapInfo.BeatmapSet?.ID ?? Guid.Empty, $@"{metadata.Artist} - {metadata.Title}"));

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new EditorSkinProvidingContainer(EditorBeatmap)
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    hitsoundEditor,
                    timeline,
                    playback,
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        RowDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Absolute, HitsoundTopBar.HEIGHT),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[] { topBar = new HitsoundTopBar() },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        gridContainer = new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Child = new HitsoundGrid(),
                                        },
                                        new Container
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            RelativeSizeAxes = Axes.Y,
                                            AutoSizeAxes = Axes.X,
                                            Children = new Drawable[]
                                            {
                                                new FrostedPanelBackground(),
                                                toolbox = new HitsoundToolbox(),
                                            }
                                        },
                                    }
                                },
                            },
                        }
                    },
                }
            };
        }

        protected override void Update()
        {
            base.Update();

            // the hit objects at the top and the lanes have to end at the same position, such that their times line up.
            float reservedWidth = toolbox.ReservedWidth;

            topBar.RightWidth = reservedWidth;
            gridContainer.Padding = new MarginPadding { Right = reservedWidth };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            hitsoundEditor.SelectionChanged += () => CanCopy.Value = hitsoundEditor.SelectedKeys.Count > 0;
            CanPaste.Value = clipboard != null;
        }

        /// <summary>
        /// The columns which keyboard shortcuts and the selection tools act on: the selected columns, or the column at the current time.
        /// </summary>
        public IReadOnlyList<HitsoundColumn> GetTargetColumns() => hitsoundEditor.GetTargetColumns(clock.CurrentTime);

        #region Keyboard

        /// <summary>
        /// Whether a text box (e.g. renaming a lane) is being typed in, in which case keyboard shortcuts shouldn't act on hitsounds.
        /// </summary>
        private bool isTyping => GetContainingInputManager()?.FocusedDrawable is TextBox;

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (isTyping)
                return false;

            if (e.Action == GlobalAction.Back)
            {
                if (hitsoundEditor.SelectedKeys.Count == 0)
                    return false;

                hitsoundEditor.ClearSelection();
                return true;
            }

            if (e.Repeat)
                return false;

            switch (e.Action)
            {
                case GlobalAction.EditorToggleWhistleSound:
                    hitsoundEditor.ToggleAddition(GetTargetColumns(), HitSampleInfo.HIT_WHISTLE);
                    return true;

                case GlobalAction.EditorToggleFinishSound:
                    hitsoundEditor.ToggleAddition(GetTargetColumns(), HitSampleInfo.HIT_FINISH);
                    return true;

                case GlobalAction.EditorToggleClapSound:
                    hitsoundEditor.ToggleAddition(GetTargetColumns(), HitSampleInfo.HIT_CLAP);
                    return true;

                case GlobalAction.EditorToggleNormalNormalBank:
                    hitsoundEditor.SetNormalBank(GetTargetColumns(), HitSampleInfo.BANK_NORMAL);
                    return true;

                case GlobalAction.EditorToggleNormalSoftBank:
                    hitsoundEditor.SetNormalBank(GetTargetColumns(), HitSampleInfo.BANK_SOFT);
                    return true;

                case GlobalAction.EditorToggleNormalDrumBank:
                    hitsoundEditor.SetNormalBank(GetTargetColumns(), HitSampleInfo.BANK_DRUM);
                    return true;

                case GlobalAction.EditorToggleAdditionAutoBank:
                    hitsoundEditor.SetAdditionBank(GetTargetColumns(), null);
                    return true;

                case GlobalAction.EditorToggleAdditionNormalBank:
                    hitsoundEditor.SetAdditionBank(GetTargetColumns(), HitSampleInfo.BANK_NORMAL);
                    return true;

                case GlobalAction.EditorToggleAdditionSoftBank:
                    hitsoundEditor.SetAdditionBank(GetTargetColumns(), HitSampleInfo.BANK_SOFT);
                    return true;

                case GlobalAction.EditorToggleAdditionDrumBank:
                    hitsoundEditor.SetAdditionBank(GetTargetColumns(), HitSampleInfo.BANK_DRUM);
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
        {
            if (isTyping)
                return false;

            switch (e.Action)
            {
                case PlatformAction.SelectAll:
                    hitsoundEditor.SelectAll();
                    return true;

                case PlatformAction.Delete:
                    DeleteOrClear(GetTargetColumns());
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
        {
        }

        /// <summary>
        /// Deletes the hitsounds in hitsound difficulty mode, and removes their additions otherwise.
        /// </summary>
        public void DeleteOrClear(IReadOnlyList<HitsoundColumn> columns)
        {
            if (hitsoundEditor.HitsoundDifficultyMode.Value)
                hitsoundEditor.DeleteColumns(columns);
            else
                hitsoundEditor.ClearAdditions(columns);
        }

        #endregion

        #region Clipboard

        public override void Copy()
        {
            var columns = hitsoundEditor.SelectedColumns;

            if (columns.Count == 0)
                return;

            double start = columns[0].Time;

            clipboard = columns.Select(c => c.GetState().WithTime(c.Time - start)).ToList();
            CanPaste.Value = true;
        }

        public override void Paste()
        {
            if (clipboard == null)
                return;

            double time = EditorBeatmap.SnapTime(clock.CurrentTime, null);
            var states = clipboard.Select(s => s.WithTime(s.Time + time)).ToList();

            int dropped = hitsoundEditor.ApplyStates(states);

            if (dropped > 0)
                notify(SlopHitsoundEditorStrings.PasteDropped(dropped), FontAwesome.Solid.ExclamationTriangle);

            hitsoundEditor.SetSelection(states.Select(s => hitsoundEditor.Map.FindClosestColumn(s.Time, HitsoundEditor.PASTE_LENIENCY)).OfType<HitsoundColumn>().Select(c => c.Key));
        }

        #endregion

        #region Difficulties

        /// <summary>
        /// The other difficulties of the beatmap set.
        /// </summary>
        public IReadOnlyList<BeatmapInfo> GetOtherDifficulties()
            => beatmap.Value.BeatmapSetInfo.Beatmaps
                      .Where(b => !b.Equals(EditorBeatmap.BeatmapInfo))
                      .OrderBy(b => b.Ruleset.OnlineID)
                      .ThenBy(b => b.StarRating)
                      .ToList();

        /// <summary>
        /// Copies the hitsounds of this difficulty to other difficulties, after confirmation.
        /// This difficulty is saved first, and the editor is reloaded afterwards.
        /// </summary>
        public void CopyToDifficulties(IReadOnlyList<BeatmapInfo> difficulties, HitsoundCopyOptions options)
        {
            if (difficulties.Count == 0 || editor == null)
                return;

            var dialog = new HitsoundCopyConfirmationDialog(difficulties.Count, () => performCopy(difficulties, options));

            if (dialogOverlay != null)
                dialogOverlay.Push(dialog);
            else
                performCopy(difficulties, options);
        }

        private void performCopy(IReadOnlyList<BeatmapInfo> difficulties, HitsoundCopyOptions options)
        {
            if (editor == null)
                return;

            var source = hitsoundEditor.Map.Columns.Select(c => c.GetState()).ToList();
            var sourceBodies = hitsoundEditor.Map.Bodies.Select(b => b.GetState()).ToList();

            // this difficulty has to be saved before the others, as saving it afterwards would write its outdated beatmap set
            // (including the hashes of the other difficulties) back to the database.
            if (!editor.Save())
                return;

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

            notify(SlopHitsoundEditorStrings.CopyResult(changedHitsounds, difficulties.Count - failed), FontAwesome.Solid.Check);

            if (droppedHitsounds > 0)
                notify(SlopHitsoundEditorStrings.CopyDropped(droppedHitsounds), FontAwesome.Solid.ExclamationTriangle);

            if (failed > 0)
                notify(SlopHitsoundEditorStrings.CopyFailed(failed), FontAwesome.Solid.Times);

            // reload with a fresh reference, such that the editor's beatmap set contains the changes of the other difficulties.
            var current = beatmapManager.QueryBeatmap(b => b.ID == EditorBeatmap.BeatmapInfo.ID);

            if (current != null)
                editor.SwitchToDifficulty(current);
        }

        /// <summary>
        /// Copies the hitsounds of another difficulty onto this difficulty. This can be undone.
        /// </summary>
        public void ImportFromDifficulty(BeatmapInfo difficulty, HitsoundCopyOptions options)
        {
            List<HitsoundColumnState> source;
            List<HitsoundBodyState> sourceBodies;

            try
            {
                var map = loadHitsounds(difficulty);

                source = map.Columns.Select(c => c.GetState()).ToList();
                sourceBodies = map.Bodies.Select(b => b.GetState()).ToList();
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Failed to load hitsounds of {difficulty.GetDisplayTitle()}");
                notify(SlopHitsoundEditorStrings.ImportFailed(difficulty.DifficultyName), FontAwesome.Solid.Times);
                return;
            }

            var result = hitsoundEditor.Import(source, sourceBodies, options);

            notify(SlopHitsoundEditorStrings.ImportResult(result.ChangedHitsounds), FontAwesome.Solid.Check);

            if (result.DroppedHitsounds > 0)
                notify(SlopHitsoundEditorStrings.CopyDropped(result.DroppedHitsounds), FontAwesome.Solid.ExclamationTriangle);
        }

        /// <summary>
        /// Creates a hitsound difficulty which plays the hitsounds of all difficulties, and switches to it.
        /// Hitsounds of this difficulty take priority over those of other difficulties at the same time.
        /// </summary>
        /// <param name="bank">
        /// The hitnormal bank which the hitsound difficulty starts with, replacing the bank which most hitsounds of this difficulty use (see <see cref="HitsoundEditor.GetBaseHitsounds()"/>).
        /// </param>
        /// <param name="volume">The volume which the hitsound difficulty starts with, replacing the volume which most hitsounds of this difficulty use.</param>
        /// <param name="removeObjects">
        /// Whether the hitsound difficulty starts without any objects, instead of with the hitsounds of all difficulties.
        /// The first hitsound which is placed uses the given bank and volume then.
        /// </param>
        public void CreateHitsoundDifficulty(string bank, int volume, bool removeObjects)
        {
            if (editor == null)
                return;

            // the new difficulty is created from the saved state of this difficulty (e.g. its timing).
            if (!editor.Save())
                return;

            BeatmapInfo newDifficulty;

            try
            {
                newDifficulty = createHitsoundDifficulty(bank, volume, removeObjects);
            }
            catch (Exception e)
            {
                Logger.Error(e, @"Failed to create a hitsound difficulty");
                return;
            }

            editor.SwitchToDifficulty(newDifficulty);
        }

        private BeatmapInfo createHitsoundDifficulty(string bank, int volume, bool removeObjects)
        {
            var currentDifficulty = EditorBeatmap.BeatmapInfo;
            var ruleset = currentDifficulty.Ruleset;

            // fetch a fresh detached reference from database to avoid polluting model instances attached to cached working beatmaps (like the editor loader does).
            var beatmapSet = beatmapManager.QueryBeatmap(b => b.ID == currentDifficulty.ID)?.BeatmapSet
                             ?? throw new InvalidOperationException(@"The beatmap set of the current difficulty couldn't be found.");

            var primary = hitsoundEditor.Map.Columns.Select(c => c.GetState()).ToList();
            var others = new List<IReadOnlyList<HitsoundColumnState>>();

            foreach (var difficulty in beatmapSet.Beatmaps.Where(b => !b.Equals(currentDifficulty)).OrderByDescending(b => b.StarRating))
            {
                try
                {
                    others.Add(loadHitsounds(difficulty).Columns.Select(c => c.GetState()).ToList());
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Failed to load hitsounds of {difficulty.GetDisplayTitle()}, which are skipped");
                }
            }

            var (baseBank, baseVolume) = hitsoundEditor.GetBaseHitsounds();
            var states = removeObjects
                ? new List<HitsoundColumnState>()
                : HitsoundDifficultyBuilder.WithBaseHitsounds(HitsoundDifficultyBuilder.Merge(primary, others), baseBank, baseVolume, bank, volume);

            var referenceWorking = beatmapManager.GetWorkingBeatmap(currentDifficulty);
            var newWorking = beatmapManager.CreateNewDifficulty(beatmapSet, referenceWorking, ruleset);
            var newInfo = newWorking.BeatmapInfo;

            newInfo.DifficultyName = NamingUtils.GetNextBestName(beatmapSet.Beatmaps.Where(b => !b.Equals(newInfo)).Select(b => b.DifficultyName), @"Hitsounds");

            var newBeatmap = new Beatmap
            {
                BeatmapInfo = newInfo,
                Difficulty = EditorBeatmap.Difficulty.Clone(),
                ControlPointInfo = newWorking.Beatmap.ControlPointInfo,
                Bookmarks = EditorBeatmap.Bookmarks.ToArray(),
                // the objects of hitsound difficulties are at the same position, and shouldn't be moved apart by stacking.
                StackLeniency = 0,
                // the samples of hitsound difficulties should sound the same regardless of rate-changing mods.
                SamplesMatchPlaybackRate = false,
            };

            applyHitsoundDifficultySettings(newBeatmap.Difficulty, ruleset);

            HitsoundEditor.SetStartingHitsounds(newInfo, bank, volume);

            newBeatmap.HitObjects.AddRange(HitsoundDifficultyBuilder.CreateHitObjects(states, ruleset, newBeatmap.Difficulty, newBeatmap.ControlPointInfo));

            beatmapManager.Save(newInfo, newBeatmap, newWorking.GetSkin(), newWorking.Storyboard);

            notify(SlopHitsoundEditorStrings.HitsoundDifficultyCreated(newInfo.DifficultyName, states.Count), FontAwesome.Solid.Check);

            return newInfo;
        }

        /// <summary>
        /// Applies the difficulty settings recommended for hitsound difficulties, whose objects are only there to play hitsounds.
        /// </summary>
        private static void applyHitsoundDifficultySettings(BeatmapDifficulty difficulty, RulesetInfo ruleset)
        {
            // only osu! and osu!catch use these settings like this (osu!mania uses the circle size as the key count, and osu!taiko doesn't use them).
            if (ruleset.OnlineID != 0 && ruleset.OnlineID != 2)
                return;

            difficulty.CircleSize = 2;
            difficulty.ApproachRate = 8;
        }

        private HitsoundMap loadHitsounds(BeatmapInfo difficulty)
        {
            // a fresh reference, in case the difficulty was saved since the editor was loaded.
            var info = beatmapManager.QueryBeatmap(b => b.ID == difficulty.ID)
                       ?? throw new InvalidOperationException($@"{difficulty.GetDisplayTitle()} couldn't be found.");

            var playable = beatmapManager.GetWorkingBeatmap(info).GetPlayableBeatmap(info.Ruleset);
            return HitsoundMap.Create(playable.HitObjects, info.Ruleset.OnlineID);
        }

        #endregion

        private void notify(LocalisableString text, IconUsage icon) => notifications?.Post(new SimpleNotification
        {
            Text = text,
            Icon = icon,
        });
    }
}
