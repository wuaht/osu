// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.OSD;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.MapsetVerifier.UI
{
    /// <summary>
    /// A centred message, e.g. when there is nothing to show.
    /// </summary>
    public partial class VerifierMessage : FillFlowContainer
    {
        private const float max_description_width = 480;

        private readonly SpriteIcon icon;
        private readonly OsuSpriteText title;
        private readonly OsuTextFlowContainer description;

        public VerifierMessage(IconUsage icon = default, LocalisableString title = default, LocalisableString description = default)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 8);
            Padding = new MarginPadding(20);

            Children = new Drawable[]
            {
                this.icon = new SpriteIcon
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Size = new Vector2(36),
                    Alpha = 0.6f,
                },
                this.title = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                },
                this.description = new OsuTextFlowContainer(s => s.Font = OsuFont.Default.With(size: 14))
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Y,
                    TextAnchor = Anchor.TopCentre,
                    Alpha = 0.7f,
                },
            };

            Set(icon, title, description);
        }

        protected override void Update()
        {
            base.Update();

            // long descriptions read as a centred block rather than a single line across the whole panel.
            description.Width = Math.Min(max_description_width, ChildSize.X);
        }

        public void Set(IconUsage icon, LocalisableString title, LocalisableString description)
        {
            this.icon.Icon = icon;
            this.icon.Alpha = icon.Equals(default(IconUsage)) ? 0 : 0.6f;
            this.title.Text = title;
            this.description.Text = description;
        }
    }

    /// <summary>
    /// The title of a panel.
    /// </summary>
    public partial class VerifierTitle : OsuSpriteText
    {
        public VerifierTitle(LocalisableString text, float size = 18)
        {
            Text = text;
            Font = OsuFont.Default.With(size: size, weight: FontWeight.Bold);
        }
    }

    /// <summary>
    /// A small header inside a panel.
    /// </summary>
    public partial class VerifierSubtitle : OsuSpriteText
    {
        public VerifierSubtitle(LocalisableString text)
        {
            Text = text;
            Font = OsuFont.Default.With(size: 12, weight: FontWeight.SemiBold);
            Alpha = 0.7f;
        }
    }

    /// <summary>
    /// The message of an issue, in which editor timestamps can be clicked to show them in the compose screen.
    /// </summary>
    public partial class IssueTextFlow : LinkFlowContainer
    {
        [Resolved]
        private VerifierScreen screen { get; set; } = null!;

        [Resolved]
        private Clipboard clipboard { get; set; } = null!;

        [Resolved]
        private OnScreenDisplay? onScreenDisplay { get; set; }

        private readonly string message;
        private readonly string category;

        /// <param name="message">The message of the issue.</param>
        /// <param name="category">The category of the issue, which decides the difficulty timestamps are shown in.</param>
        /// <param name="size">The size of the text.</param>
        public IssueTextFlow(string message, string category, float size = 14)
            : base(s => s.Font = OsuFont.Default.With(size: size))
        {
            this.message = message;
            this.category = category;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            int position = 0;

            foreach (var match in VerifierDisplay.FindTimestamps(message))
            {
                if (match.Index > position)
                    AddText(VerifierDisplay.FormatNumbers(message[position..match.Index]));

                string timestamp = match.Timestamp;

                AddLink(match.Text, () => onTimestampClicked(timestamp), VerifierStrings.TimestampTooltip.ToString(),
                    s => s.Font = s.Font.With(weight: FontWeight.SemiBold));

                position = match.Index + match.Text.Length;
            }

            if (position < message.Length)
                AddText(VerifierDisplay.FormatNumbers(message[position..]));
        }

        private void onTimestampClicked(string timestamp)
        {
            var inputManager = GetContainingInputManager();

            if (inputManager?.CurrentState.Keyboard.ControlPressed == true)
            {
                clipboard.SetText(timestamp);
                onScreenDisplay?.Display(new CopiedToClipboardToast());
                return;
            }

            screen.ShowTimestamp(timestamp, category);
        }

        protected override DrawableLinkCompiler CreateLinkCompiler(ITextPart textPart) => new DrawableLinkCompiler(textPart)
        {
            IdleColour = VerifierDisplay.TIMESTAMP,
        };
    }

    /// <summary>
    /// An editor timestamp in the message of an issue.
    /// </summary>
    /// <param name="Index">Where the timestamp starts in the message.</param>
    /// <param name="Text">The timestamp as it is written in the message.</param>
    /// <param name="Timestamp">The timestamp which the editor understands.</param>
    public readonly record struct TimestampMatch(int Index, string Text, string Timestamp);
}
