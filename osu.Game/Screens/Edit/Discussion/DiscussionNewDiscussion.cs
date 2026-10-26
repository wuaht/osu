// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Screens.Edit.Nominators;
using osuTK;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The box for posting a new discussion, or a review on the reviews page.
    /// </summary>
    public partial class DiscussionNewDiscussion : DiscussionSection
    {
        private static readonly string[] types = { @"praise", @"suggestion", @"problem", @"mapper_note", @"hype" };

        /// <summary>
        /// The types which can be embedded into reviews, with "paragraph" for plain text.
        /// </summary>
        private static readonly string[] review_types = { @"paragraph", @"praise", @"suggestion", @"problem" };

        private static readonly Regex timestamp_regex = new Regex(@"^\s*(\d{2,}):([0-5]\d)[:.](\d{3})", RegexOptions.Compiled);

        [Resolved]
        private EditorClock? editorClock { get; set; }

        private OsuSpriteText scope = null!;
        private OsuSpriteText hint = null!;

        private FillFlowContainer discussionComposer = null!;
        private FillFlowContainer<DiscussionTextButton> typeButtons = null!;
        private DiscussionTextEditor messageBox = null!;
        private Container signInHint = null!;
        private RoundedButton postButton = null!;

        private FillFlowContainer reviewComposer = null!;
        private FillFlowContainer<ReviewBlock> reviewBlocks = null!;

        private string selectedType = @"suggestion";

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new NominatorsPanel(Axes.Y)
            {
                RelativeSizeAxes = Axes.X,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Padding = new MarginPadding { Horizontal = 20, Vertical = 14 },
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
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
                                        hint = new OsuSpriteText
                                        {
                                            Font = OsuFont.Default.With(size: 13),
                                            Colour = ColourProvider.Content2,
                                        },
                                    }
                                },
                            }
                        },
                        signInHint = new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    Text = SlopDiscussionStrings.SignInHint,
                                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                                    Colour = ColourProvider.Content2,
                                },
                                new RoundedButton
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    Width = 150,
                                    Height = 30,
                                    Text = SlopDiscussionStrings.SignInButton,
                                    Action = () => Screen.SignIntoWebsite(),
                                },
                            }
                        },
                        discussionComposer = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                            Children = new Drawable[]
                            {
                                messageBox = new DiscussionTextEditor(SlopDiscussionStrings.ComposerPlaceholder),
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        typeButtons = new FillFlowContainer<DiscussionTextButton>
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(16, 0),
                                        },
                                        postButton = new RoundedButton
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Width = 150,
                                            Height = 34,
                                            Text = SlopDiscussionStrings.ComposerPost,
                                            Action = post,
                                        },
                                    }
                                },
                            }
                        },
                        reviewComposer = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                            Children = new Drawable[]
                            {
                                reviewBlocks = new FillFlowContainer<ReviewBlock>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 6),
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Children = new Drawable[]
                                    {
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            AutoSizeAxes = Axes.Both,
                                            Direction = FillDirection.Horizontal,
                                            Spacing = new Vector2(16, 0),
                                            ChildrenEnumerable = review_types.Select(t => new DiscussionTextButton(
                                                t == @"paragraph" ? SlopDiscussionStrings.ReviewAddParagraph : DiscussionDisplay.TypeName(t),
                                                t == @"paragraph" ? FontAwesome.Solid.Paragraph : DiscussionDisplay.TypeIcon(t))
                                            {
                                                AccentColour = t == @"paragraph" ? null : DiscussionDisplay.TypeColour(t, ColourProvider),
                                                Action = () => addReviewBlock(t),
                                            }),
                                        },
                                        new RoundedButton
                                        {
                                            Anchor = Anchor.CentreRight,
                                            Origin = Anchor.CentreRight,
                                            Width = 150,
                                            Height = 34,
                                            Text = SlopDiscussionStrings.ReviewPost,
                                            Action = postReview,
                                        },
                                    }
                                },
                            }
                        },
                    }
                }
            };

            foreach (string type in types)
            {
                typeButtons.Add(new DiscussionTextButton(DiscussionDisplay.TypeName(type), DiscussionDisplay.TypeIcon(type))
                {
                    AccentColour = DiscussionDisplay.TypeColour(type, ColourProvider),
                    Action = () => selectType(type),
                });
            }

            messageBox.Committed += post;

            addReviewBlock(@"paragraph");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Screen.NewDiscussionRequested += onNewDiscussionRequested;
        }

        private void onNewDiscussionRequested(string? messageType)
        {
            // the page may have changed just now, so the display is updated before focusing.
            Schedule(() =>
            {
                if (messageType != null)
                    selectType(messageType);

                Screen.ScrollIntoView(this);
                messageBox.TakeFocus();
            });
        }

        private void selectType(string type)
        {
            selectedType = type;

            if (State != null)
                UpdateDisplay(State);
        }

        protected override void UpdateDisplay(DiscussionsState state)
        {
            Alpha = state.View.Page == DiscussionPage.Events || state.BeatmapSet.DiscussionLocked ? 0 : 1;

            bool review = state.View.Page == DiscussionPage.Reviews;

            discussionComposer.Alpha = review ? 0 : 1;
            reviewComposer.Alpha = review ? 1 : 0;

            switch (state.View.Page)
            {
                case DiscussionPage.Reviews:
                    scope.Text = SlopDiscussionStrings.ModeReviews;
                    hint.Text = SlopDiscussionStrings.ReviewHint;
                    break;

                case DiscussionPage.GeneralAll:
                    scope.Text = SlopDiscussionStrings.AllDifficulties;
                    hint.Text = SlopDiscussionStrings.NewDiscussionDescription;
                    break;

                case DiscussionPage.Timeline:
                    scope.Text = state.CurrentBeatmap?.Version ?? string.Empty;
                    hint.Text = SlopDiscussionStrings.ComposerTimestampHint;
                    break;

                default:
                    scope.Text = state.CurrentBeatmap?.Version ?? string.Empty;
                    hint.Text = SlopDiscussionStrings.NewDiscussionDescription;
                    break;
            }

            var available = availableTypes(state).ToList();

            if (!available.Contains(selectedType))
                selectedType = @"suggestion";

            for (int i = 0; i < types.Length; i++)
            {
                var button = typeButtons[i];

                button.Alpha = available.Contains(types[i]) ? 1 : 0;
                button.Selected = types[i] == selectedType;
            }

            signInHint.Alpha = Screen.IsSignedIntoWebsite ? 0 : 1;

            // without the website session, it isn't known whether the user can hype, so posting signs in first.
            var attributes = state.BeatmapSet.CurrentUserAttributes;

            if (selectedType == @"hype" && attributes?.CanHype == false)
            {
                postButton.Enabled.Value = false;

                if (!string.IsNullOrEmpty(attributes.CanHypeReason))
                    hint.Text = attributes.CanHypeReason;
            }
            else
                postButton.Enabled.Value = true;
        }

        /// <summary>
        /// The types which can be posted on the current page, like the website.
        /// </summary>
        private IEnumerable<string> availableTypes(DiscussionsState state)
        {
            yield return @"praise";
            yield return @"suggestion";
            yield return @"problem";

            int? userId = state.CurrentUserId;

            bool isMapper = userId != null && (state.BeatmapSet.UserId == userId
                                               || (state.CurrentBeatmap is DiscussionBeatmap beatmap && state.IsBeatmapOwner(userId.Value, beatmap)));

            if (isMapper)
                yield return @"mapper_note";

            if (state.View.Page == DiscussionPage.GeneralAll && !isMapper && state.BeatmapSet.CanBeHyped)
                yield return @"hype";
        }

        private void post()
        {
            var state = State;

            if (state == null)
                return;

            string message = messageBox.Text.Trim();

            if (message.Length == 0)
                return;

            int? beatmapId = state.View.Page == DiscussionPage.GeneralAll ? null : state.CurrentBeatmapId;
            int? timestamp = null;

            if (state.View.Page == DiscussionPage.Timeline)
                timestamp = parseTimestamp(message) ?? (int)(editorClock?.CurrentTime ?? 0);

            Screen.PostDiscussion(beatmapId, selectedType, timestamp, message, () => messageBox.Text = string.Empty);
        }

        private static int? parseTimestamp(string message)
        {
            var match = timestamp_regex.Match(message);

            if (!match.Success)
                return null;

            return int.Parse(match.Groups[1].Value) * 60000 + int.Parse(match.Groups[2].Value) * 1000 + int.Parse(match.Groups[3].Value);
        }

        private void addReviewBlock(string type)
        {
            var block = new ReviewBlock(type);
            block.RemoveRequested = () =>
            {
                reviewBlocks.Remove(block, true);

                if (reviewBlocks.Count == 0)
                    addReviewBlock(@"paragraph");
            };

            reviewBlocks.Add(block);
        }

        private void postReview()
        {
            var state = State;

            if (state == null)
                return;

            var document = new List<object>();

            foreach (var block in reviewBlocks)
            {
                string text = block.Text.Trim();

                if (text.Length == 0)
                    continue;

                if (block.Type == @"paragraph")
                {
                    document.Add(new { type = @"paragraph", text });
                    continue;
                }

                // embeds with a timestamp are about the current difficulty, the others about all difficulties.
                int? timestamp = parseTimestamp(text);

                document.Add(new
                {
                    type = @"embed",
                    discussion_type = block.Type,
                    text,
                    beatmap_id = timestamp != null ? state.CurrentBeatmapId : null,
                    timestamp,
                });
            }

            if (document.Count == 0)
                return;

            bool hasProblems = reviewBlocks.Any(b => b.Type == @"problem" && b.Text.Trim().Length > 0);

            Screen.SubmitReview(JsonConvert.SerializeObject(document), () =>
            {
                reviewBlocks.Clear();
                addReviewBlock(@"paragraph");
            }, hasProblems);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (Screen.IsNotNull())
                Screen.NewDiscussionRequested -= onNewDiscussionRequested;

            base.Dispose(isDisposing);
        }

        /// <summary>
        /// A paragraph or an embedded discussion of a review.
        /// </summary>
        private partial class ReviewBlock : CompositeDrawable
        {
            public readonly string Type;

            public System.Action? RemoveRequested;

            private readonly DiscussionTextEditor textBox;

            public string Text => textBox.Text;

            public ReviewBlock(string type)
            {
                Type = type;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                bool paragraph = type == @"paragraph";

                InternalChildren = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Icon = paragraph ? FontAwesome.Solid.Paragraph : DiscussionDisplay.TypeIcon(type),
                        Size = new Vector2(14),
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 26, Right = 30 },
                        Child = textBox = new DiscussionTextEditor(paragraph ? SlopDiscussionStrings.ReviewParagraphPlaceholder : SlopDiscussionStrings.ReviewEmbedPlaceholder, 2),
                    },
                    new IconButton
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Icon = FontAwesome.Solid.Times,
                        Size = new Vector2(24),
                        Action = () => RemoveRequested?.Invoke(),
                    },
                };
            }

            [BackgroundDependencyLoader]
            private void load(osu.Game.Overlays.OverlayColourProvider colourProvider)
            {
                if (Type != @"paragraph")
                    InternalChildren[0].Colour = DiscussionDisplay.TypeColour(Type, colourProvider);
            }
        }
    }
}
