// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Screens.Edit.Nominators;
using osuTK;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The box for posting a new discussion, which links to the website since posting is done there.
    /// </summary>
    public partial class DiscussionNewDiscussion : DiscussionSection
    {
        private OsuSpriteText scope = null!;
        private RoundedButton button = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new NominatorsPanel(Axes.Y)
            {
                RelativeSizeAxes = Axes.X,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = 20, Vertical = 14 },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(12, 0),
                            Children = new Drawable[]
                            {
                                new SpriteIcon
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Icon = FontAwesome.Solid.CommentMedical,
                                    Size = new Vector2(22),
                                    Colour = DiscussionDisplay.BLUE_1,
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(8, 0),
                                            Children = new Drawable[]
                                            {
                                                new DiscussionTitle(SlopDiscussionStrings.NewDiscussion),
                                                scope = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                                                    Colour = ColourProvider.Content2,
                                                    Margin = new MarginPadding { Bottom = 1 },
                                                },
                                            }
                                        },
                                        new OsuSpriteText
                                        {
                                            Text = SlopDiscussionStrings.NewDiscussionDescription,
                                            Font = OsuFont.Default.With(size: 13),
                                            Colour = ColourProvider.Content2,
                                        },
                                    }
                                },
                            }
                        },
                        button = new RoundedButton
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Width = 200,
                            Height = 34,
                            Text = SlopDiscussionStrings.PostOnWebsite,
                        },
                    }
                }
            };

            button.Action = () =>
            {
                if (State != null)
                    Screen.OpenOnWebsite(DiscussionDisplay.DiscussionPageUrl(Screen.WebsiteUrl, State.BeatmapSet.Id, State.CurrentBeatmapId, State.View.Page));
            };
        }

        protected override void UpdateDisplay(DiscussionsState state)
        {
            Alpha = state.View.Page == DiscussionPage.Events || state.BeatmapSet.DiscussionLocked ? 0 : 1;

            switch (state.View.Page)
            {
                case DiscussionPage.Reviews:
                    scope.Text = SlopDiscussionStrings.ModeReviews;
                    break;

                case DiscussionPage.GeneralAll:
                    scope.Text = SlopDiscussionStrings.AllDifficulties;
                    break;

                default:
                    scope.Text = state.CurrentBeatmap?.Version ?? string.Empty;
                    break;
            }
        }
    }
}
