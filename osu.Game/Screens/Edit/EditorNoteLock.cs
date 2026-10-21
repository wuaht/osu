// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Audio;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// Locks the timing and position of objects in the editor, and allows copying the hitsounds of one object onto others while locked.
    /// </summary>
    public partial class EditorNoteLock : Component, IKeyBindingHandler<GlobalAction>
    {
        /// <summary>
        /// Whether objects are currently locked in place.
        /// </summary>
        public readonly BindableBool Enabled = new BindableBool();

        /// <summary>
        /// The samples copied by <see cref="CopySamples"/>, or <c>null</c> if none were copied yet.
        /// </summary>
        public IReadOnlyList<HitSampleInfo>? CopiedSamples { get; private set; }

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Repeat || e.Action != GlobalAction.EditorToggleNoteLock)
                return false;

            Enabled.Toggle();
            onScreenDisplay?.Display(new NoteLockToast(Enabled.Value ? CommonStrings.Enabled : CommonStrings.Disabled));
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        /// <summary>
        /// Copies the samples of <paramref name="hitObject"/> to be pasted with <see cref="PasteSamples"/>.
        /// </summary>
        public void CopySamples(HitObject hitObject)
        {
            CopiedSamples = hitObject.Samples.ToList();
            onScreenDisplay?.Display(new NoteLockToast(SlopEditorStrings.SampleNoteCopied));
        }

        /// <summary>
        /// Applies the samples copied by <see cref="CopySamples"/> to <paramref name="hitObject"/>.
        /// </summary>
        public void PasteSamples(HitObject hitObject)
        {
            if (CopiedSamples == null)
            {
                onScreenDisplay?.Display(new NoteLockToast(SlopEditorStrings.NoSampleNote));
                return;
            }

            editorBeatmap.BeginChange();
            hitObject.Samples = CopiedSamples.ToList();
            editorBeatmap.Update(hitObject);
            editorBeatmap.EndChange();

            onScreenDisplay?.Display(new NoteLockToast(SlopEditorStrings.SampleNotePasted));
        }

        private partial class NoteLockToast : Toast
        {
            public NoteLockToast(LocalisableString value)
                : base(SlopEditorStrings.NoteLock, value)
            {
            }

            [BackgroundDependencyLoader]
            private void load(RealmKeyBindingStore keyBindingStore)
            {
                ExtraText = keyBindingStore.GetBindingsStringFor(GlobalAction.EditorToggleNoteLock);
            }
        }
    }
}
