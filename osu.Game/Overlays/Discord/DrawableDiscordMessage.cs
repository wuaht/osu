// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
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
    /// A message of the chat, with its attachments, embeds and stickers.
    /// </summary>
    public partial class DrawableDiscordMessage : CompositeDrawable, IHasContextMenu
    {
        public readonly DiscordMessage Message;

        private readonly DiscordMarkdownContext context;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private DiscordClient client { get; set; } = null!;

        [Resolved]
        private IDiscordMessageActions? actions { get; set; }

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        private Container daySeparator = null!;
        private Container header = null!;
        private Container avatarContainer = null!;
        private OsuSpriteText gutterTime = null!;
        private Box hoverBackground = null!;
        private Container mainContent = null!;
        private Container messageRow = null!;
        private Container highlightContainer = null!;
        private FillFlowContainer reactionFlow = null!;
        private DiscordMessageToolbar? toolbar;

        /// <summary>
        /// Whether the message is hovered, or its toolbar is in use.
        /// </summary>
        private bool active;

        private bool showDaySeparator;
        private bool isContinuation;

        public bool ShowDaySeparator
        {
            get => showDaySeparator;
            set
            {
                if (showDaySeparator == value)
                    return;

                showDaySeparator = value;

                if (IsLoaded)
                    updateLayout();
            }
        }

        public bool IsContinuation
        {
            get => isContinuation;
            set
            {
                if (isContinuation == value)
                    return;

                isContinuation = value;

                if (IsLoaded)
                    updateLayout();
            }
        }

        private bool mentionsLocalUser =>
            Message.MentionEveryone
            || Message.Mentions.Any(u => u.Id == context.LocalUserId)
            || (Message.IsReply && Message.ReferencedMessage?.Author.Id == context.LocalUserId && Message.Author.Id != context.LocalUserId);

        public DrawableDiscordMessage(DiscordMessage message, DiscordMarkdownContext context)
        {
            Message = message;
            this.context = context;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var body = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
            };

            if (Message.IsThreadCreated)
            {
                body.Add(new OsuSpriteText
                {
                    Text = SlopDiscordStrings.StartedThread,
                    Font = DiscordStyle.UiFont(15),
                    Colour = colourProvider.Content2,
                });
            }
            else if (shouldShowContent())
            {
                body.Add(new DrawableDiscordMarkdown(Message.Content, context, Message.EditedTimestamp != null)
                {
                    TextColour = Message.Failed ? DiscordStyle.Error : colourProvider.Content1,
                });
            }

            foreach (var attachment in Message.Attachments)
                body.Add(createAttachment(attachment));

            if (Message.UnsupportedContent != null)
                body.Add(new UnsupportedContentChip(Message.UnsupportedContent));

            foreach (var embed in Message.Embeds)
            {
                var drawable = createEmbed(embed);

                if (drawable != null)
                    body.Add(drawable);
            }

            foreach (string link in Message.PreviewLinks)
                body.Add(new LinkPreviewButton(link, () => client.LoadLinkPreview(Message, link)));

            foreach (var sticker in Message.Stickers)
            {
                string? url = sticker.GetUrl();

                if (url == null)
                    continue;

                body.Add(new DiscordRemoteImage(url, sticker.FormatType == 4)
                {
                    Size = new Vector2(160),
                    ImageFillMode = FillMode.Fit,
                    ShowPlaceholder = false,
                });
            }

            if (getThread() is DiscordChannel thread)
                body.Add(new ThreadChip(thread, context.OpenChannel));

            body.Add(reactionFlow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(4),
            });

            if (Message.Failed)
            {
                body.Add(new OsuSpriteText
                {
                    Text = SlopDiscordStrings.SendFailed,
                    Font = DiscordStyle.UiFont(12),
                    Colour = DiscordStyle.Error,
                });
            }

            bool mentioned = mentionsLocalUser;

            var rows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            };

            rows.Add(daySeparator = createDaySeparator());

            var messageRows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
            };

            // Like in Discord, the mention highlight covers the reply preview too, but not the space between message groups above it.
            rows.Add(highlightContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = DiscordStyle.MentionedMessage,
                        Alpha = mentioned ? 1 : 0,
                    },
                    messageRows,
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 2,
                        Colour = DiscordStyle.MentionedMessageBar,
                        Alpha = mentioned ? 1 : 0,
                    },
                },
            });

            // The preview is added before the message, as the flow lays out children in the order they were added.
            if (Message.IsReply)
                messageRows.Add(createReplyPreview());

            messageRows.Add(messageRow = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    mainContent = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Alpha = Message.Pending ? 0.5f : 1,
                        Padding = new MarginPadding { Left = DiscordStyle.MESSAGE_GUTTER, Right = 16, Vertical = 2 },
                        Children = new Drawable[]
                        {
                            avatarContainer = new Container
                            {
                                Size = new Vector2(DiscordStyle.AVATAR_SIZE),
                                X = -DiscordStyle.MESSAGE_GUTTER + 16,
                                Child = new AuthorAvatar(Message.Author, context.OpenDirectMessage),
                            },
                            gutterTime = new OsuSpriteText
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopCentre,
                                X = -DiscordStyle.MESSAGE_GUTTER / 2,
                                Y = 4,
                                Text = Message.Timestamp.ToLocalTime().ToString(@"HH:mm"),
                                Font = DiscordStyle.UiFont(11),
                                Colour = colourProvider.Content2,
                                Alpha = 0,
                            },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Children = new Drawable[]
                                {
                                    header = createHeader(),
                                    body,
                                },
                            },
                        },
                    },
                },
            });

            InternalChild = rows;

            UpdateReactions();
        }

        private bool isOwn => Message.Author.Id == context.LocalUserId;

        /// <summary>
        /// Whether the message can be replied to, reacted to and so on, which isn't the case while it is sent.
        /// </summary>
        private bool canAct => actions != null && client.SupportsMessageActions && !Message.Pending && !Message.Failed && Message.Id != 0;

        /// <summary>
        /// Shows the current reactions of the message, after they changed.
        /// </summary>
        public void UpdateReactions()
        {
            reactionFlow.Clear();

            var messageActions = canAct ? actions : null;

            foreach (var reaction in Message.Reactions)
            {
                var emoji = reaction.Emoji;
                Action? toggle = messageActions == null ? null : () => messageActions.ToggleReaction(Message, emoji);

                if (reaction.BurstCount > 0)
                    reactionFlow.Add(new DiscordReactionChip(reaction, true, toggle, () => client.GetReactionUsers(Message, emoji, true)));

                if (reaction.Count > 0)
                    reactionFlow.Add(new DiscordReactionChip(reaction, false, toggle, () => client.GetReactionUsers(Message, emoji, false)));
            }

            if (messageActions != null && reactionFlow.Count > 0)
            {
                reactionFlow.Add(new DiscordPopoverButton(FontAwesome.Regular.Smile, SlopDiscordStrings.AddReaction,
                    () => new DiscordEmojiPicker(messageActions.GetCustomEmojis(), e => messageActions.ToggleReaction(Message, e)))
                {
                    Size = new Vector2(32, 26),
                });
            }

            reactionFlow.Alpha = reactionFlow.Count > 0 ? 1 : 0;
        }

        public MenuItem[]? ContextMenuItems => canAct ? CreateMenuItems(Message, actions!, isOwn, clipboard).ToArray() : null;

        /// <summary>
        /// The actions of the context menu of a message, which are also shown by the "more" button of its toolbar.
        /// </summary>
        public static IEnumerable<MenuItem> CreateMenuItems(DiscordMessage message, IDiscordMessageActions actions, bool own, Clipboard? clipboard)
        {
            yield return new OsuMenuItem(SlopDiscordStrings.ReplyToMessage, MenuItemType.Standard, () => actions.Reply(message));

            if (own)
                yield return new OsuMenuItem(SlopDiscordStrings.EditMessage, MenuItemType.Standard, () => actions.StartEdit(message));

            if (clipboard != null)
            {
                if (!string.IsNullOrEmpty(message.Content))
                    yield return new OsuMenuItem(SlopDiscordStrings.CopyText, MenuItemType.Standard, () => clipboard.SetText(message.Content));

                yield return new OsuMenuItem(SlopDiscordStrings.CopyMessageId, MenuItemType.Standard, () => clipboard.SetText(message.Id.ToString(CultureInfo.InvariantCulture)));
            }

            if (own)
                yield return new OsuMenuItem(SlopDiscordStrings.DeleteMessage, MenuItemType.Destructive, () => actions.Delete(message));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateLayout();
        }

        private void updateLayout()
        {
            daySeparator.Alpha = showDaySeparator ? 1 : 0;

            header.Alpha = isContinuation ? 0 : 1;
            avatarContainer.Alpha = isContinuation ? 0 : 1;
            mainContent.Padding = new MarginPadding { Left = DiscordStyle.MESSAGE_GUTTER, Right = 16, Vertical = 2 };

            // The space between message groups is outside of the highlight and hover background, like in Discord.
            // The reply preview belongs to the first message of a group.
            highlightContainer.Margin = new MarginPadding { Top = isContinuation && !Message.IsReply ? 0 : 12 };
        }

        protected override void Update()
        {
            base.Update();

            // The message is no longer hovered while its toolbar is, as the toolbar handles hover itself.
            bool nowActive = IsHovered || toolbar?.InUse == true;

            if (nowActive == active)
                return;

            active = nowActive;

            if (active)
            {
                hoverBackground.FadeTo(0.03f, 50);

                if (isContinuation)
                    gutterTime.FadeIn(50);

                showToolbar();
            }
            else
            {
                hoverBackground.FadeOut(100);
                gutterTime.FadeOut(100);
                toolbar?.FadeOut(100);
            }
        }

        private void showToolbar()
        {
            if (!canAct)
                return;

            // The toolbar is only created once it's needed, as most messages are never hovered.
            if (toolbar == null)
            {
                messageRow.Add(toolbar = new DiscordMessageToolbar(Message, actions!, isOwn)
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    X = -16,
                    Y = -16,
                    Alpha = 0,
                });
            }
            else
                toolbar.UpdateQuickReactions();

            toolbar.FadeIn(50);
        }

        /// <summary>
        /// The content is hidden if it is only the link of a media embed, like Discord does with GIFs.
        /// </summary>
        /// <summary>
        /// The thread which was started from this message, or which this message announces.
        /// </summary>
        private DiscordChannel? getThread()
        {
            if (Message.Thread is DiscordChannel thread)
            {
                // The client may know a more recent version of the thread, with its current message count.
                var known = client.GetChannel(thread.Id);

                if (known != null)
                    return known;

                thread.GuildId ??= Message.GuildId;
                thread.ParentId ??= Message.ChannelId;
                return thread;
            }

            if (Message.IsThreadCreated && Message.MessageReference?.ChannelId is ulong threadId)
            {
                return client.GetChannel(threadId) ?? new DiscordChannel
                {
                    Id = threadId,
                    Name = Message.Content,
                    Type = DiscordChannelType.PublicThread,
                    GuildId = Message.GuildId,
                    ParentId = Message.ChannelId,
                };
            }

            return null;
        }

        private bool shouldShowContent()
        {
            if (string.IsNullOrWhiteSpace(Message.Content))
                return false;

            string trimmed = Message.Content.Trim();

            return !Message.Embeds.Any(e => e.IsMediaOnly && e.Url == trimmed);
        }

        private Container createHeader()
        {
            Color4 nameColour = colourProvider.Content1;

            if (Message.GuildId is ulong guildId && client.GetGuild(guildId) is DiscordGuild guild && Message.Author.Id == context.LocalUserId && guild.LocalUserRoles != null)
            {
                var role = guild.Roles.Where(r => r.Colour != 0 && guild.LocalUserRoles.Contains(r.Id)).MaxBy(r => r.Position);

                if (role != null)
                    nameColour = DiscordStyle.FromInteger(role.Colour) ?? nameColour;
            }

            var flow = new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Text = Message.Author.DisplayName,
                        Font = DiscordStyle.UiFont(16, FontWeight.SemiBold),
                        Colour = nameColour,
                    },
                },
            };

            if (Message.Author.Bot)
            {
                flow.Add(new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 3,
                    Margin = new MarginPadding { Bottom = 2 },
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = DiscordStyle.Blurple,
                        },
                        new OsuSpriteText
                        {
                            Text = @"BOT",
                            Font = DiscordStyle.UiFont(10, FontWeight.Bold),
                            Padding = new MarginPadding { Horizontal = 4, Vertical = 1 },
                        },
                    },
                });
            }

            flow.Add(new OsuSpriteText
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Text = FormatTimestamp(Message.Timestamp),
                Font = DiscordStyle.UiFont(12),
                Colour = colourProvider.Content2,
                Margin = new MarginPadding { Bottom = 1 },
            });

            // The header is as high as a line of text, so that it and the first line are as high as the avatar.
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = DiscordStyle.LineHeight(),
                Child = flow,
            };
        }

        public static LocalisableString FormatTimestamp(DateTimeOffset timestamp)
        {
            var local = timestamp.ToLocalTime();
            string time = local.ToString(@"HH:mm");

            if (local.Date == DateTime.Today)
                return SlopDiscordStrings.TodayAt(time);

            if (local.Date == DateTime.Today.AddDays(-1))
                return SlopDiscordStrings.YesterdayAt(time);

            return local.ToString(@"dd.MM.yyyy HH:mm");
        }

        private Container createDaySeparator()
        {
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 24,
                Margin = new MarginPadding { Top = 12 },
                Padding = new MarginPadding { Horizontal = 16 },
                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Colour = colourProvider.Background2,
                    },
                    new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 4,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background5,
                            },
                            new OsuSpriteText
                            {
                                Text = Message.Timestamp.ToLocalTime().ToString(@"d. MMMM yyyy"),
                                Font = DiscordStyle.UiFont(12, FontWeight.SemiBold),
                                Colour = colourProvider.Content2,
                                Padding = new MarginPadding { Horizontal = 8, Vertical = 2 },
                            },
                        },
                    },
                },
            };
        }

        private Drawable createReplyPreview()
        {
            var referenced = Message.ReferencedMessage;

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(4, 0),
            };

            if (referenced != null)
                fillReplyPreview(flow, referenced);
            else if (Message.ReferencedMessageProvided || Message.MessageReference?.MessageId is not ulong referencedId)
                flow.Add(createReplyNote(SlopDiscordStrings.ReplyOriginalDeleted));
            else
            {
                // Some messages (like those of bots) leave out the message they reply to, which doesn't mean that it was deleted.
                flow.Add(createReplyNote(SlopDiscordStrings.ReplyOriginalLoading));

                client.GetMessage(Message.MessageReference.ChannelId ?? Message.ChannelId, referencedId).ContinueWith(t => Schedule(() =>
                {
                    var fetched = t.IsCompletedSuccessfully ? t.GetResultSafely() : null;

                    flow.Clear();

                    if (fetched != null)
                    {
                        Message.ReferencedMessage = fetched;
                        fillReplyPreview(flow, fetched);
                    }
                    else
                        flow.Add(createReplyNote(t.IsCompletedSuccessfully ? SlopDiscordStrings.ReplyOriginalDeleted : SlopDiscordStrings.ReplyOriginalUnavailable));
                }), TaskScheduler.Default);
            }

            const float height = 18;
            const float spine_thickness = 2;

            // The spine starts in the middle of the avatar and ends just before the preview.
            const float spine_x = -DiscordStyle.MESSAGE_GUTTER + 16 + DiscordStyle.AVATAR_SIZE / 2 - spine_thickness / 2;

            return new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = height,
                Margin = new MarginPadding { Top = 2 },
                Padding = new MarginPadding { Left = DiscordStyle.MESSAGE_GUTTER, Right = 16 },
                Children = new Drawable[]
                {
                    // The spine which connects the preview with the avatar of the reply.
                    // Only its top and left border are visible, with a rounded corner between them.
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.TopLeft,
                        X = spine_x,
                        Y = -spine_thickness / 2,
                        // Reaches down to the avatar, which is below the preview and the top padding of the message.
                        Size = new Vector2(-spine_x - 4, height / 2 + spine_thickness / 2 + 4),
                        Masking = true,
                        Child = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Size = new Vector2(2),
                            Masking = true,
                            CornerRadius = 6,
                            BorderThickness = spine_thickness,
                            BorderColour = colourProvider.Background2,
                            Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Alpha = 0,
                                AlwaysPresent = true,
                            },
                        },
                    },
                    flow,
                },
            };
        }

        private void fillReplyPreview(FillFlowContainer flow, DiscordMessage referenced)
        {
            string preview = referenced.Content.ReplaceLineEndings(@" ");

            if (string.IsNullOrWhiteSpace(preview))
                preview = referenced.Attachments.Count > 0 ? referenced.Attachments[0].Filename : @"…";

            flow.AddRange(new Drawable[]
            {
                new DiscordAvatar(referenced.Author.GetAvatarUrl(32), 16)
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = referenced.Author.DisplayName,
                    Font = DiscordStyle.UiFont(13, FontWeight.SemiBold),
                    Colour = colourProvider.Content1,
                },
                new TruncatingSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    MaxWidth = 500,
                    Text = preview,
                    Font = DiscordStyle.UiFont(13),
                    Colour = colourProvider.Content2,
                },
            });
        }

        private Drawable createReplyNote(LocalisableString text) => new OsuSpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = text,
            Font = DiscordStyle.UiFont(13, FontWeight.Regular).With(italics: true),
            Colour = colourProvider.Content2,
        };

        private Drawable createAttachment(DiscordAttachment attachment)
        {
            if (attachment.IsImage)
            {
                var image = new DiscordMediaImage(attachment.Url, attachment.ProxyUrl, DiscordMedia.FitSize(attachment.Width, attachment.Height), attachment.IsAnimatedImage,
                    attachment.Width, attachment.Height);

                if (!attachment.IsAnimatedImage)
                    return image;

                return new DiscordChatGif(new DiscordGif { Url = attachment.Url, Src = attachment.Url, Width = attachment.Width ?? 0, Height = attachment.Height ?? 0 }, image);
            }

            if (attachment.IsVideo)
            {
                return new DiscordMediaVideo(attachment.Url, attachment.ProxyUrl, DiscordMedia.FitSize(attachment.Width, attachment.Height), false);
            }

            if (attachment.IsAudio)
                return new DiscordMediaAudio(attachment);

            return new FileChip(attachment);
        }

        private Drawable? createEmbed(DiscordEmbed embed)
        {
            switch (embed.Type)
            {
                case @"gifv":
                    if (embed.Video?.Url == null)
                        return null;

                    var gifv = new DiscordMediaVideo(embed.Video.ProxyUrl ?? embed.Video.Url, embed.Thumbnail?.ProxyUrl ?? embed.Thumbnail?.Url,
                        DiscordMedia.FitSize(embed.Video.Width, embed.Video.Height), true);

                    return new DiscordChatGif(new DiscordGif
                    {
                        Url = embed.Url ?? embed.Video.Url,
                        Src = embed.Video.Url,
                        Width = embed.Video.Width ?? 0,
                        Height = embed.Video.Height ?? 0,
                        IsVideo = true,
                    }, gifv);

                case @"image":
                    var image = embed.Thumbnail ?? embed.Image;

                    if (image?.Url == null)
                        return null;

                    return new DiscordMediaImage(embed.Url ?? image.Url, image.ProxyUrl ?? image.Url, DiscordMedia.FitSize(image.Width, image.Height),
                        image.Url.EndsWith(@".gif", StringComparison.OrdinalIgnoreCase), image.Width, image.Height);

                case @"video" when string.IsNullOrEmpty(embed.Title) && embed.Video?.Url != null && embed.Provider == null:
                    return new DiscordMediaVideo(embed.Video.ProxyUrl ?? embed.Video.Url, embed.Thumbnail?.ProxyUrl ?? embed.Thumbnail?.Url,
                        DiscordMedia.FitSize(embed.Video.Width, embed.Video.Height), false);

                default:
                    return new RichEmbed(embed, context);
            }
        }

        private partial class AuthorAvatar : CompositeDrawable, IHasContextMenu
        {
            private readonly DiscordUser author;
            private readonly Action<DiscordUser>? openDirectMessage;

            public AuthorAvatar(DiscordUser author, Action<DiscordUser>? openDirectMessage)
            {
                this.author = author;
                this.openDirectMessage = openDirectMessage;

                RelativeSizeAxes = Axes.Both;
                InternalChild = new DiscordAvatar(author.GetAvatarUrl(80));
            }

            public MenuItem[]? ContextMenuItems => openDirectMessage == null
                ? null
                : new MenuItem[] { new OsuMenuItem(SlopDiscordStrings.OpenInDirectMessages, MenuItemType.Standard, () => openDirectMessage(author)) };
        }

        private partial class FileChip : OsuClickableContainer
        {
            private readonly DiscordAttachment attachment;

            [Resolved]
            private GameHost host { get; set; } = null!;

            public FileChip(DiscordAttachment attachment)
            {
                this.attachment = attachment;

                Size = new Vector2(400, 56);
                Masking = true;
                CornerRadius = 6;
                TooltipText = SlopDiscordStrings.OpenInBrowser;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Action = () => host.OpenUrlExternally(attachment.Url);

                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                        Alpha = 0.8f,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 14,
                        Icon = FontAwesome.Solid.File,
                        Size = new Vector2(24),
                        Colour = colourProvider.Content2,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 50, Right = 12 },
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = attachment.Filename,
                                Font = DiscordStyle.UiFont(15, FontWeight.SemiBold),
                                Colour = DiscordStyle.Link,
                            },
                            new OsuSpriteText
                            {
                                Text = DiscordMedia.FormatSize(attachment.Size),
                                Font = DiscordStyle.UiFont(12),
                                Colour = colourProvider.Content2,
                            },
                        },
                    },
                };
            }
        }

        /// <summary>
        /// Loads the preview of a link when clicked.
        /// Previews aren't loaded automatically, as that would reveal the IP address of the user to any site which is linked.
        /// </summary>
        /// <summary>
        /// Links to the thread of a message, like in Discord.
        /// </summary>
        private partial class ThreadChip : OsuClickableContainer
        {
            private readonly DiscordChannel thread;

            private Box hoverBackground = null!;

            public ThreadChip(DiscordChannel thread, Action<DiscordChannel>? openChannel)
            {
                this.thread = thread;

                Size = new Vector2(400, 56);
                Masking = true;
                CornerRadius = 6;

                if (openChannel != null)
                    Action = () => openChannel(thread);
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                        Alpha = 0.8f,
                    },
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 14,
                        Icon = FontAwesome.Solid.CommentDots,
                        Size = new Vector2(22),
                        Colour = colourProvider.Content2,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 50, Right = 12 },
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = thread.DisplayName,
                                Font = DiscordStyle.UiFont(15, FontWeight.SemiBold),
                                Colour = colourProvider.Content1,
                            },
                            new OsuSpriteText
                            {
                                Text = thread.MessageCount is int count ? SlopDiscordStrings.ThreadMessages(count) : SlopDiscordStrings.OpenThread,
                                Font = DiscordStyle.UiFont(12),
                                Colour = colourProvider.Content2,
                            },
                        },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                hoverBackground.FadeTo(0.04f, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBackground.FadeTo(0, 100);
                base.OnHoverLost(e);
            }
        }

        private partial class LinkPreviewButton : OsuClickableContainer
        {
            private readonly string url;
            private readonly Action loadPreview;

            private Box background = null!;
            private OsuSpriteText title = null!;

            public LinkPreviewButton(string url, Action loadPreview)
            {
                this.url = url;
                this.loadPreview = loadPreview;

                Size = new Vector2(400, 56);
                Masking = true;
                CornerRadius = 6;
                TooltipText = SlopDiscordStrings.LinkPreviewHint;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Action = () =>
                {
                    Enabled.Value = false;
                    title.Text = SlopDiscordStrings.LoadingLinkPreview;
                    loadPreview();
                };

                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                        Alpha = 0.8f,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 14,
                        Icon = FontAwesome.Solid.Link,
                        Size = new Vector2(24),
                        Colour = colourProvider.Content2,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 50, Right = 12 },
                        Children = new Drawable[]
                        {
                            title = new OsuSpriteText
                            {
                                Text = SlopDiscordStrings.LoadLinkPreview,
                                Font = DiscordStyle.UiFont(15, FontWeight.SemiBold),
                                Colour = DiscordStyle.Link,
                            },
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = url,
                                Font = DiscordStyle.UiFont(12),
                                Colour = colourProvider.Content2,
                            },
                        },
                    },
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                background.FadeTo(1, 100);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                background.FadeTo(0.8f, 100);
                base.OnHoverLost(e);
            }
        }

        /// <summary>
        /// Shows that a message contains something which can only be viewed in Discord, because the Social SDK only describes it.
        /// </summary>
        private partial class UnsupportedContentChip : CompositeDrawable
        {
            private readonly DiscordUnsupportedContent content;

            public UnsupportedContentChip(DiscordUnsupportedContent content)
            {
                this.content = content;

                Size = new Vector2(400, 56);
                Masking = true;
                CornerRadius = 6;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5,
                        Alpha = 0.8f,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 14,
                        Icon = getIcon(),
                        Size = new Vector2(24),
                        Colour = colourProvider.Content2,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 50, Right = 12 },
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = getTitle(),
                                Font = DiscordStyle.UiFont(15, FontWeight.SemiBold),
                                Colour = colourProvider.Content1,
                            },
                            new OsuSpriteText
                            {
                                Text = SlopDiscordStrings.UnsupportedContentHint,
                                Font = DiscordStyle.UiFont(12),
                                Colour = colourProvider.Content2,
                            },
                        },
                    },
                };
            }

            private LocalisableString getTitle()
            {
                if (!string.IsNullOrWhiteSpace(content.Title))
                    return content.Title;

                switch (content.Type)
                {
                    case DiscordUnsupportedContentType.Attachment:
                        return content.Count > 1 ? SlopDiscordStrings.UnsupportedAttachments(content.Count) : SlopDiscordStrings.UnsupportedAttachment;

                    case DiscordUnsupportedContentType.Poll:
                        return SlopDiscordStrings.UnsupportedPoll;

                    case DiscordUnsupportedContentType.VoiceMessage:
                        return SlopDiscordStrings.UnsupportedVoiceMessage;

                    case DiscordUnsupportedContentType.Thread:
                        return SlopDiscordStrings.UnsupportedThread;

                    case DiscordUnsupportedContentType.Embed:
                        return SlopDiscordStrings.UnsupportedEmbed;

                    case DiscordUnsupportedContentType.Sticker:
                        return SlopDiscordStrings.UnsupportedSticker;

                    default:
                        return SlopDiscordStrings.UnsupportedOther;
                }
            }

            private IconUsage getIcon()
            {
                switch (content.Type)
                {
                    case DiscordUnsupportedContentType.Attachment:
                        return FontAwesome.Solid.Paperclip;

                    case DiscordUnsupportedContentType.Poll:
                        return FontAwesome.Solid.PollH;

                    case DiscordUnsupportedContentType.VoiceMessage:
                        return FontAwesome.Solid.Microphone;

                    case DiscordUnsupportedContentType.Thread:
                        return FontAwesome.Solid.CommentDots;

                    case DiscordUnsupportedContentType.Embed:
                        return FontAwesome.Solid.Link;

                    case DiscordUnsupportedContentType.Sticker:
                        return FontAwesome.Solid.StickyNote;

                    default:
                        return FontAwesome.Solid.QuestionCircle;
                }
            }
        }

        /// <summary>
        /// A link preview or a bot embed.
        /// </summary>
        private partial class RichEmbed : CompositeDrawable
        {
            private readonly DiscordEmbed embed;
            private readonly DiscordMarkdownContext context;

            [Resolved]
            private GameHost host { get; set; } = null!;

            public RichEmbed(DiscordEmbed embed, DiscordMarkdownContext context)
            {
                this.embed = embed;
                this.context = context;

                AutoSizeAxes = Axes.Y;
                Width = 432;
                Masking = true;
                CornerRadius = 4;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                var content = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                };

                bool hasThumbnail = embed.Thumbnail?.Url != null && embed.Type != @"video" && embed.Type != @"article";

                if (embed.Provider?.Name != null)
                {
                    content.Add(new OsuSpriteText
                    {
                        Text = embed.Provider.Name,
                        Font = DiscordStyle.UiFont(12),
                        Colour = colourProvider.Content2,
                    });
                }

                if (embed.Author?.Name != null)
                    content.Add(createLinkText(embed.Author.Name, embed.Author.Url, DiscordStyle.UiFont(14, FontWeight.SemiBold), colourProvider.Content1));

                if (embed.Title != null)
                    content.Add(createLinkText(embed.Title, embed.Url, DiscordStyle.UiFont(16, FontWeight.SemiBold), embed.Url != null ? DiscordStyle.Link : colourProvider.Content1));

                if (!string.IsNullOrWhiteSpace(embed.Description))
                {
                    content.Add(new DrawableDiscordMarkdown(embed.Description, context, textSize: 14)
                    {
                        TextColour = colourProvider.Content1,
                        AllowJumboEmoji = false,
                    });
                }

                foreach (var field in embed.Fields)
                {
                    content.Add(new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 2),
                        Children = new Drawable[]
                        {
                            new OsuTextFlowContainer(s => s.Font = DiscordStyle.UiFont(14, FontWeight.SemiBold))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = field.Name,
                            },
                            new DrawableDiscordMarkdown(field.Value, context, textSize: 14)
                            {
                                TextColour = colourProvider.Content1,
                                AllowJumboEmoji = false,
                            },
                        },
                    });
                }

                var image = embed.Type == @"video" || embed.Type == @"article" ? embed.Image ?? embed.Thumbnail : embed.Image;

                if (image?.Url != null)
                {
                    content.Add(new DiscordMediaImage(embed.Url ?? image.Url, image.ProxyUrl ?? image.Url, DiscordMedia.FitSize(image.Width, image.Height, 400, 300),
                        image.Url.EndsWith(@".gif", StringComparison.OrdinalIgnoreCase), image.Width, image.Height));
                }

                Color4 barColour = DiscordStyle.FromInteger(embed.Colour) ?? colourProvider.Background2;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background5.Opacity(0.8f),
                    },
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 4,
                        Colour = barColour,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 16, Right = hasThumbnail ? 104 : 16, Vertical = 12 },
                        Child = content,
                    },
                };

                if (hasThumbnail)
                {
                    AddInternal(new DiscordMediaImage(embed.Url ?? embed.Thumbnail!.Url!, embed.Thumbnail!.ProxyUrl ?? embed.Thumbnail.Url,
                        DiscordMedia.FitSize(embed.Thumbnail.Width, embed.Thumbnail.Height, 80, 80), false, embed.Thumbnail.Width, embed.Thumbnail.Height)
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Margin = new MarginPadding { Top = 12, Right = 16 },
                    });
                }
            }

            private Drawable createLinkText(string text, string? url, FontUsage font, Color4 colour)
            {
                var spriteText = new OsuSpriteText
                {
                    Text = text,
                    Font = font,
                    Colour = colour,
                };

                if (url == null)
                    return spriteText;

                return new OsuClickableContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Action = () => host.OpenUrlExternally(url),
                    TooltipText = url,
                    Child = spriteText,
                };
            }
        }
    }
}
