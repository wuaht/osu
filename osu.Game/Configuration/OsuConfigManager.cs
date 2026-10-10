// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Graphics.Cursor;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.BeatmapMirrors;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Overlays.Dashboard.Friends;
using osu.Game.Overlays.Mods.Input;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.OnlinePlay.Lounge.Components;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osu.Game.Users;

namespace osu.Game.Configuration
{
    public partial class OsuConfigManager : IniConfigManager<OsuSetting>, IGameplaySettings
    {
        public OsuConfigManager(Storage storage)
            : base(storage, null, CLIENT_SETTINGS_PREFIX)
        {
        }

        protected override void InitialiseDefaults()
        {
            // UI/selection defaults
            SetDefault(OsuSetting.Ruleset, string.Empty);
            SetDefault(OsuSetting.Skin, SkinInfo.ARGON_SKIN.ToString());

            SetDefault(OsuSetting.BeatmapDetailTab, BeatmapDetailTab.Local);
            SetDefault(OsuSetting.BeatmapLeaderboardSortMode, LeaderboardSortMode.Score);
            SetDefault(OsuSetting.BeatmapDetailModsFilter, false);

            SetDefault(OsuSetting.ShowConvertedBeatmaps, true);
            SetDefault(OsuSetting.DisplayStarsMinimum, 0.0, 0, 10, 0.1);
            SetDefault(OsuSetting.DisplayStarsMaximum, 10.1, 0, 10.1, 0.1);

            SetDefault(OsuSetting.SongSelectGroupMode, GroupMode.None);
            SetDefault(OsuSetting.SongSelectSortingMode, SortMode.Title);
            SetDefault(OsuSetting.SongSelectCollectionFilter, string.Empty);

            SetDefault(OsuSetting.RandomSelectAlgorithm, RandomSelectAlgorithm.RandomPermutation);
            SetDefault(OsuSetting.ModSelectHotkeyStyle, ModSelectHotkeyStyle.Sequential);
            SetDefault(OsuSetting.ModSelectTextSearchStartsActive, true);

            SetDefault(OsuSetting.ChatDisplayHeight, ChatOverlay.DEFAULT_HEIGHT, 0.2f, 1f, 0.01f);

            SetDefault(OsuSetting.BeatmapListingCardSize, BeatmapCardSize.Normal);
            SetDefault(OsuSetting.BeatmapListingFeaturedArtistFilter, true);

            SetDefault(OsuSetting.ProfileCoverExpanded, true);

            SetDefault(OsuSetting.ToolbarClockDisplayMode, ToolbarClockDisplayMode.Full);

            SetDefault(OsuSetting.SongSelectBackgroundBlur, false);

            // Online settings
            SetDefault(OsuSetting.Username, string.Empty);
            SetDefault(OsuSetting.Token, string.Empty);

            SetDefault(OsuSetting.AutomaticallyDownloadMissingBeatmaps, true);

            SetDefault(OsuSetting.SavePassword, true).ValueChanged += enabled =>
            {
                if (enabled.NewValue)
                    SetValue(OsuSetting.SaveUsername, true);
                else
                    GetBindable<string>(OsuSetting.Token).SetDefault();
            };

            SetDefault(OsuSetting.SaveUsername, true).ValueChanged += enabled =>
            {
                if (!enabled.NewValue)
                {
                    GetBindable<string>(OsuSetting.Username).SetDefault();
                    SetValue(OsuSetting.SavePassword, false);
                }
            };

            SetDefault(OsuSetting.ExternalLinkWarning, true);
            SetDefault(OsuSetting.PreferNoVideo, false);

            SetDefault(OsuSetting.ShowOnlineExplicitContent, false);

            SetDefault(OsuSetting.NotifyOnUsernameMentioned, true);
            SetDefault(OsuSetting.NotifyOnPrivateMessage, true);
            SetDefault(OsuSetting.NotifyOnFriendPresenceChange, true);

            // Audio
            SetDefault(OsuSetting.VolumeInactive, 0.25, 0, 1, 0.01);

            SetDefault(OsuSetting.MenuVoice, true);
            SetDefault(OsuSetting.MenuMusic, true);
            SetDefault(OsuSetting.MenuTips, true);

            SetDefault(OsuSetting.AudioOffset, 0, -500.0, 500.0, 1);

            SetDefault(OsuSetting.AutomaticallyAdjustBeatmapOffset, false);

            // Input
            SetDefault(OsuSetting.MenuCursorSize, 1.0f, 0.5f, 2f, 0.01f);
            SetDefault(OsuSetting.GameplayCursorSize, 1.0f, 0.1f, 2f, 0.01f);
            SetDefault(OsuSetting.GameplayCursorDuringTouch, false);
            SetDefault(OsuSetting.AutoCursorSize, false);

            SetDefault(OsuSetting.MouseDisableButtons, false);
            SetDefault(OsuSetting.MouseDisableWheel, false);
            SetDefault(OsuSetting.ConfineMouseMode, OsuConfineMouseMode.DuringGameplay);

            SetDefault(OsuSetting.TouchDisableGameplayTaps, false);

            // Graphics
            SetDefault(OsuSetting.ShowFpsDisplay, false);

            SetDefault(OsuSetting.ShowStoryboard, true);
            SetDefault(OsuSetting.BeatmapSkins, true);
            SetDefault(OsuSetting.BeatmapColours, true);
            SetDefault(OsuSetting.BeatmapHitsounds, true);

            SetDefault(OsuSetting.CursorRotation, true);

#pragma warning disable CS0612 // Type or member is obsolete (setting default value to avoid risk of any future crashes)
            SetDefault(OsuSetting.MenuParallax, true);
#pragma warning restore CS0612 // Type or member is obsolete
            SetDefault(OsuSetting.MenuParallaxScale, 1.0f, 0.0f, 2.0f, 0.1f);

            // See https://stackoverflow.com/a/63307411 for default sourcing.
            SetDefault(OsuSetting.Prefer24HourTime, !CultureInfoHelper.SystemCulture.DateTimeFormat.ShortTimePattern.Contains(@"tt"));

            // Gameplay
            SetDefault(OsuSetting.PositionalHitsoundsLevel, 0.2f, 0, 1, 0.01f);
            SetDefault(OsuSetting.DimLevel, 0.7, 0, 1, 0.01);
            SetDefault(OsuSetting.BlurLevel, 0, 0, 1, 0.01);
            SetDefault(OsuSetting.LightenDuringBreaks, true);

            SetDefault(OsuSetting.HitLighting, true);
            SetDefault(OsuSetting.StarFountains, true);

            SetDefault(OsuSetting.HUDVisibilityMode, HUDVisibilityMode.Always);
            SetDefault(OsuSetting.ShowHealthDisplayWhenCantFail, true);
            SetDefault(OsuSetting.FadePlayfieldWhenHealthLow, true);
            SetDefault(OsuSetting.KeyOverlay, false);
            SetDefault(OsuSetting.ReplaySettingsOverlay, true);
            SetDefault(OsuSetting.ReplayPlaybackControlsExpanded, true);
            SetDefault(OsuSetting.GameplayLeaderboard, true);
            SetDefault(OsuSetting.AlwaysPlayFirstComboBreak, true);

            SetDefault(OsuSetting.FloatingComments, false);

            SetDefault(OsuSetting.ScoreDisplayMode, ScoringMode.Standardised);

            SetDefault(OsuSetting.IncreaseFirstObjectVisibility, true);
            SetDefault(OsuSetting.GameplayDisableWinKey, true);

            // Update
            SetDefault(OsuSetting.ReleaseStream, ReleaseStream.Lazer);

            SetDefault(OsuSetting.Version, string.Empty);

            SetDefault(OsuSetting.ShowFirstRunSetup, true);
            SetDefault(OsuSetting.ShowMobileDisclaimer, RuntimeInfo.IsMobile);

            SetDefault(OsuSetting.ScreenshotFormat, ScreenshotFormat.Jpg);
            SetDefault(OsuSetting.ScreenshotCaptureMenuCursor, false);

            SetDefault(OsuSetting.Scaling, ScalingMode.Off);
            SetDefault(OsuSetting.SafeAreaConsiderations, true);
            SetDefault(OsuSetting.ScalingBackgroundDim, 0.9f, 0.5f, 1f, 0.01f);

            SetDefault(OsuSetting.ScalingSizeX, 0.8f, 0.2f, 1f, 0.01f);
            SetDefault(OsuSetting.ScalingSizeY, 0.8f, 0.2f, 1f, 0.01f);

            SetDefault(OsuSetting.ScalingPositionX, 0.5f, 0f, 1f, 0.01f);
            SetDefault(OsuSetting.ScalingPositionY, 0.5f, 0f, 1f, 0.01f);

            if (RuntimeInfo.IsMobile)
                SetDefault(OsuSetting.UIScale, 1f, 0.8f, 1.1f, 0.01f);
            else
                SetDefault(OsuSetting.UIScale, 1f, 0.8f, 1.6f, 0.01f);

            SetDefault(OsuSetting.UIHoldActivationDelay, 200.0, 0.0, 500.0, 50.0);

            SetDefault(OsuSetting.IntroSequence, IntroSequence.Triangles);

            SetDefault(OsuSetting.MenuBackgroundSource, BackgroundSource.Skin);
            SetDefault(OsuSetting.SeasonalBackgroundMode, SeasonalBackgroundMode.Sometimes);

            SetDefault(OsuSetting.DiscordRichPresence, DiscordRichPresenceMode.Full);

            SetDefault(OsuSetting.EditorDim, 0.25f, 0f, 1f, 0.25f);
            SetDefault(OsuSetting.EditorWaveformOpacity, 0.25f, 0f, 1f, 0.25f);
            SetDefault(OsuSetting.EditorShowHitMarkers, true);
            SetDefault(OsuSetting.EditorAutoSeekOnPlacement, true);
            SetDefault(OsuSetting.EditorLimitedDistanceSnap, false);
            SetDefault(OsuSetting.EditorShowSpeedChanges, false);
            SetDefault(OsuSetting.EditorScaleOrigin, EditorOrigin.GridCentre);
            SetDefault(OsuSetting.EditorRotationOrigin, EditorOrigin.GridCentre);
            SetDefault(OsuSetting.EditorAdjustExistingObjectsOnTimingChanges, true);

            SetDefault(OsuSetting.HideCountryFlags, false);

            SetDefault(OsuSetting.MultiplayerRoomFilter, RoomPermissionsFilter.All);
            SetDefault(OsuSetting.MultiplayerShowInProgressFilter, true);
            SetDefault(OsuSetting.MultiplayerShowFullFilter, false);

            SetDefault(OsuSetting.LastProcessedMetadataId, -1);

            SetDefault(OsuSetting.ComboColourNormalisationAmount, 0.2f, 0f, 1f, 0.01f);
            SetDefault(OsuSetting.UserOnlineStatus, UserStatus.Online);

            SetDefault(OsuSetting.EditorTimelineShowTimingChanges, true);
            SetDefault(OsuSetting.EditorTimelineShowBreaks, true);
            SetDefault(OsuSetting.EditorTimelineShowTicks, true);

            SetDefault(OsuSetting.EditorContractSidebars, false);

            SetDefault(OsuSetting.AlwaysShowHoldForMenuButton, false);
            SetDefault(OsuSetting.AlwaysRequireHoldingForPause, false);
            SetDefault(OsuSetting.EditorShowStoryboard, true);

            SetDefault(OsuSetting.EditorSubmissionNotifyOnDiscussionReplies, true);
            SetDefault(OsuSetting.EditorSubmissionLoadInBrowserAfterSubmission, true);

            SetDefault(OsuSetting.WasSupporter, false);

            // intentionally uses `DateTime?` and not `DateTimeOffset?` because the latter fails due to `DateTimeOffset` not implementing `IConvertible`
            SetDefault(OsuSetting.LastOnlineTagsPopulation, (DateTime?)null);

            SetDefault(OsuSetting.DashboardSortMode, UserSortCriteria.LastVisit);
            SetDefault(OsuSetting.DashboardDisplayStyle, OverlayPanelDisplayStyle.Card);

            SetDefault(OsuSetting.PMFriendsOnly, false);

            // slop! settings
            SetDefault(OsuSetting.SlopFrostedSliders, false);
            SetDefault(OsuSetting.SlopFrostedSlidersFrostiness, 1f, 0f, 1f, 0.01f);
            SetDefault(OsuSetting.SlopFrostedSlidersBlur, 0.25f, 0f, 1f, 0.01f);
            SetDefault(OsuSetting.SlopFrostedSlidersAlwaysColoured, true);
            SetDefault(OsuSetting.SlopFrostedHitCircles, true);
            SetDefault(OsuSetting.SlopMenuCursorStyle, MenuCursorStyle.Default);

            SetDefault(OsuSetting.SlopEditorSkin, string.Empty);
            SetDefault(OsuSetting.SlopEditorSkinInTestMode, false);
            SetDefault(OsuSetting.SlopEditorBackupOnSave, true);
            SetDefault(OsuSetting.SlopEditorAutosaveInterval, 5, 0, 60);
            SetDefault(OsuSetting.SlopEditorShowSelectionBox, false);
            SetDefault(OsuSetting.SlopEditorShowSelectionBoxButtons, false);
            SetDefault(OsuSetting.SlopEditorShowSliderEndDragMarker, false);
            SetDefault(OsuSetting.SlopEditorImmediateDrag, true);
            SetDefault(OsuSetting.SlopEditorAnchorShape, EditorAnchorShape.Square);
            SetDefault(OsuSetting.SlopEditorVisualSpacingSnap, true);
            SetDefault(OsuSetting.SlopEditorBlanketSnap, true);
            SetDefault(OsuSetting.SlopEditorLineSnap, true);
            SetDefault(OsuSetting.SlopEditorSliderBlanketSnap, true);
            SetDefault(OsuSetting.SlopEditorRotateAroundObjectStarts, true);
            SetDefault(OsuSetting.SlopEditorTimelineSliderVelocityWithAlt, false);
            SetDefault(OsuSetting.SlopEditorShowDifficultyStrains, false);
            SetDefault(OsuSetting.SlopEditorShowOffscreenObjects, false);
            SetDefault(OsuSetting.SlopEditorWaveformStyle, EditorWaveformStyle.Default);
            SetDefault(OsuSetting.SlopEditorLoopMusic, false);

            // defaults of McOsu's FPoSu.
            SetDefault(OsuSetting.SlopFposuMouseDpi, 400, 50, 32000);
            SetDefault(OsuSetting.SlopFposuCmPer360, 30f, 1f, 200f, 0.1f);
            SetDefault(OsuSetting.SlopFposuFov, 103f, 20f, 150f, 1f);
            SetDefault(OsuSetting.SlopFposuDistance, 0.5f, 0.1f, 2f, 0.01f);
            SetDefault(OsuSetting.SlopFposuCurved, true);
            SetDefault(OsuSetting.SlopFposuInvertHorizontal, false);
            SetDefault(OsuSetting.SlopFposuInvertVertical, false);
            SetDefault(OsuSetting.SlopFposuAbsoluteMode, false);
            SetDefault(OsuSetting.SlopFposuBackgroundCube, true);
            SetDefault(OsuSetting.SlopFposuSkybox, true);

            SetDefault(OsuSetting.SlopActiveOfflineProfile, string.Empty);
            SetDefault(OsuSetting.SlopOfflineProfilesIncludeUnranked, false);

            SetDefault(OsuSetting.SlopFileSelectorRecentDirectories, string.Empty);

            SetDefault(OsuSetting.SlopBeatmapMirror, BeatmapMirror.Mino);
        }

        protected override bool CheckLookupContainsPrivateInformation(OsuSetting lookup)
        {
            switch (lookup)
            {
                case OsuSetting.Token:
                    return true;
            }

            return false;
        }

        public override TrackedSettings CreateTrackedSettings()
        {
            return new TrackedSettings
            {
                new TrackedSetting<bool>(OsuSetting.ShowFpsDisplay, state => new SettingDescription(
                    rawValue: state,
                    name: GlobalActionKeyBindingStrings.ToggleFPSCounter,
                    value: state ? CommonStrings.Enabled.ToLower() : CommonStrings.Disabled.ToLower(),
                    shortcut: LookupKeyBindings(GlobalAction.ToggleFPSDisplay))
                ),
                new TrackedSetting<bool>(OsuSetting.MouseDisableButtons, disabledState => new SettingDescription(
                    rawValue: !disabledState,
                    name: GlobalActionKeyBindingStrings.ToggleGameplayMouseButtons,
                    value: disabledState ? CommonStrings.Disabled.ToLower() : CommonStrings.Enabled.ToLower(),
                    shortcut: LookupKeyBindings(GlobalAction.ToggleGameplayMouseButtons))
                ),
                new TrackedSetting<bool>(OsuSetting.GameplayLeaderboard, state => new SettingDescription(
                    rawValue: state,
                    name: GlobalActionKeyBindingStrings.ToggleInGameLeaderboard,
                    value: state ? CommonStrings.Enabled.ToLower() : CommonStrings.Disabled.ToLower(),
                    shortcut: LookupKeyBindings(GlobalAction.ToggleInGameLeaderboard))
                ),
                new TrackedSetting<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode, visibilityMode => new SettingDescription(
                    rawValue: visibilityMode,
                    name: GameplaySettingsStrings.HUDVisibilityMode,
                    value: visibilityMode.GetLocalisableDescription(),
                    shortcut: new TranslatableString(@"_", @"{0}: {1} {2}: {3}",
                        GlobalActionKeyBindingStrings.ToggleInGameInterface,
                        LookupKeyBindings(GlobalAction.ToggleInGameInterface),
                        GlobalActionKeyBindingStrings.HoldForHUD,
                        LookupKeyBindings(GlobalAction.HoldForHUD)))
                ),
                new TrackedSetting<ScalingMode>(OsuSetting.Scaling, scalingMode => new SettingDescription(
                        rawValue: scalingMode,
                        name: GraphicsSettingsStrings.ScreenScaling,
                        value: scalingMode.GetLocalisableDescription()
                    )
                ),
                new TrackedSetting<string>(OsuSetting.Skin, skin =>
                {
                    string skinName = string.Empty;

                    if (Guid.TryParse(skin, out var id))
                        skinName = LookupSkinName(id);

                    return new SettingDescription(
                        rawValue: skinName,
                        name: SkinSettingsStrings.SkinSectionHeader,
                        value: skinName,
                        shortcut: new TranslatableString(@"_", @"{0}: {1}",
                            GlobalActionKeyBindingStrings.RandomSkin,
                            LookupKeyBindings(GlobalAction.RandomSkin))
                    );
                }),
                new TrackedSetting<float>(OsuSetting.UIScale, scale => new SettingDescription(
                        rawValue: scale,
                        name: GraphicsSettingsStrings.UIScaling,
                        value: $"{scale:N2}x"
                        // TODO: implement lookup for framework platform key bindings
                    )
                ),
            };
        }

        public Func<Guid, string> LookupSkinName { private get; set; } = _ => @"unknown";
        public Func<GlobalAction, LocalisableString> LookupKeyBindings { private get; set; } = _ => @"unknown";

        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => GetOriginalBindable<float>(OsuSetting.ComboColourNormalisationAmount);
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => GetOriginalBindable<float>(OsuSetting.PositionalHitsoundsLevel);
    }

    // IMPORTANT: These are used in user configuration files.
    // The naming of these keys should not be changed once they are deployed in a release, unless migration logic is also added.
    public enum OsuSetting
    {
        Ruleset,
        Token,
        MenuCursorSize,
        GameplayCursorSize,
        AutoCursorSize,
        GameplayCursorDuringTouch,
        DimLevel,
        BlurLevel,
        EditorDim,
        LightenDuringBreaks,
        ShowStoryboard,
        KeyOverlay,
        GameplayLeaderboard,
        PositionalHitsoundsLevel,
        AlwaysPlayFirstComboBreak,
        FloatingComments,
        HUDVisibilityMode,

        ShowHealthDisplayWhenCantFail,
        FadePlayfieldWhenHealthLow,

        /// <summary>
        /// Disables mouse buttons clicks during gameplay.
        /// </summary>
        MouseDisableButtons,
        MouseDisableWheel,
        ConfineMouseMode,

        /// <summary>
        /// Globally applied audio offset.
        /// This is added to the audio track's current time. Higher values will cause gameplay to occur earlier, relative to the audio track.
        /// </summary>
        AudioOffset,

        VolumeInactive,
        MenuMusic,
        MenuVoice,
        MenuTips,
        CursorRotation,

        [Obsolete]
        MenuParallax, // todo: can be removed 20270101

        MenuParallaxScale,
        Prefer24HourTime,
        BeatmapDetailTab,
        BeatmapLeaderboardSortMode,
        BeatmapDetailModsFilter,
        Username,
        ReleaseStream,
        SavePassword,
        SaveUsername,
        DisplayStarsMinimum,
        DisplayStarsMaximum,
        SongSelectGroupMode,
        SongSelectSortingMode,
        SongSelectCollectionFilter,
        RandomSelectAlgorithm,
        ModSelectHotkeyStyle,
        ShowFpsDisplay,
        ChatDisplayHeight,
        BeatmapListingCardSize,
        ToolbarClockDisplayMode,
        SongSelectBackgroundBlur,
        Version,
        ShowFirstRunSetup,
        ShowConvertedBeatmaps,
        Skin,
        ScreenshotFormat,
        ScreenshotCaptureMenuCursor,
        BeatmapSkins,
        BeatmapColours,
        BeatmapHitsounds,
        IncreaseFirstObjectVisibility,
        ScoreDisplayMode,
        ExternalLinkWarning,
        PreferNoVideo,
        Scaling,
        ScalingPositionX,
        ScalingPositionY,
        ScalingSizeX,
        ScalingSizeY,
        ScalingBackgroundDim,
        UIScale,
        IntroSequence,
        NotifyOnUsernameMentioned,
        NotifyOnPrivateMessage,
        NotifyOnFriendPresenceChange,
        UIHoldActivationDelay,
        HitLighting,
        StarFountains,
        MenuBackgroundSource,
        GameplayDisableWinKey,
        SeasonalBackgroundMode,
        EditorWaveformOpacity,
        EditorShowHitMarkers,
        EditorAutoSeekOnPlacement,
        DiscordRichPresence,

        ShowOnlineExplicitContent,
        LastProcessedMetadataId,
        SafeAreaConsiderations,
        ComboColourNormalisationAmount,
        ProfileCoverExpanded,
        EditorLimitedDistanceSnap,
        ReplaySettingsOverlay,
        ReplayPlaybackControlsExpanded,
        AutomaticallyDownloadMissingBeatmaps,
        EditorShowSpeedChanges,
        TouchDisableGameplayTaps,
        ModSelectTextSearchStartsActive,

        /// <summary>
        /// The status for the current user to broadcast to other players.
        /// </summary>
        UserOnlineStatus,

        MultiplayerRoomFilter,
        HideCountryFlags,
        EditorTimelineShowTimingChanges,
        EditorTimelineShowTicks,
        AlwaysShowHoldForMenuButton,
        EditorContractSidebars,
        EditorScaleOrigin,
        EditorRotationOrigin,
        EditorTimelineShowBreaks,
        EditorAdjustExistingObjectsOnTimingChanges,
        AlwaysRequireHoldingForPause,
        MultiplayerShowInProgressFilter,
        MultiplayerShowFullFilter,
        BeatmapListingFeaturedArtistFilter,
        ShowMobileDisclaimer,
        EditorShowStoryboard,
        EditorSubmissionNotifyOnDiscussionReplies,
        EditorSubmissionLoadInBrowserAfterSubmission,

        /// <summary>
        /// Cached state of whether local user is a supporter.
        /// Used to allow early checks (ie for startup samples) to be in the correct state, even if the API authentication process has not completed.
        /// </summary>
        WasSupporter,

        LastOnlineTagsPopulation,

        AutomaticallyAdjustBeatmapOffset,

        DashboardSortMode,
        DashboardDisplayStyle,

        /// <summary>
        /// Blocks private messages, multiplayer room invites, and duel requests from people not on the user's friends list.
        /// </summary>
        PMFriendsOnly,

        /// <summary>
        /// Whether slider bodies of legacy and argon skins blur the content behind them (frosted glass).
        /// </summary>
        SlopFrostedSliders,

        /// <summary>
        /// How transparent frosted slider bodies are, from 0 to 1.
        /// </summary>
        SlopFrostedSlidersFrostiness,

        /// <summary>
        /// How strongly frosted slider bodies blur the content behind them, from 0 to 1.
        /// </summary>
        SlopFrostedSlidersBlur,

        /// <summary>
        /// Whether frosted slider bodies are always coloured with the combo colour, ignoring the slider track colour of the skin.
        /// </summary>
        SlopFrostedSlidersAlwaysColoured,

        /// <summary>
        /// Whether the content behind translucent hit circles is blurred like behind frosted slider bodies.
        /// </summary>
        SlopFrostedHitCircles,

        /// <summary>
        /// The cursor used in menus and the editor.
        /// </summary>
        SlopMenuCursorStyle,

        /// <summary>
        /// The ID of the skin to use exclusively inside the beatmap editor.
        /// An empty value means the regular gameplay skin is used.
        /// </summary>
        SlopEditorSkin,

        /// <summary>
        /// Whether the editor skin should also be used when test playing from the editor.
        /// </summary>
        SlopEditorSkinInTestMode,

        /// <summary>
        /// Whether a compressed copy of the previous beatmap file should be written to the backups folder on every editor save.
        /// </summary>
        SlopEditorBackupOnSave,

        /// <summary>
        /// The interval in minutes at which the editor automatically saves the beatmap. 0 disables autosaving.
        /// </summary>
        SlopEditorAutosaveInterval,

        /// <summary>
        /// Whether the selection box (border, selection count and drag handles) is shown around selected objects in the beatmap editor.
        /// </summary>
        SlopEditorShowSelectionBox,

        /// <summary>
        /// Whether the selection box buttons (rotate, flip, reverse) are shown in the beatmap editor.
        /// </summary>
        SlopEditorShowSelectionBoxButtons,

        /// <summary>
        /// Whether the arc for adjusting a slider's length is shown at the end of selected sliders in the beatmap editor.
        /// </summary>
        SlopEditorShowSliderEndDragMarker,

        /// <summary>
        /// Whether dragging objects and slider control points in the beatmap editor starts immediately,
        /// rather than only after the mouse has moved a minimum distance.
        /// </summary>
        SlopEditorImmediateDrag,

        /// <summary>
        /// The shape of slider control points and selection box handles in the beatmap editor.
        /// </summary>
        SlopEditorAnchorShape,

        /// <summary>
        /// Whether objects in the beatmap editor snap to positions which form an equilateral triangle with two other visible objects.
        /// </summary>
        SlopEditorVisualSpacingSnap,

        /// <summary>
        /// Whether objects in the beatmap editor snap to the centres of circular arcs of visible sliders, so that they are perfectly blanketed.
        /// </summary>
        SlopEditorBlanketSnap,

        /// <summary>
        /// How the waveform is displayed in the timeline of the beatmap editor.
        /// </summary>
        SlopEditorWaveformStyle,

        /// <summary>
        /// Whether playback in the beatmap editor continues from the start of the track when the end is reached.
        /// </summary>
        SlopEditorLoopMusic,

        /// <summary>
        /// Whether objects in the beatmap editor snap to positions which continue straight lines of equally spaced visible objects.
        /// </summary>
        SlopEditorLineSnap,

        /// <summary>
        /// Whether sliders snap to positions where their circular arcs share the centre of circular arcs of other sliders, such that one blankets the other.
        /// </summary>
        SlopEditorSliderBlanketSnap,

        /// <summary>
        /// Whether rotating the selection with ctrl+shift+scroll rotates around the centre of the hit circles and slider heads, rather than of the whole selection.
        /// </summary>
        SlopEditorRotateAroundObjectStarts,

        /// <summary>
        /// Whether dragging the end of a slider on the timeline changes its velocity while holding alt, rather than shift.
        /// </summary>
        SlopEditorTimelineSliderVelocityWithAlt,

        /// <summary>
        /// Whether the difficulty strain of the beatmap is displayed in the summary timeline at the bottom of the beatmap editor.
        /// </summary>
        SlopEditorShowDifficultyStrains,

        /// <summary>
        /// Whether objects which go offscreen on 4:3 aspect ratio are outlined in the beatmap editor.
        /// </summary>
        SlopEditorShowOffscreenObjects,

        /// <summary>
        /// The DPI (counts per inch) of the mouse, used for the sensitivity of the FPoSu ruleset.
        /// </summary>
        SlopFposuMouseDpi,

        /// <summary>
        /// The distance in centimetres the mouse has to be moved for a full turn in the FPoSu ruleset.
        /// </summary>
        SlopFposuCmPer360,

        /// <summary>
        /// The horizontal field of view in degrees in the FPoSu ruleset.
        /// </summary>
        SlopFposuFov,

        /// <summary>
        /// The distance of the playfield screen from the camera in the FPoSu ruleset.
        /// </summary>
        SlopFposuDistance,

        /// <summary>
        /// Whether the playfield screen is curved around the camera in the FPoSu ruleset.
        /// </summary>
        SlopFposuCurved,

        /// <summary>
        /// Whether horizontal mouse movement is inverted in the FPoSu ruleset.
        /// </summary>
        SlopFposuInvertHorizontal,

        /// <summary>
        /// Whether vertical mouse movement is inverted in the FPoSu ruleset.
        /// </summary>
        SlopFposuInvertVertical,

        /// <summary>
        /// Whether the camera looks at the cursor (e.g. for tablets) instead of being turned by mouse movement in the FPoSu ruleset.
        /// </summary>
        SlopFposuAbsoluteMode,

        /// <summary>
        /// Whether a grid cube is displayed around the camera in the FPoSu ruleset.
        /// </summary>
        SlopFposuBackgroundCube,

        /// <summary>
        /// Whether a skybox is displayed around the camera in the FPoSu ruleset, instead of the background cube.
        /// </summary>
        SlopFposuSkybox,

        /// <summary>
        /// The ID of the offline profile which is the local user while not logged in, or empty to play as a guest.
        /// </summary>
        SlopActiveOfflineProfile,

        /// <summary>
        /// Whether scores on unranked beatmaps count for the performance, ranked score and grades of offline profiles.
        /// </summary>
        SlopOfflineProfilesIncludeUnranked,

        /// <summary>
        /// The directories which files were recently selected in, most recent first, separated by <see cref="Graphics.UserInterfaceV2.FileSelection.RecentDirectories.SEPARATOR"/>.
        /// </summary>
        SlopFileSelectorRecentDirectories,

        /// <summary>
        /// The <see cref="Online.BeatmapMirrors.BeatmapMirror"/> which beatmaps are downloaded from while not logged in or while connected to the development server.
        /// </summary>
        SlopBeatmapMirror,
    }
}
