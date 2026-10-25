// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SlopDiscussionStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopDiscussion";

        /// <summary>
        /// "discussion"
        /// </summary>
        public static LocalisableString DiscussionScreen => new TranslatableString(getKey(@"discussion_screen"), @"discussion");

        /// <summary>
        /// "Discussion mode"
        /// </summary>
        public static LocalisableString DiscussionEditorMode => new TranslatableString(getKey(@"discussion_editor_mode"), @"Discussion mode");

        /// <summary>
        /// "Loading discussion..."
        /// </summary>
        public static LocalisableString Loading => new TranslatableString(getKey(@"loading"), @"Loading discussion...");

        /// <summary>
        /// "The discussion couldn't be loaded."
        /// </summary>
        public static LocalisableString LoadingFailed => new TranslatableString(getKey(@"loading_failed"), @"The discussion couldn't be loaded.");

        /// <summary>
        /// "Retry"
        /// </summary>
        public static LocalisableString Retry => new TranslatableString(getKey(@"retry"), @"Retry");

        /// <summary>
        /// "This beatmap has been deleted."
        /// </summary>
        public static LocalisableString Deleted => new TranslatableString(getKey(@"deleted"), @"This beatmap has been deleted.");

        /// <summary>
        /// "(only moderators can see this)"
        /// </summary>
        public static LocalisableString DeletedModeratorsOnly => new TranslatableString(getKey(@"deleted_moderators_only"), @"(only moderators can see this)");

        /// <summary>
        /// "{0} unresolved issue"
        /// </summary>
        public static TranslatableString UnresolvedCountOne(string count) => new TranslatableString(getKey(@"unresolved_count_one"), @"{0} unresolved issue", count);

        /// <summary>
        /// "{0} unresolved issues"
        /// </summary>
        public static TranslatableString UnresolvedCountMany(string count) => new TranslatableString(getKey(@"unresolved_count_many"), @"{0} unresolved issues", count);

        /// <summary>
        /// "Guest difficulty by {0}"
        /// </summary>
        public static TranslatableString GuestDifficultyBy => new TranslatableString(getKey(@"guest_difficulty_by"), @"Guest difficulty by {0}");

        /// <summary>
        /// "Length (Drain length: {0})"
        /// </summary>
        public static TranslatableString StatLength(string hitLength) => new TranslatableString(getKey(@"stat_length"), @"Length (Drain length: {0})", hitLength);

        /// <summary>
        /// "BPM"
        /// </summary>
        public static LocalisableString StatBpm => new TranslatableString(getKey(@"stat_bpm"), @"BPM");

        /// <summary>
        /// "BPM (Online offset: {0})"
        /// </summary>
        public static TranslatableString StatBpmOffset(string offset) => new TranslatableString(getKey(@"stat_bpm_offset"), @"BPM (Online offset: {0})", offset);

        /// <summary>
        /// "Circle Count"
        /// </summary>
        public static LocalisableString StatCircles => new TranslatableString(getKey(@"stat_circles"), @"Circle Count");

        /// <summary>
        /// "Slider Count"
        /// </summary>
        public static LocalisableString StatSliders => new TranslatableString(getKey(@"stat_sliders"), @"Slider Count");

        /// <summary>
        /// "mapped by {0}"
        /// </summary>
        public static TranslatableString MappedBy => new TranslatableString(getKey(@"mapped_by"), @"mapped by {0}");

        /// <summary>
        /// "submitted {0}"
        /// </summary>
        public static TranslatableString DateSubmitted => new TranslatableString(getKey(@"date_submitted"), @"submitted {0}");

        /// <summary>
        /// "approved {0}"
        /// </summary>
        public static TranslatableString DateApproved => new TranslatableString(getKey(@"date_approved"), @"approved {0}");

        /// <summary>
        /// "loved {0}"
        /// </summary>
        public static TranslatableString DateLoved => new TranslatableString(getKey(@"date_loved"), @"loved {0}");

        /// <summary>
        /// "qualified {0}"
        /// </summary>
        public static TranslatableString DateQualified => new TranslatableString(getKey(@"date_qualified"), @"qualified {0}");

        /// <summary>
        /// "ranked {0}"
        /// </summary>
        public static TranslatableString DateRanked => new TranslatableString(getKey(@"date_ranked"), @"ranked {0}");

        /// <summary>
        /// "last updated {0}"
        /// </summary>
        public static TranslatableString DateUpdated => new TranslatableString(getKey(@"date_updated"), @"last updated {0}");

        /// <summary>
        /// "Watch"
        /// </summary>
        public static LocalisableString Watch => new TranslatableString(getKey(@"watch"), @"Watch");

        /// <summary>
        /// "Unwatch"
        /// </summary>
        public static LocalisableString Unwatch => new TranslatableString(getKey(@"unwatch"), @"Unwatch");

        /// <summary>
        /// "Beatmap Page"
        /// </summary>
        public static LocalisableString BeatmapPage => new TranslatableString(getKey(@"beatmap_page"), @"Beatmap Page");

        /// <summary>
        /// "Hype Train"
        /// </summary>
        public static LocalisableString HypeTrain => new TranslatableString(getKey(@"hype_train"), @"Hype Train");

        /// <summary>
        /// "Hype Beatmap!"
        /// </summary>
        public static LocalisableString HypeButton => new TranslatableString(getKey(@"hype_button"), @"Hype Beatmap!");

        /// <summary>
        /// "Already Hyped!"
        /// </summary>
        public static LocalisableString HypeButtonDone => new TranslatableString(getKey(@"hype_button_done"), @"Already Hyped!");

        /// <summary>
        /// "Nomination Status"
        /// </summary>
        public static LocalisableString NominationStatus => new TranslatableString(getKey(@"nomination_status"), @"Nomination Status");

        /// <summary>
        /// "Love"
        /// </summary>
        public static LocalisableString Love => new TranslatableString(getKey(@"love"), @"Love");

        /// <summary>
        /// "Remove from Loved"
        /// </summary>
        public static LocalisableString RemoveFromLoved => new TranslatableString(getKey(@"remove_from_loved"), @"Remove from Loved");

        /// <summary>
        /// "Leave Feedback"
        /// </summary>
        public static LocalisableString LeaveFeedback => new TranslatableString(getKey(@"leave_feedback"), @"Leave Feedback");

        /// <summary>
        /// "Disqualified {0} ({1})."
        /// </summary>
        public static TranslatableString DisqualifiedAt => new TranslatableString(getKey(@"disqualified_at"), @"Disqualified {0} ({1}).");

        /// <summary>
        /// "no reason specified"
        /// </summary>
        public static LocalisableString DisqualifiedNoReason => new TranslatableString(getKey(@"disqualified_no_reason"), @"no reason specified");

        /// <summary>
        /// "Nomination process reset {0} by {1} with new problem {2} ({3})."
        /// </summary>
        public static TranslatableString ResetAtNominationReset => new TranslatableString(getKey(@"reset_at_nomination_reset"), @"Nomination process reset {0} by {1} with new problem {2} ({3}).");

        /// <summary>
        /// "Disqualified {0} by {1} with new problem {2} ({3})."
        /// </summary>
        public static TranslatableString ResetAtDisqualify => new TranslatableString(getKey(@"reset_at_disqualify"), @"Disqualified {0} by {1} with new problem {2} ({3}).");

        /// <summary>
        /// "deleted"
        /// </summary>
        public static LocalisableString ResetMessageDeleted => new TranslatableString(getKey(@"reset_message_deleted"), @"deleted");

        /// <summary>
        /// "nominated by {0}"
        /// </summary>
        public static TranslatableString NominatedBy => new TranslatableString(getKey(@"nominated_by"), @"nominated by {0}");

        /// <summary>
        /// "This beatmap was approved on {0}!"
        /// </summary>
        public static TranslatableString StatusApproved(string date) => new TranslatableString(getKey(@"status_approved"), @"This beatmap was approved on {0}!", date);

        /// <summary>
        /// "This beatmap was added to loved on {0}!"
        /// </summary>
        public static TranslatableString StatusLoved(string date) => new TranslatableString(getKey(@"status_loved"), @"This beatmap was added to loved on {0}!", date);

        /// <summary>
        /// "This beatmap was ranked on {0}!"
        /// </summary>
        public static TranslatableString StatusRanked(string date) => new TranslatableString(getKey(@"status_ranked"), @"This beatmap was ranked on {0}!", date);

        /// <summary>
        /// "This beatmap wasn't updated since {0} so it was graveyarded..."
        /// </summary>
        public static TranslatableString StatusGraveyard(string date) => new TranslatableString(getKey(@"status_graveyard"), @"This beatmap wasn't updated since {0} so it was graveyarded...", date);

        /// <summary>
        /// "Note: This beatmap is marked as a work-in-progress by the creator."
        /// </summary>
        public static LocalisableString StatusWip => new TranslatableString(getKey(@"status_wip"), @"Note: This beatmap is marked as a work-in-progress by the creator.");

        /// <summary>
        /// "This map is estimated to be ranked {0} if no issues are found. It is #{1} in the {2}."
        /// </summary>
        public static TranslatableString RankEstimate => new TranslatableString(getKey(@"rank_estimate"), @"This map is estimated to be ranked {0} if no issues are found. It is #{1} in the {2}.");

        /// <summary>
        /// "on {0}"
        /// </summary>
        public static TranslatableString RankEstimateOn(string date) => new TranslatableString(getKey(@"rank_estimate_on"), @"on {0}", date);

        /// <summary>
        /// "soon"
        /// </summary>
        public static LocalisableString RankEstimateSoon => new TranslatableString(getKey(@"rank_estimate_soon"), @"soon");

        /// <summary>
        /// "ranking queue"
        /// </summary>
        public static LocalisableString RankEstimateQueue => new TranslatableString(getKey(@"rank_estimate_queue"), @"ranking queue");

        /// <summary>
        /// "This map is currently blocked from leaving the Qualified section until {0} are resolved."
        /// </summary>
        public static TranslatableString UnresolvedProblems => new TranslatableString(getKey(@"unresolved_problems"), @"This map is currently blocked from leaving the Qualified section until {0} are resolved.");

        /// <summary>
        /// "these problems"
        /// </summary>
        public static LocalisableString Problems => new TranslatableString(getKey(@"problems"), @"these problems");

        /// <summary>
        /// "Discussion for this beatmap has been disabled. ({0})"
        /// </summary>
        public static TranslatableString DiscussionLocked(string text) => new TranslatableString(getKey(@"discussion_locked"), @"Discussion for this beatmap has been disabled. ({0})", text);

        /// <summary>
        /// "General {0}"
        /// </summary>
        public static TranslatableString ModeGeneral(string scope) => new TranslatableString(getKey(@"mode_general"), @"General {0}", scope);

        /// <summary>
        /// "(All difficulties)"
        /// </summary>
        public static LocalisableString ScopeAllDifficulties => new TranslatableString(getKey(@"scope_all_difficulties"), @"(All difficulties)");

        /// <summary>
        /// "Timeline"
        /// </summary>
        public static LocalisableString ModeTimeline => new TranslatableString(getKey(@"mode_timeline"), @"Timeline");

        /// <summary>
        /// "Reviews"
        /// </summary>
        public static LocalisableString ModeReviews => new TranslatableString(getKey(@"mode_reviews"), @"Reviews");

        /// <summary>
        /// "History"
        /// </summary>
        public static LocalisableString ModeEvents => new TranslatableString(getKey(@"mode_events"), @"History");

        /// <summary>
        /// "Discussions"
        /// </summary>
        public static LocalisableString DiscussionsTitle => new TranslatableString(getKey(@"discussions_title"), @"Discussions");

        /// <summary>
        /// "Sort"
        /// </summary>
        public static LocalisableString Sort => new TranslatableString(getKey(@"sort"), @"Sort");

        /// <summary>
        /// "Creation time"
        /// </summary>
        public static LocalisableString SortCreatedAt => new TranslatableString(getKey(@"sort_created_at"), @"Creation time");

        /// <summary>
        /// "Timeline"
        /// </summary>
        public static LocalisableString SortTimeline => new TranslatableString(getKey(@"sort_timeline"), @"Timeline");

        /// <summary>
        /// "Last update"
        /// </summary>
        public static LocalisableString SortUpdatedAt => new TranslatableString(getKey(@"sort_updated_at"), @"Last update");

        /// <summary>
        /// "Collapse all"
        /// </summary>
        public static LocalisableString CollapseAll => new TranslatableString(getKey(@"collapse_all"), @"Collapse all");

        /// <summary>
        /// "Expand all"
        /// </summary>
        public static LocalisableString ExpandAll => new TranslatableString(getKey(@"expand_all"), @"Expand all");

        /// <summary>
        /// "No discussion matches selected filter."
        /// </summary>
        public static LocalisableString EmptyHidden => new TranslatableString(getKey(@"empty_hidden"), @"No discussion matches selected filter.");

        /// <summary>
        /// "No discussions yet!"
        /// </summary>
        public static LocalisableString Empty => new TranslatableString(getKey(@"empty"), @"No discussions yet!");

        /// <summary>
        /// "Everyone"
        /// </summary>
        public static LocalisableString UserFilterEveryone => new TranslatableString(getKey(@"user_filter_everyone"), @"Everyone");

        /// <summary>
        /// "Filter by user"
        /// </summary>
        public static LocalisableString UserFilterLabel => new TranslatableString(getKey(@"user_filter_label"), @"Filter by user");

        /// <summary>
        /// "{0} users selected"
        /// </summary>
        public static TranslatableString UserFilterSelected(int count) => new TranslatableString(getKey(@"user_filter_selected"), @"{0} users selected", count);

        /// <summary>
        /// "Mine"
        /// </summary>
        public static LocalisableString StatsMine => new TranslatableString(getKey(@"stats_mine"), @"Mine");

        /// <summary>
        /// "Notes"
        /// </summary>
        public static LocalisableString StatsMapperNotes => new TranslatableString(getKey(@"stats_mapper_notes"), @"Notes");

        /// <summary>
        /// "Resolved"
        /// </summary>
        public static LocalisableString StatsResolved => new TranslatableString(getKey(@"stats_resolved"), @"Resolved");

        /// <summary>
        /// "Pending"
        /// </summary>
        public static LocalisableString StatsPending => new TranslatableString(getKey(@"stats_pending"), @"Pending");

        /// <summary>
        /// "Praises"
        /// </summary>
        public static LocalisableString StatsPraises => new TranslatableString(getKey(@"stats_praises"), @"Praises");

        /// <summary>
        /// "All"
        /// </summary>
        public static LocalisableString StatsTotal => new TranslatableString(getKey(@"stats_total"), @"All");

        /// <summary>
        /// "Include replies"
        /// </summary>
        public static LocalisableString IncludeReplies => new TranslatableString(getKey(@"include_replies"), @"Include replies");

        /// <summary>
        /// "Show other replies"
        /// </summary>
        public static LocalisableString ShowOtherReplies => new TranslatableString(getKey(@"show_other_replies"), @"Show other replies");

        /// <summary>
        /// "Check modding history"
        /// </summary>
        public static LocalisableString ModdingHistory => new TranslatableString(getKey(@"modding_history"), @"Check modding history");

        /// <summary>
        /// "Beatmap Mapper"
        /// </summary>
        public static LocalisableString GroupMapper => new TranslatableString(getKey(@"group_mapper"), @"Beatmap Mapper");

        /// <summary>
        /// "Difficulty Guest"
        /// </summary>
        public static LocalisableString GroupGuest => new TranslatableString(getKey(@"group_guest"), @"Difficulty Guest");

        /// <summary>
        /// "Checking for updates..."
        /// </summary>
        public static LocalisableString RefreshChecking => new TranslatableString(getKey(@"refresh_checking"), @"Checking for updates...");

        /// <summary>
        /// "The discussion has updates, click to refresh."
        /// </summary>
        public static LocalisableString RefreshHasUpdates => new TranslatableString(getKey(@"refresh_has_updates"), @"The discussion has updates, click to refresh.");

        /// <summary>
        /// "No updates."
        /// </summary>
        public static LocalisableString RefreshNoUpdates => new TranslatableString(getKey(@"refresh_no_updates"), @"No updates.");

        /// <summary>
        /// "Updating..."
        /// </summary>
        public static LocalisableString RefreshUpdating => new TranslatableString(getKey(@"refresh_updating"), @"Updating...");

        /// <summary>
        /// "Nothing has happened... yet."
        /// </summary>
        public static LocalisableString EventsEmpty => new TranslatableString(getKey(@"events_empty"), @"Nothing has happened... yet.");

        /// <summary>
        /// "{0} reply is hidden."
        /// </summary>
        public static TranslatableString HiddenRepliesOne(string count) => new TranslatableString(getKey(@"hidden_replies_one"), @"{0} reply is hidden.", count);

        /// <summary>
        /// "{0} replies are hidden."
        /// </summary>
        public static TranslatableString HiddenRepliesMany(string count) => new TranslatableString(getKey(@"hidden_replies_many"), @"{0} replies are hidden.", count);

        /// <summary>
        /// "View Review Post"
        /// </summary>
        public static LocalisableString GoToParent => new TranslatableString(getKey(@"go_to_parent"), @"View Review Post");

        /// <summary>
        /// "View Discussion"
        /// </summary>
        public static LocalisableString GoToChild => new TranslatableString(getKey(@"go_to_child"), @"View Discussion");

        /// <summary>
        /// "Marked as resolved by {0}"
        /// </summary>
        public static TranslatableString SystemResolved => new TranslatableString(getKey(@"system_resolved"), @"Marked as resolved by {0}");

        /// <summary>
        /// "Reopened by {0}"
        /// </summary>
        public static TranslatableString SystemReopened => new TranslatableString(getKey(@"system_reopened"), @"Reopened by {0}");

        /// <summary>
        /// "general"
        /// </summary>
        public static LocalisableString TimestampGeneral => new TranslatableString(getKey(@"timestamp_general"), @"general");

        /// <summary>
        /// "Deleted by {0} {1}."
        /// </summary>
        public static TranslatableString DeletedBy => new TranslatableString(getKey(@"deleted_by"), @"Deleted by {0} {1}.");

        /// <summary>
        /// "Last edited by {0} {1}."
        /// </summary>
        public static TranslatableString EditedBy => new TranslatableString(getKey(@"edited_by"), @"Last edited by {0} {1}.");

        /// <summary>
        /// "Denied from obtaining kudosu."
        /// </summary>
        public static LocalisableString KudosuDenied => new TranslatableString(getKey(@"kudosu_denied"), @"Denied from obtaining kudosu.");

        /// <summary>
        /// "Permalink"
        /// </summary>
        public static LocalisableString Permalink => new TranslatableString(getKey(@"permalink"), @"Permalink");

        /// <summary>
        /// "Link copied to clipboard"
        /// </summary>
        public static LocalisableString PermalinkCopied => new TranslatableString(getKey(@"permalink_copied"), @"Link copied to clipboard");

        /// <summary>
        /// "Respond"
        /// </summary>
        public static LocalisableString Respond => new TranslatableString(getKey(@"respond"), @"Respond");

        /// <summary>
        /// "No upvotes yet"
        /// </summary>
        public static LocalisableString VotesNoneUp => new TranslatableString(getKey(@"votes_none_up"), @"No upvotes yet");

        /// <summary>
        /// "No downvotes yet"
        /// </summary>
        public static LocalisableString VotesNoneDown => new TranslatableString(getKey(@"votes_none_down"), @"No downvotes yet");

        /// <summary>
        /// "Latest upvotes:"
        /// </summary>
        public static LocalisableString VotesLatestUp => new TranslatableString(getKey(@"votes_latest_up"), @"Latest upvotes:");

        /// <summary>
        /// "Latest downvotes:"
        /// </summary>
        public static LocalisableString VotesLatestDown => new TranslatableString(getKey(@"votes_latest_down"), @"Latest downvotes:");

        /// <summary>
        /// "[DISCUSSION DELETED]"
        /// </summary>
        public static LocalisableString EmbedMissing => new TranslatableString(getKey(@"embed_missing"), @"[DISCUSSION DELETED]");

        /// <summary>
        /// "[error parsing review]"
        /// </summary>
        public static LocalisableString ReviewParseError => new TranslatableString(getKey(@"review_parse_error"), @"[error parsing review]");

        /// <summary>
        /// "New Discussion"
        /// </summary>
        public static LocalisableString NewDiscussion => new TranslatableString(getKey(@"new_discussion"), @"New Discussion");

        /// <summary>
        /// "Discussions, reviews and replies are posted on the website."
        /// </summary>
        public static LocalisableString NewDiscussionDescription => new TranslatableString(getKey(@"new_discussion_description"), @"Discussions, reviews and replies are posted on the website.");

        /// <summary>
        /// "[unknown user]"
        /// </summary>
        public static LocalisableString UnknownUser => new TranslatableString(getKey(@"unknown_user"), @"[unknown user]");

        /// <summary>
        /// "[deleted]"
        /// </summary>
        public static LocalisableString EventDeleted => new TranslatableString(getKey(@"event_deleted"), @"[deleted]");

        /// <summary>
        /// "[no preview]"
        /// </summary>
        public static LocalisableString EventNoPreview => new TranslatableString(getKey(@"event_no_preview"), @"[no preview]");

        /// <summary>
        /// "Hype!"
        /// </summary>
        public static LocalisableString TypeHype => new TranslatableString(getKey(@"type_hype"), @"Hype!");

        /// <summary>
        /// "Note"
        /// </summary>
        public static LocalisableString TypeMapperNote => new TranslatableString(getKey(@"type_mapper_note"), @"Note");

        /// <summary>
        /// "Praise"
        /// </summary>
        public static LocalisableString TypePraise => new TranslatableString(getKey(@"type_praise"), @"Praise");

        /// <summary>
        /// "Problem"
        /// </summary>
        public static LocalisableString TypeProblem => new TranslatableString(getKey(@"type_problem"), @"Problem");

        /// <summary>
        /// "Review"
        /// </summary>
        public static LocalisableString TypeReview => new TranslatableString(getKey(@"type_review"), @"Review");

        /// <summary>
        /// "Suggestion"
        /// </summary>
        public static LocalisableString TypeSuggestion => new TranslatableString(getKey(@"type_suggestion"), @"Suggestion");

        /// <summary>
        /// "All difficulties"
        /// </summary>
        public static LocalisableString AllDifficulties => new TranslatableString(getKey(@"all_difficulties"), @"All difficulties");

        /// <summary>
        /// "Show in the editor"
        /// </summary>
        public static LocalisableString ShowInEditor => new TranslatableString(getKey(@"show_in_editor"), @"Show in the editor");

        /// <summary>
        /// "Nominate"
        /// </summary>
        public static LocalisableString Nominate => new TranslatableString(getKey(@"nominate"), @"Nominate");

        /// <summary>
        /// "Disqualify"
        /// </summary>
        public static LocalisableString Disqualify => new TranslatableString(getKey(@"disqualify"), @"Disqualify");

        /// <summary>
        /// "There are still unresolved issues that must be addressed first."
        /// </summary>
        public static LocalisableString NominateUnresolved => new TranslatableString(getKey(@"nominate_unresolved"), @"There are still unresolved issues that must be addressed first.");

        /// <summary>
        /// "You've already nominated this beatmap."
        /// </summary>
        public static LocalisableString NominateAlready => new TranslatableString(getKey(@"nominate_already"), @"You've already nominated this beatmap.");

        /// <summary>
        /// "You cannot nominate this beatmap game mode."
        /// </summary>
        public static LocalisableString NominateCannot => new TranslatableString(getKey(@"nominate_cannot"), @"You cannot nominate this beatmap game mode.");

        /// <summary>
        /// "info"
        /// </summary>
        public static LocalisableString HeaderInfo => new TranslatableString(getKey(@"header_info"), @"info");

        /// <summary>
        /// "discussion"
        /// </summary>
        public static LocalisableString HeaderDiscussion => new TranslatableString(getKey(@"header_discussion"), @"discussion");

        /// <summary>
        /// "version history"
        /// </summary>
        public static LocalisableString HeaderVersions => new TranslatableString(getKey(@"header_versions"), @"version history");

        /// <summary>
        /// "edit"
        /// </summary>
        public static LocalisableString PostEdit => new TranslatableString(getKey(@"post_edit"), @"edit");

        /// <summary>
        /// "delete"
        /// </summary>
        public static LocalisableString PostDelete => new TranslatableString(getKey(@"post_delete"), @"delete");

        /// <summary>
        /// "[deleted user]"
        /// </summary>
        public static LocalisableString DeletedUser => new TranslatableString(getKey(@"deleted_user"), @"[deleted user]");

        /// <summary>
        /// "EXPLICIT"
        /// </summary>
        public static LocalisableString BadgeNsfw => new TranslatableString(getKey(@"badge_nsfw"), @"EXPLICIT");

        /// <summary>
        /// "SPOTLIGHT"
        /// </summary>
        public static LocalisableString BadgeSpotlight => new TranslatableString(getKey(@"badge_spotlight"), @"SPOTLIGHT");

        /// <summary>
        /// "FEATURED ARTIST"
        /// </summary>
        public static LocalisableString BadgeFeaturedArtist => new TranslatableString(getKey(@"badge_featured_artist"), @"FEATURED ARTIST");

        /// <summary>
        /// "This is done on the website."
        /// </summary>
        public static LocalisableString OpenOnWebsite => new TranslatableString(getKey(@"open_on_website"), @"This is done on the website.");

        /// <summary>
        /// "Post on website"
        /// </summary>
        public static LocalisableString PostOnWebsite => new TranslatableString(getKey(@"post_on_website"), @"Post on website");

        /// <summary>
        /// The text of a beatmap set event of the given type, with named placeholders (e.g. "{user}") for the parts which are linked.
        /// </summary>
        public static TranslatableString Event(string type) => type switch
        {
            @"approve" => new TranslatableString(getKey(@"event_approve"), @"Approved."),
            @"beatmap_owner_change" => new TranslatableString(getKey(@"event_beatmap_owner_change"), @"Owner of difficulty {beatmap} changed to {new_user}."),
            @"discussion_delete" => new TranslatableString(getKey(@"event_discussion_delete"), @"Moderator deleted discussion {discussion}."),
            @"discussion_lock" => new TranslatableString(getKey(@"event_discussion_lock"), @"Discussion for this beatmap has been disabled. ({text})"),
            @"discussion_post_delete" => new TranslatableString(getKey(@"event_discussion_post_delete"), @"Moderator deleted post from discussion {discussion}."),
            @"discussion_post_restore" => new TranslatableString(getKey(@"event_discussion_post_restore"), @"Moderator restored post from discussion {discussion}."),
            @"discussion_restore" => new TranslatableString(getKey(@"event_discussion_restore"), @"Moderator restored discussion {discussion}."),
            @"discussion_unlock" => new TranslatableString(getKey(@"event_discussion_unlock"), @"Discussion for this beatmap has been enabled."),
            @"disqualify" => new TranslatableString(getKey(@"event_disqualify"), @"Disqualified by {user}. Reason: {discussion} ({text})."),
            @"disqualify_legacy" => new TranslatableString(getKey(@"event_disqualify_legacy"), @"Disqualified by {user}. Reason: {text}."),
            @"genre_edit" => new TranslatableString(getKey(@"event_genre_edit"), @"Genre changed from {old} to {new}."),
            @"issue_reopen" => new TranslatableString(getKey(@"event_issue_reopen"), @"Resolved issue {discussion} by {discussion_user} reopened by {user}."),
            @"issue_resolve" => new TranslatableString(getKey(@"event_issue_resolve"), @"Issue {discussion} by {discussion_user} marked as resolved by {user}."),
            @"kudosu_allow" => new TranslatableString(getKey(@"event_kudosu_allow"), @"Kudosu denial for discussion {discussion} has been removed."),
            @"kudosu_deny" => new TranslatableString(getKey(@"event_kudosu_deny"), @"Discussion {discussion} denied for kudosu."),
            @"kudosu_gain" => new TranslatableString(getKey(@"event_kudosu_gain"), @"Discussion {discussion} by {user} obtained enough votes for kudosu."),
            @"kudosu_lost" => new TranslatableString(getKey(@"event_kudosu_lost"), @"Discussion {discussion} by {user} lost votes and granted kudosu has been removed."),
            @"kudosu_recalculate" => new TranslatableString(getKey(@"event_kudosu_recalculate"), @"Discussion {discussion} has had its kudosu grants recalculated."),
            @"language_edit" => new TranslatableString(getKey(@"event_language_edit"), @"Language changed from {old} to {new}."),
            @"love" => new TranslatableString(getKey(@"event_love"), @"Loved by {user}."),
            @"nominate" => new TranslatableString(getKey(@"event_nominate"), @"Nominated by {user}."),
            @"nominate_modes" => new TranslatableString(getKey(@"event_nominate_modes"), @"Nominated by {user} ({modes})."),
            @"nomination_reset" => new TranslatableString(getKey(@"event_nomination_reset"), @"New problem {discussion} ({text}) triggered a nomination reset."),
            @"nomination_reset_received" => new TranslatableString(getKey(@"event_nomination_reset_received"), @"Nomination by {user} was reset by {source_user} ({text})"),
            @"offset_edit" => new TranslatableString(getKey(@"event_offset_edit"), @"Online offset changed from {old} to {new}."),
            @"qualify" => new TranslatableString(getKey(@"event_qualify"), @"This beatmap has reached the required number of nominations and has been qualified."),
            @"rank" => new TranslatableString(getKey(@"event_rank"), @"Ranked."),
            @"remove_from_loved" => new TranslatableString(getKey(@"event_remove_from_loved"), @"Removed from Loved by {user}. ({text})"),
            @"tags_edit" => new TranslatableString(getKey(@"event_tags_edit"), @"Tags changed from ""{old}"" to ""{new}""."),
            @"nsfw_toggle.to_0" => new TranslatableString(getKey(@"event_nsfw_toggle_to_0"), @"Removed explicit mark"),
            @"nsfw_toggle.to_1" => new TranslatableString(getKey(@"event_nsfw_toggle_to_1"), @"Marked as explicit"),
            _ => new TranslatableString(getKey($@"event_{type}"), type),
        };

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
