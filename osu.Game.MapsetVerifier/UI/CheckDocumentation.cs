// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using MapsetVerifier.RankingCriteria.Model;
using MapsetVerifier.Server.Model;
using MapsetVerifier.Server.Service;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Containers.Markdown;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Containers.Markdown;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.MapsetVerifier.UI
{
    /// <summary>
    /// The documentation of a check: what it is for, why, and what it can find.
    /// </summary>
    public partial class CheckDocumentation : CompositeDrawable
    {
        private readonly int checkId;
        private readonly bool showOutcomes;

        private FillFlowContainer content = null!;
        private LoadingSpinner spinner = null!;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        /// <param name="checkId">The check.</param>
        /// <param name="showOutcomes">Whether to show the outcomes of the check, along with the ranking criteria they enforce.</param>
        public CheckDocumentation(int checkId, bool showOutcomes = true)
        {
            this.checkId = checkId;
            this.showOutcomes = showOutcomes;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                },
                spinner = new LoadingSpinner
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Size = new Vector2(24),
                    Margin = new MarginPadding { Top = 8 },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            spinner.Show();

            VerifierEngine.RunAsync(() => DocumentationService.GetDocumentationDetails(checkId), cancellation.Token).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Logger.Error(t.Exception, @"Mapset Verifier failed to load the documentation of a check.");

                var details = t.IsCompletedSuccessfully ? t.GetResultSafely() : null;

                Schedule(() =>
                {
                    spinner.Hide();

                    if (details != null)
                        show(details.Value);
                });
            }, TaskContinuationOptions.NotOnCanceled);
        }

        private void show(ApiDocumentationCheckDetails details)
        {
            content.Add(new VerifierMarkdown(details.Description));

            if (!showOutcomes || details.Outcomes.Count == 0)
                return;

            // styled like the headings of the description above.
            content.Add(new OsuSpriteText
            {
                Text = VerifierStrings.Outcomes,
                Font = OsuFont.Torus.With(size: 18, weight: FontWeight.SemiBold),
                Margin = new MarginPadding { Top = 12 },
            });

            foreach (var outcome in details.Outcomes.OrderBy(o => Array.IndexOf(VerifierDisplay.OUTCOME_ORDER, o.Level)))
                content.Add(new OutcomeCard(outcome));
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
        }

        private partial class OutcomeCard : CompositeDrawable
        {
            private readonly ApiDocumentationCheckDetailsOutcome outcome;

            public OutcomeCard(ApiDocumentationCheckDetailsOutcome outcome)
            {
                this.outcome = outcome;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 6;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                var colour = VerifierDisplay.GetColour(outcome.Level);

                var flow = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Padding = new MarginPadding { Left = 13, Right = 10, Vertical = 8 },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            Children = new Drawable[]
                            {
                                VerifierDisplay.CreateIcon(outcome.Level, 12).With(i =>
                                {
                                    i.Anchor = Anchor.CentreLeft;
                                    i.Origin = Anchor.CentreLeft;
                                }),
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = VerifierDisplay.GetName(outcome.Level),
                                    Colour = colour,
                                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                                },
                            }
                        },
                        // the arguments of the message are formatted as code, as they are placeholders.
                        new VerifierMarkdown(outcome.Description),
                    }
                };

                if (!string.IsNullOrWhiteSpace(outcome.Cause))
                {
                    flow.Add(new VerifierSubtitle(VerifierStrings.Cause));
                    flow.Add(new VerifierMarkdown(outcome.Cause));
                }

                if (outcome.RuleIds.Count > 0)
                    flow.Add(new RuleStatementList(outcome.RuleIds));

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 3,
                        Colour = colour,
                    },
                    flow,
                };
            }
        }
    }

    /// <summary>
    /// The ranking criteria statements which an issue or outcome refers to.
    /// </summary>
    public partial class RuleStatementList : CompositeDrawable
    {
        private readonly IReadOnlyList<string> ids;

        private FillFlowContainer flow = null!;

        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        public RuleStatementList(IReadOnlyList<string> ids)
        {
            this.ids = ids;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (ids.Count == 0)
                return;

            VerifierEngine.RunAsync(() => RankingCriteriaService.GetStatements(ids), cancellation.Token).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Logger.Error(t.Exception, @"Mapset Verifier failed to load ranking criteria statements.");

                var statements = t.IsCompletedSuccessfully ? t.GetResultSafely() : null;

                Schedule(() =>
                {
                    if (statements == null || statements.Count == 0)
                        return;

                    flow.Add(new VerifierSubtitle(VerifierStrings.Rules));

                    foreach (var statement in statements)
                        flow.Add(new StatementCard(statement));
                });
            }, TaskContinuationOptions.NotOnCanceled);
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            base.Dispose(isDisposing);
        }

        private partial class StatementCard : OsuClickableContainer
        {
            private readonly ApiRcStatement statement;

            [Resolved]
            private OsuGame? game { get; set; }

            private Box background = null!;

            public StatementCard(ApiRcStatement statement)
            {
                this.statement = statement;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Masking = true;
                CornerRadius = 5;
                TooltipText = statement.WikiUrl;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                var colour = getColour(statement.Kind);

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background3,
                        Alpha = 0.5f,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 4),
                        Padding = new MarginPadding(8),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(6, 0),
                                Children = new Drawable[]
                                {
                                    new NominatorTag(statement.Kind.ToString(), colour, textSize: 10),
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Text = statement.PageTitle,
                                        Font = OsuFont.Default.With(size: 11, weight: FontWeight.SemiBold),
                                        Alpha = 0.7f,
                                    },
                                    new SpriteIcon
                                    {
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        Icon = FontAwesome.Solid.ExternalLinkAlt,
                                        Size = new Vector2(9),
                                        Alpha = 0.5f,
                                    },
                                }
                            },
                            new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 12))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = VerifierMarkdown.StripFormatting(statement.Lead),
                            },
                        }
                    },
                };

                Action = () => game?.OpenUrlExternally(statement.WikiUrl);
            }

            protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
            {
                background.FadeTo(1, 150, Easing.OutQuint);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(osu.Framework.Input.Events.HoverLostEvent e)
            {
                background.FadeTo(0.5f, 150, Easing.OutQuint);
                base.OnHoverLost(e);
            }

            private static Color4 getColour(RcKind kind)
            {
                switch (kind)
                {
                    case RcKind.Rule:
                        return VerifierDisplay.PROBLEM;

                    case RcKind.Guideline:
                        return VerifierDisplay.WARNING;

                    default:
                        return VerifierDisplay.CHECK;
                }
            }
        }
    }

    /// <summary>
    /// Markdown from Mapset Verifier, which may contain images and HTML that can't be shown.
    /// </summary>
    public partial class VerifierMarkdown : OsuMarkdownContainer
    {
        private static readonly Regex image_regex = new Regex(@"!\[[^\]]*\]\(\s*[^\s)]+(?:\s+""(?<title>[^""]*)"")?\s*\)", RegexOptions.Compiled);
        private static readonly Regex html_regex = new Regex(@"<\/?[a-zA-Z][^>]*>", RegexOptions.Compiled);
        private static readonly Regex emphasis_regex = new Regex(@"(\*\*|__|\*|_|`)(?<text>[^*_`]+?)\1", RegexOptions.Compiled);
        private static readonly Regex block_start_regex = new Regex(@"^([-*+>#|]|\d+[.)])", RegexOptions.Compiled);
        private static readonly Regex link_regex = new Regex(@"\[(?<text>[^\]]+)\]\([^)]+\)", RegexOptions.Compiled);

        public VerifierMarkdown(string markdown)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            DocumentMargin = new MarginPadding();
            DocumentPadding = new MarginPadding();
            Text = Clean(markdown);
        }

        public override OsuMarkdownTextFlowContainer CreateTextFlow() => base.CreateTextFlow().With(f => f.Colour = Color4.White.Opacity(0.9f));

        protected override MarkdownHeading CreateHeading(HeadingBlock headingBlock) => new VerifierMarkdownHeading(headingBlock);

        /// <summary>
        /// A heading which stays below the title of the check it documents.
        /// </summary>
        private partial class VerifierMarkdownHeading : OsuMarkdownHeading
        {
            public VerifierMarkdownHeading(HeadingBlock headingBlock)
                : base(headingBlock)
            {
                Margin = new MarginPadding { Top = 8 };
            }

            protected override float GetFontSizeByLevel(int level) => level <= 1 ? 18 : 16;
        }

        /// <summary>
        /// Replaces images (which are part of the website of Mapset Verifier) with their captions, and removes HTML.
        /// </summary>
        public static string Clean(string markdown)
        {
            string text = image_regex.Replace(markdown, m =>
            {
                string title = m.Groups["title"].Value;
                return string.IsNullOrWhiteSpace(title) ? string.Empty : $"*{title}*";
            });

            text = joinWrappedLines(text);
            text = text.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
            return html_regex.Replace(text, string.Empty).Trim();
        }

        /// <summary>
        /// Joins the lines of paragraphs which are wrapped in the source, since every line break is shown as such.
        /// </summary>
        private static string joinWrappedLines(string markdown)
        {
            var result = new StringBuilder();
            bool inParagraph = false;
            bool inCodeBlock = false;

            foreach (string rawLine in markdown.Replace("\r", string.Empty).Split('\n'))
            {
                string line = rawLine.Trim();
                bool isFence = line.StartsWith("```", StringComparison.Ordinal);

                if (isFence)
                    inCodeBlock = !inCodeBlock;

                if (inCodeBlock || isFence)
                {
                    result.Append('\n').Append(rawLine);
                    inParagraph = false;
                    continue;
                }

                if (line.Length == 0)
                {
                    result.Append('\n');
                    inParagraph = false;
                    continue;
                }

                if (inParagraph && !block_start_regex.IsMatch(line))
                    result.Append(' ').Append(line);
                else
                    result.Append('\n').Append(line);

                // headings and table rows are never continued on the next line.
                inParagraph = !line.StartsWith('#') && !line.StartsWith('|');
            }

            return result.ToString();
        }

        /// <summary>
        /// Removes all formatting, for text which is shown in a single text flow.
        /// </summary>
        public static string StripFormatting(string markdown)
        {
            string text = Clean(markdown);
            text = link_regex.Replace(text, m => m.Groups["text"].Value);
            return emphasis_regex.Replace(text, m => m.Groups["text"].Value);
        }
    }
}
