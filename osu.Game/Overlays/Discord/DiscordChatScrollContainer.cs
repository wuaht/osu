// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Utils;
using osu.Game.Graphics.Containers;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// The scrollable messages of a channel, which stay at the latest message while it is scrolled to the end.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Chat.ChannelScrollContainer"/>, content which grows moves the scroll position in the same frame instead of animating it,
    /// as messages grow while their images and embeds load, which would otherwise visibly scroll the channel down after it was opened.
    /// </remarks>
    public partial class DiscordChatScrollContainer : OsuScrollContainer
    {
        /// <summary>
        /// The end is followed if the scroll position is closer to it than this.
        /// </summary>
        private const float end_leniency = 10;

        private bool trackEnd = true;

        /// <summary>
        /// The distance to the end which is kept while the content grows above the visible messages.
        /// </summary>
        private double? keptDistanceFromEnd;

        private double lastExtent;

        /// <summary>
        /// Keeps the visible messages in place while content is added above them, until the user scrolls.
        /// </summary>
        public void KeepDistanceFromEnd()
        {
            if (!trackEnd)
                keptDistanceFromEnd = ScrollableExtent - Target;
        }

        protected override void ApplyCurrentToContent()
        {
            if (!Precision.AlmostEquals(lastExtent, ScrollableExtent))
            {
                lastExtent = ScrollableExtent;

                // While the chat is scrolled past its end, it already bounces back to the end, which jumping there would cut off.
                if (trackEnd && !isPastEnd)
                    base.ScrollToEnd(false, true);
                else if (keptDistanceFromEnd is double distance)
                    base.ScrollTo(ScrollableExtent - distance, false);
            }

            base.ApplyCurrentToContent();
        }

        private bool isPastEnd => Current > ScrollableExtent || Target > ScrollableExtent;

        private void updateTrackState()
        {
            trackEnd = IsScrolledToEnd(end_leniency);
            keptDistanceFromEnd = null;
        }

        protected override void OnUserScroll(double value, bool animated = true, double? distanceDecay = null)
        {
            base.OnUserScroll(value, animated, distanceDecay);
            updateTrackState();
        }

        public new void ScrollTo(double value, bool animated = true, double? distanceDecay = null)
        {
            base.ScrollTo(value, animated, distanceDecay);
            updateTrackState();
        }

        public new void ScrollToEnd(bool animated = true, bool allowDuringDrag = false)
        {
            base.ScrollToEnd(animated, allowDuringDrag);
            trackEnd = true;
            keptDistanceFromEnd = null;
        }
    }
}
