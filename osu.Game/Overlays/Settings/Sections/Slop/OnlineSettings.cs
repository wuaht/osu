// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.BeatmapMirrors;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class OnlineSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.OnlineHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "mirror", "download", "beatmap", "update" });

        [Resolved]
        private BeatmapOwnerStore ownerStore { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        private SettingsButtonV2 clearMappersButton = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
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
            };
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
