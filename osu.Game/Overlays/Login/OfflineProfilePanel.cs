// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Overlays.OfflineProfiles;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Slop;
using osuTK;

namespace osu.Game.Overlays.Login
{
    /// <summary>
    /// Selecting an <see cref="OfflineProfile"/> to sign in with while not logged in.
    /// </summary>
    public partial class OfflineProfilePanel : FillFlowContainer
    {
        public Action? RequestHide;

        [Resolved]
        private SettingsOverlay? settings { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0f, SettingsSection.ITEM_SPACING);
            Margin = new MarginPadding { Top = SettingsSection.ITEM_SPACING };

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0f, SettingsSection.ITEM_SPACING),
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = OfflineProfileStrings.Header.ToUpper(),
                            Font = OsuFont.GetFont(weight: FontWeight.Bold),
                        },
                        new OfflineProfileDropdown
                        {
                            Caption = OfflineProfileStrings.PlayAs,
                            HintText = OfflineProfileStrings.PlayAsDescription,
                        },
                    },
                },
                new SettingsButton
                {
                    Text = OfflineProfileStrings.ManageProfiles,
                    Action = () =>
                    {
                        RequestHide?.Invoke();
                        settings?.ShowAtControl<OfflineProfileSettings>();
                    },
                },
            };
        }
    }
}
