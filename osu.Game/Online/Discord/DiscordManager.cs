// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Online.Discord.Full;
using osu.Game.Online.Discord.Sdk;

namespace osu.Game.Online.Discord
{
    /// <summary>
    /// Provides the Discord backend selected in the settings.
    /// </summary>
    public partial class DiscordManager : CompositeComponent
    {
        public IBindable<DiscordClient?> Client => client;

        private readonly Bindable<DiscordClient?> client = new Bindable<DiscordClient?>();

        private Bindable<DiscordClientMode> mode = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            mode = config.GetBindable<DiscordClientMode>(OsuSetting.SlopDiscordMode);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            mode.BindValueChanged(m =>
            {
                var previous = client.Value;

                DiscordClient next = m.NewValue == DiscordClientMode.FullClient ? new DiscordFullClient() : new DiscordSocialSdkClient();
                AddInternal(next);

                client.Value = next;

                if (previous != null)
                    RemoveInternal(previous, true);
            }, true);
        }
    }
}
