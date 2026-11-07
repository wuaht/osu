// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osuTK;

namespace osu.Game.Online.Omdb
{
    /// <summary>
    /// Shows the OMDB ratings of a beatmap and lets the user rate it, or asks for an API key if none is set.
    /// </summary>
    public partial class OmdbRatingControl : CompositeDrawable
    {
        private const float histogram_height = 60;

        private readonly int beatmapId;

        [Resolved]
        private OmdbClient omdb { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        private readonly IBindable<string> apiKey = new Bindable<string>();

        private Bindable<string> configApiKey = null!;

        private readonly FillFlowContainer content;

        private OsuTextFlowContainer statusText = null!;

        /// <summary>
        /// The beatmap as last received, which is shown again if submitting a rating fails.
        /// </summary>
        private OmdbBeatmap? beatmap;

        /// <summary>
        /// Increased on each reload, such that responses to earlier requests are ignored.
        /// </summary>
        private int version;

        private bool submitting;

        /// <param name="beatmapId">The online ID of the beatmap, which has to be submitted.</param>
        public OmdbRatingControl(int beatmapId)
        {
            this.beatmapId = beatmapId;

            AutoSizeAxes = Axes.Y;

            InternalChild = content = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            configApiKey = config.GetBindable<string>(OsuSetting.SlopOmdbApiKey);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            apiKey.BindTo(omdb.ApiKey);
            // the client applies the new key in its own callback, which may run after this one.
            apiKey.BindValueChanged(_ => Scheduler.AddOnce(reload), true);
        }

        private void reload()
        {
            version++;
            beatmap = null;
            submitting = false;

            if (!omdb.HasApiKey)
            {
                showApiKeyPrompt();
                return;
            }

            content.Clear();
            content.Add(statusText = createText());
            statusText.Text = SlopOmdbStrings.Loading;

            int requestVersion = version;

            omdb.GetBeatmapAsync(beatmapId).ContinueWith(t => Schedule(() =>
            {
                if (requestVersion != version)
                    return;

                if (t.IsCompletedSuccessfully)
                    show(t.GetResultSafely());
                else
                    statusText.Text = errorMessageOf(t);
            }));
        }

        private void submit(double? score)
        {
            if (submitting || beatmap == null)
                return;

            submitting = true;
            statusText.Text = SlopOmdbStrings.Submitting;

            int requestVersion = version;
            var previous = beatmap;

            omdb.RateAsync(beatmapId, score).ContinueWith(t => Schedule(() =>
            {
                if (requestVersion != version)
                    return;

                submitting = false;

                if (t.IsCompletedSuccessfully)
                    show(t.GetResultSafely());
                else
                {
                    // puts the slider back to the rating which is actually stored.
                    show(previous);
                    statusText.Text = errorMessageOf(t);
                }
            }));
        }

        private void show(OmdbBeatmap shown)
        {
            beatmap = shown;

            var rating = new BindableDouble(shown.OwnRating ?? 0)
            {
                MinValue = 0,
                MaxValue = 5,
                Precision = 0.5,
            };

            var header = createText(s => s.Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold));
            header.AddLink($@"{shown.Artist} - {shown.Title} [{shown.Difficulty}]", OmdbClient.GetListingUrl(shown.SetID), s => s.Font = OsuFont.GetFont(size: 16, weight: FontWeight.Bold));

            content.Clear();
            content.AddRange(new Drawable[]
            {
                header,
                new FormSliderBar<double>
                {
                    RelativeSizeAxes = Axes.X,
                    Caption = SlopOmdbStrings.YourRating,
                    HintText = shown.OwnRating == null ? SlopOmdbStrings.NotRatedYet : default,
                    Current = rating,
                    // each change is submitted, so only the released value is applied.
                    TransferValueOnCommit = true,
                    PlaySamplesOnAdjust = true,
                },
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = SlopOmdbStrings.RemoveRating,
                    Enabled = { Value = shown.OwnRating != null },
                    Action = () => submit(null),
                },
                statusText = createText(),
                createStatistics(shown),
            });

            if (shown.RatingCount > 0)
                content.Add(createHistogram(shown));

            rating.BindValueChanged(r => submit(r.NewValue));
        }

        private Drawable createStatistics(OmdbBeatmap shown)
        {
            var flow = createText();

            addRow(flow, SlopOmdbStrings.Rating, ratingSummaryOf(shown));

            var ranks = new List<LocalisableString>();

            if (shown.ChartYearRank is int yearRank)
                ranks.Add(SlopOmdbStrings.YearRank(yearRank));
            if (shown.ChartRank is int rank)
                ranks.Add(SlopOmdbStrings.OverallRank(rank));

            if (ranks.Count > 0)
                addRow(flow, SlopOmdbStrings.Ranking, string.Join(@", ", ranks.Select(r => r.ToString())));

            if (shown.Descriptors.Length > 0)
                addRow(flow, SlopOmdbStrings.Descriptors, string.Join(@", ", shown.Descriptors.Select(d => d.Name)));

            if (shown.Nominators.Length > 0)
            {
                addLabel(flow, SlopOmdbStrings.Nominators);

                for (int i = 0; i < shown.Nominators.Length; i++)
                {
                    var nominator = shown.Nominators[i];

                    if (i > 0)
                        flow.AddText(@", ");

                    if (nominator.UserID > 0)
                        flow.AddUserLink(new APIUser { Id = nominator.UserID, Username = nominator.Username });
                    else
                        flow.AddText(nominator.Username);
                }

                flow.NewLine();
            }

            return flow;
        }

        /// <summary>
        /// The number of ratings for each score from 0 to 5 in steps of 0.5.
        /// </summary>
        private Drawable createHistogram(OmdbBeatmap shown)
        {
            int max = Math.Max(5, shown.Ratings.Count > 0 ? shown.Ratings.Values.Max() : 0);

            var bars = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
            };

            for (int i = 0; i <= 10; i++)
            {
                double score = i * 0.5;
                shown.Ratings.TryGetValue(score, out int count);

                bool own = shown.OwnRating is double ownRating && Math.Abs(ownRating - score) < 0.001;

                bars.Add(new HistogramBar(score, count, (float)count / max)
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1 / 11f,
                    Height = histogram_height,
                    BarColour = own ? colours.Yellow : colours.Blue,
                });
            }

            return bars;
        }

        private void showApiKeyPrompt()
        {
            var keyText = new Bindable<string>(string.Empty);
            FormPasswordTextBox keyTextBox;

            var prompt = createText();
            prompt.Text = SlopOmdbStrings.ApiKeyPrompt;

            content.Clear();
            content.AddRange(new Drawable[]
            {
                prompt,
                keyTextBox = new FormPasswordTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    Caption = SlopSettingsStrings.OmdbApiKey,
                    Current = keyText,
                },
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = SlopOmdbStrings.SaveApiKey,
                    Action = () => saveApiKey(keyText.Value),
                },
                new RoundedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Text = SlopSettingsStrings.GetOmdbApiKey,
                    Action = () => game?.OpenUrlExternally(OmdbClient.SETTINGS_URL),
                },
            });

            keyTextBox.OnCommit += (_, _) => saveApiKey(keyText.Value);
        }

        private void saveApiKey(string key)
        {
            key = key.Trim();

            if (key.Length > 0)
                configApiKey.Value = key;
        }

        private static LocalisableString ratingSummaryOf(OmdbBeatmap shown)
        {
            if (shown.RatingCount is not int count || count == 0 || shown.WeightedAvg is not double average)
                return SlopOmdbStrings.NoRatings;

            string formatted = average.ToString(@"0.00", CultureInfo.InvariantCulture);
            return count == 1 ? SlopOmdbStrings.RatingSummarySingle(formatted) : SlopOmdbStrings.RatingSummary(formatted, count);
        }

        private static string errorMessageOf(Task task) => task.Exception?.GetBaseException().Message ?? @"The request was cancelled.";

        private static void addRow(LinkFlowContainer flow, LocalisableString label, LocalisableString value)
        {
            addLabel(flow, label);
            flow.AddText(value);
            flow.NewLine();
        }

        private static void addLabel(LinkFlowContainer flow, LocalisableString label)
        {
            flow.AddText(label, s => s.Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold));
            flow.AddText(@": ");
        }

        private static LinkFlowContainer createText(Action<SpriteText>? defaultCreationParameters = null) => new LinkFlowContainer(defaultCreationParameters ?? (s => s.Font = OsuFont.GetFont(size: 14)))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
        };

        private partial class HistogramBar : CompositeDrawable, IHasTooltip
        {
            private const float label_height = 14;

            public LocalisableString TooltipText { get; }

            private readonly Box bar;

            public HistogramBar(double score, int count, float fill)
            {
                string scoreText = score.ToString(@"0.#", CultureInfo.InvariantCulture);
                TooltipText = SlopOmdbStrings.HistogramTooltip(scoreText, count);

                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 1, Bottom = label_height },
                        Child = bar = new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.Both,
                            // empty buckets are still shown as a line.
                            Height = Math.Max(0.02f, fill),
                        },
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.BottomCentre,
                        Origin = Anchor.BottomCentre,
                        Text = scoreText,
                        Font = OsuFont.GetFont(size: 11),
                    },
                };
            }

            public Colour4 BarColour
            {
                set => bar.Colour = value;
            }
        }
    }
}
