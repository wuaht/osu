// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osu.Game.Overlays.Chat;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Shows the selected channel of the <see cref="DiscordNavigation"/>.
    /// </summary>
    public partial class DiscordChatView : CompositeDrawable
    {
        [Resolved]
        private DiscordNavigation navigation { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        private readonly Bindable<DiscordChannel?> channel = new Bindable<DiscordChannel?>();

        private Container content = null!;
        private ChannelChat? currentChat;

        public DiscordChatView()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = content = new Container
            {
                RelativeSizeAxes = Axes.Both,
            };

            channel.BindTo(navigation.Channel);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            channel.BindValueChanged(c =>
            {
                if (c.NewValue?.Id == currentChat?.Channel.Id && c.NewValue != null)
                    return;

                content.Clear();
                currentChat = null;

                if (c.NewValue == null)
                {
                    content.Add(new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 12),
                        Children = new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Icon = FontAwesome.Regular.Comments,
                                Size = new Vector2(48),
                                Colour = colourProvider.Content2,
                                Alpha = 0.6f,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = SlopDiscordStrings.SelectConversation,
                                Font = DiscordStyle.UiFont(16, FontWeight.SemiBold),
                                Colour = colourProvider.Content2,
                            },
                        },
                    });
                    return;
                }

                content.Add(currentChat = new ChannelChat(c.NewValue));
            }, true);
        }

        public void TakeTextBoxFocus() => currentChat?.TakeTextBoxFocus();

        public void KillTextBoxFocus() => currentChat?.KillTextBoxFocus();

        /// <summary>
        /// The header, messages and text box of a single channel.
        /// </summary>
        private partial class ChannelChat : CompositeDrawable
        {
            /// <summary>
            /// Older messages are loaded when the scroll position is closer to the top than this.
            /// </summary>
            private const float load_older_threshold = 400;

            public readonly DiscordChannel Channel;

            [Resolved]
            private DiscordClient client { get; set; } = null!;

            [Resolved]
            private DiscordNavigation navigation { get; set; } = null!;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            private ChannelScrollContainer scroll = null!;
            private FillFlowContainer<DrawableDiscordMessage> messageFlow = null!;
            private Container topContainer = null!;
            private LoadingSpinner topSpinner = null!;
            private HistoryTextBox textBox = null!;

            private DiscordMarkdownContext markdownContext = null!;

            private bool loadingOlder;
            private bool reachedStart;

            public ChannelChat(DiscordChannel channel)
            {
                Channel = channel;
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                markdownContext = DiscordMarkdownContext.FromClient(client, c => navigation.OpenChannel(c, client));

                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, DiscordStyle.HEADER_HEIGHT),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[] { createHeader() },
                        new Drawable[]
                        {
                            scroll = new ChannelScrollContainer
                            {
                                RelativeSizeAxes = Axes.Both,
                                ScrollbarOverlapsContent = false,
                                Child = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Padding = new MarginPadding { Bottom = 12 },
                                    Children = new Drawable[]
                                    {
                                        topContainer = new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                        },
                                        topSpinner = new LoadingSpinner
                                        {
                                            Anchor = Anchor.TopCentre,
                                            Origin = Anchor.TopCentre,
                                            Size = new Vector2(32),
                                            Margin = new MarginPadding { Vertical = 12 },
                                        },
                                        messageFlow = new FillFlowContainer<DrawableDiscordMessage>
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Vertical,
                                        },
                                    },
                                },
                            },
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Padding = new MarginPadding { Horizontal = 16, Bottom = 16, Top = 4 },
                                Child = textBox = new DiscordMessageTextBox
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = 44,
                                    LengthLimit = 2000,
                                    PlaceholderText = SlopDiscordStrings.MessagePlaceholder(Channel.IsPrivate ? Channel.DisplayName : $@"#{Channel.DisplayName}"),
                                    ReleaseFocusOnCommit = false,
                                    HoldFocus = true,
                                },
                            },
                        },
                    },
                };

                textBox.OnCommit += (_, _) => sendMessage();
            }

            private Drawable createHeader()
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

                if (Channel.Type == DiscordChannelType.DirectMessage && Channel.Recipients.FirstOrDefault() is DiscordUser recipient)
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
                        Icon = DiscordChannelSidebar.GetIcon(Channel),
                        Size = new Vector2(18),
                        Colour = colourProvider.Content2,
                    });
                }

                flow.Add(new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = Channel.DisplayName,
                    Font = DiscordStyle.UiFont(16, FontWeight.Bold),
                    Colour = colourProvider.Content1,
                });

                if (!string.IsNullOrWhiteSpace(Channel.Topic))
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
                        Text = Channel.Topic.ReplaceLineEndings(@" "),
                        Font = DiscordStyle.UiFont(14),
                        Colour = colourProvider.Content2,
                    });
                }

                return new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    Children = new Drawable[]
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
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                client.MessageCreated += onMessageCreated;
                client.MessageUpdated += onMessageUpdated;
                client.MessageDeleted += onMessageDeleted;

                if (!client.SupportsHistory)
                {
                    reachedStart = true;
                    topSpinner.Hide();
                    topContainer.Child = createNote(SlopDiscordStrings.HistoryUnavailable, FontAwesome.Solid.InfoCircle, colourProvider.Content2);
                }
                else
                    loadOlder();
            }

            protected override void Update()
            {
                base.Update();

                if (!loadingOlder && !reachedStart && messageFlow.Count > 0 && scroll.Current < load_older_threshold)
                    loadOlder();
            }

            private void loadOlder()
            {
                if (loadingOlder || reachedStart)
                    return;

                loadingOlder = true;
                topSpinner.Show();

                ulong? before = messageFlow.FirstOrDefault(m => !m.Message.Pending)?.Message.Id;

                client.GetMessages(Channel, before).ContinueWith(t => Schedule(() =>
                {
                    loadingOlder = false;
                    topSpinner.Hide();

                    if (t.IsFaulted)
                    {
                        reachedStart = true;
                        string error = t.Exception?.GetBaseException().Message ?? string.Empty;
                        topContainer.Child = createNote(SlopDiscordStrings.LoadFailed(error), FontAwesome.Solid.ExclamationTriangle, DiscordStyle.Error);
                        return;
                    }

                    var messages = t.GetResultSafely();

                    if (messages.Count == 0)
                        reachedStart = true;

                    prependMessages(messages);

                    if (reachedStart)
                        topContainer.Child = createChannelStart();
                }), TaskScheduler.Default);
            }

            private void prependMessages(IReadOnlyList<DiscordMessage> messages)
            {
                var existing = messageFlow.Select(m => m.Message.Id).ToHashSet();
                var toAdd = messages.Where(m => !existing.Contains(m.Id)).ToList();

                if (toAdd.Count == 0)
                    return;

                // Keep the distance to the end, so that the visible messages don't move when older ones are added above them.
                float distanceFromEnd = (float)(scroll.ScrollableExtent - scroll.Current);
                bool wasAtEnd = scroll.IsScrolledToEnd(10);

                var current = messageFlow.ToList();
                messageFlow.Clear(false);

                foreach (var message in toAdd.OrderBy(m => m.Timestamp))
                    messageFlow.Add(createDrawable(message));

                foreach (var drawable in current)
                    messageFlow.Add(drawable);

                updateLayout();

                if (!wasAtEnd)
                {
                    // The new content has to be measured before the scroll position can be restored.
                    ScheduleAfterChildren(() => scroll.ScrollTo(scroll.ScrollableExtent - distanceFromEnd, false));
                }
            }

            private DrawableDiscordMessage createDrawable(DiscordMessage message) => new DrawableDiscordMessage(message, markdownContext);

            private void appendMessage(DiscordMessage message)
            {
                messageFlow.Add(createDrawable(message));
                updateLayout();
            }

            /// <summary>
            /// Updates the order, the grouping and the day separators of the messages.
            /// </summary>
            private void updateLayout()
            {
                DiscordMessage? previous = null;
                int index = 0;

                foreach (var drawable in messageFlow)
                {
                    var message = drawable.Message;

                    messageFlow.SetLayoutPosition(drawable, index++);

                    bool newDay = previous != null && previous.Timestamp.ToLocalTime().Date != message.Timestamp.ToLocalTime().Date;

                    drawable.ShowDaySeparator = newDay;
                    drawable.IsContinuation = previous != null
                                              && !newDay
                                              && !message.IsReply
                                              && previous.Author.Id == message.Author.Id
                                              && message.Timestamp - previous.Timestamp < TimeSpan.FromMinutes(7);

                    previous = message;
                }
            }

            private void sendMessage()
            {
                string text = textBox.Text.Trim();

                if (string.IsNullOrEmpty(text))
                    return;

                textBox.Text = string.Empty;

                string nonce = generateNonce();

                var pending = new DiscordMessage
                {
                    Id = 0,
                    ChannelId = Channel.Id,
                    GuildId = Channel.GuildId,
                    Author = client.LocalUser.Value ?? new DiscordUser(),
                    Content = text,
                    Timestamp = DateTimeOffset.Now,
                    Nonce = nonce,
                    Pending = true,
                };

                appendMessage(pending);
                scroll.ScrollToEnd();

                client.SendMessage(Channel, text, nonce).ContinueWith(t => Schedule(() =>
                {
                    var drawable = messageFlow.FirstOrDefault(m => m.Message.Nonce == nonce && m.Message.Pending);

                    if (t.IsFaulted)
                    {
                        if (drawable != null)
                        {
                            pending.Pending = false;
                            pending.Failed = true;
                            replace(drawable, pending);
                        }

                        string error = t.Exception?.GetBaseException().Message ?? string.Empty;
                        Logger.Log($@"Failed to send a Discord message: {error}", LoggingTarget.Network);
                        return;
                    }

                    // The gateway may already have delivered the message, in which case the pending echo was replaced already.
                    if (drawable != null)
                        replaceOrRemoveDuplicate(drawable, t.GetResultSafely());
                }), TaskScheduler.Default);
            }

            private static string generateNonce()
            {
                // Discord nonces are snowflakes of the current time.
                long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1420070400000;
                return (((ulong)ms << 22) | (uint)Random.Shared.Next(0, 1 << 22)).ToString();
            }

            private void replaceOrRemoveDuplicate(DrawableDiscordMessage drawable, DiscordMessage message)
            {
                if (messageFlow.Any(m => m != drawable && m.Message.Id == message.Id && !m.Message.Pending))
                {
                    messageFlow.Remove(drawable, true);
                    updateLayout();
                }
                else
                    replace(drawable, message);
            }

            private void replace(DrawableDiscordMessage drawable, DiscordMessage message)
            {
                int position = (int)messageFlow.GetLayoutPosition(drawable);
                messageFlow.Remove(drawable, true);

                var replacement = createDrawable(message);
                messageFlow.Insert(position, replacement);
                updateLayout();
            }

            private void onMessageCreated(DiscordMessage message) => Schedule(() =>
            {
                if (message.ChannelId != Channel.Id)
                    return;

                if (messageFlow.Any(m => m.Message.Id == message.Id && !m.Message.Pending))
                    return;

                var pending = message.Nonce == null ? null : messageFlow.FirstOrDefault(m => m.Message.Pending && m.Message.Nonce == message.Nonce);

                if (pending != null)
                    replace(pending, message);
                else
                    appendMessage(message);
            });

            private void onMessageUpdated(DiscordMessage message) => Schedule(() =>
            {
                if (message.ChannelId != Channel.Id)
                    return;

                var drawable = messageFlow.FirstOrDefault(m => m.Message.Id == message.Id && !m.Message.Pending);

                if (drawable == null)
                    return;

                if (message.IsPartial)
                {
                    // Partial updates are sent when Discord resolved the embeds of the links in a message.
                    var existing = drawable.Message;
                    existing.Embeds = message.Embeds;
                    replace(drawable, existing);
                }
                else
                    replace(drawable, message);
            });

            private void onMessageDeleted(ulong channelId, ulong messageId) => Schedule(() =>
            {
                if (channelId != Channel.Id)
                    return;

                var drawable = messageFlow.FirstOrDefault(m => m.Message.Id == messageId && !m.Message.Pending);

                if (drawable == null)
                    return;

                messageFlow.Remove(drawable, true);
                updateLayout();
            });

            private Drawable createChannelStart()
            {
                bool isPrivate = Channel.IsPrivate;

                return new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Padding = new MarginPadding { Horizontal = 16, Top = 24, Bottom = 8 },
                    Children = new Drawable[]
                    {
                        new CircularContainer
                        {
                            Size = new Vector2(68),
                            Masking = true,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = colourProvider.Background3,
                                },
                                new SpriteIcon
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Icon = DiscordChannelSidebar.GetIcon(Channel),
                                    Size = new Vector2(32),
                                    Colour = colourProvider.Content1,
                                },
                            },
                        },
                        new OsuSpriteText
                        {
                            Text = isPrivate ? Channel.DisplayName : SlopDiscordStrings.ChannelStart(Channel.DisplayName),
                            Font = DiscordStyle.UiFont(26, FontWeight.Bold),
                            Colour = colourProvider.Content1,
                        },
                    },
                };
            }

            private Drawable createNote(Framework.Localisation.LocalisableString text, IconUsage icon, Color4 colour) => new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Padding = new MarginPadding { Horizontal = 16, Vertical = 12 },
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Icon = icon,
                        Size = new Vector2(14),
                        Colour = colour,
                        Margin = new MarginPadding { Top = 2 },
                    },
                    new OsuTextFlowContainer(s =>
                    {
                        s.Font = DiscordStyle.UiFont(14);
                        s.Colour = colour;
                    })
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Width = 0.9f,
                        Text = text,
                    },
                },
            };

            public void TakeTextBoxFocus()
            {
                if (textBox.IsLoaded)
                    textBox.TakeFocus();
            }

            public void KillTextBoxFocus()
            {
                if (textBox.IsLoaded)
                    textBox.KillFocus();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (client.IsNotNull())
                {
                    client.MessageCreated -= onMessageCreated;
                    client.MessageUpdated -= onMessageUpdated;
                    client.MessageDeleted -= onMessageDeleted;
                }
            }
        }

        private partial class DiscordMessageTextBox : HistoryTextBox
        {
            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                CornerRadius = 8;
                BackgroundUnfocused = colourProvider.Background4.Opacity(0.8f);
                BackgroundFocused = colourProvider.Background4.Opacity(0.9f);
                BackgroundCommit = colourProvider.Background3;
            }

            protected override SpriteText CreatePlaceholder() => base.CreatePlaceholder().With(p => p.Font = DiscordStyle.UiFont(15));
        }
    }
}
