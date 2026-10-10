// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Input;
using osuTK;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// A part of the hitsound editor which displays content along the time axis of the <see cref="HitsoundTimeline"/>.
    /// </summary>
    public abstract partial class HitsoundTimelinePart : CompositeDrawable, IBlockGlobalAltScrollVolume
    {
        /// <summary>
        /// How close (in pixels) the cursor needs to be to a hitsound to interact with it.
        /// </summary>
        protected const float INTERACTION_DISTANCE = 8;

        [Resolved]
        protected HitsoundTimeline Timeline { get; private set; } = null!;

        [Resolved]
        protected HitsoundEditor HitsoundEditor { get; private set; } = null!;

        [Resolved]
        protected HitsoundPlayback Playback { get; private set; } = null!;

        [Resolved]
        protected EditorClock EditorClock { get; private set; } = null!;

        [Resolved]
        protected EditorBeatmap EditorBeatmap { get; private set; } = null!;

        protected HitsoundTimelinePart()
        {
            RelativeSizeAxes = Axes.Both;
            Masking = true;
        }

        /// <summary>
        /// The position of the mouse in the local space of this part.
        /// </summary>
        /// <remarks>
        /// <see cref="UIEvent.MousePosition"/> can't be used, as it is in the space of the parent, which is offset in some parts (e.g. by padding).
        /// </remarks>
        protected Vector2 GetMousePosition(UIEvent e) => ToLocalSpace(e.ScreenSpaceMousePosition);

        protected float TimeToX(double time) => Timeline.TimeToX(time, DrawWidth);

        protected double XToTime(float x) => Timeline.XToTime(x, DrawWidth);

        /// <summary>
        /// The time range which is visible, extended by a margin on both sides.
        /// </summary>
        protected (double start, double end) GetVisibleRange(float margin = 20) => (XToTime(-margin), XToTime(DrawWidth + margin));

        /// <summary>
        /// Returns the hitsound column close to the given position, if there is one.
        /// </summary>
        protected HitsoundColumn? FindColumnAt(float x) => HitsoundEditor.Map.FindClosestColumn(XToTime(x), Timeline.PixelsToDuration(INTERACTION_DISTANCE));

        protected override bool OnScroll(ScrollEvent e)
        {
            // scrolling without modifiers seeks (handled by the editor), with alt it zooms like in the timeline.
            if (!e.AltPressed)
                return false;

            Timeline.AdjustZoom(e.ScrollDelta.Y);
            return true;
        }
    }
}
