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

        /// <summary>
        /// The channel which is shown, either as a <see cref="ChannelChat"/> or as a <see cref="DiscordForumView"/>.
        /// </summary>
        private ulong? shownChannelId;

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
                if (c.NewValue != null && c.NewValue.Id == shownChannelId)
                    return;

                content.Clear();
                currentChat = null;
                shownChannelId = c.NewValue?.Id;

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

                if (c.NewValue.IsForum)
                    content.Add(new DiscordForumView(c.NewValue));
                else
                    content.Add(currentChat = new ChannelChat(c.NewValue));
            }, true);
        }

        public void TakeTextBoxFocus() => currentChat?.TakeTextBoxFocus();

        public void KillTextBoxFocus() => currentChat?.KillTextBoxFocus();

        /// <summary>
        /// The header, messages and text box of a single channel.
        /// </summary>
        [Cached(typeof(IDiscordMessageActions))]
        private partial class ChannelChat : CompositeDrawable, IDiscordMessageActions
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

            [Resolved]
            private DiscordEmojiUsage? emojiUsage { get; set; }

            private DiscordChatScrollContainer scroll = null!;
            private FillFlowContainer<DrawableDiscordMessage> messageFlow = null!;
            private Container topContainer = null!;
            private LoadingSpinner topSpinner = null!;
            private DiscordMessageComposer composer = null!;

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

                // The other users of a private channel are already listed with their direct messages.
                if (Channel.GuildId != null)
                    markdownContext.OpenDirectMessage = openDirectMessage;

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
                        new Drawable[] { new DiscordChannelHeader(Channel) },
                        new Drawable[]
                        {
                            scroll = new DiscordChatScrollContainer
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
                                Child = composer = new DiscordMessageComposer(Channel, client.SupportsMessageActions, GetCustomEmojis, getMentionCandidates,
                                    Channel.GuildId is ulong guildId ? query => client.SearchMembers(guildId, query) : null)
                                {
                                    GifClient = client,
                                    GetStickers = () => DiscordStickerPicker.GetStickerGroups(client, Channel.GuildId),
                                    // Like in Discord, a picked GIF or sticker is sent right away, while the text which was written stays.
                                    GifPicked = gif => send(gif.Url, Array.Empty<DiscordUpload>(), takeReply(), null),
                                    StickerPicked = sticker => send(string.Empty, Array.Empty<DiscordUpload>(), takeReply(), sticker),
                                },
                            },
                        },
                    },
                };

                composer.TextBox.OnCommit += (_, _) => sendMessage();
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                client.MessageCreated += onMessageCreated;
                client.MessageUpdated += onMessageUpdated;
                client.MessageDeleted += onMessageDeleted;
                client.ReactionChanged += onReactionChanged;

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

                ulong? before = orderedMessages.FirstOrDefault(m => !m.Message.Pending)?.Message.Id;

                client.GetMessages(Channel, before).ContinueWith(t => Schedule(() =>
                {
                    loadingOlder = false;

                    // The spinner is hidden right away, as the content would otherwise shrink once it faded out, which makes the scroll position bounce.
                    topSpinner.Hide();
                    topSpinner.FinishTransforms(true);

                    if (t.IsFaulted)
                    {
                        reachedStart = true;
                        string error = t.Exception?.GetBaseException().Message ?? string.Empty;
                        topContainer.Child = createNote(SlopDiscordStrings.LoadFailed(error), FontAwesome.Solid.ExclamationTriangle, DiscordStyle.Error);
                        return;
                    }

                    var messages = t.GetResultSafely();

                    // Without history, only the most recent messages can be fetched, so the first page is the only one.
                    if (messages.Count == 0 || !client.SupportsHistory)
                        reachedStart = true;

                    bool initial = messageFlow.Count == 0;

                    prependMessages(messages);

                    if (initial)
                    {
                        // An opened channel starts at its latest messages and fades them in, instead of scrolling down from the top.
                        scroll.ScrollToEnd(false);
                        messageFlow.FadeInFromZero(300, Easing.OutQuint);
                    }

                    if (reachedStart)
                    {
                        topContainer.Child = client.SupportsHistory
                            ? createChannelStart()
                            : createNote(SlopDiscordStrings.HistoryUnavailable, FontAwesome.Solid.InfoCircle, colourProvider.Content2);
                    }
                }), TaskScheduler.Default);
            }

            private void prependMessages(IReadOnlyList<DiscordMessage> messages)
            {
                var existing = messageFlow.Select(m => m.Message.Id).ToHashSet();
                var toAdd = messages.Where(m => !existing.Contains(m.Id)).ToList();

                if (toAdd.Count == 0)
                    return;

                // The visible messages don't move when older ones are added above them.
                scroll.KeepDistanceFromEnd();

                var current = orderedMessages.ToList();
                messageFlow.Clear(false);

                foreach (var message in toAdd.OrderBy(m => m.Timestamp))
                    messageFlow.Add(createDrawable(message));

                foreach (var drawable in current)
                    messageFlow.Add(drawable);

                updateLayout();
            }

            private DrawableDiscordMessage createDrawable(DiscordMessage message) => new DrawableDiscordMessage(getShownMessage(message), markdownContext);

            /// <summary>
            /// The first message of a thread refers to the message it was started from, which is shown in its place like in Discord.
            /// </summary>
            private static DiscordMessage getShownMessage(DiscordMessage message)
            {
                if (!message.IsThreadStarter || message.ReferencedMessage is not DiscordMessage starter)
                    return message;

                starter.Id = message.Id;
                starter.ChannelId = message.ChannelId;
                starter.GuildId ??= message.GuildId;

                // The thread of the message is the one which is open already.
                starter.Thread = null;

                return starter;
            }

            /// <summary>
            /// The messages in the order they are shown, which differs from the order of the children once a message was replaced.
            /// </summary>
            private IEnumerable<DrawableDiscordMessage> orderedMessages => messageFlow.OrderBy(m => messageFlow.GetLayoutPosition(m));

            private void appendMessage(DiscordMessage message)
            {
                messageFlow.Insert(messageFlow.Count, createDrawable(message));
                updateLayout();
            }

            /// <summary>
            /// Updates the order, the grouping and the day separators of the messages.
            /// </summary>
            private void updateLayout()
            {
                DiscordMessage? previous = null;
                int index = 0;

                foreach (var drawable in orderedMessages.ToList())
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
                string text = composer.GetMessageText();
                var uploads = composer.Uploads.ToList();
                var replyTo = composer.ReplyTo;

                if (composer.Editing is DiscordMessage edited)
                {
                    composer.Reset();

                    if (!string.IsNullOrEmpty(text) && text != edited.Content)
                        client.EditMessage(edited, text).ContinueWith(t => logFailure(t, @"edit"), TaskContinuationOptions.OnlyOnFaulted);

                    return;
                }

                if (string.IsNullOrEmpty(text) && uploads.Count == 0)
                    return;

                composer.Reset();

                send(text, uploads, replyTo, null);
            }

            /// <summary>
            /// Takes the message which is replied to out of the composer, for sending something without its text.
            /// </summary>
            private DiscordMessage? takeReply()
            {
                var replyTo = composer.Editing == null ? composer.ReplyTo : null;

                if (replyTo != null)
                    composer.ClearContext();

                return replyTo;
            }

            private void send(string text, IReadOnlyList<DiscordUpload> uploads, DiscordMessage? replyTo, DiscordSticker? sticker)
            {
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
                    Stickers = sticker != null ? new List<DiscordSticker> { sticker } : new List<DiscordSticker>(),
                };

                appendMessage(pending);
                scroll.ScrollToEnd();

                var sending = sticker != null
                    ? client.SendSticker(Channel, sticker, nonce, replyTo)
                    : client.SendMessage(Channel, text, nonce, replyTo, uploads.Count > 0 ? uploads : null);

                sending.ContinueWith(t => Schedule(() =>
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

            private static void logFailure(Task task, string action)
            {
                string error = task.Exception?.GetBaseException().Message ?? string.Empty;
                Logger.Log($@"Failed to {action} a Discord message: {error}", LoggingTarget.Network);
            }

            public void Reply(DiscordMessage message) => composer.SetReply(message);

            public void StartEdit(DiscordMessage message) => composer.SetEdit(message);

            public void Delete(DiscordMessage message) =>
                client.DeleteMessage(message).ContinueWith(t => logFailure(t, @"delete"), TaskContinuationOptions.OnlyOnFaulted);

            public IReadOnlyList<DiscordCustomEmojiGroup> GetCustomEmojis() => DiscordEmojiPicker.GetCustomGroups(client, Channel.GuildId);

            public void ToggleReaction(DiscordMessage message, DiscordReactionEmoji emoji)
            {
                ulong localUserId = client.LocalUser.Value?.Id ?? 0;
                bool remove = message.Reactions.Any(r => r.Emoji.Equals(emoji) && r.Me);

                var update = new DiscordReactionUpdate(message.ChannelId, message.Id, emoji, localUserId, remove ? DiscordReactionUpdateType.Remove : DiscordReactionUpdateType.Add);

                // The reaction is shown right away, the echo of the gateway is ignored as it was applied already.
                applyReaction(update);

                if (!remove)
                    emojiUsage?.Record(emoji);

                (remove ? client.RemoveReaction(message, emoji) : client.AddReaction(message, emoji)).ContinueWith(t => Schedule(() =>
                {
                    logFailure(t, @"react to");
                    applyReaction(update with { Type = remove ? DiscordReactionUpdateType.Add : DiscordReactionUpdateType.Remove });
                }), TaskContinuationOptions.OnlyOnFaulted);
            }

            private void onReactionChanged(DiscordReactionUpdate update) => Schedule(() =>
            {
                if (update.ChannelId == Channel.Id)
                    applyReaction(update);
            });

            private void applyReaction(DiscordReactionUpdate update)
            {
                var drawable = messageFlow.FirstOrDefault(m => m.Message.Id == update.MessageId && !m.Message.Pending);

                if (drawable == null)
                    return;

                drawable.Message.ApplyReaction(update, client.LocalUser.Value?.Id ?? 0);
                drawable.UpdateReactions();
            }

            /// <summary>
            /// The users which can be mentioned, which are the ones known from the channel and its messages, as the members of a guild aren't known.
            /// </summary>
            private IEnumerable<DiscordUser> getMentionCandidates()
            {
                ulong localUserId = client.LocalUser.Value?.Id ?? 0;

                return Channel.Recipients
                              .Concat(orderedMessages.Reverse().SelectMany(m => m.Message.Mentions.Prepend(m.Message.Author)))
                              .Where(u => u.Id != localUserId && u.Id != 0);
            }

            private void openDirectMessage(DiscordUser user)
            {
                if (user.Id == client.LocalUser.Value?.Id)
                    return;

                client.OpenDirectMessage(user).ContinueWith(t => Schedule(() =>
                {
                    if (t.IsCompletedSuccessfully && t.GetResultSafely() is DiscordChannel channel)
                        navigation.OpenChannel(channel, client);
                    else
                        Logger.Log($@"Failed to open the direct messages with {user.Username}: {t.Exception?.GetBaseException().Message}", LoggingTarget.Network);
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
                    // Partial updates are sent when the embeds of the links in a message were resolved.
                    var existing = drawable.Message;
                    existing.Embeds = message.Embeds;
                    existing.PreviewLinks = message.PreviewLinks;
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
                if (composer.TextBox.IsLoaded)
                    composer.TextBox.TakeFocus();
            }

            public void KillTextBoxFocus()
            {
                if (composer.TextBox.IsLoaded)
                    composer.TextBox.KillFocus();
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);

                if (client.IsNotNull())
                {
                    client.MessageCreated -= onMessageCreated;
                    client.MessageUpdated -= onMessageUpdated;
                    client.MessageDeleted -= onMessageDeleted;
                    client.ReactionChanged -= onReactionChanged;
                }
            }
        }
    }
}
