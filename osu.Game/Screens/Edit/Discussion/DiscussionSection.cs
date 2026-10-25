// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// A part of the discussion screen, which is updated whenever the shown discussion changes.
    /// </summary>
    public abstract partial class DiscussionSection : CompositeDrawable
    {
        [Resolved]
        protected DiscussionScreen Screen { get; private set; } = null!;

        [Resolved]
        protected OverlayColourProvider ColourProvider { get; private set; } = null!;

        private readonly IBindable<DiscussionsState?> state = new Bindable<DiscussionsState?>();

        protected DiscussionsState? State => state.Value;

        protected DiscussionSection()
        {
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            state.BindTo(Screen.CurrentState);
            state.BindValueChanged(s =>
            {
                if (s.OldValue != null)
                    s.OldValue.Changed -= onChanged;

                if (s.NewValue != null)
                    s.NewValue.Changed += onChanged;

                if (s.NewValue != null)
                    OnStateReplaced(s.NewValue);

                onChanged();
            }, true);
        }

        private void onChanged() => Scheduler.AddOnce(updateDisplay);

        private void updateDisplay()
        {
            if (State == null)
                return;

            UpdateDisplay(State);
        }

        /// <summary>
        /// Invoked when the discussion was loaded (again), to update what only changes when it's loaded.
        /// </summary>
        protected virtual void OnStateReplaced(DiscussionsState newState)
        {
        }

        /// <summary>
        /// Updates what is shown, invoked whenever something in the discussion changed.
        /// </summary>
        protected abstract void UpdateDisplay(DiscussionsState state);

        protected override void Dispose(bool isDisposing)
        {
            if (state.Value != null)
                state.Value.Changed -= onChanged;

            base.Dispose(isDisposing);
        }
    }
}
