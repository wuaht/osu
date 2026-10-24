// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Discord;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// The name and topic of a channel above its content. Threads also show the channel they belong to, which can be clicked to open it.
    /// </summary>
    public partial class DiscordChannelHeader : CompositeDrawable
    {
        private readonly DiscordChannel channel;

        [Resolved]
        private DiscordClient client { get; set; } = null!;

        [Resolved]
        private DiscordNavigation navigation { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        public DiscordChannelHeader(DiscordChannel channel)
        {
            this.channel = channel;

            RelativeSizeAxes = Axes.Both;
            Masking = true;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var flow = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Padding = new MarginPadding { Horizontal = 16 },
            };

            if (channel.IsThread && channel.ParentId is ulong parentId && client.GetChannel(parentId) is DiscordChannel parent)
            {
                flow.Add(new ParentLink(parent)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Action = () => navigation.Channel.Value = parent,
                });

                flow.Add(new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Icon = FontAwesome.Solid.ChevronRight,
                    Size = new Vector2(10),
                    Colour = colourProvider.Content2,
                });
            }

            if (channel.Type == DiscordChannelType.DirectMessage && channel.Recipients.FirstOrDefault() is DiscordUser recipient)
            {
                flow.Add(new DiscordAvatar(recipient.GetAvatarUrl(), 24)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                });
            }
            else
            {
                flow.Add(new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Icon = DiscordChannelSidebar.GetIcon(channel),
                    Size = new Vector2(18),
                    Colour = colourProvider.Content2,
                });
            }

            flow.Add(new OsuSpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = channel.DisplayName,
                Font = DiscordStyle.UiFont(16, FontWeight.Bold),
                Colour = colourProvider.Content1,
            });

            if (!string.IsNullOrWhiteSpace(channel.Topic))
            {
                flow.Add(new Box
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(1, 20),
                    Colour = colourProvider.Background2,
                    Margin = new MarginPadding { Horizontal = 4 },
                });

                flow.Add(new TruncatingSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    // The topic takes the remaining space, which is not known to the flow.
                    MaxWidth = 600,
                    Text = channel.Topic.ReplaceLineEndings(@" "),
                    Font = DiscordStyle.UiFont(14),
                    Colour = colourProvider.Content2,
                });
            }

            InternalChildren = new Drawable[]
            {
                flow,
                new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 1,
                    Colour = Color4.Black,
                    Alpha = 0.4f,
                },
            };
        }

        private partial class ParentLink : OsuClickableContainer
        {
            private readonly DiscordChannel parent;

            private FillFlowContainer content = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public ParentLink(DiscordChannel parent)
            {
                this.parent = parent;

                AutoSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = content = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Colour = colourProvider.Content2,
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Icon = DiscordChannelSidebar.GetIcon(parent),
                            Size = new Vector2(18),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = parent.DisplayName,
                            Font = DiscordStyle.UiFont(16, FontWeight.Bold),
                        },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                content.FadeColour(colourProvider.Content1, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                content.FadeColour(colourProvider.Content2, 100);
                base.OnHoverLost(e);
            }
        }
    }
}
