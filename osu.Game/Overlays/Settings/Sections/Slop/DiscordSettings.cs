// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Discord;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class DiscordSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.DiscordHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "discord", "chat", "dm", "server", "frosted", "blur", "opacity" });

        [Resolved]
        private DiscordManager? discordManager { get; set; }

        private readonly IBindable<DiscordClient?> client = new Bindable<DiscordClient?>();
        private readonly Bindable<DiscordConnectionState> state = new Bindable<DiscordConnectionState>();
        private readonly Bindable<DiscordUser?> localUser = new Bindable<DiscordUser?>();

        private SettingsButtonV2 logoutButton = null!;

        private readonly Bindable<DiscordClientMode> mode = new Bindable<DiscordClientMode>();
        private readonly Bindable<SettingsNote.Data?> modeNote = new Bindable<SettingsNote.Data?>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopDiscordMode, mode);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<DiscordClientMode>
                {
                    Caption = SlopSettingsStrings.DiscordMode,
                    HintText = SlopSettingsStrings.DiscordModeDescription,
                    Current = config.GetBindable<DiscordClientMode>(OsuSetting.SlopDiscordMode),
                })
                {
                    Note = { BindTarget = modeNote },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = SlopSettingsStrings.DiscordFrosted,
                    HintText = SlopSettingsStrings.DiscordFrostedDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopDiscordFrosted),
                }),
                new SettingsItemV2(new FormSliderBar<float>
                {
                    Caption = SlopSettingsStrings.DiscordBackgroundOpacity,
                    Current = config.GetBindable<float>(OsuSetting.SlopDiscordBackgroundOpacity),
                    KeyboardStep = 0.01f,
                    DisplayAsPercentage = true,
                }),
                logoutButton = new DangerousSettingsButtonV2
                {
                    Action = () => client.Value?.Logout(),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (discordManager != null)
                client.BindTo(discordManager.Client);

            client.BindValueChanged(c =>
            {
                state.UnbindBindings();
                localUser.UnbindBindings();

                if (c.NewValue != null)
                {
                    state.BindTo(c.NewValue.State);
                    localUser.BindTo(c.NewValue.LocalUser);
                }

                updateLogoutButton();
            }, true);

            state.BindValueChanged(_ => updateLogoutButton());

            mode.BindValueChanged(m => modeNote.Value = m.NewValue == DiscordClientMode.FullClient
                ? new SettingsNote.Data(SlopDiscordStrings.FullClientWarning, SettingsNote.Type.Critical)
                : null, true);
            localUser.BindValueChanged(_ => updateLogoutButton(), true);
        }

        private void updateLogoutButton()
        {
            bool loggedIn = state.Value == DiscordConnectionState.Ready || state.Value == DiscordConnectionState.Connecting;

            logoutButton.Enabled.Value = loggedIn;
            logoutButton.Text = loggedIn
                ? SlopSettingsStrings.DiscordLogout(localUser.Value?.DisplayName ?? @"?")
                : SlopSettingsStrings.DiscordNotLoggedIn;
        }
    }
}
