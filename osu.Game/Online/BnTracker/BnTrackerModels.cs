// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace osu.Game.Online.BnTracker
{
    // The shapes of the BN Tracker API (v1), see docs/API.md of the BN Tracker.
    // Unknown properties are ignored, as the API may add new ones at any time.

    public static class BnTrackerLimits
    {
        /// <summary>
        /// The maximum length of a comment the BN Tracker accepts.
        /// </summary>
        public const int MAX_COMMENT_LENGTH = 5000;
    }

    /// <summary>
    /// A game mode as the BN Tracker knows it. <see cref="General"/> is used for NAT members who aren't tied to a mode, and are relevant to every beatmap set.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BnGameMode
    {
        General,
        Osu,
        Taiko,
        Catch,
        Mania,
    }

    /// <summary>
    /// The standing of a nominator in a mode.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BnNominatorLevel
    {
        Probation,
        Full,
        Evaluator,
    }

    /// <summary>
    /// Where the mapper stands with a nominator on a beatmap set.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BnNominationStatus
    {
        NotAsked,
        Declined,
        Pending,
        Maybe,
        Unlikely,
        Accepted,
    }

    /// <summary>
    /// Whether a nominator likes, or has excluded, the genre or song language of a beatmap set.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BnPreferenceMatch
    {
        Unknown,
        Matches,
        Excluded,
    }

    /// <summary>
    /// A personal marker of how urgently the mapper wants to push a beatmap set forward.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum BnBeatmapPriority
    {
        None,
        Low,
        Medium,
        High,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BnBeatmapRankStatus
    {
        Unknown,
        Graveyard,
        Wip,
        Pending,
        Qualified,
        Ranked,
        Approved,
        Loved,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BnNominatorGroup
    {
        Bn,
        Nat,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum BnHistoryEventKind
    {
        Joined,
        Left,
    }

    public class BnModeLevel
    {
        [JsonProperty("mode")]
        public BnGameMode Mode { get; set; }

        [JsonProperty("level")]
        public BnNominatorLevel Level { get; set; }
    }

    public class BnGroupHistoryEvent
    {
        [JsonProperty("date")]
        public DateTimeOffset Date { get; set; }

        [JsonProperty("mode")]
        public BnGameMode Mode { get; set; }

        [JsonProperty("group")]
        public BnNominatorGroup Group { get; set; }

        [JsonProperty("kind")]
        public BnHistoryEventKind Kind { get; set; }
    }

    /// <summary>
    /// The preferences a nominator stated on Mappers Guild. Every list is always present.
    /// </summary>
    public class BnNominatorPreferences
    {
        [JsonProperty("genres")]
        public List<string> Genres { get; set; } = new List<string>();

        [JsonProperty("genresExcluded")]
        public List<string> GenresExcluded { get; set; } = new List<string>();

        [JsonProperty("songLanguages")]
        public List<string> SongLanguages { get; set; } = new List<string>();

        [JsonProperty("songLanguagesExcluded")]
        public List<string> SongLanguagesExcluded { get; set; } = new List<string>();

        [JsonProperty("osuStyles")]
        public List<string> OsuStyles { get; set; } = new List<string>();

        [JsonProperty("osuStylesExcluded")]
        public List<string> OsuStylesExcluded { get; set; } = new List<string>();

        [JsonProperty("taikoStyles")]
        public List<string> TaikoStyles { get; set; } = new List<string>();

        [JsonProperty("taikoStylesExcluded")]
        public List<string> TaikoStylesExcluded { get; set; } = new List<string>();

        [JsonProperty("catchStyles")]
        public List<string> CatchStyles { get; set; } = new List<string>();

        [JsonProperty("catchStylesExcluded")]
        public List<string> CatchStylesExcluded { get; set; } = new List<string>();

        [JsonProperty("maniaStyles")]
        public List<string> ManiaStyles { get; set; } = new List<string>();

        [JsonProperty("maniaStylesExcluded")]
        public List<string> ManiaStylesExcluded { get; set; } = new List<string>();

        [JsonProperty("maniaKeymodes")]
        public List<string> ManiaKeymodes { get; set; } = new List<string>();

        [JsonProperty("maniaKeymodesExcluded")]
        public List<string> ManiaKeymodesExcluded { get; set; } = new List<string>();

        [JsonProperty("details")]
        public List<string> Details { get; set; } = new List<string>();

        [JsonProperty("detailsExcluded")]
        public List<string> DetailsExcluded { get; set; } = new List<string>();

        [JsonProperty("mappers")]
        public List<string> Mappers { get; set; } = new List<string>();

        [JsonProperty("mappersExcluded")]
        public List<string> MappersExcluded { get; set; } = new List<string>();
    }

    /// <summary>
    /// A BN or NAT member, independent of any beatmap set.
    /// </summary>
    public class BnNominator
    {
        public const string REQUEST_CHANNEL_PERSONAL_QUEUE = @"PersonalQueue";
        public const string REQUEST_CHANNEL_GAME_CHAT = @"GameChat";

        [JsonProperty("osuId")]
        public int OsuId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;

        [JsonProperty("avatarUrl")]
        public string AvatarUrl { get; set; } = string.Empty;

        [JsonProperty("profileUrl")]
        public string ProfileUrl { get; set; } = string.Empty;

        [JsonProperty("coverUrl")]
        public string? CoverUrl { get; set; }

        [JsonProperty("modes")]
        public List<BnModeLevel> Modes { get; set; } = new List<BnModeLevel>();

        [JsonProperty("spokenLanguages")]
        public List<string> SpokenLanguages { get; set; } = new List<string>();

        /// <summary>
        /// Any of "PersonalQueue", "GameChat" and "Closed".
        /// </summary>
        [JsonProperty("requestChannels")]
        public List<string> RequestChannels { get; set; } = new List<string>();

        [JsonProperty("isOpenForRequests")]
        public bool IsOpenForRequests { get; set; }

        [JsonProperty("requestLink")]
        public string? RequestLink { get; set; }

        /// <summary>
        /// Free text as written on Mappers Guild, which may contain BBCode and markdown images.
        /// </summary>
        [JsonProperty("requestInfo")]
        public string? RequestInfo { get; set; }

        [JsonProperty("lastOpenedForRequests")]
        public DateTimeOffset? LastOpenedForRequests { get; set; }

        [JsonProperty("preferences")]
        public BnNominatorPreferences Preferences { get; set; } = new BnNominatorPreferences();

        [JsonProperty("history")]
        public List<BnGroupHistoryEvent> History { get; set; } = new List<BnGroupHistoryEvent>();

        /// <summary>
        /// Whether this person is no longer BN/NAT. They are kept because of the history with them.
        /// </summary>
        [JsonProperty("isRemoved")]
        public bool IsRemoved { get; set; }

        [JsonProperty("removedAt")]
        public DateTimeOffset? RemovedAt { get; set; }

        [JsonProperty("firstSeenAt")]
        public DateTimeOffset FirstSeenAt { get; set; }

        [JsonProperty("lastSyncedAt")]
        public DateTimeOffset LastSyncedAt { get; set; }

        public bool HasRequestChannel(string channel) => RequestChannels.Contains(channel, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// When this person first joined the BN or NAT, if known.
        /// </summary>
        public DateTimeOffset? FirstJoinedAt => History.Where(h => h.Kind == BnHistoryEventKind.Joined).Select(h => (DateTimeOffset?)h.Date).Min();
    }

    public class BnModeProgress
    {
        [JsonProperty("mode")]
        public BnGameMode Mode { get; set; }

        [JsonProperty("required")]
        public int Required { get; set; }

        [JsonProperty("filled")]
        public int Filled { get; set; }

        [JsonProperty("isSatisfied")]
        public bool IsSatisfied { get; set; }
    }

    public class BnBeatmapSetProgress
    {
        [JsonProperty("perMode")]
        public List<BnModeProgress> PerMode { get; set; } = new List<BnModeProgress>();

        [JsonProperty("totalRequired")]
        public int TotalRequired { get; set; }

        [JsonProperty("totalFilled")]
        public int TotalFilled { get; set; }

        /// <summary>
        /// Whether every mode of the beatmap set has enough accepted nominators.
        /// </summary>
        [JsonProperty("isFullBn")]
        public bool IsFullBn { get; set; }

        [JsonProperty("statusCounts")]
        public Dictionary<BnNominationStatus, int> StatusCounts { get; set; } = new Dictionary<BnNominationStatus, int>();

        [JsonProperty("relevantNominatorCount")]
        public int RelevantNominatorCount { get; set; }
    }

    /// <summary>
    /// A beatmap set tracked on the BN Tracker.
    /// </summary>
    public class BnBeatmapSet
    {
        /// <summary>
        /// The BN Tracker's own ID of the beatmap set.
        /// </summary>
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("osuBeatmapsetId")]
        public long OsuBeatmapSetId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("artist")]
        public string Artist { get; set; } = string.Empty;

        [JsonProperty("creatorUsername")]
        public string? CreatorUsername { get; set; }

        [JsonProperty("coverUrl")]
        public string? CoverUrl { get; set; }

        [JsonProperty("osuUrl")]
        public string OsuUrl { get; set; } = string.Empty;

        [JsonProperty("modes")]
        public List<BnGameMode> Modes { get; set; } = new List<BnGameMode>();

        /// <summary>
        /// The mode with the most difficulties, which needs two nominations.
        /// </summary>
        [JsonProperty("mainMode")]
        public BnGameMode? MainMode { get; set; }

        [JsonProperty("difficultyCount")]
        public int DifficultyCount { get; set; }

        [JsonProperty("rankStatus")]
        public BnBeatmapRankStatus RankStatus { get; set; }

        [JsonProperty("genre")]
        public string? Genre { get; set; }

        [JsonProperty("language")]
        public string? Language { get; set; }

        [JsonProperty("priority")]
        public BnBeatmapPriority Priority { get; set; }

        [JsonProperty("addedAt")]
        public DateTimeOffset AddedAt { get; set; }

        [JsonProperty("progress")]
        public BnBeatmapSetProgress Progress { get; set; } = new BnBeatmapSetProgress();
    }

    public class BnComment
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;

        [JsonProperty("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class BnActivity
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [JsonProperty("fromStatus")]
        public BnNominationStatus FromStatus { get; set; }

        [JsonProperty("toStatus")]
        public BnNominationStatus ToStatus { get; set; }

        [JsonProperty("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonProperty("note")]
        public string? Note { get; set; }
    }

    public class BnPreferenceMatchResult
    {
        [JsonProperty("genre")]
        public BnPreferenceMatch Genre { get; set; }

        [JsonProperty("language")]
        public BnPreferenceMatch Language { get; set; }
    }

    /// <summary>
    /// One nominator on one beatmap set: the mapper's status with them, their fit for the set, and the comments and activity.
    /// </summary>
    public class BnSetNominator
    {
        [JsonProperty("nominatorOsuId")]
        public int NominatorOsuId { get; set; }

        /// <summary>
        /// The nominator. Always present on the endpoints used by this client.
        /// </summary>
        [JsonProperty("nominator")]
        public BnNominator Nominator { get; set; } = new BnNominator();

        /// <summary>
        /// The modes of the nominator which matter for the beatmap set.
        /// </summary>
        [JsonProperty("relevantModes")]
        public List<BnModeLevel> RelevantModes { get; set; } = new List<BnModeLevel>();

        [JsonProperty("preferenceMatch")]
        public BnPreferenceMatchResult PreferenceMatch { get; set; } = new BnPreferenceMatchResult();

        [JsonProperty("status")]
        public BnNominationStatus Status { get; set; }

        [JsonProperty("statusUpdatedAt")]
        public DateTimeOffset? StatusUpdatedAt { get; set; }

        /// <summary>
        /// Oldest first.
        /// </summary>
        [JsonProperty("comments")]
        public List<BnComment> Comments { get; set; } = new List<BnComment>();

        /// <summary>
        /// Oldest first.
        /// </summary>
        [JsonProperty("activity")]
        public List<BnActivity> Activity { get; set; } = new List<BnActivity>();

        public BnComment? LatestComment => Comments.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
    }

    public class BnBeatmapSetWithNominators
    {
        [JsonProperty("beatmapSet")]
        public BnBeatmapSet BeatmapSet { get; set; } = new BnBeatmapSet();

        [JsonProperty("nominators")]
        public List<BnSetNominator> Nominators { get; set; } = new List<BnSetNominator>();
    }

    /// <summary>
    /// The result of a change to one nominator: their new state, and the new progress of the beatmap set.
    /// </summary>
    public class BnSetNominatorChange
    {
        [JsonProperty("beatmapSet")]
        public BnBeatmapSet BeatmapSet { get; set; } = new BnBeatmapSet();

        [JsonProperty("nominator")]
        public BnSetNominator Nominator { get; set; } = new BnSetNominator();
    }

    public class BnMe
    {
        [JsonProperty("osuId")]
        public int OsuId { get; set; }

        [JsonProperty("username")]
        public string? Username { get; set; }
    }

    public class BnOsuSignInResponse
    {
        [JsonProperty("token")]
        public string Token { get; set; } = string.Empty;

        [JsonProperty("osuId")]
        public int OsuId { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; } = string.Empty;
    }

    public class BnDeviceSignIn
    {
        [JsonProperty("deviceCode")]
        public string DeviceCode { get; set; } = string.Empty;

        [JsonProperty("userCode")]
        public string UserCode { get; set; } = string.Empty;

        [JsonProperty("verificationUrlComplete")]
        public string VerificationUrlComplete { get; set; } = string.Empty;

        [JsonProperty("expiresIn")]
        public int ExpiresIn { get; set; }

        [JsonProperty("interval")]
        public int Interval { get; set; }
    }

    public class BnDeviceSignInPoll
    {
        public const string STATUS_PENDING = @"pending";
        public const string STATUS_SLOW_DOWN = @"slow_down";
        public const string STATUS_APPROVED = @"approved";
        public const string STATUS_DENIED = @"denied";
        public const string STATUS_EXPIRED = @"expired";

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonProperty("interval")]
        public int Interval { get; set; }
    }
}
