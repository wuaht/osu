// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Profile.Header.Components;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osuTK;

namespace osu.Game.Users
{
    /// <summary>
    /// User card that shows user's global and country ranks in the bottom.
    /// Meant to be used in the toolbar login overlay.
    /// </summary>
    public partial class UserRankPanel : UserPanel
    {
        private const int padding = 10;
        private const int main_content_height = 80;

        private GlobalRankDisplay globalRankDisplay = null!;
        private ProfileValueDisplay countryRankDisplay = null!;
        private LoadingLayer loadingLayer = null!;

        /// <summary>
        /// The flag, team logo and supporter icon.
        /// </summary>
        protected FillFlowContainer Details { get; private set; } = null!;

        public UserRankPanel(APIUser user)
            : base(user)
        {
            AutoSizeAxes = Axes.Y;
            CornerRadius = 10;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            BorderColour = ColourProvider?.Light1 ?? Colours.GreyVioletLighter;
        }

        [Resolved]
        private LocalUserStatisticsProvider? statisticsProvider { get; set; }

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private bool hasStatistics;
        private (int userId, string ruleset)? requestedStatistics;

        /// <summary>
        /// Returns the statistics which are displayed, or <c>null</c> if they aren't available yet.
        /// </summary>
        protected virtual UserStatistics? GetStatistics(RulesetInfo ruleset) => ruleset.IsLegacyRuleset() ? statisticsProvider?.GetStatisticsFor(ruleset) : no_online_statistics;

        /// <summary>
        /// Shown for rulesets without online statistics, so that the panel doesn't keep loading.
        /// </summary>
        private static readonly UserStatistics no_online_statistics = new UserStatistics();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (statisticsProvider != null)
                statisticsProvider.StatisticsUpdated += onStatisticsUpdated;

            ruleset.BindValueChanged(_ => UpdateDisplay(), true);
        }

        private void onStatisticsUpdated(UserStatisticsUpdate update)
        {
            if (update.Ruleset.Equals(ruleset.Value))
                UpdateDisplay();
        }

        protected override void Update()
        {
            base.Update();

            if (hasStatistics)
                return;

            // the provider's cache may be cleared and refilled around user changes without an update reaching this panel, so recover while nothing is shown.
            if (GetStatistics(ruleset.Value) != null)
                UpdateDisplay();
            else
                requestMissingStatistics();
        }

        private void requestMissingStatistics()
        {
            var user = api.LocalUser.Value;

            if (statisticsProvider == null || api.State.Value != APIState.Online || user is GuestUser || user.Id <= 1 || !ruleset.Value.IsLegacyRuleset())
                return;

            var key = (user.Id, ruleset.Value.ShortName);
            if (requestedStatistics == key)
                return;

            requestedStatistics = key;
            statisticsProvider.RefetchStatistics(ruleset.Value);
        }

        protected void UpdateDisplay()
        {
            var statistics = GetStatistics(ruleset.Value);
            hasStatistics = statistics != null;

            loadingLayer.State.Value = statistics == null ? Visibility.Visible : Visibility.Hidden;

            // TODO: implement highest rank tooltip
            // `RankHighest` resides in `APIUser`, but `api.LocalUser` doesn't update
            // maybe move to `UserStatistics` in api, so `UserStatisticsWatcher` can update the value
            globalRankDisplay.UserStatistics.Value = statistics;

            countryRankDisplay.Content.Text = statistics?.CountryRank?.ToLocalisableString("\\##,##0") ?? "-";
        }

        protected override Drawable CreateLayout()
        {
            var layout = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Container
                    {
                        Name = "Main content",
                        RelativeSizeAxes = Axes.X,
                        Height = main_content_height,
                        CornerRadius = 10,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new CoverBackground
                            {
                                RelativeSizeAxes = Axes.Both,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Model = User,
                                Alpha = 0.3f
                            },
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                Padding = new MarginPadding(padding),
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(),
                                },
                                RowDimensions = new[]
                                {
                                    new Dimension()
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        CreateAvatar().With(avatar =>
                                        {
                                            avatar.Size = new Vector2(60);
                                            avatar.Masking = true;
                                            avatar.CornerRadius = 6;
                                        }),
                                        new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding { Left = padding },
                                            ColumnDimensions = new[]
                                            {
                                                new Dimension()
                                            },
                                            RowDimensions = new[]
                                            {
                                                new Dimension(GridSizeMode.AutoSize),
                                                new Dimension()
                                            },
                                            Content = new[]
                                            {
                                                new Drawable[]
                                                {
                                                    Details = new FillFlowContainer
                                                    {
                                                        AutoSizeAxes = Axes.Both,
                                                        Direction = FillDirection.Horizontal,
                                                        Spacing = new Vector2(6),
                                                        Children = new[]
                                                        {
                                                            CreateFlag(),
                                                            CreateTeamLogo(),
                                                            // supporter icon is being added later
                                                        }
                                                    }
                                                },
                                                new Drawable[]
                                                {
                                                    CreateUsername().With(username =>
                                                    {
                                                        username.Anchor = Anchor.CentreLeft;
                                                        username.Origin = Anchor.CentreLeft;
                                                    })
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new GridContainer
                    {
                        Name = "Bottom content",
                        Margin = new MarginPadding { Top = main_content_height },
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding(padding),
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension()
                        },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                globalRankDisplay = new GlobalRankDisplay(),
                                countryRankDisplay = new ProfileValueDisplay(true)
                                {
                                    Title = UsersStrings.ShowRankCountrySimple,
                                }
                            }
                        }
                    },
                    loadingLayer = new LoadingLayer(true),
                }
            };

            if (User.IsSupporter)
            {
                Details.Add(new SupporterIcon
                {
                    Height = 26,
                    SupportLevel = User.SupportLevel
                });
            }

            return layout;
        }

        protected override bool OnHover(HoverEvent e)
        {
            BorderThickness = 2;
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            BorderThickness = 0;
            base.OnHoverLost(e);
        }

        protected override Drawable? CreateBackground() => null;

        protected override void Dispose(bool isDisposing)
        {
            if (statisticsProvider.IsNotNull())
                statisticsProvider.StatisticsUpdated -= onStatisticsUpdated;

            base.Dispose(isDisposing);
        }
    }
}
