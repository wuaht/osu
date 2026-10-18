// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays.Music;
using osuTK;
using osuTK.Graphics;
using Realms;

namespace osu.Game.Overlays.Visualizer
{
    /// <summary>
    /// A fullscreen music player which displays the playing track with an audio visualizer.
    /// </summary>
    public partial class VisualizerOverlay : OsuFocusedOverlayContainer, INamedOverlayComponent, IProvideCursor
    {
        public IconUsage Icon => FontAwesome.Solid.Headphones;
        public LocalisableString Title => SlopVisualizerStrings.HeaderTitle;
        public LocalisableString Description => SlopVisualizerStrings.HeaderDescription;

        /// <summary>
        /// The number of spectrum bands the visualizers are provided with.
        /// </summary>
        private const int band_count = 64;

        private const double transition_duration = 400;
        private const double background_fade_duration = 800;
        private const double visualizer_swap_duration = 300;
        private const double chrome_fade_duration = 300;

        /// <summary>
        /// The time without mouse movement after which the player controls and cursor are hidden.
        /// </summary>
        private const double idle_hide_delay = 2500;

        private const float top_bar_height = 70;
        private const float bottom_bar_height = 170;
        private const float horizontal_padding = 40;
        private const float playlist_width = 400;

        /// <summary>
        /// The minimum zoom of the background, so that shaking never reveals its edges.
        /// </summary>
        private const float background_base_scale = 1.05f;

        protected override double PopInOutSampleBalance => OsuGameBase.SFX_STEREO_STRENGTH * 0.75f;

        protected override bool DimMainContent => false;

        [Resolved]
        private MusicController musicController { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBeatSyncProvider beatSyncProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        /// <summary>
        /// Whether the toolbar was hidden by this overlay while idle, and should be shown again on interaction.
        /// </summary>
        private bool toolbarHiddenByIdle;

        private readonly SpectrumAnalyser analyser = new SpectrumAnalyser(band_count);

        private readonly Bindable<VisualizerStyle> style = new Bindable<VisualizerStyle>();
        private readonly BindableBool shuffle = new BindableBool(true);
        private Bindable<bool> allowTrackControl = null!;

        private Container backgroundContainer = null!;
        private Box dimBox = null!;
        private Container<VisualizerDrawable> visualizerContainer = null!;
        private VisualizerDrawable? visualizer;

        private Container chrome = null!;
        private Container topBar = null!;
        private Container bottomBar = null!;
        private Container playlistContainer = null!;
        private PlaylistOverlay? playlist;

        private PlaylistCollectionDropdown collectionDropdown = null!;
        private RoundedButton styleButton = null!;

        private MarqueeContainer title = null!;
        private MarqueeContainer artist = null!;
        private OsuSpriteText currentTime = null!;
        private OsuSpriteText totalTime = null!;
        private ProgressBar progressBar = null!;

        private MusicIconButton prevButton = null!;
        private MusicIconButton playButton = null!;
        private MusicIconButton nextButton = null!;
        private MusicIconButton shuffleButton = null!;
        private MusicIconButton playlistButton = null!;

        private InputManager inputManager = null!;

        private WorkingBeatmap? currentBeatmap;
        private Action? pendingBeatmapSwitch;
        private CancellationTokenSource? backgroundLoadCancellation;
        private CancellationTokenSource? paletteLoadCancellation;

        private readonly object paletteLock = new object();
        private (WorkingBeatmap beatmap, BackgroundPalette palette)? pendingPalette;

        private BeatmapMetadata? appliedMetadata;
        private BackgroundPalette appliedPalette = BackgroundPalette.DEFAULT;
        private int appliedPaletteSeed;

        private double lastInteractionTime;
        private bool chromeHidden;

        public VisualizerOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopVisualizerStyle, style);

            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                backgroundContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                dimBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                visualizerContainer = new Container<VisualizerDrawable>
                {
                    RelativeSizeAxes = Axes.Both,
                },
                chrome = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        topBar = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = top_bar_height,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0.6f), Color4.Black.Opacity(0)),
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Horizontal = horizontal_padding, Top = 15 },
                                    Children = new Drawable[]
                                    {
                                        collectionDropdown = new PlaylistCollectionDropdown
                                        {
                                            Width = 250,
                                        },
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(10, 0),
                                            Children = new Drawable[]
                                            {
                                                styleButton = new RoundedButton
                                                {
                                                    Anchor = Anchor.CentreRight,
                                                    Origin = Anchor.CentreRight,
                                                    Width = 200,
                                                    Action = cycleStyle,
                                                },
                                                new MusicIconButton
                                                {
                                                    Anchor = Anchor.CentreRight,
                                                    Origin = Anchor.CentreRight,
                                                    Icon = FontAwesome.Solid.Times,
                                                    TooltipText = SlopVisualizerStrings.Close,
                                                    Action = Hide,
                                                },
                                            }
                                        },
                                    }
                                },
                            }
                        },
                        playlistContainer = new Container
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            RelativeSizeAxes = Axes.Y,
                            Width = playlist_width,
                            Padding = new MarginPadding
                            {
                                Top = top_bar_height,
                                Bottom = bottom_bar_height,
                                Right = horizontal_padding,
                            },
                        },
                        bottomBar = new Container
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            Height = bottom_bar_height,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(0), Color4.Black.Opacity(0.75f)),
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.BottomLeft,
                                    Origin = Anchor.BottomLeft,
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 6),
                                    Padding = new MarginPadding { Horizontal = horizontal_padding, Bottom = 20 },
                                    Children = new Drawable[]
                                    {
                                        title = new MarqueeContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            NonOverflowingContentAnchor = Anchor.CentreLeft,
                                            CreateContent = () => createTitleText(SlopVisualizerStrings.NothingToPlay),
                                        },
                                        artist = new MarqueeContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            NonOverflowingContentAnchor = Anchor.CentreLeft,
                                            CreateContent = () => createArtistText(default),
                                        },
                                        new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 20,
                                            Margin = new MarginPadding { Top = 6 },
                                            ColumnDimensions = new[]
                                            {
                                                new Dimension(GridSizeMode.Absolute, 60),
                                                new Dimension(),
                                                new Dimension(GridSizeMode.Absolute, 60),
                                            },
                                            Content = new[]
                                            {
                                                new Drawable[]
                                                {
                                                    currentTime = createTimeText(Anchor.CentreLeft),
                                                    progressBar = new HoverableProgressBar
                                                    {
                                                        Anchor = Anchor.Centre,
                                                        Origin = Anchor.Centre,
                                                        FillColour = Color4.White,
                                                        BackgroundColour = Color4.White.Opacity(0.25f),
                                                        OnSeek = onSeek,
                                                        OnCommit = onCommit,
                                                    },
                                                    totalTime = createTimeText(Anchor.CentreRight),
                                                }
                                            }
                                        },
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Height = 50,
                                            Children = new Drawable[]
                                            {
                                                shuffleButton = new MusicIconButton
                                                {
                                                    Anchor = Anchor.CentreLeft,
                                                    Origin = Anchor.CentreLeft,
                                                    Icon = FontAwesome.Solid.Random,
                                                    TooltipText = SlopVisualizerStrings.Shuffle,
                                                    Action = shuffle.Toggle,
                                                },
                                                new FillFlowContainer
                                                {
                                                    Anchor = Anchor.Centre,
                                                    Origin = Anchor.Centre,
                                                    AutoSizeAxes = Axes.Both,
                                                    Direction = FillDirection.Horizontal,
                                                    Spacing = new Vector2(15, 0),
                                                    Children = new Drawable[]
                                                    {
                                                        prevButton = new MusicIconButton
                                                        {
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            Icon = FontAwesome.Solid.StepBackward,
                                                            TooltipText = SlopVisualizerStrings.PreviousTrack,
                                                            Action = () => musicController.PreviousTrack(),
                                                        },
                                                        playButton = new MusicIconButton
                                                        {
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            Scale = new Vector2(1.4f),
                                                            IconScale = new Vector2(1.4f),
                                                            Icon = FontAwesome.Regular.PlayCircle,
                                                            Action = () => musicController.TogglePause(),
                                                        },
                                                        nextButton = new MusicIconButton
                                                        {
                                                            Anchor = Anchor.Centre,
                                                            Origin = Anchor.Centre,
                                                            Icon = FontAwesome.Solid.StepForward,
                                                            TooltipText = SlopVisualizerStrings.NextTrack,
                                                            Action = () => musicController.NextTrack(),
                                                        },
                                                    }
                                                },
                                                playlistButton = new MusicIconButton
                                                {
                                                    Anchor = Anchor.CentreRight,
                                                    Origin = Anchor.CentreRight,
                                                    Icon = FontAwesome.Solid.Bars,
                                                    TooltipText = SlopVisualizerStrings.Playlist,
                                                    Action = togglePlaylist,
                                                },
                                            }
                                        },
                                    }
                                },
                            }
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            inputManager = GetContainingInputManager()!;

            style.BindValueChanged(s => switchVisualizer(s.NewValue), true);

            beatmap.BindDisabledChanged(_ => Scheduler.AddOnce(updateEnabledStates));
            allowTrackControl = musicController.AllowTrackControl.GetBoundCopy();
            allowTrackControl.BindValueChanged(_ => Scheduler.AddOnce(updateEnabledStates), true);

            shuffle.BindTo(musicController.Shuffle);
            shuffle.BindValueChanged(s => shuffleButton.FadeColour(s.NewValue ? colours.Yellow : Color4.White, 200, Easing.OutQuint), true);

            collectionDropdown.PlaylistCollection.BindTo(musicController.PlaylistCollection);
            collectionDropdown.PlaylistCollection.BindValueChanged(_ => ensureCurrentTrackInPlaylist());

            musicController.TrackChanged += trackChanged;
            trackChanged(beatmap.Value);
        }

        protected override void Update()
        {
            base.Update();

            // children read the analyser during their own update, which runs after this.
            analyser.Update(beatSyncProvider.CurrentAmplitudes.FrequencyAmplitudes.Span, Time.Elapsed);

            pendingBeatmapSwitch?.Invoke();
            pendingBeatmapSwitch = null;

            applyPendingPalette();
            updateBackground();
            updateProgress();
            updateChromeVisibility();
        }

        #region Visualizer

        private void switchVisualizer(VisualizerStyle newStyle)
        {
            visualizer?.FadeOut(visualizer_swap_duration, Easing.OutQuint).Expire();

            visualizer = newStyle switch
            {
                VisualizerStyle.Ncs => new NcsVisualizer(analyser),
                _ => new TrapNationVisualizer(analyser),
            };

            visualizer.Alpha = 0;
            visualizerContainer.Add(visualizer);
            visualizer.FadeIn(visualizer_swap_duration, Easing.OutQuint);

            if (appliedMetadata != null)
                visualizer.ApplyMetadata(appliedMetadata);
            visualizer.ApplyPalette(appliedPalette, appliedPaletteSeed);

            dimBox.FadeTo(visualizer.BackgroundDim, visualizer_swap_duration, Easing.OutQuint);

            var nextStyle = Enum.GetValues<VisualizerStyle>().SkipWhile(s => s != newStyle).Skip(1).FirstOrDefault();
            styleButton.Text = SlopVisualizerStrings.SwitchToStyle(nextStyle.GetLocalisableDescription());
        }

        private void cycleStyle()
        {
            var styles = Enum.GetValues<VisualizerStyle>();
            style.Value = styles[(Array.IndexOf(styles, style.Value) + 1) % styles.Length];
        }

        private void updateBackground()
        {
            if (visualizer == null)
                return;

            backgroundContainer.Scale = new Vector2(background_base_scale + analyser.Bass * visualizer.BackgroundZoom);

            // irregular frequencies, so the shake doesn't look like a repeating pattern.
            float time = (float)Time.Current;
            float shake = analyser.Beat * visualizer.BackgroundShake;
            backgroundContainer.Position = new Vector2(MathF.Sin(time * 0.071f), MathF.Cos(time * 0.053f)) * shake;
        }

        #endregion

        #region Track changes

        private void trackChanged(WorkingBeatmap working, TrackChangeDirection direction = TrackChangeDirection.None)
        {
            currentBeatmap = working;

            // deferred until the overlay is updated, so nothing is loaded while it is hidden.
            pendingBeatmapSwitch = delegate
            {
                applyMetadata(working);
                loadBackground(working);
                loadPalette(working);
            };
        }

        private void applyMetadata(WorkingBeatmap working)
        {
            if (working is DummyWorkingBeatmap)
            {
                title.CreateContent = () => createTitleText(SlopVisualizerStrings.NothingToPlay);
                artist.CreateContent = () => createArtistText(default);
                return;
            }

            var metadata = working.Metadata;

            title.CreateContent = () => createTitleText(new RomanisableString(metadata.TitleUnicode, metadata.Title));
            artist.CreateContent = () => createArtistText(new RomanisableString(metadata.ArtistUnicode, metadata.Artist));

            appliedMetadata = metadata;
            visualizer?.ApplyMetadata(metadata);
        }

        private void loadBackground(WorkingBeatmap working)
        {
            backgroundLoadCancellation?.Cancel();

            LoadComponentAsync(new TrackBackground(working), newBackground =>
            {
                if (working != currentBeatmap)
                {
                    newBackground.Dispose();
                    return;
                }

                foreach (var old in backgroundContainer)
                    old.Delay(background_fade_duration).FadeOut().Expire();

                backgroundContainer.Add(newBackground);
                newBackground.FadeInFromZero(background_fade_duration, Easing.OutQuint);
            }, (backgroundLoadCancellation = new CancellationTokenSource()).Token);
        }

        private void loadPalette(WorkingBeatmap working)
        {
            paletteLoadCancellation?.Cancel();
            paletteLoadCancellation = new CancellationTokenSource();

            BackgroundPalette.LoadAsync(working, paletteLoadCancellation.Token).ContinueWith(task =>
            {
                if (!task.IsCompletedSuccessfully)
                    return;

                lock (paletteLock)
                    pendingPalette = (working, task.GetResultSafely());
            });
        }

        private void applyPendingPalette()
        {
            (WorkingBeatmap beatmap, BackgroundPalette palette) result;

            lock (paletteLock)
            {
                if (pendingPalette == null)
                    return;

                result = pendingPalette.Value;
                pendingPalette = null;
            }

            if (result.beatmap != currentBeatmap)
                return;

            appliedPalette = result.palette;
            appliedPaletteSeed = result.beatmap.BeatmapSetInfo.ID.GetHashCode();
            visualizer?.ApplyPalette(appliedPalette, appliedPaletteSeed);
        }

        /// <summary>
        /// Moves on to the next track if the user picked a playlist which doesn't contain the playing track.
        /// </summary>
        private void ensureCurrentTrackInPlaylist()
        {
            if (beatmap.Disabled || !musicController.AllowTrackControl.Value)
                return;

            var collection = collectionDropdown.Current.Value?.Collection;

            if (collection == null)
                return;

            var setHashes = beatmap.Value.BeatmapSetInfo.Beatmaps.Select(b => b.MD5Hash).ToHashSet();

            if (!collection.PerformRead(c => c.BeatmapMD5Hashes.Any(setHashes.Contains)))
                musicController.NextTrack();
        }

        #endregion

        #region Player controls

        private void updateEnabledStates()
        {
            bool beatmapDisabled = beatmap.Disabled;
            bool trackControlDisabled = !musicController.AllowTrackControl.Value;

            if (beatmapDisabled || trackControlDisabled)
                playlist?.Hide();

            prevButton.Enabled.Value = nextButton.Enabled.Value = playlistButton.Enabled.Value = !beatmapDisabled && !trackControlDisabled;
            playButton.Enabled.Value = !trackControlDisabled;
        }

        private void togglePlaylist()
        {
            if (playlist == null)
            {
                LoadComponentAsync(playlist = new PlaylistOverlay
                {
                    RelativeSizeAxes = Axes.Both,
                }, _ =>
                {
                    playlistContainer.Add(playlist);
                    playlist.State.BindValueChanged(s => playlistButton.FadeColour(s.NewValue == Visibility.Visible ? colours.Yellow : Color4.White, 200, Easing.OutQuint), true);
                    togglePlaylist();
                });

                return;
            }

            if (!beatmap.Disabled)
                playlist.ToggleVisibility();
        }

        private void updateProgress()
        {
            var track = musicController.CurrentTrack;

            if (!track.IsDummyDevice)
            {
                if (!progressBar.Seeking)
                {
                    progressBar.EndTime = track.Length;
                    progressBar.CurrentTime = track.CurrentTime;
                }

                currentTime.Text = track.CurrentTime.ToFormattedDuration();
                totalTime.Text = track.Length.ToFormattedDuration();
                playButton.Icon = track.IsRunning ? FontAwesome.Regular.PauseCircle : FontAwesome.Regular.PlayCircle;
            }
            else
            {
                progressBar.CurrentTime = 0;
                progressBar.EndTime = 1;
                currentTime.Text = totalTime.Text = 0d.ToFormattedDuration();
                playButton.Icon = FontAwesome.Regular.PlayCircle;
            }
        }

        private double? lastSeekGameTime;
        private double? lastSeekAudioTargetTime;

        private void onSeek(double progress)
        {
            if (!musicController.IsPlaying || lastSeekGameTime == null || Time.Current - lastSeekGameTime > NowPlayingOverlay.TRACK_DRAG_SEEK_DEBOUNCE)
            {
                musicController.SeekTo(progress);
                lastSeekGameTime = Time.Current;
                lastSeekAudioTargetTime = progress;
            }
        }

        private void onCommit(double progress)
        {
            // Avoid a second seek to the same location, which could occur when using keyboard navigation from `OnSeek` and subsequent `OnCommit` calls.
            if (progress != lastSeekAudioTargetTime)
                musicController.SeekTo(progress);

            lastSeekGameTime = null;
            lastSeekAudioTargetTime = null;
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action == GlobalAction.Select)
            {
                if (!e.Repeat && musicController.AllowTrackControl.Value)
                    musicController.TogglePause();

                return true;
            }

            return base.OnPressed(e);
        }

        #endregion

        #region Idle hiding

        CursorContainer IProvideCursor.Cursor => null!;

        /// <summary>
        /// Hides the cursor while the player controls are hidden, by providing no cursor.
        /// </summary>
        public bool ProvidingUserCursor => chromeHidden;

        // required for the cursor to be overridden while hovering empty areas.
        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            lastInteractionTime = Time.Current;
            return base.OnMouseMove(e);
        }

        private void updateChromeVisibility()
        {
            if (shouldKeepChrome())
                lastInteractionTime = Time.Current;

            // never hide anything while closing, so the toolbar is not left hidden.
            bool hide = State.Value == Visibility.Visible && Time.Current - lastInteractionTime > idle_hide_delay;

            if (hide == chromeHidden)
                return;

            chromeHidden = hide;
            chrome.FadeTo(hide ? 0 : 1, chrome_fade_duration, Easing.OutQuint);

            if (hide)
            {
                if (game?.Toolbar.State.Value == Visibility.Visible)
                {
                    game.Toolbar.Hide();
                    toolbarHiddenByIdle = true;
                }
            }
            else
                restoreToolbar();
        }

        private void restoreToolbar()
        {
            if (!toolbarHiddenByIdle)
                return;

            toolbarHiddenByIdle = false;
            game?.Toolbar.Show();
        }

        private bool shouldKeepChrome()
        {
            if (playlist?.State.Value == Visibility.Visible || collectionDropdown.IsMenuOpen || progressBar.Seeking)
                return true;

            var mousePosition = inputManager.CurrentState.Mouse.Position;
            return !chromeHidden && (topBar.ReceivePositionalInputAt(mousePosition) || bottomBar.ReceivePositionalInputAt(mousePosition));
        }

        #endregion

        protected override void PopIn()
        {
            analyser.Reset();
            lastInteractionTime = Time.Current;
            musicController.EnsurePlayingSomething();

            this.FadeIn(transition_duration, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            base.PopOut();

            playlist?.Hide();
            this.FadeOut(transition_duration, Easing.OutQuint);

            if (chromeHidden)
            {
                chromeHidden = false;
                chrome.FadeIn(chrome_fade_duration, Easing.OutQuint);
            }

            restoreToolbar();

        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (musicController.IsNotNull())
                musicController.TrackChanged -= trackChanged;

            backgroundLoadCancellation?.Cancel();
            paletteLoadCancellation?.Cancel();
        }

        private static OsuSpriteText createTitleText(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = text,
            Font = OsuFont.GetFont(size: 30, weight: FontWeight.Bold),
        };

        private static OsuSpriteText createArtistText(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = text,
            Font = OsuFont.GetFont(size: 18, weight: FontWeight.SemiBold),
            Colour = Color4.White.Opacity(0.8f),
        };

        private static OsuSpriteText createTimeText(Anchor anchor) => new OsuSpriteText
        {
            Anchor = anchor,
            Origin = anchor,
            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold, fixedWidth: true),
            Colour = Color4.White.Opacity(0.8f),
        };

        private partial class MusicIconButton : IconButton
        {
            public MusicIconButton()
            {
                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                HoverColour = colours.YellowDark.Opacity(0.6f);
                FlashColour = colours.Yellow;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // works with AutoSizeAxes above to make buttons autosize with the scale animation.
                Content.AutoSizeAxes = Axes.None;
                Content.Size = new Vector2(DEFAULT_BUTTON_SIZE);
            }
        }

        private partial class HoverableProgressBar : ProgressBar
        {
            private const float collapsed_height = 4;
            private const float expanded_height = 8;

            public override bool HandleNonPositionalInput => IsHovered && base.HandleNonPositionalInput;

            public HoverableProgressBar()
                : base(true)
            {
                RelativeSizeAxes = Axes.X;
                Height = collapsed_height;
                Masking = true;
                CornerRadius = expanded_height / 2;
            }

            protected override bool OnHover(HoverEvent e)
            {
                this.ResizeHeightTo(expanded_height, 500, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                this.ResizeHeightTo(collapsed_height, 500, Easing.OutQuint);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// The beatmap background, without the dimming and blurring of the <see cref="NowPlayingOverlay"/>.
        /// </summary>
        private partial class TrackBackground : Container
        {
            private readonly WorkingBeatmap working;
            private readonly Sprite sprite;

            public TrackBackground(WorkingBeatmap working)
            {
                this.working = working;

                RelativeSizeAxes = Axes.Both;
                Depth = float.MaxValue;
                Child = sprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fill,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            }

            [BackgroundDependencyLoader]
            private void load(LargeTextureStore textures)
            {
                sprite.Texture = working.GetBackground() ?? textures.Get(@"Backgrounds/bg2");
            }
        }

        /// <summary>
        /// Selects the collection used as the <see cref="MusicController.PlaylistCollection"/>.
        /// </summary>
        private partial class PlaylistCollectionDropdown : OsuDropdown<CollectionFilterMenuItem>
        {
            /// <summary>
            /// The ID of the selected collection, or <c>null</c> for all beatmaps.
            /// </summary>
            public readonly Bindable<Guid?> PlaylistCollection = new Bindable<Guid?>();

            public bool IsMenuOpen => Menu.State == MenuState.Open;

            [Resolved]
            private RealmAccess realm { get; set; } = null!;

            [Resolved]
            private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

            private readonly AllBeatmapsCollectionFilterMenuItem allBeatmapsItem = new AllBeatmapsCollectionFilterMenuItem();

            private List<CollectionFilterMenuItem> filters = new List<CollectionFilterMenuItem>();
            private IDisposable? realmSubscription;

            /// <summary>
            /// Whether <see cref="Dropdown{T}.Current"/> is being changed to reflect external state, rather than by the user.
            /// </summary>
            private bool updatingSelection;

            protected override void LoadComplete()
            {
                base.LoadComplete();

                realmSubscription = realm.RegisterForNotifications(r => r.All<BeatmapCollection>().OrderBy(c => c.Name), collectionsChanged);

                Current.BindValueChanged(selectionChanged);
                PlaylistCollection.BindValueChanged(_ => updateSelection(), true);
            }

            private void collectionsChanged(IRealmCollection<BeatmapCollection> collections, ChangeSet? changes)
            {
                filters = new List<CollectionFilterMenuItem> { allBeatmapsItem };
                filters.AddRange(collections.Select(c => new CollectionFilterMenuItem(c.ToLive(realm))));
                filters.Add(new ManageCollectionsFilterMenuItem());

                updatingSelection = true;
                Items = filters;
                updatingSelection = false;

                // the playlist collection was deleted.
                if (PlaylistCollection.Value != null && filters.All(f => f.Collection?.ID != PlaylistCollection.Value))
                {
                    PlaylistCollection.Value = null;
                    return;
                }

                updateSelection();
            }

            private void updateSelection()
            {
                var item = filters.FirstOrDefault(f => f.Collection != null && f.Collection.ID == PlaylistCollection.Value) ?? allBeatmapsItem;

                updatingSelection = true;

                // items are equal by collection, so a renamed collection would not refresh the displayed name otherwise.
                Current.Value = allBeatmapsItem;
                Current.Value = item;

                updatingSelection = false;
            }

            private void selectionChanged(ValueChangedEvent<CollectionFilterMenuItem> filter)
            {
                // may be null while items are replaced.
                if (updatingSelection || filter.NewValue.IsNull())
                    return;

                if (filter.NewValue is ManageCollectionsFilterMenuItem)
                {
                    updatingSelection = true;
                    Current.Value = filter.OldValue;
                    updatingSelection = false;

                    manageCollectionsDialog?.Show();
                    return;
                }

                PlaylistCollection.Value = filter.NewValue.Collection?.ID;
            }

            protected override LocalisableString GenerateItemText(CollectionFilterMenuItem item) => item.CollectionName;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                realmSubscription?.Dispose();
            }
        }
    }
}
