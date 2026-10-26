// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Text;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// A multi-line text box with a toolbar for formatting the message with markdown, like the text boxes of the website.
    /// </summary>
    public partial class DiscussionTextEditor : CompositeDrawable
    {
        /// <summary>
        /// Invoked when the message is posted with ctrl-enter.
        /// </summary>
        public event Action? Committed;

        public readonly DiscussionTextArea TextArea;

        public string Text
        {
            get => TextArea.Text;
            set => TextArea.Text = value;
        }

        [Resolved]
        private EditorClock? editorClock { get; set; }

        public DiscussionTextEditor(LocalisableString placeholder = default, int minLines = 3)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    TextArea = new DiscussionTextArea(minLines)
                    {
                        PlaceholderText = placeholder,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(2, 0),
                                Children = new Drawable[]
                                {
                                    createButton(FontAwesome.Solid.Bold, SlopDiscussionStrings.FormatBold, () => TextArea.Wrap(@"**", @"**")),
                                    createButton(FontAwesome.Solid.Italic, SlopDiscussionStrings.FormatItalic, () => TextArea.Wrap(@"*", @"*")),
                                    createButton(FontAwesome.Solid.Link, SlopDiscussionStrings.FormatLink, () => TextArea.InsertLink()),
                                    createButton(FontAwesome.Solid.ListUl, SlopDiscussionStrings.FormatList, () => TextArea.PrefixLines(@"- ")),
                                    createButton(FontAwesome.Solid.QuoteRight, SlopDiscussionStrings.FormatQuote, () => TextArea.PrefixLines(@"> ")),
                                    createButton(FontAwesome.Solid.Code, SlopDiscussionStrings.FormatCode, () => TextArea.Wrap(@"`", @"`")),
                                    createButton(FontAwesome.Regular.Clock, SlopDiscussionStrings.FormatTimestamp, insertTimestamp),
                                }
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Text = SlopDiscussionStrings.FormatHint,
                                Font = OsuFont.Default.With(size: 12),
                                Alpha = 0.6f,
                            },
                        }
                    },
                }
            };

            TextArea.Committed += () => Committed?.Invoke();
        }

        private void insertTimestamp()
        {
            if (editorClock == null)
                return;

            TextArea.Insert($@"{editorClock.CurrentTime.ToEditorFormattedString()} - ");
        }

        public void TakeFocus() => GetContainingFocusManager()?.ChangeFocus(TextArea);

        private IconButton createButton(IconUsage icon, LocalisableString tooltip, Action action) => new IconButton
        {
            Icon = icon,
            TooltipText = tooltip,
            Size = new Vector2(26),
            IconScale = new Vector2(0.8f),
            Action = () =>
            {
                action();

                // keep typing after formatting.
                Schedule(TakeFocus);
            },
        };
    }

    /// <summary>
    /// A text box which wraps its text and accepts new lines.
    /// </summary>
    public partial class DiscussionTextArea : CompositeDrawable, IKeyBindingHandler<PlatformAction>
    {
        private const float font_size = 15;
        private const float line_height = font_size + 5;
        private const float padding = 8;

        /// <summary>
        /// Invoked when the text is committed with ctrl-enter.
        /// </summary>
        public event Action? Committed;

        public readonly Bindable<string> Current = new Bindable<string>(string.Empty);

        public int LengthLimit { get; set; } = 10000;

        public string Text
        {
            get => Current.Value;
            set
            {
                Current.Value = clean(value ?? string.Empty);
                selectionStart = selectionEnd = Current.Value.Length;
                invalidateLayout();
            }
        }

        public LocalisableString PlaceholderText
        {
            get => placeholder.Text;
            set => placeholder.Text = value;
        }

        public override bool AcceptsFocus => true;

        private readonly int minLines;
        private readonly FontUsage font = OsuFont.Default.With(size: font_size);

        private readonly Box background;
        private readonly Container content;
        private readonly Container selectionLayer;
        private readonly Container<OsuSpriteText> lineLayer;
        private readonly OsuSpriteText placeholder;
        private readonly Box caret;

        [Resolved]
        private FontStore fontStore { get; set; } = null!;

        [Resolved]
        private TextInputSource textInput { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        private Color4 backgroundUnfocused;
        private Color4 backgroundFocused;
        private Color4 selectionColour;

        private int selectionStart;
        private int selectionEnd;

        /// <summary>
        /// The horizontal position to keep when moving the caret up and down.
        /// </summary>
        private float? stickyX;

        private bool textInputBound;
        private bool layoutValid;
        private float layoutWidth = -1;

        private readonly List<VisualLine> lines = new List<VisualLine>();

        /// <summary>
        /// The horizontal position of each character within its line.
        /// </summary>
        private float[] positions = Array.Empty<float>();

        public DiscussionTextArea(int minLines)
        {
            this.minLines = minLines;

            RelativeSizeAxes = Axes.X;
            Masking = true;
            CornerRadius = 5;

            InternalChildren = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding(padding),
                    Children = new Drawable[]
                    {
                        selectionLayer = new Container { RelativeSizeAxes = Axes.Both },
                        placeholder = new OsuSpriteText
                        {
                            Font = font,
                            Alpha = 0.5f,
                            Y = 2,
                        },
                        lineLayer = new Container<OsuSpriteText> { RelativeSizeAxes = Axes.Both },
                        caret = new Box
                        {
                            Width = 2,
                            Height = font_size + 2,
                            Alpha = 0,
                        },
                    }
                },
            };

            Height = minLines * line_height + padding * 2;
        }

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider? colourProvider, OsuColour colours)
        {
            backgroundUnfocused = colourProvider?.Background5 ?? Color4.Black.Opacity(0.5f);
            backgroundFocused = colourProvider?.Background4 ?? OsuColour.Gray(0.3f).Opacity(0.8f);
            selectionColour = colourProvider?.Background1 ?? colours.Purple;
            BorderColour = colourProvider?.Highlight1 ?? colours.Yellow;

            background.Colour = backgroundUnfocused;
            placeholder.Colour = colourProvider?.Foreground1 ?? new Color4(180, 180, 180, 255);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(_ => invalidateLayout());
        }

        protected override void Update()
        {
            base.Update();

            if (!layoutValid || layoutWidth != content.DrawWidth)
                updateLayout();

            // blink the caret like other text boxes.
            if (HasFocus && selectionStart == selectionEnd)
                caret.Alpha = (Time.Current % 1000) < 500 ? 1 : 0;
            else
                caret.Alpha = 0;
        }

        #region Layout

        private struct VisualLine
        {
            public int Start;
            public int End;
            public float Width;
        }

        private void invalidateLayout() => layoutValid = false;

        private ITexturedCharacterGlyph? getGlyph(char c)
            => fontStore.Get(font.FontName, c) ?? fontStore.Get(null, c) ?? fontStore.Get(font.FontName, '?');

        private void updateLayout()
        {
            layoutValid = true;
            layoutWidth = content.DrawWidth;

            string text = Current.Value;
            float maxWidth = Math.Max(1, layoutWidth);

            lines.Clear();
            positions = new float[text.Length];

            int lineStart = 0;
            int lastBreak = -1;
            float x = 0;
            ITexturedCharacterGlyph? previous = null;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '\n')
                {
                    positions[i] = x;
                    lines.Add(new VisualLine { Start = lineStart, End = i, Width = x });
                    lineStart = i + 1;
                    lastBreak = -1;
                    x = 0;
                    previous = null;
                    continue;
                }

                var glyph = getGlyph(c);
                float kerning = previous != null && glyph != null ? glyph.GetKerning(previous) * font_size : 0;
                float advance = (glyph?.XAdvance ?? 0.5f) * font_size;

                if (x + kerning + advance > maxWidth && i > lineStart)
                {
                    // wrap after the last space of the line, or within the word if there is none.
                    int wrapAt = lastBreak > lineStart ? lastBreak : i;

                    lines.Add(new VisualLine { Start = lineStart, End = wrapAt, Width = positionInLine(wrapAt - 1, text) });
                    lineStart = wrapAt;
                    lastBreak = -1;

                    // lay out the moved characters again on the new line.
                    x = 0;
                    previous = null;
                    i = wrapAt - 1;
                    continue;
                }

                positions[i] = x + kerning;
                x += kerning + advance;
                previous = glyph;

                if (c == ' ')
                    lastBreak = i + 1;
            }

            lines.Add(new VisualLine { Start = lineStart, End = text.Length, Width = x });

            while (lineLayer.Count < lines.Count)
                lineLayer.Add(new OsuSpriteText { Font = font });

            while (lineLayer.Count > lines.Count)
                lineLayer.Remove(lineLayer[^1], true);

            for (int i = 0; i < lines.Count; i++)
            {
                lineLayer[i].Text = text.Substring(lines[i].Start, lines[i].End - lines[i].Start);
                lineLayer[i].Position = new Vector2(0, i * line_height + 2);
            }

            placeholder.Alpha = text.Length == 0 ? 0.5f : 0;

            Height = Math.Max(minLines, lines.Count) * line_height + padding * 2;

            updateCaret();
        }

        /// <summary>
        /// The right edge of a character within its line.
        /// </summary>
        private float positionInLine(int index, string text)
        {
            var glyph = getGlyph(text[index]);
            return positions[index] + (glyph?.XAdvance ?? 0.5f) * font_size;
        }

        private int lineOf(int index)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                if (lines[i].Start <= index)
                    return i;
            }

            return 0;
        }

        private Vector2 positionOf(int index)
        {
            int line = lineOf(index);
            var visualLine = lines[line];
            float x = index >= visualLine.End ? visualLine.Width : positions[index];

            return new Vector2(x, line * line_height);
        }

        private int indexAt(int line, float x)
        {
            line = Math.Clamp(line, 0, lines.Count - 1);
            var visualLine = lines[line];

            int best = visualLine.End;
            float bestDistance = Math.Abs(visualLine.Width - x);

            for (int i = visualLine.Start; i < visualLine.End; i++)
            {
                float distance = Math.Abs(positions[i] - x);

                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            // a wrapped line ends with the first character of the next line, so the caret stays before it.
            if (best == visualLine.End && line < lines.Count - 1 && lines[line + 1].Start == visualLine.End && best > visualLine.Start)
                best--;

            return best;
        }

        private int indexAt(Vector2 screenSpacePosition)
        {
            var pos = content.ToLocalSpace(screenSpacePosition) - new Vector2(content.Padding.Left, content.Padding.Top);
            return indexAt((int)Math.Floor(pos.Y / line_height), pos.X);
        }

        private void updateCaret()
        {
            if (!layoutValid)
                return;

            var caretPos = positionOf(selectionEnd);
            caret.Position = caretPos + new Vector2(-1, 1);

            int left = Math.Min(selectionStart, selectionEnd);
            int right = Math.Max(selectionStart, selectionEnd);

            selectionLayer.Clear();

            if (left == right)
                return;

            int firstLine = lineOf(left);
            int lastLine = lineOf(right);

            for (int line = firstLine; line <= lastLine; line++)
            {
                float start = line == firstLine ? positionOf(left).X : 0;
                float end = line == lastLine ? positionOf(right).X : lines[line].Width + 4;

                selectionLayer.Add(new Box
                {
                    Colour = selectionColour,
                    Position = new Vector2(start, line * line_height),
                    Size = new Vector2(Math.Max(2, end - start), line_height),
                });
            }
        }

        #endregion

        #region Editing

        private static string clean(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");

        private int selectionLeft => Math.Min(selectionStart, selectionEnd);
        private int selectionRight => Math.Max(selectionStart, selectionEnd);

        private string selectedText => Current.Value.Substring(selectionLeft, selectionRight - selectionLeft);

        private void select(int start, int end)
        {
            selectionStart = Math.Clamp(start, 0, Current.Value.Length);
            selectionEnd = Math.Clamp(end, 0, Current.Value.Length);
            updateCaret();
        }

        private void moveCaret(int index, bool selecting)
        {
            index = Math.Clamp(index, 0, Current.Value.Length);
            select(selecting ? selectionStart : index, index);
        }

        /// <summary>
        /// Replaces the selection with the text, keeping the caret after it.
        /// </summary>
        public void Insert(string text)
        {
            text = clean(text);

            string current = Current.Value;
            int left = selectionLeft;

            int allowed = LengthLimit - (current.Length - (selectionRight - left));

            if (allowed <= 0)
                return;

            if (text.Length > allowed)
                text = text.Substring(0, allowed);

            Current.Value = string.Concat(current.AsSpan(0, left), text, current.AsSpan(selectionRight));
            selectionStart = selectionEnd = left + text.Length;
            stickyX = null;

            updateLayout();
        }

        private void delete(int start, int end)
        {
            start = Math.Clamp(start, 0, Current.Value.Length);
            end = Math.Clamp(end, 0, Current.Value.Length);

            if (start == end)
                return;

            select(Math.Min(start, end), Math.Max(start, end));
            Insert(string.Empty);
        }

        /// <summary>
        /// Surrounds the selection with markdown, or places the caret between it when nothing is selected.
        /// </summary>
        public void Wrap(string prefix, string suffix)
        {
            int left = selectionLeft;
            string selected = selectedText;

            Insert(prefix + selected + suffix);

            if (selected.Length == 0)
                select(left + prefix.Length, left + prefix.Length);
            else
                select(left + prefix.Length, left + prefix.Length + selected.Length);
        }

        public void InsertLink()
        {
            int left = selectionLeft;
            string selected = selectedText;

            Insert($@"[{selected}](https://)");

            // the address is selected to replace it, or the text if there is none yet.
            if (selected.Length == 0)
                select(left + 1, left + 1);
            else
                select(left + selected.Length + 3, left + selected.Length + 11);
        }

        /// <summary>
        /// Prefixes every selected line, or the line of the caret.
        /// </summary>
        public void PrefixLines(string prefix)
        {
            string text = Current.Value;

            int start = text.LastIndexOf('\n', Math.Max(0, selectionLeft - 1)) + 1;

            if (selectionLeft == 0)
                start = 0;

            int end = text.IndexOf('\n', selectionRight);

            if (end < 0)
                end = text.Length;

            string[] selectedLines = text.Substring(start, end - start).Split('\n');
            string prefixed = string.Join('\n', selectedLines.Select(l => prefix + l));

            select(start, end);
            Insert(prefixed);

            if (selectedLines.Length > 1)
                select(start, start + prefixed.Length);
        }

        private int findWordBoundary(int index, int direction)
        {
            string text = Current.Value;

            if (direction < 0)
            {
                while (index > 0 && char.IsWhiteSpace(text[index - 1]))
                    index--;
                while (index > 0 && !char.IsWhiteSpace(text[index - 1]))
                    index--;
            }
            else
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                    index++;
                while (index < text.Length && !char.IsWhiteSpace(text[index]))
                    index++;
            }

            return index;
        }

        private int lineStartOf(int index) => lines[lineOf(index)].Start;

        private int lineEndOf(int index)
        {
            int line = lineOf(index);
            var visualLine = lines[line];

            // stay on the line instead of going to the start of the next wrapped line.
            if (line < lines.Count - 1 && lines[line + 1].Start == visualLine.End && visualLine.End > visualLine.Start)
                return visualLine.End - 1;

            return visualLine.End;
        }

        private void moveVertically(int direction, bool selecting)
        {
            int line = lineOf(selectionEnd);
            float x = stickyX ??= positionOf(selectionEnd).X;

            int target = line + direction;
            int index;

            if (target < 0)
                index = 0;
            else if (target >= lines.Count)
                index = Current.Value.Length;
            else
                index = indexAt(target, x);

            select(selecting ? selectionStart : index, index);
        }

        #endregion

        #region Input

        public bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
        {
            if (!HasFocus)
                return false;

            int caretIndex = selectionEnd;

            if (e.Action is not (PlatformAction.MoveBackwardLine or PlatformAction.MoveForwardLine or PlatformAction.SelectBackwardLine or PlatformAction.SelectForwardLine))
                stickyX = null;

            switch (e.Action)
            {
                case PlatformAction.Cut:
                case PlatformAction.Copy:
                    if (selectionLeft != selectionRight)
                    {
                        clipboard.SetText(selectedText);

                        if (e.Action == PlatformAction.Cut)
                            Insert(string.Empty);
                    }

                    return true;

                case PlatformAction.Paste:
                    Insert(clipboard.GetText() ?? string.Empty);
                    return true;

                case PlatformAction.SelectAll:
                    select(0, Current.Value.Length);
                    return true;

                case PlatformAction.MoveBackwardChar:
                    moveCaret(selectionLeft != selectionRight ? selectionLeft : caretIndex - 1, false);
                    return true;

                case PlatformAction.MoveForwardChar:
                    moveCaret(selectionLeft != selectionRight ? selectionRight : caretIndex + 1, false);
                    return true;

                case PlatformAction.MoveBackwardWord:
                    moveCaret(findWordBoundary(caretIndex, -1), false);
                    return true;

                case PlatformAction.MoveForwardWord:
                    moveCaret(findWordBoundary(caretIndex, 1), false);
                    return true;

                case PlatformAction.MoveBackwardLine:
                    moveCaret(lineStartOf(caretIndex), false);
                    return true;

                case PlatformAction.MoveForwardLine:
                    moveCaret(lineEndOf(caretIndex), false);
                    return true;

                case PlatformAction.SelectBackwardChar:
                    moveCaret(caretIndex - 1, true);
                    return true;

                case PlatformAction.SelectForwardChar:
                    moveCaret(caretIndex + 1, true);
                    return true;

                case PlatformAction.SelectBackwardWord:
                    moveCaret(findWordBoundary(caretIndex, -1), true);
                    return true;

                case PlatformAction.SelectForwardWord:
                    moveCaret(findWordBoundary(caretIndex, 1), true);
                    return true;

                case PlatformAction.SelectBackwardLine:
                    moveCaret(lineStartOf(caretIndex), true);
                    return true;

                case PlatformAction.SelectForwardLine:
                    moveCaret(lineEndOf(caretIndex), true);
                    return true;

                case PlatformAction.DeleteBackwardChar:
                    if (selectionLeft != selectionRight)
                        Insert(string.Empty);
                    else
                        delete(caretIndex - 1, caretIndex);
                    return true;

                case PlatformAction.DeleteForwardChar:
                    if (selectionLeft != selectionRight)
                        Insert(string.Empty);
                    else
                        delete(caretIndex, caretIndex + 1);
                    return true;

                case PlatformAction.DeleteBackwardWord:
                    if (selectionLeft != selectionRight)
                        Insert(string.Empty);
                    else
                        delete(findWordBoundary(caretIndex, -1), caretIndex);
                    return true;

                case PlatformAction.DeleteForwardWord:
                    if (selectionLeft != selectionRight)
                        Insert(string.Empty);
                    else
                        delete(caretIndex, findWordBoundary(caretIndex, 1));
                    return true;

                case PlatformAction.DeleteBackwardLine:
                    delete(lineStartOf(caretIndex), caretIndex);
                    return true;

                case PlatformAction.DeleteForwardLine:
                    delete(caretIndex, lineEndOf(caretIndex));
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
        {
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!HasFocus)
                return false;

            switch (e.Key)
            {
                case Key.Escape:
                    if (!e.Repeat)
                        GetContainingFocusManager()?.ChangeFocus(null);
                    return true;

                case Key.Enter:
                case Key.KeypadEnter:
                    // alt-enter is commonly used to toggle fullscreen.
                    if (e.AltPressed)
                        return false;

                    if (e.ControlPressed)
                    {
                        if (!e.Repeat)
                            Committed?.Invoke();
                    }
                    else
                        Insert("\n");

                    return true;

                case Key.Up:
                    moveVertically(-1, e.ShiftPressed);
                    return true;

                case Key.Down:
                    moveVertically(1, e.ShiftPressed);
                    return true;

                case Key.B when e.ControlPressed && !e.AltPressed:
                    Wrap(@"**", @"**");
                    return true;

                case Key.I when e.ControlPressed && !e.AltPressed:
                    Wrap(@"*", @"*");
                    return true;

                case Key.K when e.ControlPressed && !e.AltPressed:
                    InsertLink();
                    return true;

                // these are handled as platform actions.
                case Key.BackSpace:
                case Key.Delete:
                case Key.Left:
                case Key.Right:
                case Key.Home:
                case Key.End:
                case Key.Tab:
                case Key.PageUp:
                case Key.PageDown:
                    return false;
            }

            if (e.Key >= Key.F1 && e.Key <= Key.F35)
                return false;

            // shortcuts like ctrl-c are platform actions, and everything else types, so isn't passed on to the editor.
            return !(e.ControlPressed || e.AltPressed || e.SuperPressed);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            stickyX = null;
            moveCaret(indexAt(e.ScreenSpaceMousePosition), e.ShiftPressed);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => e.Button == MouseButton.Left;

        protected override void OnDrag(DragEvent e)
        {
            moveCaret(indexAt(e.ScreenSpaceMousePosition), true);
        }

        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            int index = indexAt(e.ScreenSpaceMousePosition);
            select(findWordBoundary(Math.Min(index + 1, Current.Value.Length), -1), findWordBoundary(index, 1));
            return true;
        }

        protected override bool OnClick(ClickEvent e)
        {
            if (textInputBound)
                textInput.EnsureActivated(new TextInputProperties(TextInputType.Text, true));

            return true;
        }

        protected override void OnFocus(FocusEvent e)
        {
            base.OnFocus(e);

            background.FadeColour(backgroundFocused, 200, Easing.OutQuint);
            BorderThickness = 3;

            if (!textInputBound)
            {
                textInput.Activate(new TextInputProperties(TextInputType.Text, true), ScreenSpaceDrawQuad.AABBFloat);
                textInput.OnTextInput += onTextInput;
                textInput.OnImeResult += onImeResult;
                textInputBound = true;
            }
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            base.OnFocusLost(e);

            background.FadeColour(backgroundUnfocused, 200, Easing.OutQuint);
            BorderThickness = 0;

            unbindTextInput();
        }

        private void unbindTextInput()
        {
            if (!textInputBound)
                return;

            textInputBound = false;
            textInput.Deactivate();
            textInput.OnTextInput -= onTextInput;
            textInput.OnImeResult -= onImeResult;
        }

        // text input can arrive outside of the update thread.
        private void onTextInput(string text) => Schedule(() => insertTyped(text));

        private void onImeResult(string text) => Schedule(() => insertTyped(text));

        private void insertTyped(string text)
        {
            text = new string(text.Where(c => !char.IsControl(c)).ToArray());

            if (text.Length > 0 && HasFocus)
                Insert(text);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (textInput != null)
                unbindTextInput();

            base.Dispose(isDisposing);
        }

        #endregion
    }
}
