// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// The short name of a user group in a rounded box, like the user group badge on the website.
    /// </summary>
    public partial class DiscussionGroupBadge : CompositeDrawable, IHasTooltip
    {
        private readonly APIUserGroup group;

        public LocalisableString TooltipText { get; }

        public DiscussionGroupBadge(APIUserGroup group)
        {
            this.group = group;

            if (group == DiscussionDisplay.MAPPER_GROUP)
                TooltipText = SlopDiscussionStrings.GroupMapper;
            else if (group == DiscussionDisplay.GUEST_GROUP)
                TooltipText = SlopDiscussionStrings.GroupGuest;
            else
                TooltipText = group.Name;

            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            var colour = DiscussionDisplay.GroupColour(group, colourProvider, colourProvider.Light1);

            Masking = true;
            CornerRadius = 4;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background6,
                },
                new OsuSpriteText
                {
                    Margin = new MarginPadding { Horizontal = 5, Vertical = 1 },
                    Text = group.ShortName,
                    Colour = colour,
                    Font = OsuFont.Default.With(size: 10, weight: FontWeight.Bold),
                },
            };
        }
    }

    /// <summary>
    /// A small rounded box with a text, used for statuses and badges.
    /// </summary>
    public partial class DiscussionPill : CompositeDrawable
    {
        public DiscussionPill(LocalisableString text, Color4 background, Color4 foreground, float fontSize = 11)
        {
            AutoSizeAxes = Axes.Both;
            Masking = true;
            CornerRadius = 4;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = background,
                },
                new OsuSpriteText
                {
                    Margin = new MarginPadding { Horizontal = 7, Vertical = 2 },
                    Text = text,
                    Colour = foreground,
                    Font = OsuFont.Default.With(size: fontSize, weight: FontWeight.Bold),
                },
            };
        }
    }

    /// <summary>
    /// A clickable text with an optional icon, like the plain buttons and links of the website.
    /// </summary>
    public partial class DiscussionTextButton : OsuClickableContainer
    {
        private readonly FillFlowContainer flow;
        private readonly OsuSpriteText text;
        private readonly SpriteIcon? icon;

        private bool selected;

        /// <summary>
        /// Whether the button is the selected one of a group, which shows it highlighted.
        /// </summary>
        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;

                if (IsLoaded)
                    updateColour();
            }
        }

        public Color4? AccentColour { get; set; }

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public DiscussionTextButton(LocalisableString label, IconUsage? iconUsage = null, float fontSize = 13)
        {
            AutoSizeAxes = Axes.Both;

            Child = flow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(5, 0),
            };

            if (iconUsage != null)
            {
                flow.Add(icon = new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Icon = iconUsage.Value,
                    Size = new Vector2(fontSize * 0.85f),
                });
            }

            flow.Add(text = new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = label,
                Font = OsuFont.Default.With(size: fontSize, weight: FontWeight.SemiBold),
            });
        }

        public LocalisableString Text
        {
            set => text.Text = value;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateColour();
        }

        protected override bool OnHover(HoverEvent e)
        {
            updateColour();
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            updateColour();
            base.OnHoverLost(e);
        }

        private void updateColour()
        {
            Color4 colour;

            if (!Enabled.Value)
                colour = colourProvider.Foreground1;
            else if (Selected)
                colour = AccentColour ?? Color4.White;
            else if (IsHovered)
                colour = (AccentColour ?? colourProvider.Light1).Lighten(0.3f);
            else
                colour = AccentColour ?? colourProvider.Content2;

            flow.FadeColour(colour, 100, Easing.OutQuint);

            text.Font = text.Font.With(weight: Selected ? FontWeight.Bold : FontWeight.SemiBold);

            if (icon != null)
                icon.Alpha = 1;
        }
    }

    /// <summary>
    /// A section of a panel with a title, like the titled sections of the website.
    /// </summary>
    public partial class DiscussionTitle : OsuSpriteText
    {
        public DiscussionTitle(LocalisableString text)
        {
            Text = text;
            Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold);
        }
    }

    public static class DiscussionLinks
    {
        /// <summary>
        /// Adds a link to a user of the discussion, or a placeholder for unknown and deleted users.
        /// </summary>
        public static void AddUser(LinkFlowContainer flow, DiscussionsState state, int? userId, Action<SpriteText>? creationParameters = null)
        {
            var user = state.GetUser(userId);

            if (user == null)
            {
                flow.AddText(SlopDiscussionStrings.UnknownUser, creationParameters);
                return;
            }

            if (user.IsDeleted)
            {
                flow.AddText(SlopDiscussionStrings.DeletedUser, creationParameters);
                return;
            }

            flow.AddUserLink(user.ToAPIUser(), creationParameters);
        }

        /// <summary>
        /// Adds a link to a discussion of the beatmap set (e.g. "#123"), which shows the discussion.
        /// </summary>
        public static void AddDiscussion(LinkFlowContainer flow, DiscussionsState state, int discussionId, Action<SpriteText>? creationParameters = null)
        {
            flow.AddLink($@"#{discussionId}", () =>
            {
                state.View.ScrollToHighlighted = true;

                if (!state.ChangeToDiscussion(discussionId))
                    state.View.ScrollToHighlighted = false;
            }, creationParameters: creationParameters);
        }
    }
}
