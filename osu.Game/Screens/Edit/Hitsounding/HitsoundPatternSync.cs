// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Keeps the places where a hitsound pattern is used the same, for changes anywhere in the editor (e.g. deleting an object in the compose screen):
    /// at the end of each change of the beatmap, the hitsounds of the places which have been changed are copied to the other places of their pattern,
    /// as part of the same change (so they are undone together).
    /// Also adds the patterns to the undo history of the editor.
    /// </summary>
    public partial class HitsoundPatternSync : CompositeDrawable
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private HitsoundPatterns patterns { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IEditorChangeHandler? changeHandler { get; set; }

        [Resolved(canBeNull: true)]
        private OsuConfigManager? config { get; set; }

        /// <summary>
        /// Performs the copying, independently of whether the hitsound screen is open.
        /// </summary>
        private readonly HitsoundEditor hitsoundEditor = new HitsoundEditor();

        /// <summary>
        /// The hitsounds of all places where patterns are used when the current change of the beatmap began.
        /// </summary>
        private List<(HitsoundPatternOccurrence occurrence, IReadOnlyList<HitsoundColumnState> states)>? statesBefore;

        /// <summary>
        /// The <see cref="HitsoundPatterns.StructureVersion"/> when the current change of the beatmap began.
        /// Changes which also change the patterns (e.g. placing or moving one) aren't copied, as they change the places on purpose.
        /// </summary>
        private int structureBefore;

        private bool syncing;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = hitsoundEditor;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            editorBeatmap.TransactionBegan += onTransactionBegan;
            editorBeatmap.TransactionEnding += onTransactionEnding;

            // creating, placing and removing patterns is undone along with the hitsounds.
            if (changeHandler is EditorChangeHandler editorChangeHandler)
            {
                editorChangeHandler.AddStateExtension(patterns);
                patterns.Changed += savePatternState;
            }
        }

        private bool isRestoring => changeHandler is EditorChangeHandler { IsRestoring: true };

        private void onTransactionBegan()
        {
            if (syncing || isRestoring || patterns.Occurrences.Count == 0)
            {
                statesBefore = null;
                return;
            }

            structureBefore = patterns.StructureVersion;
            statesBefore = hitsoundEditor.CapturePatternStates();
        }

        private void onTransactionEnding()
        {
            var before = statesBefore;
            statesBefore = null;

            if (before == null || syncing || isRestoring || patterns.StructureVersion != structureBefore)
                return;

            // the mode may have been changed in the hitsound screen, and decides whether objects are created and deleted when copying.
            hitsoundEditor.HitsoundDifficultyMode.Value = HitsoundPreferences.IsHitsoundDifficulty(config, editorBeatmap.BeatmapInfo);

            syncing = true;

            try
            {
                hitsoundEditor.SyncPatterns(before);
            }
            finally
            {
                syncing = false;
            }
        }

        private void savePatternState() => changeHandler?.SaveState();

        protected override void Dispose(bool isDisposing)
        {
            if (editorBeatmap.IsNotNull())
            {
                editorBeatmap.TransactionBegan -= onTransactionBegan;
                editorBeatmap.TransactionEnding -= onTransactionEnding;
            }

            if (patterns.IsNotNull())
            {
                patterns.Changed -= savePatternState;

                if (changeHandler is EditorChangeHandler editorChangeHandler)
                    editorChangeHandler.RemoveStateExtension(patterns);
            }

            base.Dispose(isDisposing);
        }
    }
}
