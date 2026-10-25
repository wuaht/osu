// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Discord;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// The text box of a chat, with the files which are attached, the message which is replied to or edited, and the suggestions of mentions.
    /// </summary>
    public partial class DiscordMessageComposer : CompositeDrawable
    {
        /// <summary>
        /// Larger files aren't read, as even Nitro doesn't allow uploading them.
        /// </summary>
        private const long max_upload_size = 500 * 1024 * 1024;

        private const int max_suggestions = 8;

        public readonly DiscordMessageTextBox TextBox;

        /// <summary>
        /// The message which is replied to.
        /// </summary>
        public DiscordMessage? ReplyTo { get; private set; }

        /// <summary>
        /// The message which is edited instead of sending a new one.
        /// </summary>
        public DiscordMessage? Editing { get; private set; }

        public IReadOnlyList<DiscordUpload> Uploads => uploads;

        private readonly List<DiscordUpload> uploads = new List<DiscordUpload>();

        private readonly bool supportsActions;
        private readonly Func<IReadOnlyList<DiscordCustomEmojiGroup>> getCustomEmojis;
        private readonly Func<IEnumerable<DiscordUser>> getMentionCandidates;
        private readonly Func<string, Task<IReadOnlyList<DiscordUser>>>? searchMembers;

        /// <summary>
        /// The members which were found by searching the guild, as most of its members aren't known otherwise.
        /// </summary>
        private readonly Dictionary<ulong, DiscordUser> searchedMembers = new Dictionary<ulong, DiscordUser>();

        private readonly HashSet<string> searchedQueries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private ScheduledDelegate? scheduledSearch;

        /// <summary>
        /// The mentions which were picked, by the text which is shown for them in the text box.
        /// </summary>
        private readonly Dictionary<string, string> mentions = new Dictionary<string, string>();

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        /// <summary>
        /// The client which the GIFs of the GIF picker are searched with, which is only shown if <see cref="supportsActions"/>.
        /// </summary>
        public DiscordClient? GifClient { get; init; }

        /// <summary>
        /// The stickers which can be sent, which are only shown if <see cref="supportsActions"/>.
        /// </summary>
        public Func<IReadOnlyList<DiscordStickerGroup>>? GetStickers { get; init; }

        /// <summary>
        /// Called with a GIF which was picked to be sent.
        /// </summary>
        public Action<DiscordGif>? GifPicked { get; init; }

        /// <summary>
        /// Called with a sticker which was picked to be sent.
        /// </summary>
        public Action<DiscordSticker>? StickerPicked { get; init; }

        private Container contextBar = null!;
        private DiscordPopoverButton emojiButton = null!;
        private OsuSpriteText contextText = null!;
        private FillFlowContainer<AttachmentChip> attachmentFlow = null!;
        private FillFlowContainer<Suggestion> suggestionFlow = null!;
        private Container suggestionContainer = null!;

        /// <summary>
        /// The suggestion which is accepted with tab or enter, which the arrow keys move.
        /// </summary>
        private int selectedSuggestion;

        private IReadOnlyList<DiscordStandardEmoji>? standardEmojis;

        /// <summary>
        /// The standard emojis by their shortcodes, which are converted when the message is sent.
        /// </summary>
        private Dictionary<string, string>? standardShortcodes;

        /// <param name="channel">The channel which is written in.</param>
        /// <param name="supportsActions">Whether files, replies, edits and so on are supported by the client.</param>
        /// <param name="getCustomEmojis">The custom emojis which can be written.</param>
        /// <param name="getMentionCandidates">The users which can be mentioned.</param>
        /// <param name="searchMembers">Searches the members of the guild by the start of their names, if the channel is in one.</param>
        public DiscordMessageComposer(DiscordChannel channel, bool supportsActions, Func<IReadOnlyList<DiscordCustomEmojiGroup>> getCustomEmojis,
                                      Func<IEnumerable<DiscordUser>> getMentionCandidates, Func<string, Task<IReadOnlyList<DiscordUser>>>? searchMembers = null)
        {
            this.supportsActions = supportsActions;
            this.getCustomEmojis = getCustomEmojis;
            this.getMentionCandidates = getMentionCandidates;
            this.searchMembers = searchMembers;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            TextBox = new DiscordMessageTextBox(supportsActions)
            {
                RelativeSizeAxes = Axes.X,
                Height = 44,
                LengthLimit = 2000,
                PlaceholderText = SlopDiscordStrings.MessagePlaceholder(channel.IsPrivate ? channel.DisplayName : $@"#{channel.DisplayName}"),
                ReleaseFocusOnCommit = false,
                HoldFocus = true,
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    suggestionContainer = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Masking = true,
                        CornerRadius = 8,
                        Margin = new MarginPadding { Bottom = 6 },
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background4,
                            },
                            suggestionFlow = new FillFlowContainer<Suggestion>
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding(6),
                            },
                        },
                    },
                    contextBar = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 32,
                        Masking = true,
                        CornerRadius = 8,
                        Alpha = 0,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background5,
                            },
                            contextText = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 12,
                                Font = DiscordStyle.UiFont(13),
                                Colour = colourProvider.Content2,
                            },
                            new DiscordIconButton(FontAwesome.Solid.TimesCircle, SlopDiscordStrings.Cancel, ClearContext)
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                X = -4,
                                Size = new Vector2(26),
                            },
                        },
                    },
                    attachmentFlow = new FillFlowContainer<AttachmentChip>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(8),
                        Padding = new MarginPadding { Vertical = 8 },
                        Alpha = 0,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            TextBox,
                            supportsActions
                                ? new DiscordPopoverButton(FontAwesome.Solid.PlusCircle, SlopDiscordStrings.UploadFile, () => new FilePickerPopover(AddFile))
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    X = 6,
                                }
                                : Empty(),
                            supportsActions
                                ? new DiscordPopoverButton(FontAwesome.Solid.Film, SlopDiscordStrings.SelectGif, createGifPicker)
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    X = -70,
                                }
                                : Empty(),
                            supportsActions
                                ? new DiscordPopoverButton(FontAwesome.Regular.StickyNote, SlopDiscordStrings.SelectSticker, createStickerPicker)
                                {
                                    Anchor = Anchor.CentreRight,
                                    Origin = Anchor.CentreRight,
                                    X = -38,
                                }
                                : Empty(),
                            emojiButton = new DiscordPopoverButton(FontAwesome.Regular.Smile, SlopDiscordStrings.SelectEmoji, createEmojiPicker)
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                X = -6,
                            },
                        },
                    },
                },
            };

            TextBox.PasteAttachments = pasteAttachments;
            TextBox.AcceptSuggestion = acceptSelectedSuggestion;
            TextBox.MoveSuggestion = moveSuggestion;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            TextBox.Current.BindValueChanged(_ => updateSuggestions());

            DiscordEmojiData.Load().ContinueWith(t => Schedule(() =>
            {
                if (!t.IsCompletedSuccessfully)
                    return;

                standardEmojis = t.GetResultSafely().SelectMany(c => c.Emojis).ToList();
                standardShortcodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var emoji in standardEmojis)
                {
                    foreach (string code in emoji.Shortcodes)
                        standardShortcodes.TryAdd(code, emoji.Unicode);
                }

                if (currentEmojiQuery != null)
                    updateSuggestions();
            }), TaskScheduler.Default);
        }

        /// <summary>
        /// Code, which is left as it is, or an emoji written as :name: outside of a custom emoji.
        /// </summary>
        private static readonly Regex emoji_shortcode_regex = new Regex(@"(```[\s\S]*?```|`[^`\n]*`)|(?<!<|<a):([\w+-]{1,32}):", RegexOptions.Compiled);

        /// <summary>
        /// The text which is sent, with the mentions and emojis converted to the format of Discord.
        /// </summary>
        public string GetMessageText()
        {
            string text = TextBox.Text.Trim();

            foreach (var (shown, mention) in mentions.OrderByDescending(m => m.Key.Length))
                text = text.Replace(shown, mention, StringComparison.Ordinal);

            var customEmojis = getCustomEmojis().SelectMany(g => g.Emojis).ToList();

            // Like in Discord, :name: is written as the emoji, where custom emojis take precedence over standard emojis of the same name.
            return emoji_shortcode_regex.Replace(text, m =>
            {
                if (m.Groups[1].Success)
                    return m.Value;

                string name = m.Groups[2].Value;
                var custom = customEmojis.FirstOrDefault(e => e.Name == name) ?? customEmojis.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (custom != null)
                    return DiscordEmoji.ToMessageText(new DiscordReactionEmoji { Id = custom.Id, Name = custom.Name, Animated = custom.Animated });

                return standardShortcodes?.GetValueOrDefault(name) ?? m.Value;
            });
        }

        public void SetReply(DiscordMessage message)
        {
            Editing = null;
            ReplyTo = message;
            contextText.Text = SlopDiscordStrings.ReplyingTo(message.Author.DisplayName);
            contextBar.Show();
            TextBox.TakeFocus();
        }

        public void SetEdit(DiscordMessage message)
        {
            ReplyTo = null;
            Editing = message;
            contextText.Text = SlopDiscordStrings.EditingMessage;
            contextBar.Show();

            mentions.Clear();
            TextBox.Text = message.Content;
            TextBox.TakeFocus();
        }

        /// <summary>
        /// Stops replying to or editing a message.
        /// </summary>
        public void ClearContext()
        {
            if (Editing != null)
                TextBox.Text = string.Empty;

            ReplyTo = null;
            Editing = null;
            contextBar.Hide();
        }

        /// <summary>
        /// Clears everything after a message was sent.
        /// </summary>
        public void Reset()
        {
            TextBox.Text = string.Empty;
            mentions.Clear();
            uploads.Clear();
            attachmentFlow.Clear();
            attachmentFlow.Hide();
            ReplyTo = null;
            Editing = null;
            contextBar.Hide();
        }

        public void AddUpload(DiscordUpload upload)
        {
            if (!supportsActions || Editing != null)
                return;

            // Discord allows ten attachments per message.
            if (uploads.Count >= 10)
                return;

            uploads.Add(upload);
            attachmentFlow.Add(new AttachmentChip(upload, () => removeUpload(upload)));
            attachmentFlow.Show();
        }

        public void AddFile(FileInfo file)
        {
            try
            {
                if (!file.Exists || file.Length > max_upload_size)
                    return;

                AddUpload(new DiscordUpload(file.Name, File.ReadAllBytes(file.FullName)));
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to read a file for Discord: {e.Message}", LoggingTarget.Network);
            }
        }

        private void removeUpload(DiscordUpload upload)
        {
            uploads.Remove(upload);
            attachmentFlow.RemoveAll(c => c.Upload == upload, true);

            if (uploads.Count == 0)
                attachmentFlow.Hide();
        }

        private Popover createGifPicker() => new DiscordGifPicker(GifClient!, g => GifPicked?.Invoke(g))
        {
            AllowableAnchors = new[] { Anchor.TopCentre, Anchor.TopLeft, Anchor.TopRight },
            MaxHeight = spaceAbovePickers,
        };

        private Popover createStickerPicker() => new DiscordStickerPicker(GetStickers?.Invoke() ?? Array.Empty<DiscordStickerGroup>(), s => StickerPicked?.Invoke(s))
        {
            AllowableAnchors = new[] { Anchor.TopCentre, Anchor.TopLeft, Anchor.TopRight },
            MaxHeight = spaceAbovePickers,
        };

        /// <summary>
        /// The space above the text box, which the pickers are limited to so that they don't cover it.
        /// </summary>
        private float spaceAbovePickers => -emojiButton.ToLocalSpace(new Vector2(emojiButton.ScreenSpaceDrawQuad.TopLeft.X, 0)).Y - 30;

        private Popover createEmojiPicker()
        {
            // The picker only opens above the composer, as it would otherwise fall back to covering it when it doesn't fit.
            // The arrow and the distance to the button are left free above it.
            float spaceAbove = spaceAbovePickers;

            return new DiscordEmojiPicker(getCustomEmojis(), insertEmoji)
            {
                AllowableAnchors = new[] { Anchor.TopCentre, Anchor.TopLeft, Anchor.TopRight },
                MaxHeight = spaceAbove,
            };
        }

        private void insertEmoji(DiscordReactionEmoji emoji)
        {
            TextBox.Insert(DiscordEmoji.ToMessageText(emoji));
            TextBox.TakeFocus();
        }

        /// <summary>
        /// Attaches the files or the image of the clipboard, returning whether there were any.
        /// Text is pasted as usual, so an image is only used if there is no text.
        /// </summary>
        private bool pasteAttachments()
        {
            if (!supportsActions || Editing != null)
                return false;

            var files = getClipboardFiles();

            if (files.Count > 0)
            {
                foreach (string path in files)
                    AddFile(new FileInfo(path));

                return true;
            }

            if (!string.IsNullOrEmpty(clipboard.GetText()))
                return false;

            try
            {
                using var image = clipboard.GetImage<Rgba32>();

                if (image == null)
                    return false;

                using var stream = new MemoryStream();
                image.SaveAsPng(stream);
                AddUpload(new DiscordUpload(@"image.png", stream.ToArray()));
                return true;
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to paste an image into Discord: {e.Message}", LoggingTarget.Runtime);
                return false;
            }
        }

        #region Suggestions

        /// <summary>
        /// The emoji which is written at the end of the text, without the colon, once at least two characters of it are written like in Discord.
        /// </summary>
        private string? currentEmojiQuery
        {
            get
            {
                string text = TextBox.Text;
                int colon = text.LastIndexOf(':');

                if (colon < 0 || (colon > 0 && !char.IsWhiteSpace(text[colon - 1])))
                    return null;

                string query = text[(colon + 1)..];

                return query.Length is >= 2 and <= 32 && query.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '+') ? query : null;
            }
        }

        private void updateSuggestions()
        {
            // The member search updates the mention suggestions after a delay, which shouldn't move the selection back to the first one.
            string? previouslySelected = suggestionFlow.ElementAtOrDefault(selectedSuggestion)?.Identity;

            suggestionFlow.Clear();
            selectedSuggestion = 0;

            if (currentMentionQuery is string mentionQuery)
                addMentionSuggestions(mentionQuery);
            else if (currentEmojiQuery is string emojiQuery)
                addEmojiSuggestions(emojiQuery);

            if (previouslySelected != null)
                selectedSuggestion = Math.Max(0, suggestionFlow.ToList().FindIndex(s => s.Identity == previouslySelected));

            updateSelectedSuggestion();
            suggestionContainer.Alpha = suggestionFlow.Count > 0 ? 1 : 0;
        }

        private void addEmojiSuggestions(string query)
        {
            // Exact names first, then names which start with the query, then the others, with custom emojis before standard ones.
            int rank(string name) => name.Equals(query, StringComparison.OrdinalIgnoreCase) ? 0 : name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1 : 2;

            var custom = getCustomEmojis()
                         .SelectMany(g => g.Emojis)
                         .Where(e => e.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                         .Select(e => (rank: rank(e.Name), name: e.Name, emoji: new DiscordReactionEmoji { Id = e.Id, Name = e.Name, Animated = e.Animated }));

            var standard = (standardEmojis ?? Array.Empty<DiscordStandardEmoji>())
                           .Select(e => (emoji: e, code: e.Shortcodes.OrderBy(rank).FirstOrDefault(c => c.Contains(query, StringComparison.OrdinalIgnoreCase))))
                           .Where(e => e.code != null)
                           .Select(e => (rank: rank(e.code!), name: e.code!, emoji: new DiscordReactionEmoji { Name = e.emoji.Unicode }));

            var matches = custom.Concat(standard)
                                .Select((e, index) => (e.rank, e.name, e.emoji, index))
                                .OrderBy(e => e.rank)
                                .ThenBy(e => e.index)
                                .Take(max_suggestions);

            foreach (var (_, name, emoji, _) in matches)
                suggestionFlow.Add(new EmojiSuggestion(emoji, name, () => acceptEmojiSuggestion(emoji, name)));
        }

        private bool moveSuggestion(int direction)
        {
            if (suggestionFlow.Count == 0 || suggestionContainer.Alpha == 0)
                return false;

            selectedSuggestion = (selectedSuggestion + direction + suggestionFlow.Count) % suggestionFlow.Count;
            updateSelectedSuggestion();
            return true;
        }

        private void updateSelectedSuggestion()
        {
            int index = 0;

            foreach (var suggestion in suggestionFlow)
                suggestion.Selected = index++ == selectedSuggestion;
        }

        private bool acceptSelectedSuggestion()
        {
            var selected = suggestionFlow.ElementAtOrDefault(selectedSuggestion);

            if (selected == null || suggestionContainer.Alpha == 0)
                return false;

            selected.TriggerClick();
            return true;
        }

        private void acceptEmojiSuggestion(DiscordReactionEmoji emoji, string name)
        {
            string text = TextBox.Text;
            int colon = text.LastIndexOf(':');

            if (colon < 0)
                return;

            // Custom emojis are shown by their name, which is converted when the message is sent.
            TextBox.Text = text[..colon] + (emoji.Id == null ? emoji.Name : $@":{name}:") + @" ";
            TextBox.TakeFocus();
        }

        #endregion

        #region Mentions

        /// <summary>
        /// The mention which is written at the end of the text, without the @.
        /// </summary>
        private string? currentMentionQuery
        {
            get
            {
                string text = TextBox.Text;
                int at = text.LastIndexOf('@');

                if (at < 0 || (at > 0 && !char.IsWhiteSpace(text[at - 1])))
                    return null;

                string query = text[(at + 1)..];

                return query.Length <= 32 && !query.Any(char.IsWhiteSpace) ? query : null;
            }
        }

        private void addMentionSuggestions(string query)
        {
            scheduleSearch(query);

            var matches = getMentionCandidates()
                          .Concat(searchedMembers.Values)
                          .DistinctBy(u => u.Id)
                          .Where(u => u.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) || u.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                          .OrderByDescending(u => u.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase) || u.Username.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                          .ThenBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase)
                          .Take(max_suggestions);

            foreach (var user in matches)
                suggestionFlow.Add(new MentionSuggestion(user, () => acceptSuggestion(user)));
        }

        private void scheduleSearch(string query)
        {
            scheduledSearch?.Cancel();

            if (searchMembers == null || query.Length == 0 || searchedQueries.Contains(query))
                return;

            // The search is delayed while typing, so that only the final query is sent.
            scheduledSearch = Scheduler.AddDelayed(() =>
            {
                searchedQueries.Add(query);

                searchMembers(query).ContinueWith(t => Schedule(() =>
                {
                    if (!t.IsCompletedSuccessfully)
                        return;

                    foreach (var member in t.GetResultSafely())
                        searchedMembers[member.Id] = member;

                    if (currentMentionQuery != null)
                        updateSuggestions();
                }), TaskScheduler.Default);
            }, 200);
        }

        private void acceptSuggestion(DiscordUser user)
        {
            string text = TextBox.Text;
            int at = text.LastIndexOf('@');

            if (at < 0)
                return;

            // The text box shows the name, which is replaced with the actual mention when the message is sent.
            string shown = $@"@{user.DisplayName}";
            mentions[shown] = $@"<@{user.Id}>";

            TextBox.Text = text[..at] + shown + @" ";
            TextBox.TakeFocus();
        }

        #endregion

        #region Clipboard files

        /// <summary>
        /// The paths of the files which were copied, which only Windows is supported for.
        /// </summary>
        private static IReadOnlyList<string> getClipboardFiles()
        {
            if (!OperatingSystem.IsWindows())
                return Array.Empty<string>();

            var files = new List<string>();

            try
            {
                if (!IsClipboardFormatAvailable(cf_hdrop) || !OpenClipboard(IntPtr.Zero))
                    return files;

                try
                {
                    IntPtr drop = GetClipboardData(cf_hdrop);

                    if (drop == IntPtr.Zero)
                        return files;

                    uint count = DragQueryFile(drop, uint.MaxValue, null, 0);

                    for (uint i = 0; i < count; i++)
                    {
                        uint length = DragQueryFile(drop, i, null, 0);
                        var builder = new StringBuilder((int)length + 1);

                        if (DragQueryFile(drop, i, builder, (uint)builder.Capacity) > 0)
                            files.Add(builder.ToString());
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            catch (Exception e)
            {
                Logger.Log($@"Failed to read the copied files: {e.Message}", LoggingTarget.Runtime);
            }

            return files.Where(File.Exists).ToList();
        }

        private const uint cf_hdrop = 15;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr newOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint format);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint DragQueryFile(IntPtr drop, uint file, StringBuilder? fileName, uint size);

        #endregion

        public partial class DiscordMessageTextBox : HistoryTextBox
        {
            private readonly bool hasButtons;

            /// <summary>
            /// Attaches what was copied instead of pasting text, returning whether it did.
            /// </summary>
            public Func<bool>? PasteAttachments { get; set; }

            /// <summary>
            /// Accepts the first suggestion with tab, returning whether there was one.
            /// </summary>
            public Func<bool>? AcceptSuggestion { get; set; }

            /// <summary>
            /// Moves the selected suggestion by the given direction, returning whether suggestions are shown.
            /// </summary>
            public Func<int, bool>? MoveSuggestion { get; set; }

            public DiscordMessageTextBox(bool hasButtons)
            {
                this.hasButtons = hasButtons;
            }

            // Space for the buttons over the text box.
            protected override float LeftRightPadding => hasButtons ? 44 : 10;

            // The GIF, sticker and emoji buttons are on the right.
            protected override float RightPadding => hasButtons ? 108 : LeftRightPadding;

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                CornerRadius = 8;
                BackgroundUnfocused = colourProvider.Background4.Opacity(0.8f);
                BackgroundFocused = colourProvider.Background4.Opacity(0.9f);
                BackgroundCommit = colourProvider.Background3;
            }

            protected override SpriteText CreatePlaceholder() => base.CreatePlaceholder().With(p => p.Font = DiscordStyle.UiFont(15));

            /// <summary>
            /// Inserts text at the caret.
            /// </summary>
            public void Insert(string text) => InsertString(text);

            public override bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
            {
                if (e.Action == PlatformAction.Paste && PasteAttachments?.Invoke() == true)
                    return true;

                return base.OnPressed(e);
            }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                switch (e.Key)
                {
                    case Key.Up:
                    case Key.Down:
                        if (MoveSuggestion?.Invoke(e.Key == Key.Up ? -1 : 1) == true)
                            return true;

                        break;

                    case Key.Tab:
                    case Key.Enter:
                    case Key.KeypadEnter:
                        if (AcceptSuggestion?.Invoke() == true)
                            return true;

                        break;
                }

                return base.OnKeyDown(e);
            }
        }

        private abstract partial class Suggestion : OsuClickableContainer
        {
            private Box hoverBackground = null!;
            private Box selectedBackground = null!;

            private bool selected;

            public bool Selected
            {
                get => selected;
                set
                {
                    selected = value;

                    if (IsLoaded)
                        selectedBackground.Alpha = value ? 0.1f : 0;
                }
            }

            protected Suggestion(Action select)
            {
                Action = select;

                RelativeSizeAxes = Axes.X;
                Height = 34;
                Masking = true;
                CornerRadius = 4;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                Children = new Drawable[]
                {
                    selectedBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = selected ? 0.1f : 0,
                    },
                    hoverBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                        Alpha = 0,
                    },
                    CreateContent(colourProvider),
                };
            }

            /// <summary>
            /// What is suggested, which the selection is kept by when the suggestions are updated.
            /// </summary>
            public abstract string Identity { get; }

            protected abstract Drawable CreateContent(OverlayColourProvider colourProvider);

            protected override bool OnHover(HoverEvent e)
            {
                hoverBackground.FadeTo(0.06f, 50);
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                hoverBackground.FadeOut(100);
                base.OnHoverLost(e);
            }
        }

        private partial class EmojiSuggestion : Suggestion
        {
            private readonly DiscordReactionEmoji emoji;
            private readonly string name;

            public EmojiSuggestion(DiscordReactionEmoji emoji, string name, Action select)
                : base(select)
            {
                this.emoji = emoji;
                this.name = name;
            }

            public override string Identity => $@":{name}:";

            protected override Drawable CreateContent(OverlayColourProvider colourProvider) => new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Padding = new MarginPadding { Horizontal = 8 },
                Children = new Drawable[]
                {
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(24),
                        Child = DiscordEmoji.CreateDrawable(emoji, 22),
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = $@":{name}:",
                        Font = DiscordStyle.UiFont(14, FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                    },
                },
            };
        }

        private partial class MentionSuggestion : Suggestion
        {
            public readonly DiscordUser User;

            public MentionSuggestion(DiscordUser user, Action select)
                : base(select)
            {
                User = user;
            }

            public override string Identity => $@"@{User.Id}";

            protected override Drawable CreateContent(OverlayColourProvider colourProvider) => new FillFlowContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Padding = new MarginPadding { Horizontal = 8 },
                Children = new Drawable[]
                {
                    new DiscordAvatar(User.GetAvatarUrl(48), 24)
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = User.DisplayName,
                        Font = DiscordStyle.UiFont(14, FontWeight.SemiBold),
                        Colour = colourProvider.Content1,
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = User.Username,
                        Font = DiscordStyle.UiFont(13),
                        Colour = colourProvider.Content2,
                    },
                },
            };
        }

        private partial class AttachmentChip : CompositeDrawable, IHasTooltip
        {
            public readonly DiscordUpload Upload;

            private readonly Action remove;

            public LocalisableString TooltipText => Upload.Filename;

            public AttachmentChip(DiscordUpload upload, Action remove)
            {
                Upload = upload;
                this.remove = remove;

                Size = new Vector2(180, 52);
                Masking = true;
                CornerRadius = 8;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background4,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 12,
                        Size = new Vector2(20),
                        Icon = FontAwesome.Regular.File,
                        Colour = colourProvider.Content2,
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding { Left = 42, Right = 34 },
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = Upload.Filename,
                                Font = DiscordStyle.UiFont(13, FontWeight.SemiBold),
                                Colour = colourProvider.Content1,
                            },
                            new OsuSpriteText
                            {
                                Text = formatSize(Upload.Data.LongLength),
                                Font = DiscordStyle.UiFont(11),
                                Colour = colourProvider.Content2,
                            },
                        },
                    },
                    new DiscordIconButton(FontAwesome.Solid.Trash, SlopDiscordStrings.RemoveAttachment, remove)
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -4,
                        Size = new Vector2(28),
                        HoverColour = DiscordStyle.Error,
                    },
                };
            }

            private static string formatSize(long bytes)
            {
                if (bytes < 1024)
                    return $@"{bytes} B";

                if (bytes < 1024 * 1024)
                    return $@"{bytes / 1024f:0.#} KB";

                return $@"{bytes / 1024f / 1024f:0.#} MB";
            }
        }

        private partial class FilePickerPopover : OsuPopover
        {
            private readonly OsuFileSelector fileSelector;
            private readonly Action<FileInfo> onSelected;

            public FilePickerPopover(Action<FileInfo> onSelected)
                : base(false)
            {
                this.onSelected = onSelected;

                Child = new Container
                {
                    Size = new Vector2(600, 400),
                    Padding = new MarginPadding { Bottom = 1 },
                    Child = fileSelector = new OsuFileSelector
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                fileSelector.CurrentFile.BindValueChanged(f =>
                {
                    if (f.NewValue == null)
                        return;

                    onSelected(f.NewValue);
                    this.HidePopover();
                });
            }
        }
    }
}
