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
using osu.Game.Graphics.ColourEffects;
using osu.Game.Graphics.Cursor;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.BeatmapMirrors;
using osu.Game.Online.BnTracker;
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

            SetDefault(OsuSetting.HitsoundVolume, 0.6, 0, 1, 0.01);

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
            SetDefault(OsuSetting.SlopForceArgonCursorTrail, false);
            SetDefault(OsuSetting.SlopSliderBorderComboColour, false);
            SetDefault(OsuSetting.SlopCursorColourEffect, false);
            SetDefault(OsuSetting.SlopCursorColourEffectMode, ColourEffectMode.Rainbow);
            SetDefault(OsuSetting.SlopCursorColourEffectGradient, ColourEffect.DEFAULT_GRADIENT);
            SetDefault(OsuSetting.SlopCursorTrailColourEffect, false);
            SetDefault(OsuSetting.SlopCursorTrailColourEffectMode, ColourEffectMode.Rainbow);
            SetDefault(OsuSetting.SlopCursorTrailColourEffectGradient, ColourEffect.DEFAULT_GRADIENT);
            SetDefault(OsuSetting.SlopComboColourEffect, false);
            SetDefault(OsuSetting.SlopComboColourEffectMode, ColourEffectMode.Rainbow);
            SetDefault(OsuSetting.SlopComboColourEffectGradient, ColourEffect.DEFAULT_COMBO_GRADIENT);
            SetDefault(OsuSetting.SlopColourEffectSpeed, 1f, 0.1f, 5f, 0.05f);
            SetDefault(OsuSetting.SlopColourEffectRainbowSaturation, 0.7f, 0f, 1f, 0.01f);

            SetDefault(OsuSetting.SlopDefaultMods, string.Empty);

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
            SetDefault(OsuSetting.SlopEditorTriangleCentreSnap, true);
            SetDefault(OsuSetting.SlopEditorSliderBlanketSnap, true);
            SetDefault(OsuSetting.SlopEditorRotateAroundObjectStarts, true);
            SetDefault(OsuSetting.SlopEditorTimelineSliderVelocityWithAlt, false);
            SetDefault(OsuSetting.SlopEditorShowDifficultyStrains, false);
            SetDefault(OsuSetting.SlopEditorShowOffscreenObjects, false);
            SetDefault(OsuSetting.SlopEditorWaveformStyle, EditorWaveformStyle.Default);
            SetDefault(OsuSetting.SlopEditorUnsnapMode, EditorUnsnapMode.Enabled);
            SetDefault(OsuSetting.SlopEditorUnsnapDivisors, @"10,12,16");
            SetDefault(OsuSetting.SlopEditorLoopMusic, false);
            SetDefault(OsuSetting.SlopEditorMutedEndsSoftC999, false);
            SetDefault(OsuSetting.SlopEditorZoomOut, false);
            SetDefault(OsuSetting.SlopEditorShowGrid, true);
            SetDefault(OsuSetting.SlopEditorRotateAroundSelection, true);
            SetDefault(OsuSetting.SlopEditorToolKeysSwapped, false);
            SetDefault(OsuSetting.SlopAngledFlipKeyMoved, false);
            SetDefault(OsuSetting.SlopHitsoundEditorFrostedLanes, false);
            SetDefault(OsuSetting.SlopHitsoundEditorToolboxExpandOnHover, true);
            SetDefault(OsuSetting.SlopEditorAutoApplyHitsoundDifficulty, false);
            SetDefault(OsuSetting.SlopHitsoundDifficultyModes, string.Empty);
            SetDefault(OsuSetting.SlopHitsoundSyncExcludedDifficulties, string.Empty);
            SetDefault(OsuSetting.SlopHitsoundDefaultNormalBanks, string.Empty);
            SetDefault(OsuSetting.SlopBnTrackerFrostedPanels, false);

            SetDefault(OsuSetting.SlopMapsetVerifierShowMinor, false);
            SetDefault(OsuSetting.SlopMapsetVerifierHiddenMinorChecks, string.Empty);
            SetDefault(OsuSetting.SlopMapsetVerifierShowIssueDetails, true);
            SetDefault(OsuSetting.SlopMapsetVerifierShowGamemodeDifficultyNames, true);
            SetDefault(OsuSetting.SlopMapsetVerifierAutoSnapshot, true);
            SetDefault(OsuSetting.SlopMapsetVerifierShowCheckRunDelta, true);
            SetDefault(OsuSetting.SlopMapsetVerifierDeltaShowUnchanged, false);
            SetDefault(OsuSetting.SlopMapsetVerifierStrainDisplayMode, MapsetVerifierStrainDisplayMode.Strain);
            SetDefault(OsuSetting.SlopMapsetVerifierShowAdvancedAudioAnalysis, false);
            SetDefault(OsuSetting.SlopMapsetVerifierShowCheckSpeedStats, false);
            SetDefault(OsuSetting.SlopMapsetVerifierRecheckOnSave, true);
            SetDefault(OsuSetting.SlopMapsetVerifierFrostedPanels, false);

            // defaults of McOsu's FPoSu.
            SetDefault(OsuSetting.SlopFposuMouseDpi, 400, 50, 32000);
            SetDefault(OsuSetting.SlopFposuCmPer360, 30.0, 1.0, 200.0, 0.000001);
            SetDefault(OsuSetting.SlopFposuSensitivityGame, 0, 0, 7);
            SetDefault(OsuSetting.SlopFposuGameSensitivity, 1.0, 0.0, 200.0, 0.000001);
            SetDefault(OsuSetting.SlopFposuFov, 103f, 20f, 150f, 1f);
            SetDefault(OsuSetting.SlopFposuDistance, 0.5f, 0.1f, 2f, 0.01f);
            SetDefault(OsuSetting.SlopFposuCurved, true);
            SetDefault(OsuSetting.SlopFposuInvertHorizontal, false);
            SetDefault(OsuSetting.SlopFposuInvertVertical, false);
            SetDefault(OsuSetting.SlopFposuAbsoluteMode, false);
            SetDefault(OsuSetting.SlopFposuBackgroundCube, true);
            SetDefault(OsuSetting.SlopFposuSkybox, true);
            SetDefault(OsuSetting.SlopFposuShowBackground, true);
            SetDefault(OsuSetting.SlopFposuScaleCursorWithDistance, false);

            SetDefault(OsuSetting.SlopSkipBreaks, false);

            SetDefault(OsuSetting.SlopActiveOfflineProfile, string.Empty);
            SetDefault(OsuSetting.SlopOfflineProfilesIncludeUnranked, false);

            SetDefault(OsuSetting.SlopFileSelectorRecentDirectories, string.Empty);

            SetDefault(OsuSetting.SlopBeatmapMirror, BeatmapMirror.Mino);

            SetDefault(OsuSetting.SlopBnTrackerServer, BnTrackerClient.DEFAULT_SERVER);
            SetDefault(OsuSetting.SlopBnTrackerToken, string.Empty);

            SetDefault(OsuSetting.SlopUnofficialBuildNotifiedVersion, string.Empty);
            SetDefault(OsuSetting.SlopUnsupportedRealtimeNotifiedVersion, string.Empty);
            SetDefault(OsuSetting.SlopShowGreatJudgements, true);
            SetDefault(OsuSetting.SlopShowSliderEnds, true);
            SetDefault(OsuSetting.SlopShowComboNumbers, true);
            SetDefault(OsuSetting.SlopHideFollowPoints, false);
            SetDefault(OsuSetting.SlopHideSliderBodies, false);
            SetDefault(OsuSetting.SlopWhiteApproachCircles, false);
            SetDefault(OsuSetting.SlopDisableKiaiEffects, false);
            SetDefault(OsuSetting.SlopAnimateHitCircleOverlay, false);
            SetDefault(OsuSetting.SlopInstafadeSliders, false);
            SetDefault(OsuSetting.SlopDisableFollowCircleAnimations, false);
            SetDefault(OsuSetting.SlopDisableReverseArrowAnimations, false);
            SetDefault(OsuSetting.SlopInstafadeNumbers, false);
            SetDefault(OsuSetting.SlopForceDisableCursorExpand, false);
            SetDefault(OsuSetting.SlopKillOnFail, true);
            SetDefault(OsuSetting.SlopDisableSoftFailEffects, false);
            SetDefault(OsuSetting.SlopIgnoreBeatmapVolume, IgnoreBeatmapVolumeMode.Never);
            SetDefault(OsuSetting.SlopMuteHitsounds, MuteHitsoundsMode.Never);
            SetDefault(OsuSetting.SlopFlashlightMinSize, false);
            SetDefault(OsuSetting.SlopFlashlightIgnoreComboBreaks, false);
            SetDefault(OsuSetting.SlopInstantComboCounter, false);
            SetDefault(OsuSetting.SlopInstantScoreDisplay, false);
            SetDefault(OsuSetting.SlopFastBackgroundDim, false);
            SetDefault(OsuSetting.SlopCursorTrailStyle, CursorTrailStyle.Default);
            SetDefault(OsuSetting.SlopSmootherCursorTrail, false);
            SetDefault(OsuSetting.SlopManiaJudgementCounter, ManiaJudgementCounterPosition.Disabled);
            SetDefault(OsuSetting.SlopNoManiaJudgementAnimations, false);
            SetDefault(OsuSetting.SlopNoTaikoJudgementAnimations, false);
            SetDefault(OsuSetting.SlopTaikoSkin, string.Empty);
            SetDefault(OsuSetting.SlopCatchSkin, string.Empty);
            SetDefault(OsuSetting.SlopManiaSkin, string.Empty);
            SetDefault(OsuSetting.SlopEditorOnlyAutosaveOwnMaps, true);
            SetDefault(OsuSetting.SlopEditorAutosaveToBackups, false);
            SetDefault(OsuSetting.SlopEditorMyMapsAliases, string.Empty);
            SetDefault(OsuSetting.SlopEditorNoAutomaticBreaks, false);
            SetDefault(OsuSetting.SlopEditorDrawScreenBorders, false);
            SetDefault(OsuSetting.SlopEditorAlwaysHideFps, false);
            SetDefault(OsuSetting.SlopEditorDifficultyStars, true);
            SetDefault(OsuSetting.SlopEditorAlwaysIgnoreBeatmapSamples, false);
            SetDefault(OsuSetting.SlopEditorPointInsertPreview, false);
            SetDefault(OsuSetting.SlopEditorSliderCurveColour, SliderCurveColourStyle.AnchorAndLine);
            SetDefault(OsuSetting.SlopEditorDisableAltDistanceSnap, false);
            SetDefault(OsuSetting.SlopEditorSliderPartOnFirstClick, false);
            SetDefault(OsuSetting.SlopEditorNoAudioStretch, false);
            SetDefault(OsuSetting.SlopEditorPreviewMods, false);
            SetDefault(OsuSetting.SlopEditorSongSelectShowPlayButtons, false);
            SetDefault(OsuSetting.SlopEditorManiaPlayfieldWaveform, true);
            SetDefault(OsuSetting.SlopEditorTextEditor, @"notepad.exe");
            SetDefault(OsuSetting.SlopEditorShowAutoplayCursor, false);
            SetDefault(OsuSetting.SlopEditorAutoplayCursorOpacity, 0.6f, 0f, 1f, 0.01f);
            SetDefault(OsuSetting.SlopEditorSnapSliderEndsToLaterTimingPoints, true);
            SetDefault(OsuSetting.SlopEditorAutoplayCursorSkin, string.Empty);
            SetDefault(OsuSetting.SlopEditorAutoplayEngine, SlopAutoplayEngine.Realistic);
            SetDefault(OsuSetting.SlopEditorFlashAdditions, false);
            SetDefault(OsuSetting.SlopEditorTrueSliderEnds, false);
            SetDefault(OsuSetting.SlopEditorAnchorSnap, true);
            SetDefault(OsuSetting.SlopEditorStackSnap, true);
            SetDefault(OsuSetting.SlopEditorCustomRotation, 60, 0, 180);
            SetDefault(OsuSetting.SlopEditorAlignBackground, false);
            SetDefault(OsuSetting.SlopMinimalReplayFrames, false);
            SetDefault(OsuSetting.SlopSkipIntro, false);
            SetDefault(OsuSetting.SlopInstantScreenTransitions, false);
            SetDefault(OsuSetting.SlopFrostedSettings, true);
            SetDefault(OsuSetting.SlopSongSelectSortDirection, SongSelectSortDirection.TopToBottom);
            SetDefault(OsuSetting.SlopBackupOnUpdate, true);
            SetDefault(OsuSetting.SlopStrainGraphLowColour, @"#FFFFFF");
            SetDefault(OsuSetting.SlopStrainGraphHighColour, @"#FFFFFF");
            SetDefault(OsuSetting.SlopStrainGraphActiveAlpha, 75, 0, 100);
            SetDefault(OsuSetting.SlopStrainGraphInactiveAlpha, 25, 0, 100);
            SetDefault(OsuSetting.SlopManiaSnapColour2, @"#FC3636");
            SetDefault(OsuSetting.SlopManiaSnapColour3, @"#0093FD");
            SetDefault(OsuSetting.SlopManiaSnapColour4, @"#813BC6");
            SetDefault(OsuSetting.SlopManiaSnapColour6, @"#F2C94C");
            SetDefault(OsuSetting.SlopManiaSnapColour8, @"#D34B8C");
            SetDefault(OsuSetting.SlopManiaSnapColour12, @"#F2994A");
            SetDefault(OsuSetting.SlopManiaSnapColour16, @"#00D1FF");
            SetDefault(OsuSetting.SlopManiaSnapColourOther, @"#FFFFFF");
            SetDefault(OsuSetting.SlopKeyOverlayColourK1, @"#FFDE00");
            SetDefault(OsuSetting.SlopKeyOverlayColourK2, @"#FFDE00");
            SetDefault(OsuSetting.SlopKeyOverlayColourM1, @"#F8009E");
            SetDefault(OsuSetting.SlopKeyOverlayColourM2, @"#F8009E");
            SetDefault(OsuSetting.SlopAutoplayEngine, SlopAutoplayEngine.Realistic);
            SetDefault(OsuSetting.SlopSaveAutoplayReplays, false);
            SetDefault(OsuSetting.SlopMikuMedalUnlocked, false);
            SetDefault(OsuSetting.SlopMikuTakeover, false);
            SetDefault(OsuSetting.SlopAutoPlusFollowSliders, true);
            SetDefault(OsuSetting.SlopAutoPlusFollowSliderEnds, true);
            SetDefault(OsuSetting.SlopAutoPlusRotation, false);
            SetDefault(OsuSetting.SlopAutoPlusBounceBorder, false);
            SetDefault(OsuSetting.SlopAutoPlusAlternateButtons, true);
            SetDefault(OsuSetting.SlopAutoPlusCubicSplineInterpolation, true);
            SetDefault(OsuSetting.SlopAutoPlusHalfCircleInterpolation, false);
            SetDefault(OsuSetting.SlopAutoPlusHalfCircleStreamInterpolation, true);
            SetDefault(OsuSetting.SlopAutoPlusAngularStreamInterpolation, true);
            SetDefault(OsuSetting.SlopAutoPlusInterpolationFrequency, 1000.0, 0.0, 1000.0, 1.0);
            SetDefault(OsuSetting.SlopAutoPlusSliderSpeedFactor, 4.0, 0.0, 20.0, 0.1);
            SetDefault(OsuSetting.SlopAutoPlusStreamSpeedFactor, 4.0, 0.0, 20.0, 0.1);
            SetDefault(OsuSetting.SlopAutoPlusStreamHalfCircleBorderAngle, 30.0, 0.0, 80.0, 1.0);
            SetDefault(OsuSetting.SlopAutoPlusZeroInterpolation, false);
            SetDefault(OsuSetting.SlopAutoPlusTag, false);
            SetDefault(OsuSetting.SlopAutoPlusTagCount, 0, 0, 16);
            SetDefault(OsuSetting.SlopAutoPlusTagSplit, AutoPlusTagSplitMode.Colour);
            SetDefault(OsuSetting.SlopAutoPlusTagColours, AutoPlusTagColourSource.MapColours);
            SetDefault(OsuSetting.SlopAllowOverlaysDuringGameplay, true);
        }

        protected override bool CheckLookupContainsPrivateInformation(OsuSetting lookup)
        {
            switch (lookup)
            {
                case OsuSetting.Token:
                case OsuSetting.SlopBnTrackerToken:
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
        HitsoundVolume,

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
        /// Whether the gameplay cursor cycles through rainbow or gradient colours.
        /// </summary>
        SlopCursorColourEffect,

        /// <summary>
        /// The <see cref="ColourEffectMode"/> of the gameplay cursor colours.
        /// </summary>
        SlopCursorColourEffectMode,

        /// <summary>
        /// The colours the gameplay cursor cycles through in <see cref="ColourEffectMode.Gradient"/> mode, as comma-separated hex codes.
        /// </summary>
        SlopCursorColourEffectGradient,

        /// <summary>
        /// Whether the cursor trail cycles through rainbow or gradient colours.
        /// </summary>
        SlopCursorTrailColourEffect,

        /// <summary>
        /// The <see cref="ColourEffectMode"/> of the cursor trail colours.
        /// </summary>
        SlopCursorTrailColourEffectMode,

        /// <summary>
        /// The colours the cursor trail cycles through in <see cref="ColourEffectMode.Gradient"/> mode, as comma-separated hex codes.
        /// </summary>
        SlopCursorTrailColourEffectGradient,

        /// <summary>
        /// Whether combo colours of hit objects are replaced by cycling rainbow or gradient colours.
        /// </summary>
        SlopComboColourEffect,

        /// <summary>
        /// The <see cref="ColourEffectMode"/> of the combo colours.
        /// </summary>
        SlopComboColourEffectMode,

        /// <summary>
        /// The colours combo colours cycle through in <see cref="ColourEffectMode.Gradient"/> mode, as comma-separated hex codes.
        /// </summary>
        SlopComboColourEffectGradient,

        /// <summary>
        /// How fast all rainbow and gradient colour effects cycle. Shared so that all effects stay in sync.
        /// </summary>
        SlopColourEffectSpeed,

        /// <summary>
        /// The saturation of all rainbow colour effects. Lower values make dark hues like blue and purple brighter.
        /// </summary>
        SlopColourEffectRainbowSaturation,

        /// <summary>
        /// The mods which are selected by default for each ruleset, serialised as JSON.
        /// </summary>
        SlopDefaultMods,

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
        /// Which divisors hit object nodes may be snapped to before they are marked as unsnapped in the timeline of the beatmap editor.
        /// </summary>
        SlopEditorUnsnapMode,

        /// <summary>
        /// The comma-separated divisors hit object nodes may be snapped to with <see cref="EditorUnsnapMode.Enabled"/>.
        /// </summary>
        SlopEditorUnsnapDivisors,

        /// <summary>
        /// Whether playback in the beatmap editor continues from the start of the track when the end is reached.
        /// </summary>
        SlopEditorLoopMusic,

        /// <summary>
        /// Whether muting object ends in the editor also sets their samples to the soft bank with custom index 999.
        /// </summary>
        SlopEditorMutedEndsSoftC999,

        /// <summary>
        /// Whether the playfield of the compose screen is shrunk to leave room around it.
        /// </summary>
        SlopEditorZoomOut,

        /// <summary>
        /// Whether the positional snap grid is drawn in the compose screen.
        /// </summary>
        SlopEditorShowGrid,

        /// <summary>
        /// Whether the rotation hotkeys and scroll rotation rotate around the selection, instead of around the centre of the playfield.
        /// </summary>
        SlopEditorRotateAroundSelection,

        /// <summary>
        /// Whether the stored osu! editor tool key bindings were migrated to the stream tool being on 5 and the grid tool on 6.
        /// Not exposed to the user.
        /// </summary>
        SlopEditorToolKeysSwapped,
        SlopAngledFlipKeyMoved,

        /// <summary>
        /// Whether the lanes of the hitsound editor have a frosted glass background, instead of an opaque one.
        /// </summary>
        SlopHitsoundEditorFrostedLanes,

        /// <summary>
        /// Whether the contracted toolbox of the hitsound editor expands while hovering it, like the toolboxes of the compose screen.
        /// </summary>
        SlopHitsoundEditorToolboxExpandOnHover,

        /// <summary>
        /// Whether hit objects which are placed or moved in a difficulty get the hitsounds of the hitsound difficulty of the beatmap set.
        /// </summary>
        SlopEditorAutoApplyHitsoundDifficulty,

        /// <summary>
        /// Difficulties for which hitsound difficulty mode was turned on or off in the hitsound editor, unlike their name suggests (see <see cref="Screens.Edit.Hitsounding.HitsoundPreferences"/>).
        /// </summary>
        SlopHitsoundDifficultyModes,

        /// <summary>
        /// Difficulties which don't receive the hitsounds of the hitsound difficulty (see <see cref="Screens.Edit.Hitsounding.HitsoundPreferences"/>).
        /// </summary>
        SlopHitsoundSyncExcludedDifficulties,

        /// <summary>
        /// The banks of the hitnormals played along with new additions, by beatmap set (see <see cref="Screens.Edit.Hitsounding.HitsoundPreferences"/>).
        /// </summary>
        SlopHitsoundDefaultNormalBanks,

        /// <summary>
        /// Whether objects in the beatmap editor snap to positions which continue straight lines of equally spaced visible objects.
        /// </summary>
        SlopEditorLineSnap,

        /// <summary>
        /// Whether objects in the beatmap editor snap to the centre of equilateral triangles formed by three visible objects.
        /// </summary>
        SlopEditorTriangleCentreSnap,

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
        /// The DPI (counts per inch) of the mouse, used for the sensitivity of the osu!fps ruleset.
        /// </summary>
        SlopFposuMouseDpi,

        /// <summary>
        /// The distance in centimetres the mouse has to be moved for a full turn in the osu!fps ruleset.
        /// </summary>
        SlopFposuCmPer360,

        /// <summary>
        /// The game (as an index) whose sensitivity is entered instead of cm/360 in the osu!fps ruleset.
        /// </summary>
        SlopFposuSensitivityGame,

        /// <summary>
        /// The sensitivity in the selected game, which is converted to cm/360 in the osu!fps ruleset.
        /// </summary>
        SlopFposuGameSensitivity,

        /// <summary>
        /// The horizontal field of view in degrees in the osu!fps ruleset.
        /// </summary>
        SlopFposuFov,

        /// <summary>
        /// The distance of the playfield screen from the camera in the osu!fps ruleset.
        /// </summary>
        SlopFposuDistance,

        /// <summary>
        /// Whether the playfield screen is curved around the camera in the osu!fps ruleset.
        /// </summary>
        SlopFposuCurved,

        /// <summary>
        /// Whether horizontal mouse movement is inverted in the osu!fps ruleset.
        /// </summary>
        SlopFposuInvertHorizontal,

        /// <summary>
        /// Whether vertical mouse movement is inverted in the osu!fps ruleset.
        /// </summary>
        SlopFposuInvertVertical,

        /// <summary>
        /// Whether the camera looks at the cursor (e.g. for tablets) instead of being turned by mouse movement in the osu!fps ruleset.
        /// </summary>
        SlopFposuAbsoluteMode,

        /// <summary>
        /// Whether a grid cube is displayed around the camera in the osu!fps ruleset.
        /// </summary>
        SlopFposuBackgroundCube,

        /// <summary>
        /// Whether a skybox is displayed around the camera in the osu!fps ruleset, instead of the background cube.
        /// </summary>
        SlopFposuSkybox,

        /// <summary>
        /// Whether the beatmap background, storyboard and video are shown on the screen in the osu!fps ruleset. When disabled, the objects float in front of the skybox or background cube.
        /// </summary>
        SlopFposuShowBackground,

        /// <summary>
        /// Whether the cursor is scaled with the distance of the playfield in the osu!fps ruleset, instead of keeping its size when the distance changes.
        /// </summary>
        SlopFposuScaleCursorWithDistance,

        /// <summary>
        /// Whether a skip button is displayed during breaks, like the one at the start of a beatmap.
        /// </summary>
        SlopSkipBreaks,

        /// <summary>
        /// Whether the cursor trail is always replaced with the argon cursor trail, regardless of the current skin.
        /// </summary>
        SlopForceArgonCursorTrail,
        SlopSliderBorderComboColour,

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

        /// <summary>
        /// The version of the game for which the user was notified that it isn't an official build, such that it is only shown once after installing a version.
        /// </summary>
        SlopUnofficialBuildNotifiedVersion,

        /// <summary>
        /// The version of the game for which the user was notified that the servers don't support its realtime functionality, such that it is only shown once after installing a version.
        /// </summary>
        SlopUnsupportedRealtimeNotifiedVersion,

        /// <summary>
        /// The address of the BN Tracker server, which tracks the Beatmap Nominators asked to nominate a beatmap set (see the "request" screen of the beatmap editor).
        /// </summary>
        SlopBnTrackerServer,

        /// <summary>
        /// The API token of the BN Tracker server.
        /// </summary>
        SlopBnTrackerToken,

        /// <summary>
        /// Whether the panels of the "request" screen of the beatmap editor have a frosted glass background, instead of an opaque one.
        /// </summary>
        SlopBnTrackerFrostedPanels,

        /// <summary>
        /// Whether negligible (minor) issues are included on the checks page of the mapset verifier.
        /// </summary>
        SlopMapsetVerifierShowMinor,

        /// <summary>
        /// The IDs of the checks whose negligible issues stay hidden even while negligible issues are shown, separated by commas.
        /// </summary>
        SlopMapsetVerifierHiddenMinorChecks,

        /// <summary>
        /// Whether hovering an issue of the mapset verifier shows its details and the ranking criteria it enforces.
        /// </summary>
        SlopMapsetVerifierShowIssueDetails,

        /// <summary>
        /// Whether the mapset verifier names difficulty levels like the game mode does (e.g. "Kantan" in osu!taiko).
        /// </summary>
        SlopMapsetVerifierShowGamemodeDifficultyNames,

        /// <summary>
        /// Whether the mapset verifier stores a snapshot of the beatmap set before each check run.
        /// </summary>
        SlopMapsetVerifierAutoSnapshot,

        /// <summary>
        /// Whether the mapset verifier shows what changed since the previous check run.
        /// </summary>
        SlopMapsetVerifierShowCheckRunDelta,

        /// <summary>
        /// Whether the changes since the previous check run also list the unchanged issues.
        /// </summary>
        SlopMapsetVerifierDeltaShowUnchanged,

        /// <summary>
        /// What the star rating chart of the mapset verifier's difficulty overview shows.
        /// </summary>
        SlopMapsetVerifierStrainDisplayMode,

        /// <summary>
        /// Whether the audio overview of the mapset verifier shows the frequency analysis.
        /// </summary>
        SlopMapsetVerifierShowAdvancedAudioAnalysis,

        /// <summary>
        /// Whether the mapset verifier shows how long each check took.
        /// </summary>
        SlopMapsetVerifierShowCheckSpeedStats,

        /// <summary>
        /// Whether the mapset verifier checks the beatmap set again after saving it in the editor.
        /// </summary>
        SlopMapsetVerifierRecheckOnSave,

        /// <summary>
        /// Whether the panels of the mapset verifier have a frosted glass background, instead of an opaque one.
        /// </summary>
        SlopMapsetVerifierFrostedPanels,

        /// <summary>
        /// Whether judgements of the highest hit result (300s) are displayed.
        /// </summary>
        SlopShowGreatJudgements,

        /// <summary>
        /// Whether slider end circles are displayed during gameplay.
        /// </summary>
        SlopShowSliderEnds,

        /// <summary>
        /// Whether combo numbers are displayed on hit circles.
        /// </summary>
        SlopShowComboNumbers,

        /// <summary>
        /// Whether follow points are hidden.
        /// </summary>
        SlopHideFollowPoints,

        /// <summary>
        /// Whether slider bodies are hidden.
        /// </summary>
        SlopHideSliderBodies,

        /// <summary>
        /// Whether approach circles are white instead of using the combo colour.
        /// </summary>
        SlopWhiteApproachCircles,

        /// <summary>
        /// Whether kiai time effects should be ignored during gameplay.
        /// </summary>
        SlopDisableKiaiEffects,

        /// <summary>
        /// Whether legacy hitcircleoverlay animations should use the skin's configured framerate.
        /// </summary>
        SlopAnimateHitCircleOverlay,

        /// <summary>
        /// Whether sliders should disappear instantly after they end.
        /// </summary>
        SlopInstafadeSliders,

        /// <summary>
        /// Whether follow circle animations should be disabled.
        /// </summary>
        SlopDisableFollowCircleAnimations,

        /// <summary>
        /// Whether reverse arrow animations should be disabled.
        /// </summary>
        SlopDisableReverseArrowAnimations,

        /// <summary>
        /// Whether combo numbers should disappear instantly when a circle is hit.
        /// </summary>
        SlopInstafadeNumbers,

        /// <summary>
        /// Whether the cursor should never expand on click, regardless of skin.
        /// </summary>
        SlopForceDisableCursorExpand,

        /// <summary>
        /// Whether failing should end gameplay. When disabled, failing only marks the score as failed, like in multiplayer.
        /// </summary>
        SlopKillOnFail,

        /// <summary>
        /// Whether the dimming effect and notification should be hidden when failing with <see cref="SlopKillOnFail"/> disabled.
        /// </summary>
        SlopDisableSoftFailEffects,

        /// <summary>
        /// When beatmap sample volumes should be ignored during gameplay.
        /// </summary>
        SlopIgnoreBeatmapVolume,

        /// <summary>
        /// When hitsounds should be muted during gameplay.
        /// </summary>
        SlopMuteHitsounds,

        /// <summary>
        /// Whether the flashlight should always use its minimum (200x combo) size.
        /// </summary>
        SlopFlashlightMinSize,

        /// <summary>
        /// Whether combo breaks should not shrink the flashlight back, letting its size progress as if no break happened.
        /// </summary>
        SlopFlashlightIgnoreComboBreaks,

        /// <summary>
        /// Whether the combo counter should skip its increase and decrease animations.
        /// </summary>
        SlopInstantComboCounter,

        /// <summary>
        /// Whether the score and accuracy displays should skip their rolling animations.
        /// </summary>
        SlopInstantScoreDisplay,

        /// <summary>
        /// Whether gameplay background dim changes should happen much faster.
        /// </summary>
        SlopFastBackgroundDim,

        /// <summary>
        /// The style of the gameplay cursor trail.
        /// </summary>
        SlopCursorTrailStyle,

        /// <summary>
        /// Whether the cursor trail should create new trail points after half the usual movement.
        /// </summary>
        SlopSmootherCursorTrail,

        /// <summary>
        /// Where the osu!mania judgement counter is displayed.
        /// </summary>
        SlopManiaJudgementCounter,

        /// <summary>
        /// Whether the scaling animation of osu!mania judgements should be removed.
        /// </summary>
        SlopNoManiaJudgementAnimations,

        /// <summary>
        /// Whether the scaling animation of osu!taiko judgements should be removed.
        /// </summary>
        SlopNoTaikoJudgementAnimations,

        /// <summary>
        /// The ID of the skin used during osu!taiko gameplay, or empty to use the main skin.
        /// </summary>
        SlopTaikoSkin,

        /// <summary>
        /// The ID of the skin used during osu!catch gameplay, or empty to use the main skin.
        /// </summary>
        SlopCatchSkin,

        /// <summary>
        /// The ID of the skin used during osu!mania gameplay, or empty to use the main skin.
        /// </summary>
        SlopManiaSkin,

        /// <summary>
        /// Whether autosaving is restricted to beatmaps created by the local user (or one of their aliases).
        /// </summary>
        SlopEditorOnlyAutosaveOwnMaps,

        /// <summary>
        /// Whether autosaves are written to the backups folder instead of overwriting the beatmap.
        /// </summary>
        SlopEditorAutosaveToBackups,

        /// <summary>
        /// Comma-separated names which are treated as the local user when checking beatmap ownership.
        /// </summary>
        SlopEditorMyMapsAliases,

        /// <summary>
        /// Whether the editor should never insert breaks automatically.
        /// </summary>
        SlopEditorNoAutomaticBreaks,

        /// <summary>
        /// Whether the bounds of the gameplay screen are shown around the osu! editor playfield.
        /// </summary>
        SlopEditorDrawScreenBorders,

        /// <summary>
        /// Whether the FPS counter is always hidden in the editor.
        /// </summary>
        SlopEditorAlwaysHideFps,

        /// <summary>
        /// Whether the star rating of each difficulty is shown in the editor's difficulty switcher.
        /// </summary>
        SlopEditorDifficultyStars,

        /// <summary>
        /// Whether the editor always uses the skin's samples instead of the beatmap's.
        /// </summary>
        SlopEditorAlwaysIgnoreBeatmapSamples,

        /// <summary>
        /// Whether the insertion of a new slider control point is previewed while holding Ctrl.
        /// </summary>
        SlopEditorPointInsertPreview,

        /// <summary>
        /// How the curve types of slider paths are highlighted in the editor.
        /// </summary>
        SlopEditorSliderCurveColour,

        /// <summary>
        /// Whether holding Alt no longer temporarily toggles distance snap.
        /// </summary>
        SlopEditorDisableAltDistanceSnap,

        /// <summary>
        /// Whether clicking a slider's head or tail selects that part for hitsounding even if the slider wasn't selected before.
        /// </summary>
        SlopEditorSliderPartOnFirstClick,

        /// <summary>
        /// Whether the editor changes the pitch together with the playback speed in all screens instead of stretching the audio.
        /// </summary>
        SlopEditorNoAudioStretch,

        /// <summary>
        /// Whether the editor keeps the selected rate adjust mods (and Hidden in osu!) for playback and test play.
        /// </summary>
        SlopEditorPreviewMods,

        /// <summary>
        /// Whether the mods, random and options footer buttons are shown in the beatmap editor song select.
        /// </summary>
        SlopEditorSongSelectShowPlayButtons,

        /// <summary>
        /// Whether the waveform is displayed under the playfield instead of in the timeline in the osu!mania compose screen.
        /// </summary>
        SlopEditorManiaPlayfieldWaveform,

        /// <summary>
        /// The program used to open beatmap files in a text editor from the editor.
        /// </summary>
        SlopEditorTextEditor,

        /// <summary>
        /// Whether the cursor of the autoplay replay is displayed in the editor.
        /// </summary>
        SlopEditorShowAutoplayCursor,

        /// <summary>
        /// The opacity of the autoplay cursor in the editor.
        /// </summary>
        SlopEditorAutoplayCursorOpacity,

        /// <summary>
        /// Whether slider ends snap to the ticks of the timing point active at the end of the slider, instead of the one at its start.
        /// </summary>
        SlopEditorSnapSliderEndsToLaterTimingPoints,

        /// <summary>
        /// The skin whose cursor is used for the autoplay cursor in the editor, or empty to use the editor skin.
        /// </summary>
        SlopEditorAutoplayCursorSkin,

        /// <summary>
        /// The autoplay engine used to generate the replay which drives gameplay in the editor.
        /// </summary>
        SlopEditorAutoplayEngine,

        /// <summary>
        /// Whether the whistle, finish and clap buttons flash in the editor whenever their addition is played.
        /// </summary>
        SlopEditorFlashAdditions,

        /// <summary>
        /// Whether sliders are displayed with the end at which they are actually judged in the editor.
        /// </summary>
        SlopEditorTrueSliderEnds,

        /// <summary>
        /// Whether dragged slider anchors snap to nearby objects and other anchors.
        /// </summary>
        SlopEditorAnchorSnap,

        /// <summary>
        /// Whether dragged objects snap onto other objects to stack them.
        /// </summary>
        SlopEditorStackSnap,

        /// <summary>
        /// The angle in degrees by which the selection is rotated with the custom rotation shortcuts.
        /// </summary>
        SlopEditorCustomRotation,

        /// <summary>
        /// Whether the editor background is scaled and moved to align with the playfield, like in gameplay.
        /// </summary>
        SlopEditorAlignBackground,

        /// <summary>
        /// Whether replays only record frames which contain important data, such as key presses and judgements.
        /// </summary>
        SlopMinimalReplayFrames,

        /// <summary>
        /// Whether the intro sequence is skipped on startup, going to the main menu directly.
        /// </summary>
        SlopSkipIntro,

        /// <summary>
        /// Whether the fade transitions of screens are finished instantly.
        /// </summary>
        SlopInstantScreenTransitions,

        /// <summary>
        /// Whether the background of the settings is blurred behind them (frosted glass).
        /// </summary>
        SlopFrostedSettings,

        /// <summary>
        /// The direction in which the song select carousel is sorted.
        /// </summary>
        SlopSongSelectSortDirection,

        /// <summary>
        /// Whether the beatmap files of a beatmap set are backed up before it gets updated.
        /// </summary>
        SlopBackupOnUpdate,

        /// <summary>
        /// The colour of low strains in the editor's difficulty strain graph.
        /// </summary>
        SlopStrainGraphLowColour,

        /// <summary>
        /// The colour of high strains in the editor's difficulty strain graph.
        /// </summary>
        SlopStrainGraphHighColour,

        /// <summary>
        /// The opacity (in percent) of the part of the editor's difficulty strain graph before the current time.
        /// </summary>
        SlopStrainGraphActiveAlpha,

        /// <summary>
        /// The opacity (in percent) of the part of the editor's difficulty strain graph after the current time.
        /// </summary>
        SlopStrainGraphInactiveAlpha,

        /// <summary>
        /// The colour of mania notes snapped to 1/1 or 1/2 when using timing-based note colouring.
        /// </summary>
        SlopManiaSnapColour2,

        /// <summary>
        /// The colour of mania notes snapped to 1/3 when using timing-based note colouring.
        /// </summary>
        SlopManiaSnapColour3,

        /// <summary>
        /// The colour of mania notes snapped to 1/4 when using timing-based note colouring.
        /// </summary>
        SlopManiaSnapColour4,

        /// <summary>
        /// The colour of mania notes snapped to 1/6 when using timing-based note colouring.
        /// </summary>
        SlopManiaSnapColour6,

        /// <summary>
        /// The colour of mania notes snapped to 1/8 when using timing-based note colouring.
        /// </summary>
        SlopManiaSnapColour8,

        /// <summary>
        /// The colour of mania notes snapped to 1/12 when using timing-based note colouring.
        /// </summary>
        SlopManiaSnapColour12,

        /// <summary>
        /// The colour of mania notes snapped to 1/16 when using timing-based note colouring.
        /// </summary>
        SlopManiaSnapColour16,

        /// <summary>
        /// The colour of mania notes with any other snap when using timing-based note colouring.
        /// </summary>
        SlopManiaSnapColourOther,

        /// <summary>
        /// The colour of the first key of the legacy key overlay when pressed.
        /// </summary>
        SlopKeyOverlayColourK1,

        /// <summary>
        /// The colour of the second key of the legacy key overlay when pressed.
        /// </summary>
        SlopKeyOverlayColourK2,

        /// <summary>
        /// The colour of the third key of the legacy key overlay when pressed.
        /// </summary>
        SlopKeyOverlayColourM1,

        /// <summary>
        /// The colour of the fourth key of the legacy key overlay when pressed.
        /// </summary>
        SlopKeyOverlayColourM2,

        /// <summary>
        /// The engine used to generate the cursor movement of the osu! autoplay mod.
        /// </summary>
        SlopAutoplayEngine,

        /// <summary>
        /// Whether replays of autoplay plays should be saved after finishing the map.
        /// </summary>
        SlopSaveAutoplayReplays,

        /// <summary>
        /// Whether the client-side Miku medal was unlocked by watching a Miku autoplay up to the results screen.
        /// </summary>
        SlopMikuMedalUnlocked,

        /// <summary>
        /// Whether the menu logo and the triangles intro use the Miku variants (requires <see cref="SlopMikuMedalUnlocked"/>).
        /// </summary>
        SlopMikuTakeover,

        /// <summary>
        /// Whether Auto+ follows the slider ball.
        /// </summary>
        SlopAutoPlusFollowSliders,

        /// <summary>
        /// Whether Auto+ moves to the visible slider ends.
        /// </summary>
        SlopAutoPlusFollowSliderEnds,

        /// <summary>
        /// Whether Auto+ rotates around the points between jumps.
        /// </summary>
        SlopAutoPlusRotation,

        /// <summary>
        /// Whether Auto+ reverses the cursor position when it goes offscreen.
        /// </summary>
        SlopAutoPlusBounceBorder,

        /// <summary>
        /// Whether Auto+ alternates the buttons it presses.
        /// </summary>
        SlopAutoPlusAlternateButtons,

        /// <summary>
        /// Whether Auto+ uses cubic spline interpolation for cursor dancing.
        /// </summary>
        SlopAutoPlusCubicSplineInterpolation,

        /// <summary>
        /// Whether Auto+ uses half-circle interpolation for cursor dancing.
        /// </summary>
        SlopAutoPlusHalfCircleInterpolation,

        /// <summary>
        /// Whether Auto+ uses half-circle interpolation for streams.
        /// </summary>
        SlopAutoPlusHalfCircleStreamInterpolation,

        /// <summary>
        /// Whether Auto+ uses angular interpolation for streams.
        /// </summary>
        SlopAutoPlusAngularStreamInterpolation,

        /// <summary>
        /// How many interpolated cursor positions Auto+ generates per second.
        /// </summary>
        SlopAutoPlusInterpolationFrequency,

        /// <summary>
        /// The speed factor Auto+ applies when entering and leaving sliders.
        /// </summary>
        SlopAutoPlusSliderSpeedFactor,

        /// <summary>
        /// The speed factor Auto+ applies when entering and leaving streams.
        /// </summary>
        SlopAutoPlusStreamSpeedFactor,

        /// <summary>
        /// The border angle of the Auto+ half-circle stream interpolation.
        /// </summary>
        SlopAutoPlusStreamHalfCircleBorderAngle,

        /// <summary>
        /// Whether Auto+ moves the cursor to the top-left corner whenever possible.
        /// </summary>
        SlopAutoPlusZeroInterpolation,

        /// <summary>
        /// Whether Auto+ uses multiple cursors to simulate TAG multiplayer.
        /// </summary>
        SlopAutoPlusTag,

        /// <summary>
        /// The amount of Auto+ TAG cursors (0 uses the amount of colours).
        /// </summary>
        SlopAutoPlusTagCount,

        /// <summary>
        /// How the objects are split between the Auto+ TAG cursors.
        /// </summary>
        SlopAutoPlusTagSplit,

        /// <summary>
        /// Where the colours of the Auto+ TAG cursors come from.
        /// </summary>
        SlopAutoPlusTagColours,

        /// <summary>
        /// Whether overlays such as the settings can be opened while actively playing.
        /// </summary>
        SlopAllowOverlaysDuringGameplay,
    }
}
