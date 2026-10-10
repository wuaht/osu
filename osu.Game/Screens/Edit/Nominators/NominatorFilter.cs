// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Online.BnTracker;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// What the nominators of the "request" screen are filtered and sorted by, like on the BN Tracker website.
    /// </summary>
    public class NominatorFilter
    {
        public readonly Bindable<string> Search = new Bindable<string>(string.Empty);

        /// <summary>
        /// The modes of the beatmap set to show nominators of. Empty to show all.
        /// </summary>
        public readonly BindableList<BnGameMode> Modes = new BindableList<BnGameMode>();

        public readonly Bindable<NominatorGroupFilter> Group = new Bindable<NominatorGroupFilter>();

        public readonly Bindable<NominatorStatusFilter> Status = new Bindable<NominatorStatusFilter>();

        /// <summary>
        /// The language the nominators speak. Empty for any.
        /// </summary>
        public readonly Bindable<string> SpokenLanguage = new Bindable<string>(string.Empty);

        public readonly Bindable<NominatorPreferenceFilter> GenreMatch = new Bindable<NominatorPreferenceFilter>();

        public readonly Bindable<NominatorPreferenceFilter> LanguageMatch = new Bindable<NominatorPreferenceFilter>();

        public readonly Bindable<NominatorLastOpenedFilter> LastOpened = new Bindable<NominatorLastOpenedFilter>();

        public readonly Bindable<NominatorSortOrder> Sort = new Bindable<NominatorSortOrder>();

        public readonly BindableBool OpenOnly = new BindableBool();

        public readonly BindableBool HideRemoved = new BindableBool();

        /// <summary>
        /// Invoked when anything changes.
        /// </summary>
        public event Action? Changed;

        public NominatorFilter()
        {
            Search.BindValueChanged(_ => Changed?.Invoke());
            Modes.BindCollectionChanged((_, _) => Changed?.Invoke());
            Group.BindValueChanged(_ => Changed?.Invoke());
            Status.BindValueChanged(_ => Changed?.Invoke());
            SpokenLanguage.BindValueChanged(_ => Changed?.Invoke());
            GenreMatch.BindValueChanged(_ => Changed?.Invoke());
            LanguageMatch.BindValueChanged(_ => Changed?.Invoke());
            LastOpened.BindValueChanged(_ => Changed?.Invoke());
            Sort.BindValueChanged(_ => Changed?.Invoke());
            OpenOnly.BindValueChanged(_ => Changed?.Invoke());
            HideRemoved.BindValueChanged(_ => Changed?.Invoke());
        }

        /// <summary>
        /// How many filters narrow down the nominators, not counting the search and sort order.
        /// </summary>
        public int ActiveFilterCount =>
            (Modes.Count > 0 ? 1 : 0)
            + (Group.Value != NominatorGroupFilter.All ? 1 : 0)
            + (Status.Value != NominatorStatusFilter.All ? 1 : 0)
            + (!string.IsNullOrEmpty(SpokenLanguage.Value) ? 1 : 0)
            + (GenreMatch.Value != NominatorPreferenceFilter.Any ? 1 : 0)
            + (LanguageMatch.Value != NominatorPreferenceFilter.Any ? 1 : 0)
            + (LastOpened.Value != NominatorLastOpenedFilter.AnyTime ? 1 : 0)
            + (OpenOnly.Value ? 1 : 0)
            + (HideRemoved.Value ? 1 : 0);

        /// <summary>
        /// Resets the search, all filters and the sort order.
        /// </summary>
        public void Clear()
        {
            Search.SetDefault();
            Modes.Clear();
            Group.SetDefault();
            Status.SetDefault();
            SpokenLanguage.SetDefault();
            GenreMatch.SetDefault();
            LanguageMatch.SetDefault();
            LastOpened.SetDefault();
            OpenOnly.SetDefault();
            HideRemoved.SetDefault();
            Sort.SetDefault();
        }

        /// <summary>
        /// Filters and sorts the nominators.
        /// </summary>
        public IEnumerable<BnSetNominator> Apply(IEnumerable<BnSetNominator> nominators, DateTimeOffset now)
        {
            var items = nominators;

            string search = Search.Value.Trim();

            if (search.Length > 0)
                items = items.Where(n => n.Nominator.Username.Contains(search, StringComparison.OrdinalIgnoreCase));

            if (Modes.Count > 0)
            {
                var modes = Modes.ToHashSet();
                items = items.Where(n => n.RelevantModes.Any(m => m.Mode == BnGameMode.General || modes.Contains(m.Mode)));
            }

            if (getLevel(Group.Value) is BnNominatorLevel level)
                items = items.Where(n => n.RelevantModes.Any(m => m.Level == level));

            if (OpenOnly.Value)
                items = items.Where(n => n.Nominator.IsOpenForRequests);

            if (!string.IsNullOrEmpty(SpokenLanguage.Value))
                items = items.Where(n => n.Nominator.SpokenLanguages.Contains(SpokenLanguage.Value, StringComparer.OrdinalIgnoreCase));

            if (getStatus(Status.Value) is BnNominationStatus status)
                items = items.Where(n => n.Status == status);

            if (getDays(LastOpened.Value) is int days)
            {
                var cutoff = now.AddDays(-days);
                items = items.Where(n => n.Nominator.LastOpenedForRequests >= cutoff);
            }

            if (HideRemoved.Value)
                items = items.Where(n => !n.Nominator.IsRemoved);

            if (getMatch(GenreMatch.Value) is BnPreferenceMatch genreMatch)
                items = items.Where(n => n.PreferenceMatch.Genre == genreMatch);

            if (getMatch(LanguageMatch.Value) is BnPreferenceMatch languageMatch)
                items = items.Where(n => n.PreferenceMatch.Language == languageMatch);

            switch (Sort.Value)
            {
                case NominatorSortOrder.NameDescending:
                    return items.OrderByDescending(n => n.Nominator.Username, StringComparer.OrdinalIgnoreCase);

                case NominatorSortOrder.LastOpenedNewest:
                    return items.OrderByDescending(n => n.Nominator.LastOpenedForRequests ?? DateTimeOffset.MinValue)
                                .ThenBy(n => n.Nominator.Username, StringComparer.OrdinalIgnoreCase);

                case NominatorSortOrder.LastOpenedOldest:
                    return items.OrderBy(n => n.Nominator.LastOpenedForRequests ?? DateTimeOffset.MaxValue)
                                .ThenBy(n => n.Nominator.Username, StringComparer.OrdinalIgnoreCase);

                case NominatorSortOrder.Status:
                    return items.OrderBy(n => n.Status).ThenBy(n => n.Nominator.Username, StringComparer.OrdinalIgnoreCase);

                default:
                    return items.OrderBy(n => n.Nominator.Username, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static BnNominatorLevel? getLevel(NominatorGroupFilter group)
        {
            switch (group)
            {
                case NominatorGroupFilter.Nat:
                    return BnNominatorLevel.Evaluator;

                case NominatorGroupFilter.Bn:
                    return BnNominatorLevel.Full;

                case NominatorGroupFilter.ProbationaryBn:
                    return BnNominatorLevel.Probation;

                default:
                    return null;
            }
        }

        private static BnNominationStatus? getStatus(NominatorStatusFilter status)
            => status == NominatorStatusFilter.All ? null : (BnNominationStatus)(status - 1);

        private static BnPreferenceMatch? getMatch(NominatorPreferenceFilter match)
        {
            switch (match)
            {
                case NominatorPreferenceFilter.Likes:
                    return BnPreferenceMatch.Matches;

                case NominatorPreferenceFilter.Excludes:
                    return BnPreferenceMatch.Excluded;

                case NominatorPreferenceFilter.NoSignal:
                    return BnPreferenceMatch.Unknown;

                default:
                    return null;
            }
        }

        private static int? getDays(NominatorLastOpenedFilter lastOpened)
        {
            switch (lastOpened)
            {
                case NominatorLastOpenedFilter.SevenDays:
                    return 7;

                case NominatorLastOpenedFilter.ThirtyDays:
                    return 30;

                case NominatorLastOpenedFilter.NinetyDays:
                    return 90;

                default:
                    return null;
            }
        }
    }

    public enum NominatorGroupFilter
    {
        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.AllGroups))]
        All,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.LevelNat))]
        Nat,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.LevelFull))]
        Bn,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.LevelProbation))]
        ProbationaryBn,
    }

    /// <summary>
    /// <see cref="All"/>, followed by the values of <see cref="BnNominationStatus"/> in the same order.
    /// </summary>
    public enum NominatorStatusFilter
    {
        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.AllStatuses))]
        All,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.StatusNotAsked))]
        NotAsked,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.StatusDeclined))]
        Declined,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.StatusPending))]
        Pending,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.StatusMaybe))]
        Maybe,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.StatusUnlikely))]
        Unlikely,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.StatusAccepted))]
        Accepted,
    }

    public enum NominatorPreferenceFilter
    {
        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.PreferenceAny))]
        Any,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.PreferenceLikes))]
        Likes,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.PreferenceExcludes))]
        Excludes,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.PreferenceNoSignal))]
        NoSignal,
    }

    public enum NominatorLastOpenedFilter
    {
        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.LastOpenedAnyTime))]
        AnyTime,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.LastOpenedSevenDays))]
        SevenDays,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.LastOpenedThirtyDays))]
        ThirtyDays,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.LastOpenedNinetyDays))]
        NinetyDays,
    }

    public enum NominatorSortOrder
    {
        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.SortNameAscending))]
        NameAscending,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.SortNameDescending))]
        NameDescending,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.SortLastOpenedNewest))]
        LastOpenedNewest,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.SortLastOpenedOldest))]
        LastOpenedOldest,

        [LocalisableDescription(typeof(SlopNominatorsStrings), nameof(SlopNominatorsStrings.SortStatus))]
        Status,
    }
}
