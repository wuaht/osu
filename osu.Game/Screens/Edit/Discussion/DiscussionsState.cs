// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The discussions of a beatmap set grouped and filtered like on the discussion page of the website.
    /// </summary>
    public class DiscussionsState
    {
        /// <summary>
        /// The rulesets in the order they're shown on the website.
        /// </summary>
        public static readonly string[] RULESETS = { @"osu", @"taiko", @"fruits", @"mania" };

        public readonly DiscussionBeatmapSet BeatmapSet;

        public readonly DiscussionViewState View;

        /// <summary>
        /// The ID of the signed in user, if any.
        /// </summary>
        public readonly int? CurrentUserId;

        public readonly IReadOnlyDictionary<int, BeatmapDiscussion> Discussions;

        public readonly IReadOnlyDictionary<int, DiscussionBeatmap> Beatmaps;

        public readonly IReadOnlyDictionary<int, DiscussionUser> Users;

        /// <summary>
        /// The beatmaps grouped by ruleset in the order of <see cref="RULESETS"/>, only containing rulesets which have beatmaps.
        /// </summary>
        public readonly IReadOnlyList<(string Ruleset, List<DiscussionBeatmap> Beatmaps)> GroupedBeatmaps;

        /// <summary>
        /// Invoked whenever something which is shown changed.
        /// </summary>
        public event Action? Changed;

        public DiscussionsState(DiscussionBeatmapSet beatmapSet, DiscussionViewState view, int? currentUserId)
        {
            BeatmapSet = beatmapSet;
            View = view;
            CurrentUserId = currentUserId;

            Discussions = beatmapSet.Discussions.GroupBy(d => d.Id).ToDictionary(g => g.Key, g => g.First());
            Beatmaps = beatmapSet.Beatmaps.GroupBy(b => b.Id).ToDictionary(g => g.Key, g => g.First());
            Users = beatmapSet.RelatedUsers.GroupBy(u => u.Id).ToDictionary(g => g.Key, g => g.First());

            GroupedBeatmaps = RULESETS.Select(r => (r, sortBeatmaps(beatmapSet.Beatmaps.Where(b => b.Mode == r))))
                                      .Where(g => g.Item2.Count > 0)
                                      .ToList();

            if (view.BeatmapSetId != beatmapSet.Id || !view.Initialised)
            {
                view.BeatmapSetId = beatmapSet.Id;
                view.Initialised = true;
                view.Page = DiscussionPage.GeneralAll;
                view.Filter = beatmapSet.Status is @"approved" or @"ranked" ? DiscussionFilter.Praises : DiscussionFilter.Total;
                view.PreviousPage = null;
                view.PreviousFilter = null;
                view.SelectedUserIds.Clear();
                view.Collapsed.Clear();
                view.DefaultCollapsed = false;
                view.HighlightedDiscussionId = null;
                view.CurrentBeatmapId = null;
                view.ReadPostIds.Clear();

                // like on the website, everything which is there when opening the page is read.
                foreach (var post in beatmapSet.Discussions.SelectMany(d => d.Posts))
                    view.ReadPostIds.Add(post.Id);
            }

            if (view.CurrentBeatmapId == null || !Beatmaps.ContainsKey(view.CurrentBeatmapId.Value))
                view.CurrentBeatmapId = (FindDefault() ?? beatmapSet.Beatmaps.FirstOrDefault())?.Id;
        }

        /// <summary>
        /// Notifies that something in <see cref="View"/> was changed.
        /// </summary>
        public void NotifyChanged()
        {
            cache = null;
            Changed?.Invoke();
        }

        #region Beatmaps

        public DiscussionBeatmap? CurrentBeatmap => View.CurrentBeatmapId is int id ? Beatmaps.GetValueOrDefault(id) : null;

        public int? CurrentBeatmapId => CurrentBeatmap?.Id;

        public List<DiscussionBeatmap> BeatmapsOfRuleset(string ruleset) => GroupedBeatmaps.FirstOrDefault(g => g.Ruleset == ruleset).Beatmaps ?? new List<DiscussionBeatmap>();

        /// <summary>
        /// The beatmaps sorted like on the website, by ruleset and difficulty.
        /// </summary>
        public IEnumerable<DiscussionBeatmap> SortedBeatmaps => GroupedBeatmaps.SelectMany(g => g.Beatmaps);

        public IEnumerable<string> RulesetsWithoutDeletedBeatmaps => GroupedBeatmaps.Where(g => g.Beatmaps.Any(b => b.DeletedAt == null)).Select(g => g.Ruleset);

        /// <summary>
        /// The beatmap which is selected by default, the visible beatmap closest to 1 star.
        /// </summary>
        public DiscussionBeatmap? FindDefault()
        {
            foreach (var group in GroupedBeatmaps)
            {
                var beatmap = FindDefault(group.Beatmaps.Where(b => !b.Convert).ToList());

                if (beatmap != null)
                    return beatmap;
            }

            return null;
        }

        public static DiscussionBeatmap? FindDefault(IReadOnlyList<DiscussionBeatmap> items)
        {
            if (items.Count == 0)
                return null;

            double? currentDelta = null;
            DiscussionBeatmap? current = null;

            foreach (var item in items)
            {
                double delta = Math.Abs(item.DifficultyRating - 1.0);

                if (item.DeletedAt == null && !item.Convert && (currentDelta == null || delta < currentDelta))
                {
                    currentDelta = delta;
                    current = item;
                }
            }

            return current ?? items[^1];
        }

        private static List<DiscussionBeatmap> sortBeatmaps(IEnumerable<DiscussionBeatmap> beatmaps)
        {
            var list = beatmaps.ToList();

            if (list.Count == 0)
                return list;

            if (list[0].Mode == @"mania")
                return list.OrderByDescending(b => b.Convert).ThenBy(b => b.CircleSize).ThenBy(b => b.DifficultyRating).ToList();

            return list.OrderByDescending(b => b.Convert).ThenBy(b => b.DifficultyRating).ToList();
        }

        public bool HasGuestOwners(DiscussionBeatmap beatmap) => beatmap.Owners?.Any(o => o.Id != BeatmapSet.UserId) == true;

        public bool IsBeatmapOwner(int userId, DiscussionBeatmap beatmap) => beatmap.Owners?.Any(o => o.Id == userId) == true;

        #endregion

        #region Discussions

        private Cache? cache;

        private Cache current => cache ??= new Cache(this);

        public IEnumerable<BeatmapDiscussion> NonDeletedDiscussions => Discussions.Values.Where(d => d.DeletedAt == null);

        /// <summary>
        /// The discussions of the current beatmap (and all difficulties) grouped by filter.
        /// </summary>
        public IReadOnlyDictionary<DiscussionFilter, List<BeatmapDiscussion>> DiscussionsByFilter => current.ByFilter;

        /// <summary>
        /// The discussions of the current beatmap and filter grouped by page.
        /// </summary>
        public IReadOnlyDictionary<DiscussionPage, List<BeatmapDiscussion>> DiscussionsByMode => current.ByMode;

        /// <summary>
        /// <see cref="DiscussionsByMode"/>, only containing discussions of the selected users.
        /// </summary>
        public IReadOnlyDictionary<DiscussionPage, List<BeatmapDiscussion>> DiscussionsForSelectedUserByMode => current.ForSelectedUsersByMode;

        /// <summary>
        /// The discussions of the current page, sorted.
        /// </summary>
        public IReadOnlyList<BeatmapDiscussion> SortedDiscussions => current.Sorted;

        public bool IsTimelineVisible => View.Page == DiscussionPage.Timeline && View.CurrentSort == DiscussionSort.Timeline;

        public int TotalHypeCount => NonDeletedDiscussions.Count(d => d.MessageType == @"hype");

        public bool HasCurrentUserHyped => CurrentUserId is int userId
                                           && DiscussionsByFilter[DiscussionFilter.Hype].Any(d => d.BeatmapId == null && d.UserId == userId);

        public int UnresolvedCountOfBeatmap(int beatmapId) => current.UnresolvedByBeatmap.GetValueOrDefault(beatmapId);

        public int? UnresolvedCountOfRuleset(string ruleset) => current.UnresolvedByMode.TryGetValue(ruleset, out int count) ? count : null;

        public BeatmapDiscussion? FirstUnresolvedDiscussion => NonDeletedDiscussions.FirstOrDefault(d =>
            d.CanBeResolved && !d.Resolved && (d.BeatmapId == null || Beatmaps.GetValueOrDefault(d.BeatmapId.Value)?.DeletedAt == null));

        public BeatmapDiscussion? GetDiscussion(int? id) => id is int value ? Discussions.GetValueOrDefault(value) : null;

        public DiscussionUser? GetUser(int? id) => id is int value ? Users.GetValueOrDefault(value) : null;

        /// <summary>
        /// The users who nominated the beatmap set since the last disqualification or nomination reset.
        /// </summary>
        public List<DiscussionUser> Nominators
        {
            get
            {
                var nominators = new List<DiscussionUser>();

                for (int i = BeatmapSet.Events.Count - 1; i >= 0; i--)
                {
                    var e = BeatmapSet.Events[i];

                    if (e.Type is @"disqualify" or @"nomination_reset")
                        break;

                    if (e.Type == @"nominate" && GetUser(e.UserId) is DiscussionUser user)
                        nominators.Insert(0, user);
                }

                return nominators;
            }
        }

        public string? CalculatedMainRuleset => BeatmapSet.EligibleMainRulesets.Count == 1 ? BeatmapSet.EligibleMainRulesets[0] : null;

        public int NominationsCurrent
        {
            get
            {
                var nominations = BeatmapSet.Nominations;

                if (nominations?.Current == null)
                    return 0;

                if (nominations.LegacyMode)
                    return nominations.Current.Type == Newtonsoft.Json.Linq.JTokenType.Integer ? nominations.Current.Value<int>() : 0;

                return nominations.Current is Newtonsoft.Json.Linq.JObject obj ? obj.Properties().Sum(p => p.Value.Type == Newtonsoft.Json.Linq.JTokenType.Integer ? p.Value.Value<int>() : 0) : 0;
            }
        }

        public int NominationsRequired
        {
            get
            {
                var nominations = BeatmapSet.Nominations;

                if (nominations == null)
                    return 0;

                if (nominations.LegacyMode)
                    return nominations.Required ?? 0;

                var meta = nominations.RequiredMeta;

                if (meta == null)
                    return 0;

                return meta.MainRuleset + meta.NonMainRuleset * (RulesetsWithoutDeletedBeatmaps.Count() - 1);
            }
        }

        /// <summary>
        /// The number of nominations of a ruleset in hybrid mode.
        /// </summary>
        public int NominationsOfRuleset(string ruleset)
        {
            if (BeatmapSet.Nominations?.Current is Newtonsoft.Json.Linq.JObject obj && obj[ruleset] is Newtonsoft.Json.Linq.JToken token && token.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
                return token.Value<int>();

            return 0;
        }

        public bool IsUnread(BeatmapDiscussion discussion) => discussion.Posts.Any(p => !View.ReadPostIds.Contains(p.Id));

        public static DiscussionPage PageOf(BeatmapDiscussion discussion)
        {
            if (discussion.MessageType == @"review")
                return DiscussionPage.Reviews;

            if (discussion.BeatmapId != null)
                return discussion.Timestamp != null ? DiscussionPage.Timeline : DiscussionPage.General;

            return DiscussionPage.GeneralAll;
        }

        #endregion

        #region Actions

        public void ChangeDiscussionPage(DiscussionPage page)
        {
            if (page == DiscussionPage.Events)
            {
                if (View.Page != DiscussionPage.Events)
                {
                    View.PreviousPage = View.Page;
                    View.PreviousFilter = View.Filter;
                }
            }
            else if (View.Page == DiscussionPage.Events && View.PreviousFilter is DiscussionFilter previousFilter && View.Filter != previousFilter)
                View.Filter = previousFilter;

            View.Page = page;
            NotifyChanged();
        }

        public void ChangeFilter(DiscussionFilter filter)
        {
            if (View.Page == DiscussionPage.Events)
                View.Page = View.PreviousPage ?? DiscussionPage.General;

            View.Filter = filter;
            NotifyChanged();
        }

        public void ChangeSort(DiscussionSort sort)
        {
            if (View.Page == DiscussionPage.Events)
                return;

            View.Sort[View.Page] = sort;
            NotifyChanged();
        }

        public void ChangeGameMode(string ruleset)
        {
            var beatmap = FindDefault(BeatmapsOfRuleset(ruleset));

            if (beatmap != null)
                View.CurrentBeatmapId = beatmap.Id;

            NotifyChanged();
        }

        public void ChangeBeatmap(int beatmapId)
        {
            View.CurrentBeatmapId = beatmapId;
            ChangeDiscussionPage(DiscussionPage.Timeline);
        }

        /// <summary>
        /// Shows a discussion, changing the page, beatmap and filters such that it's shown.
        /// </summary>
        /// <returns>Whether the discussion exists.</returns>
        public bool ChangeToDiscussion(int discussionId)
        {
            var discussion = GetDiscussion(discussionId);

            if (discussion == null)
                return false;

            var page = PageOf(discussion);

            if (discussion.BeatmapId != null)
                View.CurrentBeatmapId = discussion.BeatmapId;

            cache = null;

            // unset type filter if discussion would have been filtered out.
            if (!DiscussionsByMode[page].Any(d => d.Id == discussion.Id))
                View.Filter = DiscussionFilter.Total;

            // unset user filter if new discussion would have been filtered out.
            if (!View.SelectedUserIds.Contains(discussion.UserId))
                View.SelectedUserIds.Clear();

            View.Page = page;
            View.HighlightedDiscussionId = discussion.Id;
            NotifyChanged();
            return true;
        }

        public void ToggleUser(int? userId)
        {
            if (userId == null)
                View.SelectedUserIds.Clear();
            else if (!View.SelectedUserIds.Remove(userId.Value))
                View.SelectedUserIds.Add(userId.Value);

            NotifyChanged();
        }

        public void SetAllCollapsed(bool collapsed)
        {
            View.DefaultCollapsed = collapsed;
            View.Collapsed.Clear();
            NotifyChanged();
        }

        #endregion

        private class Cache
        {
            public readonly Dictionary<DiscussionFilter, List<BeatmapDiscussion>> ByFilter = new Dictionary<DiscussionFilter, List<BeatmapDiscussion>>();
            public readonly Dictionary<DiscussionPage, List<BeatmapDiscussion>> ByMode = new Dictionary<DiscussionPage, List<BeatmapDiscussion>>();
            public readonly Dictionary<DiscussionPage, List<BeatmapDiscussion>> ForSelectedUsersByMode;
            public readonly List<BeatmapDiscussion> Sorted;
            public readonly Dictionary<int, int> UnresolvedByBeatmap = new Dictionary<int, int>();
            public readonly Dictionary<string, int> UnresolvedByMode = new Dictionary<string, int>();

            public Cache(DiscussionsState state)
            {
                var view = state.View;
                int? currentBeatmapId = state.CurrentBeatmapId;

                var forSelectedBeatmap = state.NonDeletedDiscussions.Where(d => d.BeatmapId == null || d.BeatmapId == currentBeatmapId).ToList();

                foreach (var filter in Enum.GetValues<DiscussionFilter>())
                    ByFilter[filter] = new List<BeatmapDiscussion>();

                ByFilter[DiscussionFilter.Total] = forSelectedBeatmap;

                var reviewsWithPending = new List<BeatmapDiscussion>();

                foreach (var d in forSelectedBeatmap)
                {
                    if (state.CurrentUserId != null && d.UserId == state.CurrentUserId)
                        ByFilter[DiscussionFilter.Mine].Add(d);

                    switch (d.MessageType)
                    {
                        case @"hype":
                            ByFilter[DiscussionFilter.Hype].Add(d);
                            ByFilter[DiscussionFilter.Praises].Add(d);
                            break;

                        case @"mapper_note":
                            ByFilter[DiscussionFilter.MapperNotes].Add(d);
                            break;

                        case @"praise":
                            ByFilter[DiscussionFilter.Praises].Add(d);
                            break;
                    }

                    if (d.CanBeResolved)
                    {
                        if (d.Resolved)
                            ByFilter[DiscussionFilter.Resolved].Add(d);
                        else
                        {
                            ByFilter[DiscussionFilter.Pending].Add(d);

                            // only reviews with unresolved discussions get added.
                            if (state.GetDiscussion(d.ParentId) is BeatmapDiscussion parent && parent.MessageType == @"review" && !reviewsWithPending.Contains(parent))
                                reviewsWithPending.Add(parent);
                        }
                    }
                }

                ByFilter[DiscussionFilter.Pending].AddRange(reviewsWithPending);

                foreach (var page in modes)
                    ByMode[page] = new List<BeatmapDiscussion>();

                foreach (var d in ByFilter[view.Filter])
                {
                    if (d.MessageType == @"review")
                        ByMode[DiscussionPage.Reviews].Add(d);
                    else if (d.BeatmapId == null)
                        ByMode[DiscussionPage.GeneralAll].Add(d);
                    else if (d.BeatmapId == currentBeatmapId)
                        ByMode[d.Timestamp != null ? DiscussionPage.Timeline : DiscussionPage.General].Add(d);
                }

                if (view.SelectedUserIds.Count == 0)
                    ForSelectedUsersByMode = ByMode;
                else
                {
                    ForSelectedUsersByMode = new Dictionary<DiscussionPage, List<BeatmapDiscussion>>();

                    foreach (var page in modes)
                    {
                        ForSelectedUsersByMode[page] = ByMode[page].Where(d =>
                            view.SelectedUserIds.Contains(d.UserId)
                            || (view.RepliesIncludeSelectedUsers && d.Posts.Any(p => view.SelectedUserIds.Contains(p.UserId) && p.DeletedAt == null))).ToList();
                    }
                }

                Sorted = view.Page == DiscussionPage.Events ? new List<BeatmapDiscussion>() : sort(ForSelectedUsersByMode[view.Page], view.CurrentSort);

                // show at least 0 for available rulesets
                foreach (var beatmap in state.BeatmapSet.Beatmaps)
                    UnresolvedByMode.TryAdd(beatmap.Mode, 0);

                foreach (var d in state.NonDeletedDiscussions)
                {
                    if (d.BeatmapId is int beatmapId && d.CanBeResolved && !d.Resolved)
                    {
                        UnresolvedByBeatmap[beatmapId] = UnresolvedByBeatmap.GetValueOrDefault(beatmapId) + 1;

                        if (state.Beatmaps.GetValueOrDefault(beatmapId) is DiscussionBeatmap beatmap)
                            UnresolvedByMode[beatmap.Mode] = UnresolvedByMode.GetValueOrDefault(beatmap.Mode) + 1;
                    }
                }
            }

            private static readonly DiscussionPage[] modes = { DiscussionPage.Reviews, DiscussionPage.GeneralAll, DiscussionPage.General, DiscussionPage.Timeline };

            private static List<BeatmapDiscussion> sort(List<BeatmapDiscussion> discussions, DiscussionSort sort)
            {
                var list = discussions.ToList();

                list.Sort((a, b) =>
                {
                    // stick the mapper note, but not for timeline sort, and if both are mapper notes do base comparison.
                    if (sort != DiscussionSort.Timeline && (a.MessageType == @"mapper_note" || b.MessageType == @"mapper_note") && a.MessageType != b.MessageType)
                        return a.MessageType == @"mapper_note" ? -1 : 1;

                    switch (sort)
                    {
                        case DiscussionSort.CreatedAt:
                            return a.CreatedAt == b.CreatedAt ? a.Id.CompareTo(b.Id) : a.CreatedAt.CompareTo(b.CreatedAt);

                        case DiscussionSort.Timeline:
                            if (a.Timestamp == null || b.Timestamp == null)
                                return 0;

                            return a.Timestamp == b.Timestamp ? a.Id.CompareTo(b.Id) : a.Timestamp.Value.CompareTo(b.Timestamp.Value);

                        default:
                            var aLast = a.LastPostAt ?? a.CreatedAt;
                            var bLast = b.LastPostAt ?? b.CreatedAt;
                            return aLast == bLast ? b.Id.CompareTo(a.Id) : bLast.CompareTo(aLast);
                    }
                });

                return list;
            }
        }
    }
}
