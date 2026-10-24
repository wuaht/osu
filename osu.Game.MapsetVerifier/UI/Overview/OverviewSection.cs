// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK;

namespace osu.Game.MapsetVerifier.UI.Overview
{
    /// <summary>
    /// A section of the overview, which shows the result of one of the analyses of the beatmap set.
    /// </summary>
    public abstract partial class OverviewSection : CompositeDrawable
    {
        public abstract LocalisableString Title { get; }

        public abstract IconUsage Icon { get; }

        /// <summary>
        /// The position of the analysis this section shows in the order in which the analyses are run,
        /// see <see cref="VerifierOverview.CompletedAnalyses"/>.
        /// </summary>
        protected abstract int AnalysisIndex { get; }

        [Resolved]
        protected OverlayColourProvider ColourProvider { get; private set; } = null!;

        [Resolved]
        protected OsuColour Colours { get; private set; } = null!;

        private readonly FillFlowContainer content;

        private object? displayedData;
        private PlaceholderState placeholderState = PlaceholderState.None;
        private VerifierOverview? lastOverview;

        protected OverviewSection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    header = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 0),
                        Margin = new MarginPadding { Top = 4 },
                    },
                    content = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                    },
                }
            };
        }

        private readonly FillFlowContainer header;

        [BackgroundDependencyLoader]
        private void load()
        {
            header.AddRange(new Drawable[]
            {
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Icon = Icon,
                    Size = new Vector2(16),
                    Colour = ColourProvider.Highlight1,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = Title,
                    Font = OsuFont.Default.With(size: 20, weight: FontWeight.Bold),
                },
            });
        }

        /// <summary>
        /// The data which this section shows, or null if the analysis hasn't finished or failed.
        /// The content is recreated whenever this changes.
        /// </summary>
        protected abstract object? GetData(VerifierOverview overview);

        /// <summary>
        /// Why the analysis failed, if it did.
        /// </summary>
        protected virtual string? GetError(object data) => null;

        protected abstract IEnumerable<Drawable> CreateContent(object data);

        public void UpdateFrom(VerifierOverview? overview)
        {
            lastOverview = overview;

            object? data = overview == null ? null : GetData(overview);

            if (data == null)
            {
                displayedData = null;
                bool loading = overview == null || overview.CompletedAnalyses <= AnalysisIndex;
                showPlaceholder(loading ? PlaceholderState.Loading : PlaceholderState.Empty);
                return;
            }

            if (EqualityComparer<object?>.Default.Equals(data, displayedData))
                return;

            displayedData = data;
            placeholderState = PlaceholderState.None;

            string? error = GetError(data);

            content.Clear();

            if (error != null)
                content.Add(new OverviewCard().Add(new SectionPlaceholder(false, error)));
            else
                content.AddRange(CreateContent(data));

            content.FadeInFromZero(200, Easing.OutQuint);
        }

        /// <summary>
        /// Recreates the content, e.g. after another difficulty was selected.
        /// </summary>
        protected void Refresh()
        {
            displayedData = null;
            placeholderState = PlaceholderState.None;
            UpdateFrom(lastOverview);
        }

        private void showPlaceholder(PlaceholderState state)
        {
            if (placeholderState == state)
                return;

            placeholderState = state;
            content.Child = new OverviewCard().Add(new SectionPlaceholder(state == PlaceholderState.Loading));
        }

        private enum PlaceholderState
        {
            None,
            Loading,
            Empty,
        }

        /// <summary>
        /// The text of a boolean value.
        /// </summary>
        protected static string YesNo(bool value) => (value ? VerifierStrings.Yes : VerifierStrings.No).ToString();

        protected static string Format(double value, string format = @"0.##") => value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
    }
}
