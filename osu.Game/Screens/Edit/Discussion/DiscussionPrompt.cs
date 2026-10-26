// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Screens.Edit.Discussion
{
    /// <summary>
    /// Shows a <see cref="DiscussionPrompt"/> above the discussion, such as for signing into the website or entering a reason.
    /// </summary>
    public partial class DiscussionPromptOverlay : Container
    {
        private readonly Container content;

        public DiscussionPrompt? Current { get; private set; }

        public DiscussionPromptOverlay()
        {
            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black.Opacity(0.6f),
                },
                content = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                },
            };
        }

        public void Show(DiscussionPrompt prompt)
        {
            Current?.Expire();

            Current = prompt;
            prompt.Closed += () =>
            {
                if (Current != prompt)
                    return;

                Current = null;
                this.FadeOut(200, Easing.OutQuint);
                prompt.Expire();
            };

            content.Add(prompt);
            this.FadeIn(200, Easing.OutQuint);
        }

        public override bool PropagatePositionalInputSubTree => Alpha > 0;
        public override bool PropagateNonPositionalInputSubTree => Alpha > 0;

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnClick(ClickEvent e)
        {
            // clicking outside closes the prompt, like a dialog.
            if (Current != null && !Current.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                Current.Close();

            return true;
        }

        protected override bool OnScroll(ScrollEvent e) => true;

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape && Current != null)
            {
                Current.Close();
                return true;
            }

            return base.OnKeyDown(e);
        }
    }

    /// <summary>
    /// A small form above the discussion, with a title, a description, inputs and buttons.
    /// </summary>
    public partial class DiscussionPrompt : CompositeDrawable
    {
        public event Action? Closed;

        protected readonly FillFlowContainer Inputs;

        private readonly FillFlowContainer buttons;
        private readonly LinkFlowContainer description;
        private readonly TruncatingSpriteText error;
        private readonly LoadingLayer loading;

        private readonly LocalisableString title;
        private readonly LocalisableString descriptionText;

        private bool busy;

        public DiscussionPrompt(LocalisableString title, LocalisableString description = default)
        {
            this.title = title;
            descriptionText = description;

            Width = 520;
            AutoSizeAxes = Axes.Y;
            Masking = true;
            CornerRadius = 10;

            InternalChildren = new Drawable[]
            {
                Background = new Box { RelativeSizeAxes = Axes.Both },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    Padding = new MarginPadding(20),
                    Children = new Drawable[]
                    {
                        new DiscussionTitle(title),
                        this.description = new LinkFlowContainer(s => s.Font = OsuFont.Default.With(size: 14))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                        Inputs = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                        },
                        error = new TruncatingSpriteText
                        {
                            RelativeSizeAxes = Axes.X,
                            Font = OsuFont.Default.With(size: 14, weight: FontWeight.SemiBold),
                            Colour = DiscussionDisplay.RED_1,
                            Alpha = 0,
                        },
                        buttons = new FillFlowContainer
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(8, 0),
                        },
                    }
                },
                loading = new LoadingLayer(true),
            };

            AddButton(WebCommonStrings.ButtonsCancel, Close, false);
        }

        protected Box Background { get; }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            Background.Colour = colourProvider.Background4;
            description.Colour = colourProvider.Content2;

            if (descriptionText != default)
                description.AddText(descriptionText);
            else
                description.Hide();
        }

        /// <summary>
        /// Adds a button, the first added one (after cancel) being the main one.
        /// </summary>
        public RoundedButton AddButton(LocalisableString text, Action action, bool primary = true)
        {
            var button = new RoundedButton
            {
                Width = 150,
                Height = 34,
                Text = text,
                Action = action,
            };

            if (primary)
                button.BackgroundColour = DiscussionDisplay.BLUE_DARKER;

            buttons.Add(button);
            return button;
        }

        public void ShowError(string? message)
        {
            error.Text = message ?? string.Empty;
            error.Alpha = string.IsNullOrEmpty(message) ? 0 : 1;
        }

        /// <summary>
        /// Runs a task while showing the prompt as busy, closing it once the task succeeded or showing why it failed.
        /// </summary>
        public void Run(Func<Task> task, Action? onSuccess = null, bool closeOnSuccess = true)
        {
            if (busy)
                return;

            busy = true;
            loading.Show();
            ShowError(null);

            Task.Run(task).ContinueWith(t => Schedule(() =>
            {
                busy = false;
                loading.Hide();

                if (t.IsCompletedSuccessfully)
                {
                    if (closeOnSuccess)
                        Close();

                    onSuccess?.Invoke();
                    return;
                }

                ShowError(t.Exception?.GetBaseException().Message);
            }));
        }

        public void Close() => Closed?.Invoke();

        public OsuTextBox AddTextBox(LocalisableString placeholder, string text = "", bool password = false, Action? onCommit = null)
        {
            var textBox = password ? new OsuPasswordTextBox() : new OsuTextBox();

            textBox.RelativeSizeAxes = Axes.X;
            textBox.Height = 36;
            textBox.PlaceholderText = placeholder;
            textBox.Text = text;
            textBox.LengthLimit = 10000;
            textBox.CommitOnFocusLost = false;

            if (onCommit != null)
                textBox.OnCommit += (_, newText) => onCommit();

            Inputs.Add(textBox);
            return textBox;
        }

        /// <summary>
        /// Adds a multi-line text box with a toolbar for formatting with markdown, for messages.
        /// </summary>
        public DiscussionTextEditor AddTextEditor(LocalisableString placeholder, string text = "", Action? onCommit = null)
        {
            var editor = new DiscussionTextEditor(placeholder, 5) { Text = text };

            if (onCommit != null)
                editor.Committed += onCommit;

            Inputs.Add(editor);
            return editor;
        }

        public Bindable<bool> AddCheckbox(LocalisableString label, bool value)
        {
            var checkbox = new OsuCheckbox
            {
                LabelText = label,
                Current = { Value = value },
            };

            Inputs.Add(checkbox);
            return checkbox.Current;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // focus the first input, such that typing works immediately.
            if (Inputs.OfType<DiscussionTextEditor>().FirstOrDefault() is DiscussionTextEditor editor)
                Schedule(editor.TakeFocus);
            else if (Inputs.OfType<OsuTextBox>().FirstOrDefault() is OsuTextBox textBox)
                Schedule(() => GetContainingFocusManager()?.ChangeFocus(textBox));
        }

        public override string ToString() => title.ToString();
    }

    /// <summary>
    /// Asks for a text, such as a reason.
    /// </summary>
    public partial class DiscussionTextPrompt : DiscussionPrompt
    {
        public DiscussionTextPrompt(LocalisableString title, LocalisableString description, LocalisableString placeholder, LocalisableString submit, Func<string, Task> onSubmit,
                                    string text = "", bool allowEmpty = false, Action? onSuccess = null, bool multiline = false)
            : base(title, description)
        {
            Func<string> getText = null!;

            void submitText()
            {
                string value = getText().Trim();

                if (!allowEmpty && value.Length == 0)
                {
                    ShowError(SlopDiscussionStrings.PromptEmpty.ToString());
                    return;
                }

                Run(() => onSubmit(value), onSuccess);
            }

            if (multiline)
            {
                var editor = AddTextEditor(placeholder, text, submitText);
                getText = () => editor.Text;
            }
            else
            {
                var textBox = AddTextBox(placeholder, text, onCommit: submitText);
                getText = () => textBox.Text;
            }

            AddButton(submit, submitText);
        }
    }

    /// <summary>
    /// Asks to choose some of a few options, such as the rulesets to nominate for.
    /// </summary>
    public partial class DiscussionChoicePrompt : DiscussionPrompt
    {
        public DiscussionChoicePrompt(LocalisableString title, LocalisableString description, IEnumerable<(string Key, LocalisableString Label, bool Selected)> options, LocalisableString submit,
                                      Func<string[], Task> onSubmit, Action? onSuccess = null)
            : base(title, description)
        {
            var choices = options.Select(o => (o.Key, Selected: AddCheckbox(o.Label, o.Selected))).ToList();

            AddButton(submit, () =>
            {
                string[] selected = choices.Where(c => c.Selected.Value).Select(c => c.Key).ToArray();

                if (selected.Length == 0)
                {
                    ShowError(SlopDiscussionStrings.PromptNothingSelected.ToString());
                    return;
                }

                Run(() => onSubmit(selected), onSuccess);
            });
        }
    }
}
