// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osu.Game.Screens.Edit.Nominators;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Shows the friends and group conversations as cards (like the friends of the dashboard), which open the conversation when clicked.
    /// </summary>
    public partial class DiscordFriendsView : CompositeDrawable
    {
        private const float horizontal_padding = 40;

        private const float card_spacing = 10;

        private float cardWidth = FriendCard.MIN_WIDTH;

        [Resolved]
        private DiscordClient client { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly BindableList<DiscordChannel> privateChannels = new BindableList<DiscordChannel>();

        private BasicSearchTextBox searchTextBox = null!;
        private FriendSearchContainer cardFlow = null!;
        private OsuSpriteText emptyText = null!;

        public DiscordFriendsView()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(GridSizeMode.Absolute, DiscordStyle.HEADER_HEIGHT),
                    new Dimension(GridSizeMode.AutoSize),
                    new Dimension(),
                },
                Content = new[]
                {
                    new Drawable[] { createHeader() },
                    new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding { Horizontal = horizontal_padding, Vertical = 20 },
                            Child = searchTextBox = new BasicSearchTextBox
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 40,
                                ReleaseFocusOnCommit = false,
                                HoldFocus = true,
                                PlaceholderText = SlopDiscordStrings.SearchFriends,
                            },
                        },
                    },
                    new Drawable[]
                    {
                        new OsuScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            ScrollbarOverlapsContent = false,
                            Child = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Horizontal = horizontal_padding, Bottom = 20 },
                                Children = new Drawable[]
                                {
                                    cardFlow = new FriendSearchContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Spacing = new Vector2(card_spacing),
                                    },
                                    emptyText = new OsuSpriteText
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Font = DiscordStyle.UiFont(16, FontWeight.SemiBold),
                                        Colour = colourProvider.Content2,
                                        Margin = new MarginPadding { Top = 20 },
                                        Alpha = 0,
                                    },
                                },
                            },
                        },
                    },
                },
            };

            privateChannels.BindTo(client.PrivateChannels);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            privateChannels.BindCollectionChanged((_, _) => Scheduler.AddOnce(updateCards), true);
            searchTextBox.Current.BindValueChanged(s =>
            {
                cardFlow.SearchTerm = s.NewValue;
                Scheduler.AddOnce(updateEmptyText);
            });

            client.UserUpdated += onUserUpdated;
        }

        protected override void Update()
        {
            base.Update();

            // As many cards as fit next to each other, sized to fill the width.
            float available = cardFlow.DrawWidth;
            int columns = Math.Max(1, (int)((available + card_spacing) / (FriendCard.MIN_WIDTH + card_spacing)));
            float width = MathF.Floor(Math.Max(FriendCard.MIN_WIDTH, (available - card_spacing * (columns - 1)) / columns));

            if (width == cardWidth)
                return;

            cardWidth = width;

            foreach (var card in cardFlow.Children)
                card.Width = cardWidth;
        }

        private Drawable createHeader() => new FillFlowContainer
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(8, 0),
            Padding = new MarginPadding { Horizontal = 16 },
            Children = new Drawable[]
            {
                new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Icon = FontAwesome.Solid.UserFriends,
                    Size = new Vector2(18),
                    Colour = colourProvider.Content2,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = SlopDiscordStrings.Friends,
                    Font = DiscordStyle.UiFont(16, FontWeight.Bold),
                    Colour = colourProvider.Content1,
                },
            },
        };

        private void updateCards()
        {
            // Keep the existing cards so that their avatars don't have to be loaded again.
            var existing = cardFlow.Children.ToDictionary(c => c.Channel.Id);

            foreach (var card in existing.Values.Where(c => !privateChannels.Contains(c.Channel)).ToArray())
                cardFlow.Remove(card, true);

            foreach (var channel in privateChannels)
            {
                if (existing.TryGetValue(channel.Id, out var card) && card.Channel == channel)
                    continue;

                if (card != null)
                    cardFlow.Remove(card, true);

                cardFlow.Add(new FriendCard(channel) { Width = cardWidth });
            }

            cardFlow.Sort();
            updateEmptyText();
        }

        private void updateEmptyText()
        {
            if (privateChannels.Count == 0)
            {
                emptyText.Text = SlopDiscordStrings.NoFriends;
                emptyText.Show();
            }
            else if (!string.IsNullOrEmpty(cardFlow.SearchTerm) && !cardFlow.Children.Any(c => c.MatchingFilter))
            {
                emptyText.Text = SlopDiscordStrings.NoFriendsFound;
                emptyText.Show();
            }
            else
                emptyText.Hide();
        }

        private void onUserUpdated(DiscordUser user) => Schedule(() =>
        {
            bool changed = false;

            foreach (var card in cardFlow.Children.Where(c => c.Channel.Recipients.Any(r => r.Id == user.Id)))
            {
                card.UpdateUser();
                changed = true;
            }

            // The order depends on the status.
            if (changed)
                cardFlow.Sort();
        });

        public void TakeSearchFocus() => Schedule(() => GetContainingFocusManager()?.ChangeFocus(searchTextBox));

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (client.IsNotNull())
                client.UserUpdated -= onUserUpdated;
        }

        private partial class FriendSearchContainer : SearchContainer<FriendCard>
        {
            public void Sort() => InvalidateLayout();

            public override IEnumerable<Drawable> FlowingChildren => base.FlowingChildren
                                                                          .OfType<FriendCard>()
                                                                          .OrderBy(c => getStatusOrder(c.Status))
                                                                          .ThenBy(c => c.Channel.DisplayName, StringComparer.CurrentCultureIgnoreCase);

            private static int getStatusOrder(DiscordUserStatus status)
            {
                switch (status)
                {
                    case DiscordUserStatus.Online:
                        return 0;

                    case DiscordUserStatus.Idle:
                        return 1;

                    case DiscordUserStatus.DoNotDisturb:
                        return 2;

                    case DiscordUserStatus.Unknown:
                        return 3;

                    default:
                        return 4;
                }
            }
        }

        /// <summary>
        /// A friend or group conversation in the style of the nominator cards of the editor.
        /// </summary>
        private partial class FriendCard : OsuClickableContainer, IFilterable
        {
            /// <summary>
            /// The minimum width of a card. Cards are wider to fill the available width.
            /// </summary>
            public const float MIN_WIDTH = 230;

            private const float height = 84;

            private const float padding = 8;

            private const float avatar_size = 40;

            public readonly DiscordChannel Channel;

            public DiscordUserStatus Status => recipient?.Status ?? DiscordUserStatus.Unknown;

            [Resolved]
            private DiscordClient client { get; set; } = null!;

            [Resolved]
            private DiscordNavigation navigation { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [Resolved]
            private DiscordTextureStore textures { get; set; } = null!;

            private DiscordUser? recipient => Channel.Type == DiscordChannelType.DirectMessage ? Channel.Recipients.FirstOrDefault() : null;

            private Container background = null!;
            private Container avatarContainer = null!;
            private Box hover = null!;
            private Box statusStrip = null!;
            private OsuSpriteText nameText = null!;
            private OsuSpriteText usernameText = null!;
            private FillFlowContainer tags = null!;

            private string? avatarUrl;
            private string? backgroundSource;

            public FriendCard(DiscordChannel channel)
            {
                Channel = channel;

                Size = new Vector2(MIN_WIDTH, height);
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    background = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    // Banners and profile colours can be bright, so they are darkened enough to keep the text readable, especially further right.
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = ColourInfo.GradientHorizontal(colourProvider.Background4.Opacity(0.4f), colourProvider.Background4.Opacity(0.9f)),
                    },
                    hover = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    statusStrip = new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 4,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 6),
                        Padding = new MarginPadding { Vertical = padding, Left = padding + 6, Right = padding },
                        Children = new Drawable[]
                        {
                            new GridContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = avatar_size,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.Absolute, avatar_size),
                                    new Dimension(GridSizeMode.Absolute, 10),
                                    new Dimension(),
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        avatarContainer = new Container
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Masking = true,
                                            CornerRadius = 5,
                                        },
                                        Empty(),
                                        new FillFlowContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Direction = FillDirection.Vertical,
                                            Spacing = new Vector2(0, 2),
                                            Children = new Drawable[]
                                            {
                                                nameText = new TruncatingSpriteText
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Font = DiscordStyle.UiFont(15, FontWeight.Bold),
                                                },
                                                usernameText = new TruncatingSpriteText
                                                {
                                                    RelativeSizeAxes = Axes.X,
                                                    Font = DiscordStyle.UiFont(12, FontWeight.SemiBold),
                                                    Alpha = 0.7f,
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                            tags = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(4),
                            },
                        },
                    },
                };

                Action = () => navigation.OpenChannel(Channel, client);

                UpdateUser();
            }

            /// <summary>
            /// Shows the current name, avatar and status of the conversation.
            /// </summary>
            public void UpdateUser()
            {
                nameText.Text = Channel.DisplayName;
                usernameText.Text = recipient?.Username ?? string.Empty;

                string? url = getAvatarUrl();

                if (url != avatarUrl || avatarContainer.Count == 0)
                {
                    avatarUrl = url;
                    avatarContainer.Child = createAvatar(url);
                }

                updateBackground(url);

                var (text, colour) = getStatus();

                statusStrip.Colour = colour ?? colourProvider.Background1;

                tags.Clear();

                if (Channel.Type == DiscordChannelType.DirectMessage)
                {
                    tags.Add(new NominatorTag(text, colour ?? colourProvider.Content2, new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(6),
                    }, 11));
                }
                else
                {
                    tags.Add(new NominatorTag(text, DiscordStyle.Blurple, new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(9),
                        Icon = FontAwesome.Solid.Users,
                    }, 11));
                }
            }

            /// <summary>
            /// Shows the banner of the user, or their profile colour like Discord does (which is the colour of the avatar if they didn't choose one).
            /// </summary>
            private void updateBackground(string? avatar)
            {
                string? bannerUrl = recipient?.GetBannerUrl();
                int? accentColour = recipient?.AccentColour;

                string? source = bannerUrl ?? (accentColour != null ? $@"#{accentColour:X6}" : avatar);

                if (source == backgroundSource)
                    return;

                backgroundSource = source;

                if (bannerUrl != null)
                {
                    background.Child = new DiscordRemoteImage(bannerUrl)
                    {
                        RelativeSizeAxes = Axes.Both,
                        ImageFillMode = FillMode.Fill,
                        ShowPlaceholder = false,
                    };
                    return;
                }

                var colourBox = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                };

                background.Child = colourBox;

                if (accentColour != null)
                {
                    showColour(colourBox, new Color4((accentColour.Value >> 16 & 0xff) / 255f, (accentColour.Value >> 8 & 0xff) / 255f, (accentColour.Value & 0xff) / 255f, 1));
                    return;
                }

                if (avatar == null)
                {
                    showColour(colourBox, DiscordStyle.Blurple);
                    return;
                }

                textures.GetDominantColourAsync(avatar).ContinueWith(t =>
                {
                    if (t.GetResultSafely() is Color4 colour)
                        Schedule(() => showColour(colourBox, colour));
                });
            }

            private static void showColour(Box box, Color4 colour)
            {
                // Darken bright colours (like the white of a sketched avatar) to keep the text and tags readable.
                const float max_luminance = 0.25f;

                float luminance = 0.2126f * colour.R + 0.7152f * colour.G + 0.0722f * colour.B;

                if (luminance > max_luminance)
                {
                    float scale = max_luminance / luminance;
                    colour = new Color4(colour.R * scale, colour.G * scale, colour.B * scale, 1);
                }

                box.Colour = colour;
                box.FadeIn(200);
            }

            private string? getAvatarUrl()
            {
                if (!string.IsNullOrEmpty(Channel.Icon))
                    return $@"https://cdn.discordapp.com/channel-icons/{Channel.Id}/{Channel.Icon}.png?size=128";

                if (Channel.Type == DiscordChannelType.GroupDirectMessage && Channel.Recipients.Count != 1)
                    return null;

                return Channel.Recipients.FirstOrDefault()?.GetAvatarUrl(128);
            }

            private Drawable createAvatar(string? url)
            {
                if (url != null)
                    return new DiscordRemoteImage(url) { RelativeSizeAxes = Axes.Both };

                return new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = DiscordStyle.Blurple,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = FontAwesome.Solid.Users,
                            Size = new Vector2(18),
                        },
                    },
                };
            }

            private (LocalisableString text, Color4? colour) getStatus()
            {
                if (Channel.Type != DiscordChannelType.DirectMessage)
                    return (SlopDiscordStrings.Members(Channel.Recipients.Count + 1), null);

                switch (Status)
                {
                    case DiscordUserStatus.Online:
                        return (SlopDiscordStrings.StatusOnline, Color4Extensions.FromHex(@"23a55a"));

                    case DiscordUserStatus.Idle:
                        return (SlopDiscordStrings.StatusIdle, Color4Extensions.FromHex(@"f0b232"));

                    case DiscordUserStatus.DoNotDisturb:
                        return (SlopDiscordStrings.StatusDoNotDisturb, Color4Extensions.FromHex(@"f23f43"));

                    case DiscordUserStatus.Offline:
                        return (SlopDiscordStrings.StatusOffline, null);

                    default:
                        return (SlopDiscordStrings.StatusOffline, null);
                }
            }

            protected override bool OnHover(HoverEvent e)
            {
                hover.FadeTo(0.05f, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hover.FadeOut(100);
                base.OnHoverLost(e);
            }

            public IEnumerable<LocalisableString> FilterTerms
            {
                get
                {
                    yield return Channel.DisplayName;

                    foreach (var r in Channel.Recipients)
                    {
                        yield return r.Username;
                        yield return r.DisplayName;
                    }
                }
            }

            private bool matchingFilter = true;

            public bool MatchingFilter
            {
                get => matchingFilter;
                set
                {
                    matchingFilter = value;
                    this.FadeTo(value ? 1 : 0);
                }
            }

            public bool FilteringActive { set { } }
        }
    }
}
