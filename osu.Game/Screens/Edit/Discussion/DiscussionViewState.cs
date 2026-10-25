// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The pages of the discussion, in the order of the tabs on the website.
    /// </summary>
    public enum DiscussionPage
    {
        Reviews,
        GeneralAll,
        General,
        Timeline,
        Events,
    }

    /// <summary>
    /// The filters of the discussions, in the order of the stats on the website.
    /// </summary>
    public enum DiscussionFilter
    {
        Mine,
        MapperNotes,
        Resolved,
        Pending,
        Praises,
        Total,
        Hype,
    }

    public enum DiscussionSort
    {
        CreatedAt,
        Timeline,
        UpdatedAt,
    }

    /// <summary>
    /// What is shown in the discussion screen, which is kept while switching difficulties (which recreates the screen).
    /// </summary>
    public class DiscussionViewState
    {
        /// <summary>
        /// The beatmap set this state is of, such that it's reset when another beatmap set is opened.
        /// </summary>
        public int BeatmapSetId;

        public bool Initialised;

        /// <summary>
        /// The last loaded beatmap set, shown right away when the screen is recreated (e.g. after switching difficulties) while it's loaded again.
        /// </summary>
        public DiscussionBeatmapSet? LoadedBeatmapSet;

        public DiscussionPage Page = DiscussionPage.GeneralAll;

        public DiscussionFilter Filter = DiscussionFilter.Total;

        /// <summary>
        /// The page and filter to return to when leaving the history.
        /// </summary>
        public DiscussionPage? PreviousPage;

        public DiscussionFilter? PreviousFilter;

        /// <summary>
        /// The ID of the selected beatmap (on the website), or null for the default one.
        /// </summary>
        public int? CurrentBeatmapId;

        public readonly Dictionary<DiscussionPage, DiscussionSort> Sort = new Dictionary<DiscussionPage, DiscussionSort>
        {
            { DiscussionPage.GeneralAll, DiscussionSort.UpdatedAt },
            { DiscussionPage.General, DiscussionSort.UpdatedAt },
            { DiscussionPage.Timeline, DiscussionSort.Timeline },
            { DiscussionPage.Reviews, DiscussionSort.UpdatedAt },
        };

        public readonly HashSet<int> SelectedUserIds = new HashSet<int>();

        public bool RepliesIncludeSelectedUsers;

        public bool ShowOtherReplies = true;

        public bool DefaultCollapsed;

        /// <summary>
        /// Discussions which were collapsed or expanded individually, overriding <see cref="DefaultCollapsed"/>.
        /// </summary>
        public readonly Dictionary<int, bool> Collapsed = new Dictionary<int, bool>();

        public int? HighlightedDiscussionId;

        /// <summary>
        /// Whether the highlighted discussion should be scrolled to once it's shown.
        /// </summary>
        public bool ScrollToHighlighted;

        /// <summary>
        /// The posts which were seen, posts which aren't in here are marked as new.
        /// </summary>
        public readonly HashSet<int> ReadPostIds = new HashSet<int>();

        public bool IsCollapsed(int discussionId) => Collapsed.TryGetValue(discussionId, out bool collapsed) ? collapsed : DefaultCollapsed;

        public DiscussionSort CurrentSort => Sort.GetValueOrDefault(Page, DiscussionSort.UpdatedAt);
    }
}
