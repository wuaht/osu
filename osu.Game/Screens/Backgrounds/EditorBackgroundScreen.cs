// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Screens.Edit;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Backgrounds
{
    public partial class EditorBackgroundScreen : BackgroundScreen
    {
        private readonly Container dimContainer;

        private CancellationTokenSource? cancellationTokenSource;
        private Bindable<float> dimLevel = null!;
        private Bindable<bool> showStoryboard = null!;
        private Bindable<bool> alignBackground = null!;

        /// <summary>
        /// Returns the playfield which the background should be aligned with, if enabled by <see cref="OsuSetting.SlopEditorAlignBackground"/>.
        /// </summary>
        public Func<Drawable?>? AlignmentTarget { get; set; }

        private BeatmapBackgroundWithStoryboard background = null!;

        private readonly Container content;

        /// <summary>
        /// A blurred, full-screen copy of the background, filling the area outside of <see cref="content"/> while it is aligned with the playfield.
        /// </summary>
        private readonly Container backdrop;
        private readonly Box blackBox;

        // We retrieve IBindable<WorkingBeatmap> from our dependency cache instead of passing WorkingBeatmap directly into EditorBackgroundScreen.
        // Otherwise, DummyWorkingBeatmap will be erroneously passed in whenever creating a new beatmap (since the Schedule() in the Editor that populates
        // a new WorkingBeatmap with correct values generally runs after EditorBackgroundScreen is created), which causes any background changes to not be displayed.
        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        public EditorBackgroundScreen(EditorBeatmap editorBeatmap)
        {
            InternalChild = dimContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    // This adds overdraw but makes transitions not suck.
                    // There's probably a better way to do this, but it's high effort.
                    blackBox = new Box
                    {
                        Colour = Color4.Black,
                        RelativeSizeAxes = Axes.Both,
                    },
                    backdrop = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                    },
                    content = new EditorSkinProvidingContainer(editorBeatmap)
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            dimLevel = config.GetBindable<float>(OsuSetting.EditorDim);
            showStoryboard = config.GetBindable<bool>(OsuSetting.EditorShowStoryboard);
            alignBackground = config.GetBindable<bool>(OsuSetting.SlopEditorAlignBackground);

            content.Child = createContent();
            backdrop.Child = createBackdrop();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            dimLevel.BindValueChanged(_ => dimContainer.FadeColour(OsuColour.Gray(1 - dimLevel.Value), 500, Easing.OutQuint), true);
        }

        protected override void Update()
        {
            base.Update();

            Drawable? target = alignBackground.Value ? AlignmentTarget?.Invoke() : null;

            if (target?.IsPresent == true && DrawHeight > 0)
            {
                RectangleF playfield = ToLocalSpace(target.ScreenSpaceDrawQuad).AABBFloat;

                if (playfield.Height > 0)
                {
                    // like osu!stable, the background is aligned as if the playfield was shown in gameplay,
                    // where it covers 384 of the 480 pixels of the background's height and is shifted down by 8 pixels.
                    float scale = playfield.Height / playfield_height;

                    content.RelativeSizeAxes = Axes.None;
                    content.Origin = Anchor.Centre;
                    content.Size = new Vector2(background_height * scale * DrawWidth / DrawHeight, background_height * scale);
                    content.Position = new Vector2(playfield.Centre.X, playfield.Centre.Y - playfield_shift * scale);
                    backdrop.Alpha = 1;
                    return;
                }
            }

            content.RelativeSizeAxes = Axes.Both;
            content.Origin = Anchor.TopLeft;
            content.Size = Vector2.One;
            content.Position = Vector2.Zero;
            backdrop.Alpha = 0;
        }

        private const float playfield_height = 384;
        private const float background_height = 480;
        private const float playfield_shift = 8;
        private const float backdrop_blur_sigma = 20;

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            blackBox.LifetimeEnd = LatestTransformEndTime;
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            // The storyboard will do weird things with clock time changing on exit, so let's just hide it instead.
            background.UnloadStoryboard();

            return base.OnExiting(e);
        }

        public void RefreshBackgroundAsync()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            LoadComponentAsync(createContent(), d => content.Child = d, cancellationTokenSource.Token);
            LoadComponentAsync(createBackdrop(), d => backdrop.Child = d, cancellationTokenSource.Token);
        }

        private Drawable createContent() => background = new BeatmapBackgroundWithStoryboard(beatmap.Value)
        {
            RelativeSizeAxes = Axes.Both,
            ShowStoryboard = { BindTarget = showStoryboard },
        };

        private Drawable createBackdrop()
        {
            var backdropBackground = new BeatmapBackground(beatmap.Value);
            backdropBackground.BlurTo(new Vector2(backdrop_blur_sigma));
            return backdropBackground;
        }

        public override bool Equals(BackgroundScreen? other)
        {
            if (other is not EditorBackgroundScreen otherBeatmapBackground)
                return false;

            return base.Equals(other) && beatmap == otherBeatmapBackground.beatmap;
        }
    }
}
