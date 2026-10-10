// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopNominatorsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopNominators";

        /// <summary>
        /// "request"
        /// </summary>
        public static LocalisableString NominatorsScreen => new TranslatableString(getKey(@"nominators_screen"), @"request");

        /// <summary>
        /// "Request mode"
        /// </summary>
        public static LocalisableString NominatorsEditorMode => new TranslatableString(getKey(@"nominators_editor_mode"), @"Request mode");

        /// <summary>
        /// "Not asked"
        /// </summary>
        public static LocalisableString StatusNotAsked => new TranslatableString(getKey(@"status_not_asked"), @"Not asked");

        /// <summary>
        /// "Declined"
        /// </summary>
        public static LocalisableString StatusDeclined => new TranslatableString(getKey(@"status_declined"), @"Declined");

        /// <summary>
        /// "Pending"
        /// </summary>
        public static LocalisableString StatusPending => new TranslatableString(getKey(@"status_pending"), @"Pending");

        /// <summary>
        /// "Maybe"
        /// </summary>
        public static LocalisableString StatusMaybe => new TranslatableString(getKey(@"status_maybe"), @"Maybe");

        /// <summary>
        /// "Unlikely"
        /// </summary>
        public static LocalisableString StatusUnlikely => new TranslatableString(getKey(@"status_unlikely"), @"Unlikely");

        /// <summary>
        /// "Accepted"
        /// </summary>
        public static LocalisableString StatusAccepted => new TranslatableString(getKey(@"status_accepted"), @"Accepted");

        /// <summary>
        /// "NAT"
        /// </summary>
        public static LocalisableString LevelNat => new TranslatableString(getKey(@"level_nat"), @"NAT");

        /// <summary>
        /// "BN"
        /// </summary>
        public static LocalisableString LevelFull => new TranslatableString(getKey(@"level_full"), @"BN");

        /// <summary>
        /// "Probationary BN"
        /// </summary>
        public static LocalisableString LevelProbation => new TranslatableString(getKey(@"level_probation"), @"Probationary BN");

        /// <summary>
        /// "All modes"
        /// </summary>
        public static LocalisableString ModeGeneral => new TranslatableString(getKey(@"mode_general"), @"All modes");

        /// <summary>
        /// "Unknown"
        /// </summary>
        public static LocalisableString RankStatusUnknown => new TranslatableString(getKey(@"rank_status_unknown"), @"Unknown");

        /// <summary>
        /// "Graveyard"
        /// </summary>
        public static LocalisableString RankStatusGraveyard => new TranslatableString(getKey(@"rank_status_graveyard"), @"Graveyard");

        /// <summary>
        /// "WIP"
        /// </summary>
        public static LocalisableString RankStatusWip => new TranslatableString(getKey(@"rank_status_wip"), @"WIP");

        /// <summary>
        /// "Pending"
        /// </summary>
        public static LocalisableString RankStatusPending => new TranslatableString(getKey(@"rank_status_pending"), @"Pending");

        /// <summary>
        /// "Qualified"
        /// </summary>
        public static LocalisableString RankStatusQualified => new TranslatableString(getKey(@"rank_status_qualified"), @"Qualified");

        /// <summary>
        /// "Ranked"
        /// </summary>
        public static LocalisableString RankStatusRanked => new TranslatableString(getKey(@"rank_status_ranked"), @"Ranked");

        /// <summary>
        /// "Approved"
        /// </summary>
        public static LocalisableString RankStatusApproved => new TranslatableString(getKey(@"rank_status_approved"), @"Approved");

        /// <summary>
        /// "Loved"
        /// </summary>
        public static LocalisableString RankStatusLoved => new TranslatableString(getKey(@"rank_status_loved"), @"Loved");

        /// <summary>
        /// "No priority"
        /// </summary>
        public static LocalisableString PriorityNone => new TranslatableString(getKey(@"priority_none"), @"No priority");

        /// <summary>
        /// "Low priority"
        /// </summary>
        public static LocalisableString PriorityLow => new TranslatableString(getKey(@"priority_low"), @"Low priority");

        /// <summary>
        /// "Medium priority"
        /// </summary>
        public static LocalisableString PriorityMedium => new TranslatableString(getKey(@"priority_medium"), @"Medium priority");

        /// <summary>
        /// "High priority"
        /// </summary>
        public static LocalisableString PriorityHigh => new TranslatableString(getKey(@"priority_high"), @"High priority");

        /// <summary>
        /// "Change the priority of this beatmap set"
        /// </summary>
        public static LocalisableString ChangePriority => new TranslatableString(getKey(@"change_priority"), @"Change the priority of this beatmap set");

        /// <summary>
        /// "Change the status"
        /// </summary>
        public static LocalisableString ChangeStatus => new TranslatableString(getKey(@"change_status"), @"Change the status");

        /// <summary>
        /// "This person is no longer BN or NAT, so the status can't be changed anymore."
        /// </summary>
        public static LocalisableString RemovedStatusLocked => new TranslatableString(getKey(@"removed_status_locked"), @"This person is no longer BN or NAT, so the status can't be changed anymore.");

        /// <summary>
        /// "Filters"
        /// </summary>
        public static LocalisableString Filters => new TranslatableString(getKey(@"filters"), @"Filters");

        /// <summary>
        /// "{0} active"
        /// </summary>
        public static LocalisableString ActiveFilters(int count) => new TranslatableString(getKey(@"active_filters"), @"{0} active", count);

        /// <summary>
        /// "Clear all"
        /// </summary>
        public static LocalisableString ClearFilters => new TranslatableString(getKey(@"clear_filters"), @"Clear all");

        /// <summary>
        /// "Search by name..."
        /// </summary>
        public static LocalisableString SearchPlaceholder => new TranslatableString(getKey(@"search_placeholder"), @"Search by name...");

        /// <summary>
        /// "Modes"
        /// </summary>
        public static LocalisableString Modes => new TranslatableString(getKey(@"modes"), @"Modes");

        /// <summary>
        /// "Group"
        /// </summary>
        public static LocalisableString Group => new TranslatableString(getKey(@"group"), @"Group");

        /// <summary>
        /// "All groups"
        /// </summary>
        public static LocalisableString AllGroups => new TranslatableString(getKey(@"all_groups"), @"All groups");

        /// <summary>
        /// "Status"
        /// </summary>
        public static LocalisableString Status => new TranslatableString(getKey(@"status"), @"Status");

        /// <summary>
        /// "All statuses"
        /// </summary>
        public static LocalisableString AllStatuses => new TranslatableString(getKey(@"all_statuses"), @"All statuses");

        /// <summary>
        /// "Spoken language"
        /// </summary>
        public static LocalisableString SpokenLanguage => new TranslatableString(getKey(@"spoken_language"), @"Spoken language");

        /// <summary>
        /// "Any spoken language"
        /// </summary>
        public static LocalisableString AnySpokenLanguage => new TranslatableString(getKey(@"any_spoken_language"), @"Any spoken language");

        /// <summary>
        /// "Genre"
        /// </summary>
        public static LocalisableString Genre => new TranslatableString(getKey(@"genre"), @"Genre");

        /// <summary>
        /// "Compares the genre of the beatmap set with the genre preferences of the nominators."
        /// </summary>
        public static LocalisableString GenreHint => new TranslatableString(getKey(@"genre_hint"), @"Compares the genre of the beatmap set with the genre preferences of the nominators.");

        /// <summary>
        /// "Song language"
        /// </summary>
        public static LocalisableString SongLanguage => new TranslatableString(getKey(@"song_language"), @"Song language");

        /// <summary>
        /// "Compares the song language of the beatmap set with the language preferences of the nominators."
        /// </summary>
        public static LocalisableString SongLanguageHint => new TranslatableString(getKey(@"song_language_hint"), @"Compares the song language of the beatmap set with the language preferences of the nominators.");

        /// <summary>
        /// "Any"
        /// </summary>
        public static LocalisableString PreferenceAny => new TranslatableString(getKey(@"preference_any"), @"Any");

        /// <summary>
        /// "Likes it"
        /// </summary>
        public static LocalisableString PreferenceLikes => new TranslatableString(getKey(@"preference_likes"), @"Likes it");

        /// <summary>
        /// "Excludes it"
        /// </summary>
        public static LocalisableString PreferenceExcludes => new TranslatableString(getKey(@"preference_excludes"), @"Excludes it");

        /// <summary>
        /// "No preference"
        /// </summary>
        public static LocalisableString PreferenceNoSignal => new TranslatableString(getKey(@"preference_no_signal"), @"No preference");

        /// <summary>
        /// "Last opened for requests"
        /// </summary>
        public static LocalisableString LastOpenedFilter => new TranslatableString(getKey(@"last_opened_filter"), @"Last opened for requests");

        /// <summary>
        /// "When the nominators last opened their queue for requests."
        /// </summary>
        public static LocalisableString LastOpenedFilterHint => new TranslatableString(getKey(@"last_opened_filter_hint"), @"When the nominators last opened their queue for requests.");

        /// <summary>
        /// "Any time"
        /// </summary>
        public static LocalisableString LastOpenedAnyTime => new TranslatableString(getKey(@"last_opened_any_time"), @"Any time");

        /// <summary>
        /// "Within 7 days"
        /// </summary>
        public static LocalisableString LastOpenedSevenDays => new TranslatableString(getKey(@"last_opened_seven_days"), @"Within 7 days");

        /// <summary>
        /// "Within 30 days"
        /// </summary>
        public static LocalisableString LastOpenedThirtyDays => new TranslatableString(getKey(@"last_opened_thirty_days"), @"Within 30 days");

        /// <summary>
        /// "Within 90 days"
        /// </summary>
        public static LocalisableString LastOpenedNinetyDays => new TranslatableString(getKey(@"last_opened_ninety_days"), @"Within 90 days");

        /// <summary>
        /// "Sort by"
        /// </summary>
        public static LocalisableString Sort => new TranslatableString(getKey(@"sort"), @"Sort by");

        /// <summary>
        /// "Name (A-Z)"
        /// </summary>
        public static LocalisableString SortNameAscending => new TranslatableString(getKey(@"sort_name_ascending"), @"Name (A-Z)");

        /// <summary>
        /// "Name (Z-A)"
        /// </summary>
        public static LocalisableString SortNameDescending => new TranslatableString(getKey(@"sort_name_descending"), @"Name (Z-A)");

        /// <summary>
        /// "Last opened (newest)"
        /// </summary>
        public static LocalisableString SortLastOpenedNewest => new TranslatableString(getKey(@"sort_last_opened_newest"), @"Last opened (newest)");

        /// <summary>
        /// "Last opened (oldest)"
        /// </summary>
        public static LocalisableString SortLastOpenedOldest => new TranslatableString(getKey(@"sort_last_opened_oldest"), @"Last opened (oldest)");

        /// <summary>
        /// "Status"
        /// </summary>
        public static LocalisableString SortStatus => new TranslatableString(getKey(@"sort_status"), @"Status");

        /// <summary>
        /// "Open only"
        /// </summary>
        public static LocalisableString OpenOnly => new TranslatableString(getKey(@"open_only"), @"Open only");

        /// <summary>
        /// "Only shows nominators who are currently open for requests."
        /// </summary>
        public static LocalisableString OpenOnlyHint => new TranslatableString(getKey(@"open_only_hint"), @"Only shows nominators who are currently open for requests.");

        /// <summary>
        /// "Hide removed"
        /// </summary>
        public static LocalisableString HideRemoved => new TranslatableString(getKey(@"hide_removed"), @"Hide removed");

        /// <summary>
        /// "Hides people who are no longer BN or NAT."
        /// </summary>
        public static LocalisableString HideRemovedHint => new TranslatableString(getKey(@"hide_removed_hint"), @"Hides people who are no longer BN or NAT.");

        /// <summary>
        /// "Showing {0} of {1} nominators"
        /// </summary>
        public static LocalisableString ShowingNominators(int shown, int total) => new TranslatableString(getKey(@"showing_nominators"), @"Showing {0} of {1} nominators", shown, total);

        /// <summary>
        /// "No nominators match the filters."
        /// </summary>
        public static LocalisableString NoNominatorsMatch => new TranslatableString(getKey(@"no_nominators_match"), @"No nominators match the filters.");

        /// <summary>
        /// "Select multiple"
        /// </summary>
        public static LocalisableString SelectMultiple => new TranslatableString(getKey(@"select_multiple"), @"Select multiple");

        /// <summary>
        /// "Cancel selection"
        /// </summary>
        public static LocalisableString CancelSelection => new TranslatableString(getKey(@"cancel_selection"), @"Cancel selection");

        /// <summary>
        /// "Select all shown"
        /// </summary>
        public static LocalisableString SelectAllShown => new TranslatableString(getKey(@"select_all_shown"), @"Select all shown");

        /// <summary>
        /// "{0} selected"
        /// </summary>
        public static LocalisableString SelectedCount(int count) => new TranslatableString(getKey(@"selected_count"), @"{0} selected", count);

        /// <summary>
        /// "Set to:"
        /// </summary>
        public static LocalisableString SetStatusTo => new TranslatableString(getKey(@"set_status_to"), @"Set to:");

        /// <summary>
        /// "Clear"
        /// </summary>
        public static LocalisableString ClearSelection => new TranslatableString(getKey(@"clear_selection"), @"Clear");

        /// <summary>
        /// "Last opened"
        /// </summary>
        public static LocalisableString LastOpened => new TranslatableString(getKey(@"last_opened"), @"Last opened");

        /// <summary>
        /// "Never opened for requests"
        /// </summary>
        public static LocalisableString NeverOpened => new TranslatableString(getKey(@"never_opened"), @"Never opened for requests");

        /// <summary>
        /// "No comments"
        /// </summary>
        public static LocalisableString NoComments => new TranslatableString(getKey(@"no_comments"), @"No comments");

        /// <summary>
        /// "+{0}"
        /// </summary>
        public static LocalisableString MoreComments(int count) => new TranslatableString(getKey(@"more_comments"), @"+{0}", count);

        /// <summary>
        /// "Not asked yet"
        /// </summary>
        public static LocalisableString NotAskedYet => new TranslatableString(getKey(@"not_asked_yet"), @"Not asked yet");

        /// <summary>
        /// "Removed"
        /// </summary>
        public static LocalisableString Removed => new TranslatableString(getKey(@"removed"), @"Removed");

        /// <summary>
        /// "No longer BN or NAT"
        /// </summary>
        public static LocalisableString RemovedTooltip => new TranslatableString(getKey(@"removed_tooltip"), @"No longer BN or NAT");

        /// <summary>
        /// "Open"
        /// </summary>
        public static LocalisableString Open => new TranslatableString(getKey(@"open"), @"Open");

        /// <summary>
        /// "Open for requests"
        /// </summary>
        public static LocalisableString OpenTooltip => new TranslatableString(getKey(@"open_tooltip"), @"Open for requests");

        /// <summary>
        /// "Closed"
        /// </summary>
        public static LocalisableString Closed => new TranslatableString(getKey(@"closed"), @"Closed");

        /// <summary>
        /// "Not open for requests"
        /// </summary>
        public static LocalisableString ClosedTooltip => new TranslatableString(getKey(@"closed_tooltip"), @"Not open for requests");

        /// <summary>
        /// "The genre of this beatmap set is one they like."
        /// </summary>
        public static LocalisableString GenreMatchesTooltip => new TranslatableString(getKey(@"genre_matches_tooltip"), @"The genre of this beatmap set is one they like.");

        /// <summary>
        /// "They excluded the genre of this beatmap set."
        /// </summary>
        public static LocalisableString GenreExcludedTooltip => new TranslatableString(getKey(@"genre_excluded_tooltip"), @"They excluded the genre of this beatmap set.");

        /// <summary>
        /// "The song language of this beatmap set is one they like."
        /// </summary>
        public static LocalisableString LanguageMatchesTooltip => new TranslatableString(getKey(@"language_matches_tooltip"), @"The song language of this beatmap set is one they like.");

        /// <summary>
        /// "Language"
        /// </summary>
        public static LocalisableString LanguageTag => new TranslatableString(getKey(@"language_tag"), @"Language");

        /// <summary>
        /// "They excluded the song language of this beatmap set."
        /// </summary>
        public static LocalisableString LanguageExcludedTooltip => new TranslatableString(getKey(@"language_excluded_tooltip"), @"They excluded the song language of this beatmap set.");

        /// <summary>
        /// "Show details"
        /// </summary>
        public static LocalisableString ShowDetails => new TranslatableString(getKey(@"show_details"), @"Show details");

        /// <summary>
        /// "Set status"
        /// </summary>
        public static LocalisableString SetStatus => new TranslatableString(getKey(@"set_status"), @"Set status");

        /// <summary>
        /// "Open request queue"
        /// </summary>
        public static LocalisableString OpenRequestQueue => new TranslatableString(getKey(@"open_request_queue"), @"Open request queue");

        /// <summary>
        /// "View profile"
        /// </summary>
        public static LocalisableString ViewProfile => new TranslatableString(getKey(@"view_profile"), @"View profile");

        /// <summary>
        /// "by {0}"
        /// </summary>
        public static LocalisableString ByArtist(string artist) => new TranslatableString(getKey(@"by_artist"), @"by {0}", artist);

        /// <summary>
        /// "by {0} · mapped by {1}"
        /// </summary>
        public static LocalisableString ByArtistMappedBy(string artist, string mapper) => new TranslatableString(getKey(@"by_artist_mapped_by"), @"by {0} · mapped by {1}", artist, mapper);

        /// <summary>
        /// "{0} difficulties"
        /// </summary>
        public static LocalisableString DifficultyCount(int count) => new TranslatableString(getKey(@"difficulty_count"), @"{0} difficulties", count);

        /// <summary>
        /// "The main mode, which needs two nominations."
        /// </summary>
        public static LocalisableString MainModeTooltip => new TranslatableString(getKey(@"main_mode_tooltip"), @"The main mode, which needs two nominations.");

        /// <summary>
        /// "Not tracked yet"
        /// </summary>
        public static LocalisableString NotTrackedYet => new TranslatableString(getKey(@"not_tracked_yet"), @"Not tracked yet");

        /// <summary>
        /// "The beatmap set is added to the BN Tracker as soon as something is changed."
        /// </summary>
        public static LocalisableString NotTrackedYetTooltip => new TranslatableString(getKey(@"not_tracked_yet_tooltip"), @"The beatmap set is added to the BN Tracker as soon as something is changed.");

        /// <summary>
        /// "Fully nominated"
        /// </summary>
        public static LocalisableString FullyNominated => new TranslatableString(getKey(@"fully_nominated"), @"Fully nominated");

        /// <summary>
        /// "{0} of {1} nominations"
        /// </summary>
        public static LocalisableString NominationProgress(int filled, int required) => new TranslatableString(getKey(@"nomination_progress"), @"{0} of {1} nominations", filled, required);

        /// <summary>
        /// "{0}: {1} of {2} nominations"
        /// </summary>
        public static LocalisableString ModeProgressTooltip(LocalisableString mode, int filled, int required) => new TranslatableString(getKey(@"mode_progress_tooltip"), @"{0}: {1} of {2} nominations", mode, filled, required);

        /// <summary>
        /// "View on osu!"
        /// </summary>
        public static LocalisableString ViewOnOsu => new TranslatableString(getKey(@"view_on_osu"), @"View on osu!");

        /// <summary>
        /// "View on the BN Tracker website"
        /// </summary>
        public static LocalisableString ViewOnBnTracker => new TranslatableString(getKey(@"view_on_bn_tracker"), @"View on the BN Tracker website");

        /// <summary>
        /// "Refresh"
        /// </summary>
        public static LocalisableString Refresh => new TranslatableString(getKey(@"refresh"), @"Refresh");

        /// <summary>
        /// "Stop tracking"
        /// </summary>
        public static LocalisableString StopTracking => new TranslatableString(getKey(@"stop_tracking"), @"Stop tracking");

        /// <summary>
        /// "Stop tracking this beatmap set?"
        /// </summary>
        public static LocalisableString StopTrackingConfirmation => new TranslatableString(getKey(@"stop_tracking_confirmation"), @"Stop tracking this beatmap set?");

        /// <summary>
        /// "All statuses, comments and activity of it are deleted from the BN Tracker."
        /// </summary>
        public static LocalisableString StopTrackingConfirmationBody => new TranslatableString(getKey(@"stop_tracking_confirmation_body"), @"All statuses, comments and activity of it are deleted from the BN Tracker.");

        /// <summary>
        /// "Back"
        /// </summary>
        public static LocalisableString Back => new TranslatableString(getKey(@"back"), @"Back");

        /// <summary>
        /// "Send message"
        /// </summary>
        public static LocalisableString SendMessage => new TranslatableString(getKey(@"send_message"), @"Send message");

        /// <summary>
        /// "Copy username"
        /// </summary>
        public static LocalisableString CopyUsername => new TranslatableString(getKey(@"copy_username"), @"Copy username");

        /// <summary>
        /// "Copied "{0}" to the clipboard."
        /// </summary>
        public static LocalisableString UsernameCopied(string username) => new TranslatableString(getKey(@"username_copied"), @"Copied ""{0}"" to the clipboard.", username);

        /// <summary>
        /// "This person is no longer BN or NAT. The history with them is kept, but the status can't be changed anymore."
        /// </summary>
        public static LocalisableString RemovedNotice => new TranslatableString(getKey(@"removed_notice"), @"This person is no longer BN or NAT. The history with them is kept, but the status can't be changed anymore.");

        /// <summary>
        /// "This person is no longer BN or NAT since {0}. The history with them is kept, but the status can't be changed anymore."
        /// </summary>
        public static LocalisableString RemovedNoticeSince(LocalisableString date) => new TranslatableString(getKey(@"removed_notice_since"), @"This person is no longer BN or NAT since {0}. The history with them is kept, but the status can't be changed anymore.", date);

        /// <summary>
        /// "How to request"
        /// </summary>
        public static LocalisableString HowToRequest => new TranslatableString(getKey(@"how_to_request"), @"How to request");

        /// <summary>
        /// "Not open for requests at the moment."
        /// </summary>
        public static LocalisableString NotOpenForRequests => new TranslatableString(getKey(@"not_open_for_requests"), @"Not open for requests at the moment.");

        /// <summary>
        /// "Personal queue"
        /// </summary>
        public static LocalisableString PersonalQueue => new TranslatableString(getKey(@"personal_queue"), @"Personal queue");

        /// <summary>
        /// "Game chat"
        /// </summary>
        public static LocalisableString GameChat => new TranslatableString(getKey(@"game_chat"), @"Game chat");

        /// <summary>
        /// "Not specified"
        /// </summary>
        public static LocalisableString NotSpecified => new TranslatableString(getKey(@"not_specified"), @"Not specified");

        /// <summary>
        /// "Request queue"
        /// </summary>
        public static LocalisableString RequestQueue => new TranslatableString(getKey(@"request_queue"), @"Request queue");

        /// <summary>
        /// "Last opened for requests"
        /// </summary>
        public static LocalisableString LastOpenedForRequests => new TranslatableString(getKey(@"last_opened_for_requests"), @"Last opened for requests");

        /// <summary>
        /// "Preferences"
        /// </summary>
        public static LocalisableString Preferences => new TranslatableString(getKey(@"preferences"), @"Preferences");

        /// <summary>
        /// "Values in red are excluded."
        /// </summary>
        public static LocalisableString PreferencesHint => new TranslatableString(getKey(@"preferences_hint"), @"Values in red are excluded.");

        /// <summary>
        /// "Genres"
        /// </summary>
        public static LocalisableString PreferenceGenres => new TranslatableString(getKey(@"preference_genres"), @"Genres");

        /// <summary>
        /// "Song languages"
        /// </summary>
        public static LocalisableString PreferenceSongLanguages => new TranslatableString(getKey(@"preference_song_languages"), @"Song languages");

        /// <summary>
        /// "Details"
        /// </summary>
        public static LocalisableString PreferenceDetails => new TranslatableString(getKey(@"preference_details"), @"Details");

        /// <summary>
        /// "Mapper experience"
        /// </summary>
        public static LocalisableString PreferenceMappers => new TranslatableString(getKey(@"preference_mappers"), @"Mapper experience");

        /// <summary>
        /// "osu! style"
        /// </summary>
        public static LocalisableString PreferenceOsuStyles => new TranslatableString(getKey(@"preference_osu_styles"), @"osu! style");

        /// <summary>
        /// "osu!taiko style"
        /// </summary>
        public static LocalisableString PreferenceTaikoStyles => new TranslatableString(getKey(@"preference_taiko_styles"), @"osu!taiko style");

        /// <summary>
        /// "osu!catch style"
        /// </summary>
        public static LocalisableString PreferenceCatchStyles => new TranslatableString(getKey(@"preference_catch_styles"), @"osu!catch style");

        /// <summary>
        /// "osu!mania style"
        /// </summary>
        public static LocalisableString PreferenceManiaStyles => new TranslatableString(getKey(@"preference_mania_styles"), @"osu!mania style");

        /// <summary>
        /// "Keymodes"
        /// </summary>
        public static LocalisableString PreferenceKeymodes => new TranslatableString(getKey(@"preference_keymodes"), @"Keymodes");

        /// <summary>
        /// "Speaks"
        /// </summary>
        public static LocalisableString PreferenceSpokenLanguages => new TranslatableString(getKey(@"preference_spoken_languages"), @"Speaks");

        /// <summary>
        /// "Excluded"
        /// </summary>
        public static LocalisableString Excluded => new TranslatableString(getKey(@"excluded"), @"Excluded");

        /// <summary>
        /// "BN / NAT history"
        /// </summary>
        public static LocalisableString History => new TranslatableString(getKey(@"history"), @"BN / NAT history");

        /// <summary>
        /// "First joined"
        /// </summary>
        public static LocalisableString FirstJoined => new TranslatableString(getKey(@"first_joined"), @"First joined");

        /// <summary>
        /// "Joined {0} · {1}"
        /// </summary>
        public static LocalisableString HistoryJoined(LocalisableString group, LocalisableString mode) => new TranslatableString(getKey(@"history_joined"), @"Joined {0} · {1}", group, mode);

        /// <summary>
        /// "Left {0} · {1}"
        /// </summary>
        public static LocalisableString HistoryLeft(LocalisableString group, LocalisableString mode) => new TranslatableString(getKey(@"history_left"), @"Left {0} · {1}", group, mode);

        /// <summary>
        /// "First seen by the BN Tracker"
        /// </summary>
        public static LocalisableString FirstSeen => new TranslatableString(getKey(@"first_seen"), @"First seen by the BN Tracker");

        /// <summary>
        /// "Last updated"
        /// </summary>
        public static LocalisableString LastSynced => new TranslatableString(getKey(@"last_synced"), @"Last updated");

        /// <summary>
        /// "Comments"
        /// </summary>
        public static LocalisableString Comments => new TranslatableString(getKey(@"comments"), @"Comments");

        /// <summary>
        /// "Add a comment..."
        /// </summary>
        public static LocalisableString AddCommentPlaceholder => new TranslatableString(getKey(@"add_comment_placeholder"), @"Add a comment...");

        /// <summary>
        /// "Add"
        /// </summary>
        public static LocalisableString AddComment => new TranslatableString(getKey(@"add_comment"), @"Add");

        /// <summary>
        /// "No comments yet."
        /// </summary>
        public static LocalisableString NoCommentsYet => new TranslatableString(getKey(@"no_comments_yet"), @"No comments yet.");

        /// <summary>
        /// "Delete comment"
        /// </summary>
        public static LocalisableString DeleteComment => new TranslatableString(getKey(@"delete_comment"), @"Delete comment");

        /// <summary>
        /// "Delete this comment?"
        /// </summary>
        public static LocalisableString DeleteCommentConfirmation => new TranslatableString(getKey(@"delete_comment_confirmation"), @"Delete this comment?");

        /// <summary>
        /// "It can't be restored."
        /// </summary>
        public static LocalisableString DeleteCommentConfirmationBody => new TranslatableString(getKey(@"delete_comment_confirmation_body"), @"It can't be restored.");

        /// <summary>
        /// "Activity"
        /// </summary>
        public static LocalisableString Activity => new TranslatableString(getKey(@"activity"), @"Activity");

        /// <summary>
        /// "No status changes yet."
        /// </summary>
        public static LocalisableString NoActivityYet => new TranslatableString(getKey(@"no_activity_yet"), @"No status changes yet.");

        /// <summary>
        /// "[image]"
        /// </summary>
        public static LocalisableString Image => new TranslatableString(getKey(@"image"), @"[image]");

        /// <summary>
        /// "Not submitted yet"
        /// </summary>
        public static LocalisableString NotSubmittedTitle => new TranslatableString(getKey(@"not_submitted_title"), @"Not submitted yet");

        /// <summary>
        /// "Nominators can only be tracked for beatmap sets which were submitted to osu!."
        /// </summary>
        public static LocalisableString NotSubmittedDescription => new TranslatableString(getKey(@"not_submitted_description"), @"Nominators can only be tracked for beatmap sets which were submitted to osu!.");

        /// <summary>
        /// "No BN Tracker server"
        /// </summary>
        public static LocalisableString NoServerTitle => new TranslatableString(getKey(@"no_server_title"), @"No BN Tracker server");

        /// <summary>
        /// "Set the address of the BN Tracker server in the settings."
        /// </summary>
        public static LocalisableString NoServerDescription => new TranslatableString(getKey(@"no_server_description"), @"Set the address of the BN Tracker server in the settings.");

        /// <summary>
        /// "Open settings"
        /// </summary>
        public static LocalisableString OpenSettings => new TranslatableString(getKey(@"open_settings"), @"Open settings");

        /// <summary>
        /// "Not signed in to the BN Tracker"
        /// </summary>
        public static LocalisableString SignedOutTitle => new TranslatableString(getKey(@"signed_out_title"), @"Not signed in to the BN Tracker");

        /// <summary>
        /// "Sign in with your osu! account, or in the browser."
        /// </summary>
        public static LocalisableString SignedOutDescription => new TranslatableString(getKey(@"signed_out_description"), @"Sign in with your osu! account, or in the browser.");

        /// <summary>
        /// "You aren't logged in to osu!, so sign in to the BN Tracker in the browser."
        /// </summary>
        public static LocalisableString SignedOutOfflineDescription => new TranslatableString(getKey(@"signed_out_offline_description"), @"You aren't logged in to osu!, so sign in to the BN Tracker in the browser.");

        /// <summary>
        /// "Sign in with osu! account"
        /// </summary>
        public static LocalisableString SignInWithOsu => new TranslatableString(getKey(@"sign_in_with_osu"), @"Sign in with osu! account");

        /// <summary>
        /// "Sign in with browser"
        /// </summary>
        public static LocalisableString SignInWithBrowser => new TranslatableString(getKey(@"sign_in_with_browser"), @"Sign in with browser");

        /// <summary>
        /// "Signing in..."
        /// </summary>
        public static LocalisableString SigningIn => new TranslatableString(getKey(@"signing_in"), @"Signing in...");

        /// <summary>
        /// "Sign in in the browser"
        /// </summary>
        public static LocalisableString SignInWithBrowserTitle => new TranslatableString(getKey(@"sign_in_with_browser_title"), @"Sign in in the browser");

        /// <summary>
        /// "Check that the browser shows this code, then allow the sign-in."
        /// </summary>
        public static LocalisableString SignInWithBrowserDescription => new TranslatableString(getKey(@"sign_in_with_browser_description"), @"Check that the browser shows this code, then allow the sign-in.");

        /// <summary>
        /// "Open browser again"
        /// </summary>
        public static LocalisableString OpenBrowserAgain => new TranslatableString(getKey(@"open_browser_again"), @"Open browser again");

        /// <summary>
        /// "Copy link"
        /// </summary>
        public static LocalisableString CopyLink => new TranslatableString(getKey(@"copy_link"), @"Copy link");

        /// <summary>
        /// "Cancel"
        /// </summary>
        public static LocalisableString Cancel => new TranslatableString(getKey(@"cancel"), @"Cancel");

        /// <summary>
        /// "Loading nominators..."
        /// </summary>
        public static LocalisableString Loading => new TranslatableString(getKey(@"loading"), @"Loading nominators...");

        /// <summary>
        /// "Loading failed"
        /// </summary>
        public static LocalisableString LoadingFailed => new TranslatableString(getKey(@"loading_failed"), @"Loading failed");

        /// <summary>
        /// "Retry"
        /// </summary>
        public static LocalisableString Retry => new TranslatableString(getKey(@"retry"), @"Retry");

        /// <summary>
        /// "Not your beatmap set"
        /// </summary>
        public static LocalisableString NotOwnedTitle => new TranslatableString(getKey(@"not_owned_title"), @"Not your beatmap set");

        /// <summary>
        /// "This beatmap set was uploaded by {0}. Only your own beatmap sets can be tracked."
        /// </summary>
        public static LocalisableString NotOwnedDescription(string owner) => new TranslatableString(getKey(@"not_owned_description"), @"This beatmap set was uploaded by {0}. Only your own beatmap sets can be tracked.", owner);

        /// <summary>
        /// "This beatmap set was uploaded by someone else. Only your own beatmap sets can be tracked."
        /// </summary>
        public static LocalisableString NotOwnedDescriptionUnknown => new TranslatableString(getKey(@"not_owned_description_unknown"), @"This beatmap set was uploaded by someone else. Only your own beatmap sets can be tracked.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
