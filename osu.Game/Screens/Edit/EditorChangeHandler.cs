// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Extensions;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Tracks changes to the <see cref="Editor"/>.
    /// </summary>
    public abstract partial class EditorChangeHandler : TransactionalCommitComponent, IEditorChangeHandler
    {
        public readonly Bindable<bool> CanUndo = new Bindable<bool>();
        public readonly Bindable<bool> CanRedo = new Bindable<bool>();

        public event Action? OnStateChange;

        private readonly List<byte[]> savedStates = new List<byte[]>();

        /// <summary>
        /// The states of the <see cref="IEditorStateExtension"/>s for each saved state.
        /// </summary>
        private readonly List<Dictionary<IEditorStateExtension, string>> savedExtensionStates = new List<Dictionary<IEditorStateExtension, string>>();

        private readonly List<IEditorStateExtension> stateExtensions = new List<IEditorStateExtension>();

        private int currentState = -1;

        /// <summary>
        /// A SHA-2 hash representing the current visible editor state.
        /// </summary>
        public string CurrentStateHash
        {
            get
            {
                ensureStateSaved();

                using (var stream = new MemoryStream(savedStates[currentState]))
                    return stream.ComputeSHA2Hash();
            }
        }

        private bool isRestoring;

        /// <summary>
        /// Whether a saved state is currently being restored (by undoing or redoing).
        /// </summary>
        public bool IsRestoring => isRestoring;

        public const int MAX_SAVED_STATES = 50;

        /// <summary>
        /// Adds state which isn't part of the beatmap to the undo history, from now on.
        /// </summary>
        public void AddStateExtension(IEditorStateExtension extension)
        {
            if (stateExtensions.Contains(extension))
                return;

            ensureStateSaved();
            stateExtensions.Add(extension);

            // the current state is the one to return to when undoing the first change of the extension.
            savedExtensionStates[currentState][extension] = extension.GetState();
        }

        public void RemoveStateExtension(IEditorStateExtension extension)
        {
            stateExtensions.Remove(extension);

            foreach (var states in savedExtensionStates)
                states.Remove(extension);
        }

        public override void BeginChange()
        {
            ensureStateSaved();

            base.BeginChange();
        }

        private void ensureStateSaved()
        {
            if (savedStates.Count == 0)
                SaveState();
        }

        protected override void UpdateState()
        {
            if (isRestoring)
                return;

            using (var stream = new MemoryStream())
            {
                WriteCurrentStateToStream(stream);
                byte[] newState = stream.ToArray();
                var newExtensionStates = stateExtensions.ToDictionary(e => e, e => e.GetState());

                // if the previous state is binary equal we don't need to push a new one, unless this is the initial state.
                if (savedStates.Count > 0 && newState.SequenceEqual(savedStates[currentState]) && extensionStatesEqual(savedExtensionStates[currentState], newExtensionStates)) return;

                if (currentState < savedStates.Count - 1)
                {
                    savedStates.RemoveRange(currentState + 1, savedStates.Count - currentState - 1);
                    savedExtensionStates.RemoveRange(currentState + 1, savedExtensionStates.Count - currentState - 1);
                }

                if (savedStates.Count > MAX_SAVED_STATES)
                {
                    savedStates.RemoveAt(0);
                    savedExtensionStates.RemoveAt(0);
                }

                savedStates.Add(newState);
                savedExtensionStates.Add(newExtensionStates);

                currentState = savedStates.Count - 1;

                OnStateChange?.Invoke();
                updateBindables();
            }
        }

        public void RestoreState(int direction)
        {
            if (TransactionActive)
                return;

            if (savedStates.Count == 0)
                return;

            int newState = Math.Clamp(currentState + direction, 0, savedStates.Count - 1);
            if (currentState == newState)
                return;

            isRestoring = true;

            ApplyStateChange(savedStates[currentState], savedStates[newState]);

            foreach (var (extension, state) in savedExtensionStates[newState])
            {
                if (stateExtensions.Contains(extension) && savedExtensionStates[currentState].GetValueOrDefault(extension) != state)
                    extension.ApplyState(state);
            }

            currentState = newState;

            isRestoring = false;

            OnStateChange?.Invoke();
            updateBindables();
        }

        /// <summary>
        /// Write a serialised copy of the currently tracked state to the provided stream.
        /// This will be stored as a state which can be restored in the future.
        /// </summary>
        /// <param name="stream">The stream which the state should be written to.</param>
        protected abstract void WriteCurrentStateToStream(MemoryStream stream);

        /// <summary>
        /// Given a previous and new state, apply any changes required to bring the current state in line with the new state.
        /// </summary>
        /// <param name="previousState">The previous (current before this call) serialised state.</param>
        /// <param name="newState">The new state to be applied.</param>
        protected abstract void ApplyStateChange(byte[] previousState, byte[] newState);

        private static bool extensionStatesEqual(Dictionary<IEditorStateExtension, string> a, Dictionary<IEditorStateExtension, string> b)
            => a.Count == b.Count && a.All(e => b.TryGetValue(e.Key, out string? other) && other == e.Value);

        private void updateBindables()
        {
            CanUndo.Value = savedStates.Count > 0 && currentState > 0;
            CanRedo.Value = currentState < savedStates.Count - 1;
        }
    }

    /// <summary>
    /// State which isn't part of the beatmap, but is undone and redone along with it (see <see cref="EditorChangeHandler.AddStateExtension"/>).
    /// </summary>
    public interface IEditorStateExtension
    {
        /// <summary>
        /// A serialised copy of the current state.
        /// </summary>
        string GetState();

        /// <summary>
        /// Restores a state returned by <see cref="GetState"/>.
        /// </summary>
        void ApplyState(string state);
    }
}
