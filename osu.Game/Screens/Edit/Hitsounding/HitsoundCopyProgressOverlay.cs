// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// Shows the progress of copying hitsounds to other difficulties, and blocks input to the hitsound editor meanwhile
    /// (e.g. saving the current difficulty again would write its outdated beatmap set back to the database).
    /// </summary>
    public partial class HitsoundCopyProgressOverlay : VisibilityContainer, IKeyBindingHandler<GlobalAction>, IKeyBindingHandler<PlatformAction>
    {
        private const float duration = 200;

        private Container content = null!;
        private OsuSpriteText statusText = null!;
        private OsuSpriteText countText = null!;
        private ProgressBar progressBar = null!;

        private int total;

        public HitsoundCopyProgressOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0.5f,
                },
                content = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 400,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(20),
                            Spacing = new Vector2(10),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = SlopHitsoundEditorStrings.CopyProgressHeader,
                                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                },
                                statusText = new TruncatingSpriteText
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Font = OsuFont.GetFont(size: 14),
                                },
                                progressBar = new ProgressBar(false)
                                {
                                    Height = 6,
                                    FillColour = colourProvider.Highlight1,
                                    BackgroundColour = colourProvider.Background6,
                                },
                                countText = new OsuSpriteText
                                {
                                    Anchor = Anchor.TopRight,
                                    Origin = Anchor.TopRight,
                                    Font = OsuFont.GetFont(size: 12),
                                    Colour = colourProvider.Content2,
                                },
                            }
                        },
                    }
                },
            };
        }

        /// <summary>
        /// Shows the overlay for copying to the given number of difficulties.
        /// </summary>
        public void Begin(int difficultyCount)
        {
            total = difficultyCount;

            statusText.Text = string.Empty;
            progressBar.CurrentTime = 0;
            countText.Text = SlopHitsoundEditorStrings.CopyProgressCount(0, total);

            Show();
        }

        /// <summary>
        /// Updates the progress. Loading and saving a difficulty are counted as half of it each.
        /// </summary>
        /// <param name="difficulty">The difficulty being processed.</param>
        /// <param name="saving">Whether it is being saved (rather than loaded).</param>
        /// <param name="index">The index of the difficulty.</param>
        public void SetProgress(BeatmapInfo difficulty, bool saving, int index)
        {
            string name = difficulty.DifficultyName;

            statusText.Text = saving ? SlopHitsoundEditorStrings.CopyProgressSaving(name) : SlopHitsoundEditorStrings.CopyProgressLoading(name);
            countText.Text = SlopHitsoundEditorStrings.CopyProgressCount(saving ? index : 0, total);
            progressBar.CurrentTime = total == 0 ? 0 : (double)((saving ? total : 0) + index) / (2 * total);
        }

        /// <summary>
        /// Fills the progress bar and hides the overlay.
        /// </summary>
        public void End()
        {
            progressBar.CurrentTime = 1;
            countText.Text = SlopHitsoundEditorStrings.CopyProgressCount(total, total);

            Hide();
        }

        protected override void PopIn()
        {
            this.FadeIn(duration, Easing.OutQuint);
            content.ScaleTo(0.9f).ScaleTo(1, duration, Easing.OutQuint);
        }

        protected override void PopOut() => this.FadeOut(duration, Easing.OutQuint);

        // block all input to the editor while visible.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        protected override bool Handle(UIEvent e)
        {
            if (e is ScrollEvent or MouseEvent or KeyDownEvent)
                return true;

            return base.Handle(e);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e) => true;

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        public bool OnPressed(KeyBindingPressEvent<PlatformAction> e) => true;

        public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
        {
        }
    }
}
