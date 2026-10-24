// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;

namespace osu.Game.MapsetVerifier.UI.Checks
{
    /// <summary>
    /// The issues found in the beatmap set, per difficulty.
    /// </summary>
    public partial class ChecksTab : VerifierTab
    {
        public const float SIDEBAR_WIDTH = 300;

        private DependencyContainer dependencies = null!;

        private IssueFilter filter = null!;

        private DifficultyPicker difficultyPicker = null!;
        private IssueList issueList = null!;
        private ChecksSidebar sidebar = null!;
        private IssueDetails details = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs(filter = new IssueFilter(parent.Get<OsuConfigManager>()));
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, VerifierScreen.SPACING),
                    new Dimension(GridSizeMode.Absolute, SIDEBAR_WIDTH),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            RowDimensions = new[]
                            {
                                new Dimension(GridSizeMode.AutoSize),
                                new Dimension(GridSizeMode.Absolute, VerifierScreen.SPACING),
                                new Dimension(),
                            },
                            Content = new[]
                            {
                                new Drawable[] { difficultyPicker = new DifficultyPicker() },
                                new[] { Empty() },
                                new Drawable[] { issueList = new IssueList() },
                            }
                        },
                        Empty(),
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                sidebar = new ChecksSidebar(),
                                details = new IssueDetails { Alpha = 0 },
                            }
                        },
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            filter.SelectedIssue.BindValueChanged(s =>
            {
                if (s.NewValue == null)
                {
                    details.FadeOut(200, Easing.OutQuint);
                    sidebar.FadeIn(200, Easing.OutQuint);
                }
                else
                {
                    details.Show(s.NewValue.Value.Issue, s.NewValue.Value.Category);
                    details.FadeIn(200, Easing.OutQuint);
                    sidebar.FadeOut(200, Easing.OutQuint);
                }
            });
        }

        protected override void OnSessionChanged()
        {
            difficultyPicker.UpdateFrom(Session);
            issueList.UpdateFrom(Session);
            sidebar.UpdateFrom(Session);

            // the issue which is shown may not exist anymore after checking again.
            if (filter.SelectedIssue.Value is var (issue, category) && Session.Results?.Get(category)?.Issues.Any(i => i.CheckId == issue.CheckId && i.Message == issue.Message) != true)
                filter.SelectedIssue.Value = null;
        }

        public override bool OnBack()
        {
            if (filter.SelectedIssue.Value == null)
                return false;

            filter.SelectedIssue.Value = null;
            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            filter?.UnbindAll();
        }
    }
}
