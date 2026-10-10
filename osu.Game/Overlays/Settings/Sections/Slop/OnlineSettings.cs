// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.BeatmapMirrors;
using osu.Game.Online.BnTracker;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class OnlineSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.OnlineHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "mirror", "download", "beatmap", "update", "bn", "nominator", "tracker", "request" });

        [Resolved]
        private BeatmapOwnerStore ownerStore { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private BnTrackerClient bnTracker { get; set; } = null!;

        private SettingsButtonV2 clearMappersButton = null!;
        private SettingsButtonV2 bnTrackerSignOutButton = null!;

        private Bindable<string> bnTrackerServer = null!;

        /// <summary>
        /// The text of the server text box, which is only applied when committing, as changing the server signs out.
        /// </summary>
        private readonly Bindable<string> bnTrackerServerText = new Bindable<string>(string.Empty);

        private readonly IBindable<BnMe?> bnTrackerUser = new Bindable<BnMe?>();
        private readonly IBindable<bool> bnTrackerSignedIn = new Bindable<bool>();

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            bnTrackerServer = config.GetBindable<string>(OsuSetting.SlopBnTrackerServer);

            FormTextBox serverTextBox;

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<BeatmapMirror>
                {
                    Caption = SlopSettingsStrings.BeatmapMirror,
                    HintText = SlopSettingsStrings.BeatmapMirrorDescription,
                    Current = config.GetBindable<BeatmapMirror>(OsuSetting.SlopBeatmapMirror),
                }),
                clearMappersButton = new DangerousSettingsButtonV2
                {
                    Text = SlopSettingsStrings.ClearStoredMappers,
                    Action = clearMappers,
                },
                new SettingsItemV2(serverTextBox = new FormTextBox
                {
                    Caption = SlopSettingsStrings.BnTrackerServer,
                    HintText = SlopSettingsStrings.BnTrackerServerDescription,
                    PlaceholderText = BnTrackerClient.DEFAULT_SERVER,
                    Current = bnTrackerServerText,
                }),
                bnTrackerSignOutButton = new SettingsButtonV2
                {
                    Action = () => logFailure(bnTracker.SignOutAsync()),
                },
            };

            serverTextBox.OnCommit += (_, _) =>
            {
                string address = bnTrackerServerText.Value.Trim();

                // an empty address goes back to the default server.
                if (address.Length == 0)
                    bnTrackerServer.SetDefault();
                else
                    bnTrackerServer.Value = address;

                bnTrackerServerText.Value = bnTrackerServer.Value;
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            bnTrackerServer.BindValueChanged(s => bnTrackerServerText.Value = s.NewValue, true);

            bnTrackerUser.BindTo(bnTracker.User);
            bnTrackerSignedIn.BindTo(bnTracker.IsSignedIn);

            bnTrackerUser.BindValueChanged(_ => updateBnTrackerAccount());
            bnTrackerSignedIn.BindValueChanged(signedIn =>
            {
                // the account isn't known yet after starting the game.
                if (signedIn.NewValue && bnTrackerUser.Value == null)
                    logFailure(bnTracker.GetMeAsync());

                updateBnTrackerAccount();
            }, true);
        }

        private static void logFailure(Task task) => task.ContinueWith(t => Logger.Log($@"BN Tracker request failed: {t.Exception?.GetBaseException().Message}"), TaskContinuationOptions.OnlyOnFaulted);

        private void updateBnTrackerAccount()
        {
            bnTrackerSignOutButton.Enabled.Value = bnTrackerSignedIn.Value;

            if (!bnTrackerSignedIn.Value)
                bnTrackerSignOutButton.Text = SlopSettingsStrings.BnTrackerNotSignedIn;
            else if (bnTrackerUser.Value?.Username is string username)
                bnTrackerSignOutButton.Text = SlopSettingsStrings.BnTrackerSignOut(username);
            else
                bnTrackerSignOutButton.Text = SlopSettingsStrings.BnTrackerSignOutUnknownUser;
        }

        private void clearMappers()
        {
            clearMappersButton.Enabled.Value = false;

            ownerStore.ClearAsync().ContinueWith(_ => Schedule(() =>
            {
                clearMappersButton.Enabled.Value = true;
                notifications?.Post(new SimpleNotification { Text = SlopSettingsStrings.StoredMappersCleared });
            }));
        }
    }
}
