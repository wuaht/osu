// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Components.Timelines.Summary;
using osuTK;

namespace osu.Game.Screens.Edit
{
    internal partial class BottomBar : CompositeDrawable
    {
        /// <summary>
        /// Kept compact (matching osu!stable) to leave more space for the playfield.
        /// </summary>
        public const float HEIGHT = 34;

        public TestGameplayButton TestGameplayButton { get; private set; } = null!;

        private IBindable<bool> saveInProgress = null!;
        private Bindable<bool> composerFocusMode = null!;

        [BackgroundDependencyLoader]
        private void load(Editor editor)
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            RelativeSizeAxes = Axes.X;

            Height = HEIGHT;

            Masking = true;

            InternalChildren = new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        // the outer columns are sized to fit their contents (the timestamp and the playback speed label), so that the summary timeline gets the remaining space.
                        new Dimension(GridSizeMode.Absolute, 120),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 205),
                        new Dimension(GridSizeMode.Absolute, HitObjectComposer.TOOLBOX_CONTRACTED_SIZE_RIGHT),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new TimeInfoContainer { RelativeSizeAxes = Axes.Both },
                            new SummaryTimeline { RelativeSizeAxes = Axes.Both },
                            new PlaybackControl { RelativeSizeAxes = Axes.Both },
                            TestGameplayButton = new TestGameplayButton
                            {
                                RelativeSizeAxes = Axes.Both,
                                Size = new Vector2(1),
                                Action = editor.TestGameplay,
                            }
                        },
                    }
                }
            };

            saveInProgress = editor.MutationTracker.InProgress.GetBoundCopy();
            composerFocusMode = editor.ComposerFocusMode.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            saveInProgress.BindValueChanged(_ => TestGameplayButton.Enabled.Value = !saveInProgress.Value, true);
            composerFocusMode.BindValueChanged(_ =>
            {
                // Transforms should be kept in sync with other usages of composer focus mode.
                foreach (var c in this.ChildrenOfType<BottomBarContainer>())
                {
                    if (!composerFocusMode.Value)
                        c.Background.FadeIn(750, Easing.OutQuint);
                    else
                        c.Background.Delay(600).FadeTo(0.5f, 4000, Easing.OutQuint);
                }
            }, true);
        }

        protected override bool OnMouseDown(MouseDownEvent e) => true;
        protected override bool OnClick(ClickEvent e) => true;
    }
}
