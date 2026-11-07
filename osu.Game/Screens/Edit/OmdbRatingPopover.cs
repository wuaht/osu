// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Online.Omdb;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Screens.Edit
{
    /// <summary>
    /// An invisible drawable which hosts an <see cref="OmdbRatingPopover"/>.
    /// </summary>
    public partial class OmdbRatingAnchor : DraggablePopoverAnchor
    {
        private readonly int beatmapId;

        public OmdbRatingAnchor(int beatmapId)
        {
            this.beatmapId = beatmapId;
        }

        protected override DraggablePopover CreatePopover() => new OmdbRatingPopover(beatmapId);
    }

    public partial class OmdbRatingPopover : DraggablePopover
    {
        public OmdbRatingPopover(int beatmapId)
        {
            Child = new OmdbRatingControl(beatmapId) { Width = 400 };
        }
    }
}
