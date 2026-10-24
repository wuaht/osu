// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.MapsetVerifier.UI
{
    public static class VerifierStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SlopMapsetVerifier";

        /// <summary>
        /// "Problem"
        /// </summary>
        public static LocalisableString LevelProblem => new TranslatableString(getKey(@"level_problem"), @"Problem");

        /// <summary>
        /// "Warning"
        /// </summary>
        public static LocalisableString LevelWarning => new TranslatableString(getKey(@"level_warning"), @"Warning");

        /// <summary>
        /// "Negligible"
        /// </summary>
        public static LocalisableString LevelMinor => new TranslatableString(getKey(@"level_minor"), @"Negligible");

        /// <summary>
        /// "Info"
        /// </summary>
        public static LocalisableString LevelInfo => new TranslatableString(getKey(@"level_info"), @"Info");

        /// <summary>
        /// "Error"
        /// </summary>
        public static LocalisableString LevelError => new TranslatableString(getKey(@"level_error"), @"Error");

        /// <summary>
        /// "No issues"
        /// </summary>
        public static LocalisableString LevelCheck => new TranslatableString(getKey(@"level_check"), @"No issues");

        /// <summary>
        /// "Checks"
        /// </summary>
        public static LocalisableString TabChecks => new TranslatableString(getKey(@"tab_checks"), @"Checks");

        /// <summary>
        /// "Documentation"
        /// </summary>
        public static LocalisableString TabDocumentation => new TranslatableString(getKey(@"tab_documentation"), @"Documentation");

        /// <summary>
        /// "Snapshots"
        /// </summary>
        public static LocalisableString TabSnapshots => new TranslatableString(getKey(@"tab_snapshots"), @"Snapshots");

        /// <summary>
        /// "Overview"
        /// </summary>
        public static LocalisableString TabOverview => new TranslatableString(getKey(@"tab_overview"), @"Overview");

        /// <summary>
        /// "General"
        /// </summary>
        public static LocalisableString General => new TranslatableString(getKey(@"general"), @"General");

        /// <summary>
        /// "Check again (Ctrl+F5)"
        /// </summary>
        public static LocalisableString RefreshTooltip => new TranslatableString(getKey(@"refresh_tooltip"), @"Check again (Ctrl+F5)");

        /// <summary>
        /// "Not checked yet"
        /// </summary>
        public static LocalisableString NotRunYet => new TranslatableString(getKey(@"not_run_yet"), @"Not checked yet");

        /// <summary>
        /// "Checking..."
        /// </summary>
        public static LocalisableString Checking => new TranslatableString(getKey(@"checking"), @"Checking...");

        /// <summary>
        /// "Checking... {0}/{1}"
        /// </summary>
        public static LocalisableString CheckingProgress(int completed, int total) => new TranslatableString(getKey(@"checking_progress"), @"Checking... {0}/{1}", completed, total);

        /// <summary>
        /// "Checked at {0}"
        /// </summary>
        public static LocalisableString CheckedAt(string time) => new TranslatableString(getKey(@"checked_at"), @"Checked at {0}", time);

        /// <summary>
        /// "The checks failed"
        /// </summary>
        public static LocalisableString CheckFailed => new TranslatableString(getKey(@"check_failed"), @"The checks failed");

        /// <summary>
        /// "Mapset Verifier couldn't check this beatmap set: {0}"
        /// </summary>
        public static LocalisableString CheckFailedDescription(string error) => new TranslatableString(getKey(@"check_failed_description"), @"Mapset Verifier couldn't check this beatmap set: {0}", error);

        /// <summary>
        /// "by {0}, mapped by {1}"
        /// </summary>
        public static LocalisableString ByArtistMappedBy(string artist, string creator) => new TranslatableString(getKey(@"by_artist_mapped_by"), @"by {0}, mapped by {1}", artist, creator);

        /// <summary>
        /// "{0} difficulties"
        /// </summary>
        public static LocalisableString DifficultyCount(int count) => new TranslatableString(getKey(@"difficulty_count"), @"{0} difficulties", count);

        /// <summary>
        /// "{0} issues"
        /// </summary>
        public static LocalisableString IssueCount(int count) => new TranslatableString(getKey(@"issue_count"), @"{0} issues", count);

        /// <summary>
        /// "1 issue"
        /// </summary>
        public static LocalisableString SingleIssue => new TranslatableString(getKey(@"single_issue"), @"1 issue");

        /// <summary>
        /// "{0} of {1} issues shown"
        /// </summary>
        public static LocalisableString FilteredIssueCount(int shown, int total) => new TranslatableString(getKey(@"filtered_issue_count"), @"{0} of {1} issues shown", shown, total);

        /// <summary>
        /// "No issues found"
        /// </summary>
        public static LocalisableString NoIssues => new TranslatableString(getKey(@"no_issues"), @"No issues found");

        /// <summary>
        /// "Nothing to point out here, at least not for Mapset Verifier."
        /// </summary>
        public static LocalisableString NoIssuesDescription => new TranslatableString(getKey(@"no_issues_description"), @"Nothing to point out here, at least not for Mapset Verifier.");

        /// <summary>
        /// "All issues are hidden"
        /// </summary>
        public static LocalisableString NoVisibleIssues => new TranslatableString(getKey(@"no_visible_issues"), @"All issues are hidden");

        /// <summary>
        /// "Issues of the hidden levels are not shown, change the filter to see them."
        /// </summary>
        public static LocalisableString NoVisibleIssuesDescription => new TranslatableString(getKey(@"no_visible_issues_description"), @"Issues of the hidden levels are not shown, change the filter to see them.");

        /// <summary>
        /// "Show {0} more"
        /// </summary>
        public static LocalisableString ShowMore(int count) => new TranslatableString(getKey(@"show_more"), @"Show {0} more", count);

        /// <summary>
        /// "Show less"
        /// </summary>
        public static LocalisableString ShowLess => new TranslatableString(getKey(@"show_less"), @"Show less");

        /// <summary>
        /// "Copy all ({0})"
        /// </summary>
        public static LocalisableString CopyAll(int count) => new TranslatableString(getKey(@"copy_all"), @"Copy all ({0})", count);

        /// <summary>
        /// "Copy all issues?"
        /// </summary>
        public static LocalisableString CopyAllConfirmation => new TranslatableString(getKey(@"copy_all_confirmation"), @"Copy all issues?");

        /// <summary>
        /// "This copies {0} issues to the clipboard."
        /// </summary>
        public static LocalisableString CopyAllConfirmationBody(int count) => new TranslatableString(getKey(@"copy_all_confirmation_body"), @"This copies {0} issues to the clipboard.", count);

        /// <summary>
        /// "Copied to the clipboard"
        /// </summary>
        public static LocalisableString Copied => new TranslatableString(getKey(@"copied"), @"Copied to the clipboard");

        /// <summary>
        /// "Copied {0} issues to the clipboard"
        /// </summary>
        public static LocalisableString CopiedIssues(int count) => new TranslatableString(getKey(@"copied_issues"), @"Copied {0} issues to the clipboard", count);

        /// <summary>
        /// "Show in compose (ctrl+click to copy)"
        /// </summary>
        public static LocalisableString TimestampTooltip => new TranslatableString(getKey(@"timestamp_tooltip"), @"Show in compose (ctrl+click to copy)");

        /// <summary>
        /// "Interpreted as"
        /// </summary>
        public static LocalisableString InterpretedAs => new TranslatableString(getKey(@"interpreted_as"), @"Interpreted as");

        /// <summary>
        /// "{0} (detected)"
        /// </summary>
        public static LocalisableString DetectedLevel(string level) => new TranslatableString(getKey(@"detected_level"), @"{0} (detected)", level);

        /// <summary>
        /// "Checking again..."
        /// </summary>
        public static LocalisableString Reinterpreting => new TranslatableString(getKey(@"reinterpreting"), @"Checking again...");

        /// <summary>
        /// "Star rating"
        /// </summary>
        public static LocalisableString StarRating => new TranslatableString(getKey(@"star_rating"), @"Star rating");

        /// <summary>
        /// "Filter"
        /// </summary>
        public static LocalisableString Filter => new TranslatableString(getKey(@"filter"), @"Filter");

        /// <summary>
        /// "Levels"
        /// </summary>
        public static LocalisableString Levels => new TranslatableString(getKey(@"levels"), @"Levels");

        /// <summary>
        /// "Show negligible issues"
        /// </summary>
        public static LocalisableString ShowNegligible => new TranslatableString(getKey(@"show_negligible"), @"Show negligible issues");

        /// <summary>
        /// "Negligible issues rarely matter, so they are hidden by default."
        /// </summary>
        public static LocalisableString ShowNegligibleDescription => new TranslatableString(getKey(@"show_negligible_description"), @"Negligible issues rarely matter, so they are hidden by default.");

        /// <summary>
        /// "Hidden negligible checks"
        /// </summary>
        public static LocalisableString HiddenNegligibleChecks => new TranslatableString(getKey(@"hidden_negligible_checks"), @"Hidden negligible checks");

        /// <summary>
        /// "Negligible issues of these checks are always hidden."
        /// </summary>
        public static LocalisableString HiddenNegligibleChecksDescription => new TranslatableString(getKey(@"hidden_negligible_checks_description"), @"Negligible issues of these checks are always hidden.");

        /// <summary>
        /// "Hide negligible issues of this check"
        /// </summary>
        public static LocalisableString HideNegligibleOfCheck => new TranslatableString(getKey(@"hide_negligible_of_check"), @"Hide negligible issues of this check");

        /// <summary>
        /// "Show negligible issues of this check again"
        /// </summary>
        public static LocalisableString ShowNegligibleOfCheck => new TranslatableString(getKey(@"show_negligible_of_check"), @"Show negligible issues of this check again");

        /// <summary>
        /// "None, hide them using the eye next to a check."
        /// </summary>
        public static LocalisableString NoHiddenNegligibleChecks => new TranslatableString(getKey(@"no_hidden_negligible_checks"), @"None, hide them using the eye next to a check.");

        /// <summary>
        /// "Changes since the last check"
        /// </summary>
        public static LocalisableString Changes => new TranslatableString(getKey(@"changes"), @"Changes since the last check");

        /// <summary>
        /// "Since {0}"
        /// </summary>
        public static LocalisableString ChangesSince(string time) => new TranslatableString(getKey(@"changes_since"), @"Since {0}", time);

        /// <summary>
        /// "This is the first check run, so there is nothing to compare with yet."
        /// </summary>
        public static LocalisableString NoPreviousRun => new TranslatableString(getKey(@"no_previous_run"), @"This is the first check run, so there is nothing to compare with yet.");

        /// <summary>
        /// "Nothing changed since the last check."
        /// </summary>
        public static LocalisableString NoChanges => new TranslatableString(getKey(@"no_changes"), @"Nothing changed since the last check.");

        /// <summary>
        /// "New"
        /// </summary>
        public static LocalisableString DeltaNew => new TranslatableString(getKey(@"delta_new"), @"New");

        /// <summary>
        /// "Resolved"
        /// </summary>
        public static LocalisableString DeltaResolved => new TranslatableString(getKey(@"delta_resolved"), @"Resolved");

        /// <summary>
        /// "Worsened"
        /// </summary>
        public static LocalisableString DeltaWorsened => new TranslatableString(getKey(@"delta_worsened"), @"Worsened");

        /// <summary>
        /// "Improved"
        /// </summary>
        public static LocalisableString DeltaImproved => new TranslatableString(getKey(@"delta_improved"), @"Improved");

        /// <summary>
        /// "Unchanged"
        /// </summary>
        public static LocalisableString DeltaUnchanged => new TranslatableString(getKey(@"delta_unchanged"), @"Unchanged");

        /// <summary>
        /// "Clear history"
        /// </summary>
        public static LocalisableString ClearHistory => new TranslatableString(getKey(@"clear_history"), @"Clear history");

        /// <summary>
        /// "Clear the check run history?"
        /// </summary>
        public static LocalisableString ClearHistoryConfirmation => new TranslatableString(getKey(@"clear_history_confirmation"), @"Clear the check run history?");

        /// <summary>
        /// "The next check run won't show any changes."
        /// </summary>
        public static LocalisableString ClearHistoryConfirmationBody => new TranslatableString(getKey(@"clear_history_confirmation_body"), @"The next check run won't show any changes.");

        /// <summary>
        /// "Check speed"
        /// </summary>
        public static LocalisableString CheckTimings => new TranslatableString(getKey(@"check_timings"), @"Check speed");

        /// <summary>
        /// "{0} ms in total"
        /// </summary>
        public static LocalisableString CheckTimingsTotal(string ms) => new TranslatableString(getKey(@"check_timings_total"), @"{0} ms in total", ms);

        /// <summary>
        /// "Issue"
        /// </summary>
        public static LocalisableString IssueDetails => new TranslatableString(getKey(@"issue_details"), @"Issue");

        /// <summary>
        /// "Close"
        /// </summary>
        public static LocalisableString CloseDetails => new TranslatableString(getKey(@"close_details"), @"Close");

        /// <summary>
        /// "Ranking criteria"
        /// </summary>
        public static LocalisableString Rules => new TranslatableString(getKey(@"rules"), @"Ranking criteria");

        /// <summary>
        /// "Copy"
        /// </summary>
        public static LocalisableString Copy => new TranslatableString(getKey(@"copy"), @"Copy");

        /// <summary>
        /// "About this check"
        /// </summary>
        public static LocalisableString CheckDescription => new TranslatableString(getKey(@"check_description"), @"About this check");

        /// <summary>
        /// "Outcomes"
        /// </summary>
        public static LocalisableString Outcomes => new TranslatableString(getKey(@"outcomes"), @"Outcomes");

        /// <summary>
        /// "Cause"
        /// </summary>
        public static LocalisableString Cause => new TranslatableString(getKey(@"cause"), @"Cause");

        /// <summary>
        /// "by {0}"
        /// </summary>
        public static LocalisableString Author(string author) => new TranslatableString(getKey(@"author"), @"by {0}", author);

        /// <summary>
        /// "Search checks"
        /// </summary>
        public static LocalisableString SearchChecks => new TranslatableString(getKey(@"search_checks"), @"Search checks");

        /// <summary>
        /// "All modes"
        /// </summary>
        public static LocalisableString AllModes => new TranslatableString(getKey(@"all_modes"), @"All modes");

        /// <summary>
        /// "Select a check"
        /// </summary>
        public static LocalisableString NoDocumentationSelected => new TranslatableString(getKey(@"no_documentation_selected"), @"Select a check");

        /// <summary>
        /// "Its purpose, reasoning and outcomes are shown here."
        /// </summary>
        public static LocalisableString NoDocumentationSelectedDescription => new TranslatableString(getKey(@"no_documentation_selected_description"), @"Its purpose, reasoning and outcomes are shown here.");

        /// <summary>
        /// "No checks found"
        /// </summary>
        public static LocalisableString NoChecksFound => new TranslatableString(getKey(@"no_checks_found"), @"No checks found");

        /// <summary>
        /// "General checks"
        /// </summary>
        public static LocalisableString GeneralChecks => new TranslatableString(getKey(@"general_checks"), @"General checks");

        /// <summary>
        /// "Difficulty checks"
        /// </summary>
        public static LocalisableString DifficultyChecks => new TranslatableString(getKey(@"difficulty_checks"), @"Difficulty checks");

        /// <summary>
        /// "{0} checks"
        /// </summary>
        public static LocalisableString ChecksCount(int count) => new TranslatableString(getKey(@"checks_count"), @"{0} checks", count);

        /// <summary>
        /// "1 check"
        /// </summary>
        public static LocalisableString SingleCheck => new TranslatableString(getKey(@"single_check"), @"1 check");

        /// <summary>
        /// "History"
        /// </summary>
        public static LocalisableString SnapshotHistory => new TranslatableString(getKey(@"snapshot_history"), @"History");

        /// <summary>
        /// "Changes"
        /// </summary>
        public static LocalisableString SnapshotChanges => new TranslatableString(getKey(@"snapshot_changes"), @"Changes");

        /// <summary>
        /// "No snapshots yet"
        /// </summary>
        public static LocalisableString NoSnapshots => new TranslatableString(getKey(@"no_snapshots"), @"No snapshots yet");

        /// <summary>
        /// "A snapshot of the beatmap set is created whenever it is checked, so changes between check runs can be seen here."
        /// </summary>
        public static LocalisableString NoSnapshotsDescription => new TranslatableString(getKey(@"no_snapshots_description"), @"A snapshot of the beatmap set is created whenever it is checked, so changes between check runs can be seen here.");

        /// <summary>
        /// "Snapshots are disabled in the settings, enable them to record changes when checking."
        /// </summary>
        public static LocalisableString NoSnapshotsDisabledDescription => new TranslatableString(getKey(@"no_snapshots_disabled_description"), @"Snapshots are disabled in the settings, enable them to record changes when checking.");

        /// <summary>
        /// "Loading snapshots..."
        /// </summary>
        public static LocalisableString LoadingSnapshots => new TranslatableString(getKey(@"loading_snapshots"), @"Loading snapshots...");

        /// <summary>
        /// "The snapshots couldn't be loaded: {0}"
        /// </summary>
        public static LocalisableString SnapshotsNotSubmitted => new TranslatableString(getKey(@"snapshots_not_submitted"), @"Not submitted yet");

        public static LocalisableString SnapshotsNotSubmittedDescription => new TranslatableString(getKey(@"snapshots_not_submitted_description"),
            @"Snapshots need a submitted mapset, and this one hasn't been uploaded yet. Submit it, or download the submitted version from the osu! website, to start taking snapshots.");

        public static LocalisableString SnapshotsFailed(string error) => new TranslatableString(getKey(@"snapshots_failed"), @"The snapshots couldn't be loaded: {0}", error);

        /// <summary>
        /// "Current"
        /// </summary>
        public static LocalisableString CurrentState => new TranslatableString(getKey(@"current_state"), @"Current");

        /// <summary>
        /// "The changes since the latest snapshot, which aren't saved as a snapshot yet."
        /// </summary>
        public static LocalisableString CurrentStateTooltip => new TranslatableString(getKey(@"current_state_tooltip"), @"The changes since the latest snapshot, which aren't saved as a snapshot yet.");

        /// <summary>
        /// "Initial snapshot"
        /// </summary>
        public static LocalisableString InitialSnapshot => new TranslatableString(getKey(@"initial_snapshot"), @"Initial snapshot");

        /// <summary>
        /// "Nothing changed in this snapshot."
        /// </summary>
        public static LocalisableString NoChangesInCommit => new TranslatableString(getKey(@"no_changes_in_commit"), @"Nothing changed in this snapshot.");

        /// <summary>
        /// "Select a snapshot to see its changes."
        /// </summary>
        public static LocalisableString SelectSnapshot => new TranslatableString(getKey(@"select_snapshot"), @"Select a snapshot to see its changes.");

        /// <summary>
        /// "{0} changes"
        /// </summary>
        public static LocalisableString ChangesCount(int count) => new TranslatableString(getKey(@"changes_count"), @"{0} changes", count);

        /// <summary>
        /// "Before"
        /// </summary>
        public static LocalisableString OldValue => new TranslatableString(getKey(@"old_value"), @"Before");

        /// <summary>
        /// "After"
        /// </summary>
        public static LocalisableString NewValue => new TranslatableString(getKey(@"new_value"), @"After");

        /// <summary>
        /// "Loading..."
        /// </summary>
        public static LocalisableString Loading => new TranslatableString(getKey(@"loading"), @"Loading...");

        /// <summary>
        /// "Some parts couldn't be analysed: {0}"
        /// </summary>
        public static LocalisableString OverviewFailed(string error) => new TranslatableString(getKey(@"overview_failed"), @"Some parts couldn't be analysed: {0}", error);

        /// <summary>
        /// "Metadata"
        /// </summary>
        public static LocalisableString SectionMetadata => new TranslatableString(getKey(@"section_metadata"), @"Metadata");

        /// <summary>
        /// "Resources"
        /// </summary>
        public static LocalisableString SectionResources => new TranslatableString(getKey(@"section_resources"), @"Resources");

        /// <summary>
        /// "Statistics"
        /// </summary>
        public static LocalisableString SectionStatistics => new TranslatableString(getKey(@"section_statistics"), @"Statistics");

        /// <summary>
        /// "Settings"
        /// </summary>
        public static LocalisableString SectionSettings => new TranslatableString(getKey(@"section_settings"), @"Settings");

        /// <summary>
        /// "Objects"
        /// </summary>
        public static LocalisableString SectionObjects => new TranslatableString(getKey(@"section_objects"), @"Objects");

        /// <summary>
        /// "Difficulty"
        /// </summary>
        public static LocalisableString SectionDifficulty => new TranslatableString(getKey(@"section_difficulty"), @"Difficulty");

        /// <summary>
        /// "Audio"
        /// </summary>
        public static LocalisableString SectionAudio => new TranslatableString(getKey(@"section_audio"), @"Audio");

        /// <summary>
        /// "Video"
        /// </summary>
        public static LocalisableString SectionVideo => new TranslatableString(getKey(@"section_video"), @"Video");

        /// <summary>
        /// "Colours"
        /// </summary>
        public static LocalisableString SectionColours => new TranslatableString(getKey(@"section_colours"), @"Colours");

        /// <summary>
        /// "Game mode"
        /// </summary>
        public static LocalisableString GameMode => new TranslatableString(getKey(@"game_mode"), @"Game mode");

        /// <summary>
        /// "Artist"
        /// </summary>
        public static LocalisableString Artist => new TranslatableString(getKey(@"artist"), @"Artist");

        /// <summary>
        /// "Artist (unicode)"
        /// </summary>
        public static LocalisableString ArtistUnicode => new TranslatableString(getKey(@"artist_unicode"), @"Artist (unicode)");

        /// <summary>
        /// "Title"
        /// </summary>
        public static LocalisableString Title => new TranslatableString(getKey(@"title"), @"Title");

        /// <summary>
        /// "Title (unicode)"
        /// </summary>
        public static LocalisableString TitleUnicode => new TranslatableString(getKey(@"title_unicode"), @"Title (unicode)");

        /// <summary>
        /// "Creator"
        /// </summary>
        public static LocalisableString Creator => new TranslatableString(getKey(@"creator"), @"Creator");

        /// <summary>
        /// "Source"
        /// </summary>
        public static LocalisableString Source => new TranslatableString(getKey(@"source"), @"Source");

        /// <summary>
        /// "Tags"
        /// </summary>
        public static LocalisableString Tags => new TranslatableString(getKey(@"tags"), @"Tags");

        /// <summary>
        /// "Beatmap ID"
        /// </summary>
        public static LocalisableString BeatmapId => new TranslatableString(getKey(@"beatmap_id"), @"Beatmap ID");

        /// <summary>
        /// "N/A"
        /// </summary>
        public static LocalisableString NotAvailable => new TranslatableString(getKey(@"not_available"), @"N/A");

        /// <summary>
        /// "Not the same in all difficulties"
        /// </summary>
        public static LocalisableString Inconsistent => new TranslatableString(getKey(@"inconsistent"), @"Not the same in all difficulties");

        /// <summary>
        /// "Hit sounds"
        /// </summary>
        public static LocalisableString HitSounds => new TranslatableString(getKey(@"hit_sounds"), @"Hit sounds");

        /// <summary>
        /// "Backgrounds"
        /// </summary>
        public static LocalisableString Backgrounds => new TranslatableString(getKey(@"backgrounds"), @"Backgrounds");

        /// <summary>
        /// "Videos"
        /// </summary>
        public static LocalisableString Videos => new TranslatableString(getKey(@"videos"), @"Videos");

        /// <summary>
        /// "Storyboard"
        /// </summary>
        public static LocalisableString Storyboard => new TranslatableString(getKey(@"storyboard"), @"Storyboard");

        /// <summary>
        /// "Audio file"
        /// </summary>
        public static LocalisableString AudioFile => new TranslatableString(getKey(@"audio_file"), @"Audio file");

        /// <summary>
        /// "Total size: {0}"
        /// </summary>
        public static LocalisableString TotalSize(string size) => new TranslatableString(getKey(@"total_size"), @"Total size: {0}", size);

        /// <summary>
        /// "File"
        /// </summary>
        public static LocalisableString FileName => new TranslatableString(getKey(@"file_name"), @"File");

        /// <summary>
        /// "Format"
        /// </summary>
        public static LocalisableString Format => new TranslatableString(getKey(@"format"), @"Format");

        /// <summary>
        /// "Size"
        /// </summary>
        public static LocalisableString Size => new TranslatableString(getKey(@"size"), @"Size");

        /// <summary>
        /// "Duration"
        /// </summary>
        public static LocalisableString Duration => new TranslatableString(getKey(@"duration"), @"Duration");

        /// <summary>
        /// "Usages"
        /// </summary>
        public static LocalisableString Usages => new TranslatableString(getKey(@"usages"), @"Usages");

        /// <summary>
        /// "Used by"
        /// </summary>
        public static LocalisableString UsedBy => new TranslatableString(getKey(@"used_by"), @"Used by");

        /// <summary>
        /// "Resolution"
        /// </summary>
        public static LocalisableString Resolution => new TranslatableString(getKey(@"resolution"), @"Resolution");

        /// <summary>
        /// "No custom hit sounds"
        /// </summary>
        public static LocalisableString NoHitSounds => new TranslatableString(getKey(@"no_hit_sounds"), @"No custom hit sounds");

        /// <summary>
        /// "No backgrounds"
        /// </summary>
        public static LocalisableString NoBackgrounds => new TranslatableString(getKey(@"no_backgrounds"), @"No backgrounds");

        /// <summary>
        /// "No videos"
        /// </summary>
        public static LocalisableString NoVideos => new TranslatableString(getKey(@"no_videos"), @"No videos");

        /// <summary>
        /// "Storyboard file"
        /// </summary>
        public static LocalisableString OsbFile => new TranslatableString(getKey(@"osb_file"), @"Storyboard file");

        /// <summary>
        /// "unused"
        /// </summary>
        public static LocalisableString OsbUnused => new TranslatableString(getKey(@"osb_unused"), @"unused");

        /// <summary>
        /// "No .osb file"
        /// </summary>
        public static LocalisableString NoOsb => new TranslatableString(getKey(@"no_osb"), @"No .osb file");

        /// <summary>
        /// "Sprites"
        /// </summary>
        public static LocalisableString Sprites => new TranslatableString(getKey(@"sprites"), @"Sprites");

        /// <summary>
        /// "Animations"
        /// </summary>
        public static LocalisableString Animations => new TranslatableString(getKey(@"animations"), @"Animations");

        /// <summary>
        /// "Samples"
        /// </summary>
        public static LocalisableString Samples => new TranslatableString(getKey(@"samples"), @"Samples");

        /// <summary>
        /// "Circles"
        /// </summary>
        public static LocalisableString Circles => new TranslatableString(getKey(@"circles"), @"Circles");

        /// <summary>
        /// "Sliders"
        /// </summary>
        public static LocalisableString Sliders => new TranslatableString(getKey(@"sliders"), @"Sliders");

        /// <summary>
        /// "Spinners"
        /// </summary>
        public static LocalisableString Spinners => new TranslatableString(getKey(@"spinners"), @"Spinners");

        /// <summary>
        /// "Hold notes"
        /// </summary>
        public static LocalisableString HoldNotes => new TranslatableString(getKey(@"hold_notes"), @"Hold notes");

        /// <summary>
        /// "Columns"
        /// </summary>
        public static LocalisableString Columns => new TranslatableString(getKey(@"columns"), @"Columns");

        /// <summary>
        /// "New combos"
        /// </summary>
        public static LocalisableString NewCombos => new TranslatableString(getKey(@"new_combos"), @"New combos");

        /// <summary>
        /// "Breaks"
        /// </summary>
        public static LocalisableString Breaks => new TranslatableString(getKey(@"breaks"), @"Breaks");

        /// <summary>
        /// "Uninherited lines"
        /// </summary>
        public static LocalisableString UninheritedLines => new TranslatableString(getKey(@"uninherited_lines"), @"Uninherited lines");

        /// <summary>
        /// "Inherited lines"
        /// </summary>
        public static LocalisableString InheritedLines => new TranslatableString(getKey(@"inherited_lines"), @"Inherited lines");

        /// <summary>
        /// "Kiai time"
        /// </summary>
        public static LocalisableString KiaiTime => new TranslatableString(getKey(@"kiai_time"), @"Kiai time");

        /// <summary>
        /// "Drain time"
        /// </summary>
        public static LocalisableString DrainTime => new TranslatableString(getKey(@"drain_time"), @"Drain time");

        /// <summary>
        /// "Play time"
        /// </summary>
        public static LocalisableString PlayTime => new TranslatableString(getKey(@"play_time"), @"Play time");

        /// <summary>
        /// "General"
        /// </summary>
        public static LocalisableString GeneralSettings => new TranslatableString(getKey(@"general_settings"), @"General");

        /// <summary>
        /// "Difficulty"
        /// </summary>
        public static LocalisableString DifficultySettings => new TranslatableString(getKey(@"difficulty_settings"), @"Difficulty");

        /// <summary>
        /// "Audio lead-in"
        /// </summary>
        public static LocalisableString AudioLeadIn => new TranslatableString(getKey(@"audio_lead_in"), @"Audio lead-in");

        /// <summary>
        /// "Stack leniency"
        /// </summary>
        public static LocalisableString StackLeniency => new TranslatableString(getKey(@"stack_leniency"), @"Stack leniency");

        /// <summary>
        /// "Countdown"
        /// </summary>
        public static LocalisableString Countdown => new TranslatableString(getKey(@"countdown"), @"Countdown");

        /// <summary>
        /// "Countdown offset"
        /// </summary>
        public static LocalisableString CountdownOffset => new TranslatableString(getKey(@"countdown_offset"), @"Countdown offset");

        /// <summary>
        /// "Letterbox in breaks"
        /// </summary>
        public static LocalisableString LetterboxInBreaks => new TranslatableString(getKey(@"letterbox_in_breaks"), @"Letterbox in breaks");

        /// <summary>
        /// "Widescreen storyboard"
        /// </summary>
        public static LocalisableString WidescreenStoryboard => new TranslatableString(getKey(@"widescreen_storyboard"), @"Widescreen storyboard");

        /// <summary>
        /// "Preview time"
        /// </summary>
        public static LocalisableString PreviewTime => new TranslatableString(getKey(@"preview_time"), @"Preview time");

        /// <summary>
        /// "Use skin sprites"
        /// </summary>
        public static LocalisableString UseSkinSprites => new TranslatableString(getKey(@"use_skin_sprites"), @"Use skin sprites");

        /// <summary>
        /// "Skin preference"
        /// </summary>
        public static LocalisableString SkinPreference => new TranslatableString(getKey(@"skin_preference"), @"Skin preference");

        /// <summary>
        /// "Epilepsy warning"
        /// </summary>
        public static LocalisableString EpilepsyWarning => new TranslatableString(getKey(@"epilepsy_warning"), @"Epilepsy warning");

        /// <summary>
        /// "HP drain"
        /// </summary>
        public static LocalisableString HpDrain => new TranslatableString(getKey(@"hp_drain"), @"HP drain");

        /// <summary>
        /// "Circle size"
        /// </summary>
        public static LocalisableString CircleSize => new TranslatableString(getKey(@"circle_size"), @"Circle size");

        /// <summary>
        /// "Overall difficulty"
        /// </summary>
        public static LocalisableString OverallDifficulty => new TranslatableString(getKey(@"overall_difficulty"), @"Overall difficulty");

        /// <summary>
        /// "Approach rate"
        /// </summary>
        public static LocalisableString ApproachRate => new TranslatableString(getKey(@"approach_rate"), @"Approach rate");

        /// <summary>
        /// "Slider tick rate"
        /// </summary>
        public static LocalisableString SliderTickRate => new TranslatableString(getKey(@"slider_tick_rate"), @"Slider tick rate");

        /// <summary>
        /// "Slider velocity"
        /// </summary>
        public static LocalisableString SliderVelocity => new TranslatableString(getKey(@"slider_velocity"), @"Slider velocity");

        /// <summary>
        /// "Yes"
        /// </summary>
        public static LocalisableString Yes => new TranslatableString(getKey(@"yes"), @"Yes");

        /// <summary>
        /// "No"
        /// </summary>
        public static LocalisableString No => new TranslatableString(getKey(@"no"), @"No");

        /// <summary>
        /// "Objects"
        /// </summary>
        public static LocalisableString Objects => new TranslatableString(getKey(@"objects"), @"Objects");

        /// <summary>
        /// "Edges"
        /// </summary>
        public static LocalisableString Edges => new TranslatableString(getKey(@"edges"), @"Edges");

        /// <summary>
        /// "Unsnapped"
        /// </summary>
        public static LocalisableString Unsnapped => new TranslatableString(getKey(@"unsnapped"), @"Unsnapped");

        /// <summary>
        /// "{0} unsnapped ({1})"
        /// </summary>
        public static LocalisableString UnsnappedPercentage(int count, string percentage) => new TranslatableString(getKey(@"unsnapped_percentage"), @"{0} unsnapped ({1})", count, percentage);

        /// <summary>
        /// "Snapping"
        /// </summary>
        public static LocalisableString Snappings => new TranslatableString(getKey(@"snappings"), @"Snapping");

        /// <summary>
        /// "Object types"
        /// </summary>
        public static LocalisableString ObjectTypes => new TranslatableString(getKey(@"object_types"), @"Object types");

        /// <summary>
        /// "Timing"
        /// </summary>
        public static LocalisableString TimingSegments => new TranslatableString(getKey(@"timing_segments"), @"Timing");

        /// <summary>
        /// "Long periods without hit sounds"
        /// </summary>
        public static LocalisableString HitsoundGaps => new TranslatableString(getKey(@"hitsound_gaps"), @"Long periods without hit sounds");

        /// <summary>
        /// "Column usage"
        /// </summary>
        public static LocalisableString ColumnUsage => new TranslatableString(getKey(@"column_usage"), @"Column usage");

        /// <summary>
        /// "Timeline"
        /// </summary>
        public static LocalisableString Timeline => new TranslatableString(getKey(@"timeline"), @"Timeline");

        /// <summary>
        /// "BPM"
        /// </summary>
        public static LocalisableString Bpm => new TranslatableString(getKey(@"bpm"), @"BPM");

        /// <summary>
        /// "Meter"
        /// </summary>
        public static LocalisableString Meter => new TranslatableString(getKey(@"meter"), @"Meter");

        /// <summary>
        /// "Sampleset"
        /// </summary>
        public static LocalisableString Sampleset => new TranslatableString(getKey(@"sampleset"), @"Sampleset");

        /// <summary>
        /// "Star rating"
        /// </summary>
        public static LocalisableString StarRatingGraph => new TranslatableString(getKey(@"star_rating_graph"), @"Star rating");

        /// <summary>
        /// "Strain"
        /// </summary>
        public static LocalisableString StrainGraph => new TranslatableString(getKey(@"strain_graph"), @"Strain");

        /// <summary>
        /// "Slider velocity"
        /// </summary>
        public static LocalisableString SliderVelocityGraph => new TranslatableString(getKey(@"slider_velocity_graph"), @"Slider velocity");

        /// <summary>
        /// "Volume"
        /// </summary>
        public static LocalisableString VolumeGraph => new TranslatableString(getKey(@"volume_graph"), @"Volume");

        /// <summary>
        /// "Skills"
        /// </summary>
        public static LocalisableString Skills => new TranslatableString(getKey(@"skills"), @"Skills");

        /// <summary>
        /// "Bitrate"
        /// </summary>
        public static LocalisableString Bitrate => new TranslatableString(getKey(@"bitrate"), @"Bitrate");

        /// <summary>
        /// "{0} kbps"
        /// </summary>
        public static LocalisableString BitrateValue(string bitrate) => new TranslatableString(getKey(@"bitrate_value"), @"{0} kbps", bitrate);

        /// <summary>
        /// "Variable bitrate"
        /// </summary>
        public static LocalisableString Vbr => new TranslatableString(getKey(@"vbr"), @"Variable bitrate");

        /// <summary>
        /// "Constant bitrate"
        /// </summary>
        public static LocalisableString Cbr => new TranslatableString(getKey(@"cbr"), @"Constant bitrate");

        /// <summary>
        /// "Channels"
        /// </summary>
        public static LocalisableString Channels => new TranslatableString(getKey(@"channels"), @"Channels");

        /// <summary>
        /// "Mono"
        /// </summary>
        public static LocalisableString Mono => new TranslatableString(getKey(@"mono"), @"Mono");

        /// <summary>
        /// "Stereo"
        /// </summary>
        public static LocalisableString Stereo => new TranslatableString(getKey(@"stereo"), @"Stereo");

        /// <summary>
        /// "Balance"
        /// </summary>
        public static LocalisableString Balance => new TranslatableString(getKey(@"balance"), @"Balance");

        /// <summary>
        /// "Louder channel"
        /// </summary>
        public static LocalisableString LouderChannel => new TranslatableString(getKey(@"louder_channel"), @"Louder channel");

        /// <summary>
        /// "Stereo width"
        /// </summary>
        public static LocalisableString StereoWidth => new TranslatableString(getKey(@"stereo_width"), @"Stereo width");

        /// <summary>
        /// "Phase correlation"
        /// </summary>
        public static LocalisableString PhaseCorrelation => new TranslatableString(getKey(@"phase_correlation"), @"Phase correlation");

        /// <summary>
        /// "Sample rate"
        /// </summary>
        public static LocalisableString SampleRate => new TranslatableString(getKey(@"sample_rate"), @"Sample rate");

        /// <summary>
        /// "Codec"
        /// </summary>
        public static LocalisableString Codec => new TranslatableString(getKey(@"codec"), @"Codec");

        /// <summary>
        /// "Integrated loudness"
        /// </summary>
        public static LocalisableString IntegratedLoudness => new TranslatableString(getKey(@"integrated_loudness"), @"Integrated loudness");

        /// <summary>
        /// "Loudness range"
        /// </summary>
        public static LocalisableString LoudnessRange => new TranslatableString(getKey(@"loudness_range"), @"Loudness range");

        /// <summary>
        /// "True peak"
        /// </summary>
        public static LocalisableString TruePeak => new TranslatableString(getKey(@"true_peak"), @"True peak");

        /// <summary>
        /// "Dynamic range"
        /// </summary>
        public static LocalisableString DynamicRange => new TranslatableString(getKey(@"dynamic_range"), @"Dynamic range");

        /// <summary>
        /// "Crest factor"
        /// </summary>
        public static LocalisableString CrestFactor => new TranslatableString(getKey(@"crest_factor"), @"Crest factor");

        /// <summary>
        /// "Compression"
        /// </summary>
        public static LocalisableString Compression => new TranslatableString(getKey(@"compression"), @"Compression");

        /// <summary>
        /// "Clipping"
        /// </summary>
        public static LocalisableString Clipping => new TranslatableString(getKey(@"clipping"), @"Clipping");

        /// <summary>
        /// "{0} clipped sections"
        /// </summary>
        public static LocalisableString ClippingCount(int count) => new TranslatableString(getKey(@"clipping_count"), @"{0} clipped sections", count);

        /// <summary>
        /// "None"
        /// </summary>
        public static LocalisableString None => new TranslatableString(getKey(@"none"), @"None");

        /// <summary>
        /// "Loudness"
        /// </summary>
        public static LocalisableString Loudness => new TranslatableString(getKey(@"loudness"), @"Loudness");

        /// <summary>
        /// "Frequencies"
        /// </summary>
        public static LocalisableString FrequencyAnalysis => new TranslatableString(getKey(@"frequency_analysis"), @"Frequencies");

        /// <summary>
        /// "Spectrogram"
        /// </summary>
        public static LocalisableString Spectrogram => new TranslatableString(getKey(@"spectrogram"), @"Spectrogram");

        /// <summary>
        /// "Dominant frequency"
        /// </summary>
        public static LocalisableString DominantFrequency => new TranslatableString(getKey(@"dominant_frequency"), @"Dominant frequency");

        /// <summary>
        /// "Fundamental frequency"
        /// </summary>
        public static LocalisableString FundamentalFrequency => new TranslatableString(getKey(@"fundamental_frequency"), @"Fundamental frequency");

        /// <summary>
        /// "The frequency analysis is disabled in the settings."
        /// </summary>
        public static LocalisableString AdvancedAudioDisabled => new TranslatableString(getKey(@"advanced_audio_disabled"), @"The frequency analysis is disabled in the settings.");

        /// <summary>
        /// "Analysing the frequencies..."
        /// </summary>
        public static LocalisableString AnalysingAudio => new TranslatableString(getKey(@"analysing_audio"), @"Analysing the frequencies...");

        /// <summary>
        /// "Frame rate"
        /// </summary>
        public static LocalisableString FrameRate => new TranslatableString(getKey(@"frame_rate"), @"Frame rate");

        /// <summary>
        /// "variable"
        /// </summary>
        public static LocalisableString VariableFrameRate => new TranslatableString(getKey(@"variable_frame_rate"), @"variable");

        /// <summary>
        /// "Audio track"
        /// </summary>
        public static LocalisableString AudioTrack => new TranslatableString(getKey(@"audio_track"), @"Audio track");

        /// <summary>
        /// "Offset"
        /// </summary>
        public static LocalisableString Offset => new TranslatableString(getKey(@"offset"), @"Offset");

        /// <summary>
        /// "Container"
        /// </summary>
        public static LocalisableString Container => new TranslatableString(getKey(@"container"), @"Container");

        /// <summary>
        /// "Warnings"
        /// </summary>
        public static LocalisableString Warnings => new TranslatableString(getKey(@"warnings"), @"Warnings");

        /// <summary>
        /// "Combo colours"
        /// </summary>
        public static LocalisableString ComboColours => new TranslatableString(getKey(@"combo_colours"), @"Combo colours");

        /// <summary>
        /// "Slider border"
        /// </summary>
        public static LocalisableString SliderBorder => new TranslatableString(getKey(@"slider_border"), @"Slider border");

        /// <summary>
        /// "Slider track"
        /// </summary>
        public static LocalisableString SliderTrack => new TranslatableString(getKey(@"slider_track"), @"Slider track");

        /// <summary>
        /// "Luminosity"
        /// </summary>
        public static LocalisableString Luminosity => new TranslatableString(getKey(@"luminosity"), @"Luminosity");

        /// <summary>
        /// "Not used in this mode"
        /// </summary>
        public static LocalisableString NotApplicable => new TranslatableString(getKey(@"not_applicable"), @"Not used in this mode");

        /// <summary>
        /// "No combo colours, the ones of the skin are used."
        /// </summary>
        public static LocalisableString NoComboColours => new TranslatableString(getKey(@"no_combo_colours"), @"No combo colours, the ones of the skin are used.");

        /// <summary>
        /// "Nothing to show"
        /// </summary>
        public static LocalisableString NoAnalysis => new TranslatableString(getKey(@"no_analysis"), @"Nothing to show");

        /// <summary>
        /// "Previous difficulty ([)"
        /// </summary>
        public static LocalisableString PreviousDifficulty => new TranslatableString(getKey(@"previous_difficulty"), @"Previous difficulty ([)");

        /// <summary>
        /// "Next difficulty (])"
        /// </summary>
        public static LocalisableString NextDifficulty => new TranslatableString(getKey(@"next_difficulty"), @"Next difficulty (])");

        /// <summary>
        /// "Note"
        /// </summary>
        public static LocalisableString Note => new TranslatableString(getKey(@"note"), @"Note");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
