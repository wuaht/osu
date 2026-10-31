// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Localisation;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// Shows the tooltips of the Discord overlay, in which unicode emoji (such as in names or reactions) are shown as images.
    /// </summary>
    public partial class DiscordTooltipContainer : OsuTooltipContainer
    {
        private const float emoji_size = 16;

        public DiscordTooltipContainer(CursorContainer? cursor)
            : base(cursor!)
        {
        }

        protected override ITooltip CreateTooltip() => new DiscordTooltip();

        private partial class DiscordTooltip : OsuTooltip
        {
            [Resolved]
            private LocalisationManager localisation { get; set; } = null!;

            protected override void SetText(OsuTextFlowContainer text, LocalisableString content)
            {
                text.Clear();
                text.AddTextWithEmoji(localisation.GetLocalisedString(content), emoji_size);
            }
        }
    }
}
