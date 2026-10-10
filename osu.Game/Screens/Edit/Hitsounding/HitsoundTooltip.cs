// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// A drawable whose tooltip is shown as a <see cref="HitsoundTooltip"/>, which highlights keywords of its text.
    /// </summary>
    public interface IHasHitsoundTooltip : IHasTooltip, IHasCustomTooltip<LocalisableString>
    {
        ITooltip<LocalisableString> IHasCustomTooltip<LocalisableString>.GetCustomTooltip() => new HitsoundTooltip();

        LocalisableString IHasCustomTooltip<LocalisableString>.TooltipContent => TooltipText;
    }

    /// <summary>
    /// A tooltip like the default one, which highlights keywords, such that longer explanations are easier to skim:
    /// the text before a colon at the start of a line in bold (e.g. the sounds in the hitsound guide), keys in the accent colour of the editor, banks in their colour,
    /// and mouse actions and values in semi-bold. Lines which only consist of a heading (e.g. "Which hitsounds ...:") are titles, which are greyed out instead.
    /// </summary>
    public partial class HitsoundTooltip : VisibilityContainer, ITooltip<LocalisableString>
    {
        private const float max_width = 500;

        /// <summary>
        /// Keys and key combinations, e.g. "Ctrl", "Shift+W/E/R", "Ctrl+C" or "Q/W/E/R".
        /// </summary>
        private const string key_pattern = @"\b(?:Ctrl|Shift|Alt)(?:\s*\+\s*(?:Ctrl|Shift|Alt|scroll|drag|click|[A-Z](?:/[A-Z])*\b))*|\b[QWER](?:/[QWER])+\b|\b(?:Escape|Enter)\b";

        private const string mouse_pattern = @"\b(?:[Ll]eft|[Rr]ight|[Dd]ouble)\s+(?:click|drag)\b|\b(?:[Cc]lick|[Dd]rag|[Ss]croll)\b";

        private const string bank_pattern = @"\b(?:Normal|Soft|Drum|normal|soft|drum)\b";

        private const string sample_pattern = @"\b(?:Hitnormal|Whistle|Finish|Clap|hitnormal|whistle|finish|clap)(?:es|s)?\b";

        private const string value_pattern = @"\b\d+(?:-\d+)?\s?(?:%|ms)";

        private static readonly Regex keyword_regex = new Regex(
            $@"(?<key>{key_pattern})|(?<mouse>{mouse_pattern})|(?<bank>{bank_pattern})|(?<sample>{sample_pattern})|(?<value>{value_pattern})",
            RegexOptions.Compiled);

        /// <summary>
        /// The text before a colon at the start of a line, e.g. "Volume area" or "Kick".
        /// </summary>
        private static readonly Regex heading_regex = new Regex(@"^([^:\n]{1,60}):(?=\s|$)", RegexOptions.Compiled);

        /// <summary>
        /// The accent colour of the editor.
        /// </summary>
        private static readonly Colour4 key_colour = new OverlayColourProvider(OverlayColourScheme.Aquamarine).Highlight1;

        private readonly Box background;
        private readonly TextFlowContainer text;

        private bool instantMovement = true;
        private LocalisableString? lastContent;

        public HitsoundTooltip()
        {
            AutoSizeAxes = Axes.Both;
            AutoSizeEasing = Easing.OutQuint;

            CornerRadius = 5;
            Masking = true;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = Color4.Black.Opacity(40),
                Radius = 5,
            };

            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.9f,
                },
                text = new TextFlowContainer
                {
                    Margin = new MarginPadding(5),
                    AutoSizeAxes = Axes.Both,
                    MaximumSize = new Vector2(max_width, float.PositiveInfinity),
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            background.Colour = colours.Gray3;
        }

        public void SetContent(LocalisableString content)
        {
            if (lastContent?.Equals(content) == true)
                return;

            AutoSizeDuration = IsPresent ? 250 : 0;

            text.Clear();
            AddFormattedText(text, content.ToString());

            lastContent = content;
        }

        /// <summary>
        /// Adds text to a text flow, highlighting its keywords.
        /// </summary>
        public static void AddFormattedText(TextFlowContainer flow, string content)
        {
            string[] lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');

                if (i > 0)
                    flow.NewParagraph();

                var heading = heading_regex.Match(line);

                if (heading.Success)
                {
                    bool isTitle = string.IsNullOrWhiteSpace(line[heading.Length..]);

                    if (isTitle)
                        addText(flow, heading.Value, Colour4.White.Opacity(0.6f), FontWeight.SemiBold);
                    else
                        addText(flow, heading.Value, Colour4.White, FontWeight.Bold);

                    line = line[heading.Length..];
                }

                addLine(flow, line);
            }
        }

        private static void addLine(TextFlowContainer flow, string line)
        {
            int position = 0;

            foreach (Match match in keyword_regex.Matches(line))
            {
                if (match.Index > position)
                    addPlain(flow, line[position..match.Index]);

                // only keys are bold, such that headings stand out the most. banks are recognisable by their colour, and samples only need to stand out slightly.
                if (match.Groups[@"key"].Success)
                    addText(flow, match.Value, key_colour, FontWeight.Bold);
                else if (match.Groups[@"bank"].Success)
                    addText(flow, match.Value, HitsoundLane.GetBankColour(getBank(match.Value)), FontWeight.Regular);
                else if (match.Groups[@"sample"].Success)
                    addText(flow, match.Value, Colour4.White, FontWeight.Regular);
                else
                    addText(flow, match.Value, Colour4.White, FontWeight.SemiBold);

                position = match.Index + match.Length;
            }

            if (position < line.Length)
                addPlain(flow, line[position..]);
        }

        private static string getBank(string word)
        {
            switch (word.ToLowerInvariant())
            {
                case HitSampleInfo.BANK_SOFT:
                    return HitSampleInfo.BANK_SOFT;

                case HitSampleInfo.BANK_DRUM:
                    return HitSampleInfo.BANK_DRUM;

                default:
                    return HitSampleInfo.BANK_NORMAL;
            }
        }

        /// <summary>
        /// Text which isn't highlighted is slightly dimmed, such that the highlighted text stands out.
        /// </summary>
        private static void addPlain(TextFlowContainer flow, string value) => addText(flow, value, Colour4.White.Opacity(0.8f), FontWeight.Regular);

        private static void addText(TextFlowContainer flow, string value, Colour4 colour, FontWeight weight)
        {
            flow.AddText(value, s =>
            {
                s.Font = OsuFont.GetFont(weight: weight);
                s.Colour = colour;
            });
        }

        protected override void PopIn()
        {
            instantMovement |= !IsPresent;
            this.FadeIn(300, Easing.OutQuint);
        }

        protected override void PopOut() => this.Delay(150).FadeOut(300, Easing.OutQuint);

        public void Move(Vector2 pos)
        {
            if (instantMovement)
            {
                Position = pos;
                instantMovement = false;
            }
            else
            {
                // like the default tooltip, it is moved every frame, so it can follow the cursor smoothly.
                Position = Interpolation.ValueAt(Time.Elapsed, Position, pos, 0, 120, Easing.OutQuint);
            }
        }
    }
}
