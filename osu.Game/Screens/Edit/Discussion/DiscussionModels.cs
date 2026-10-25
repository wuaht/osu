// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The response of the discussion page of a beatmap set on the website (<c>/beatmapsets/{id}/discussion</c>).
    /// </summary>
    public class DiscussionResponse
    {
        [JsonProperty(@"beatmapset")]
        public DiscussionBeatmapSet BeatmapSet { get; set; } = null!;
    }

    public class DiscussionBeatmapSet
    {
        [JsonProperty(@"id")]
        public int Id { get; set; }

        [JsonProperty(@"artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonProperty(@"artist_unicode")]
        public string? ArtistUnicode { get; set; }

        [JsonProperty(@"title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty(@"title_unicode")]
        public string? TitleUnicode { get; set; }

        [JsonProperty(@"creator")]
        public string Creator { get; set; } = string.Empty;

        [JsonProperty(@"user_id")]
        public int UserId { get; set; }

        [JsonProperty(@"status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty(@"covers")]
        public Dictionary<string, string>? Covers { get; set; }

        [JsonProperty(@"bpm")]
        public double Bpm { get; set; }

        [JsonProperty(@"offset")]
        public int Offset { get; set; }

        [JsonProperty(@"nsfw")]
        public bool Nsfw { get; set; }

        [JsonProperty(@"spotlight")]
        public bool Spotlight { get; set; }

        [JsonProperty(@"track_id")]
        public int? TrackId { get; set; }

        [JsonProperty(@"ranked")]
        public int Ranked { get; set; }

        [JsonProperty(@"version_count")]
        public int VersionCount { get; set; }

        [JsonProperty(@"can_be_hyped")]
        public bool CanBeHyped { get; set; }

        [JsonProperty(@"discussion_locked")]
        public bool DiscussionLocked { get; set; }

        [JsonProperty(@"deleted_at")]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty(@"last_updated")]
        public DateTimeOffset? LastUpdated { get; set; }

        [JsonProperty(@"ranked_date")]
        public DateTimeOffset? RankedDate { get; set; }

        [JsonProperty(@"submitted_date")]
        public DateTimeOffset? SubmittedDate { get; set; }

        [JsonProperty(@"hype")]
        public DiscussionHype? Hype { get; set; }

        [JsonProperty(@"nominations")]
        public DiscussionNominations? Nominations { get; set; }

        [JsonProperty(@"eligible_main_rulesets")]
        public List<string> EligibleMainRulesets { get; set; } = new List<string>();

        [JsonProperty(@"current_user_attributes")]
        public DiscussionCurrentUserAttributes? CurrentUserAttributes { get; set; }

        [JsonProperty(@"beatmaps")]
        public List<DiscussionBeatmap> Beatmaps { get; set; } = new List<DiscussionBeatmap>();

        [JsonProperty(@"discussions")]
        public List<BeatmapDiscussion> Discussions { get; set; } = new List<BeatmapDiscussion>();

        [JsonProperty(@"events")]
        public List<BeatmapSetEvent> Events { get; set; } = new List<BeatmapSetEvent>();

        [JsonProperty(@"related_users")]
        public List<DiscussionUser> RelatedUsers { get; set; } = new List<DiscussionUser>();

        public string? GetCover(string name) => Covers != null && Covers.TryGetValue(name, out string? url) ? url : null;
    }

    public class DiscussionHype
    {
        [JsonProperty(@"current")]
        public int Current { get; set; }

        [JsonProperty(@"required")]
        public int Required { get; set; }
    }

    public class DiscussionCurrentUserAttributes
    {
        [JsonProperty(@"can_hype")]
        public bool CanHype { get; set; }

        [JsonProperty(@"can_hype_reason")]
        public string? CanHypeReason { get; set; }

        [JsonProperty(@"can_love")]
        public bool CanLove { get; set; }

        [JsonProperty(@"can_remove_from_loved")]
        public bool CanRemoveFromLoved { get; set; }

        [JsonProperty(@"is_watching")]
        public bool IsWatching { get; set; }
    }

    public class DiscussionNominations
    {
        [JsonProperty(@"legacy_mode")]
        public bool LegacyMode { get; set; }

        /// <summary>
        /// Whether the signed in user nominated the beatmap set.
        /// </summary>
        [JsonProperty(@"nominated")]
        public bool Nominated { get; set; }

        /// <summary>
        /// The number of nominations in legacy mode, otherwise the number of nominations per ruleset.
        /// </summary>
        [JsonProperty(@"current")]
        public JToken? Current { get; set; }

        /// <summary>
        /// The number of required nominations in legacy mode.
        /// </summary>
        [JsonProperty(@"required")]
        public int? Required { get; set; }

        [JsonProperty(@"required_meta")]
        public DiscussionNominationsRequiredMeta? RequiredMeta { get; set; }

        [JsonProperty(@"disqualification")]
        public BeatmapSetEvent? Disqualification { get; set; }

        [JsonProperty(@"nomination_reset")]
        public BeatmapSetEvent? NominationReset { get; set; }

        [JsonProperty(@"ranking_eta")]
        public DateTimeOffset? RankingEta { get; set; }

        [JsonProperty(@"ranking_queue_position")]
        public int? RankingQueuePosition { get; set; }
    }

    public class DiscussionNominationsRequiredMeta
    {
        [JsonProperty(@"main_ruleset")]
        public int MainRuleset { get; set; }

        [JsonProperty(@"non_main_ruleset")]
        public int NonMainRuleset { get; set; }
    }

    public class DiscussionBeatmap
    {
        [JsonProperty(@"id")]
        public int Id { get; set; }

        [JsonProperty(@"beatmapset_id")]
        public int BeatmapSetId { get; set; }

        [JsonProperty(@"version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty(@"mode")]
        public string Mode { get; set; } = @"osu";

        [JsonProperty(@"difficulty_rating")]
        public double DifficultyRating { get; set; }

        [JsonProperty(@"user_id")]
        public int UserId { get; set; }

        [JsonProperty(@"total_length")]
        public int TotalLength { get; set; }

        [JsonProperty(@"hit_length")]
        public int HitLength { get; set; }

        [JsonProperty(@"bpm")]
        public double Bpm { get; set; }

        [JsonProperty(@"count_circles")]
        public int CountCircles { get; set; }

        [JsonProperty(@"count_sliders")]
        public int CountSliders { get; set; }

        [JsonProperty(@"cs")]
        public double CircleSize { get; set; }

        [JsonProperty(@"convert")]
        public bool Convert { get; set; }

        [JsonProperty(@"deleted_at")]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty(@"owners")]
        public List<DiscussionBeatmapOwner>? Owners { get; set; }
    }

    public class DiscussionBeatmapOwner
    {
        [JsonProperty(@"id")]
        public int Id { get; set; }

        [JsonProperty(@"username")]
        public string Username { get; set; } = string.Empty;
    }

    public class BeatmapDiscussion
    {
        [JsonProperty(@"id")]
        public int Id { get; set; }

        [JsonProperty(@"beatmapset_id")]
        public int BeatmapSetId { get; set; }

        [JsonProperty(@"beatmap_id")]
        public int? BeatmapId { get; set; }

        [JsonProperty(@"user_id")]
        public int UserId { get; set; }

        [JsonProperty(@"deleted_by_id")]
        public int? DeletedById { get; set; }

        [JsonProperty(@"message_type")]
        public string MessageType { get; set; } = string.Empty;

        [JsonProperty(@"parent_id")]
        public int? ParentId { get; set; }

        [JsonProperty(@"timestamp")]
        public int? Timestamp { get; set; }

        [JsonProperty(@"resolved")]
        public bool Resolved { get; set; }

        [JsonProperty(@"can_be_resolved")]
        public bool CanBeResolved { get; set; }

        [JsonProperty(@"can_grant_kudosu")]
        public bool CanGrantKudosu { get; set; }

        [JsonProperty(@"kudosu_denied")]
        public bool KudosuDenied { get; set; }

        [JsonProperty(@"created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty(@"updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty(@"deleted_at")]
        public DateTimeOffset? DeletedAt { get; set; }

        [JsonProperty(@"last_post_at")]
        public DateTimeOffset? LastPostAt { get; set; }

        [JsonProperty(@"posts")]
        public List<BeatmapDiscussionPost> Posts { get; set; } = new List<BeatmapDiscussionPost>();

        [JsonProperty(@"votes")]
        public BeatmapDiscussionVotes? Votes { get; set; }

        [JsonProperty(@"current_user_attributes")]
        public BeatmapDiscussionCurrentUserAttributes? CurrentUserAttributes { get; set; }

        public BeatmapDiscussionPost? StartingPost => Posts.Count > 0 ? Posts[0] : null;
    }

    public class BeatmapDiscussionCurrentUserAttributes
    {
        /// <summary>
        /// The vote of the signed in user, 1 for an upvote, -1 for a downvote or 0.
        /// </summary>
        [JsonProperty(@"vote_score")]
        public int VoteScore { get; set; }
    }

    public class BeatmapDiscussionVotes
    {
        [JsonProperty(@"up")]
        public int Up { get; set; }

        [JsonProperty(@"down")]
        public int Down { get; set; }

        [JsonProperty(@"voters")]
        public BeatmapDiscussionVoters? Voters { get; set; }
    }

    public class BeatmapDiscussionVoters
    {
        [JsonProperty(@"up")]
        public List<int> Up { get; set; } = new List<int>();

        [JsonProperty(@"down")]
        public List<int> Down { get; set; } = new List<int>();
    }

    public class BeatmapDiscussionPost
    {
        [JsonProperty(@"id")]
        public int Id { get; set; }

        [JsonProperty(@"beatmapset_discussion_id")]
        public int DiscussionId { get; set; }

        [JsonProperty(@"user_id")]
        public int UserId { get; set; }

        [JsonProperty(@"last_editor_id")]
        public int? LastEditorId { get; set; }

        [JsonProperty(@"deleted_by_id")]
        public int? DeletedById { get; set; }

        [JsonProperty(@"system")]
        public bool System { get; set; }

        /// <summary>
        /// The text of the post, or for system posts an object such as <c>{"type":"resolved","value":true}</c>.
        /// </summary>
        [JsonProperty(@"message")]
        public JToken? Message { get; set; }

        [JsonProperty(@"created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty(@"updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty(@"deleted_at")]
        public DateTimeOffset? DeletedAt { get; set; }

        public string MessageText => Message?.Type == JTokenType.String ? Message.Value<string>() ?? string.Empty : string.Empty;
    }

    public class BeatmapSetEvent
    {
        [JsonProperty(@"id")]
        public long Id { get; set; }

        [JsonProperty(@"type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Null, a string, or an object with the details of the event.
        /// </summary>
        [JsonProperty(@"comment")]
        public JToken? Comment { get; set; }

        [JsonProperty(@"created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonProperty(@"user_id")]
        public int? UserId { get; set; }

        [JsonProperty(@"beatmapset_id")]
        public int? BeatmapSetId { get; set; }

        [JsonProperty(@"discussion_id")]
        public int? DiscussionId { get; set; }

        /// <summary>
        /// The ID of the discussion of the event, which is stored in the comment.
        /// </summary>
        public int? CommentDiscussionId => Comment is JObject obj && obj[@"beatmap_discussion_id"] is JToken id && id.Type == JTokenType.Integer ? id.Value<int>() : null;
    }

    public class DiscussionUser
    {
        [JsonProperty(@"id")]
        public int Id { get; set; }

        [JsonProperty(@"username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty(@"avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonProperty(@"country_code")]
        public string? CountryCode { get; set; }

        [JsonProperty(@"default_group")]
        public string? DefaultGroup { get; set; }

        [JsonProperty(@"is_bot")]
        public bool IsBot { get; set; }

        [JsonProperty(@"is_deleted")]
        public bool IsDeleted { get; set; }

        [JsonProperty(@"profile_colour")]
        public string? ProfileColour { get; set; }

        [JsonProperty(@"groups")]
        public List<APIUserGroup>? Groups { get; set; }

        private APIUser? apiUser;

        /// <summary>
        /// This user as an <see cref="APIUser"/>, for avatars and user panels.
        /// </summary>
        public APIUser ToAPIUser() => apiUser ??= new APIUser
        {
            Id = Id,
            Username = Username,
            AvatarUrl = AvatarUrl,
            CountryCode = Enum.TryParse(CountryCode, out osu.Game.Users.CountryCode code) ? code : default,
            IsBot = IsBot,
            Colour = ProfileColour,
            Groups = Groups?.ToArray(),
        };
    }
}
