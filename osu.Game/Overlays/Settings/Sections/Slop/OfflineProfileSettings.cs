// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.OfflineProfiles;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.OfflineProfiles;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    /// <summary>
    /// Creating, selecting and editing <see cref="OfflineProfile"/>s.
    /// </summary>
    public partial class OfflineProfileSettings : SettingsSubsection
    {
        protected override LocalisableString Header => OfflineProfileStrings.Header;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "profile", "offline", "account", "user", "avatar", "banner", "unranked", "pp" });

        [Resolved]
        private OfflineProfileManager manager { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private UserProfileOverlay? profileOverlay { get; set; }

        private readonly IBindable<OfflineProfile?> activeProfile = new Bindable<OfflineProfile?>();

        private FormTextBox newUsername = null!;
        private FillFlowContainer activeProfileSettings = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new SettingsItemV2(new OfflineProfileDropdown
                {
                    Caption = OfflineProfileStrings.PlayAs,
                    HintText = OfflineProfileStrings.PlayAsDescription,
                }),
                new SettingsItemV2(newUsername = new FormTextBox
                {
                    Caption = OfflineProfileStrings.NewProfile,
                    PlaceholderText = OfflineProfileStrings.Username,
                }),
                new SettingsButtonV2
                {
                    Text = OfflineProfileStrings.CreateProfile,
                    Action = createProfile,
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = OfflineProfileStrings.IncludeUnrankedBeatmaps,
                    HintText = OfflineProfileStrings.IncludeUnrankedBeatmapsDescription,
                    Current = config.GetBindable<bool>(OsuSetting.SlopOfflineProfilesIncludeUnranked),
                }),
                activeProfileSettings = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            activeProfile.BindTo(manager.ActiveProfile);
            activeProfile.BindValueChanged(_ => recreateActiveProfileSettings(), true);
        }

        private void createProfile()
        {
            var profile = manager.Create(newUsername.Current.Value, out string? error);

            if (profile == null)
            {
                showError(error);
                return;
            }

            newUsername.Current.Value = string.Empty;
            manager.SetActiveProfile(profile);
        }

        /// <summary>
        /// Creates the settings of the active profile.
        /// </summary>
        private void recreateActiveProfileSettings()
        {
            activeProfileSettings.Clear();

            var profile = activeProfile.Value;

            if (profile == null)
                return;

            FormTextBox username;
            FormEnumDropdown<CountryCode> country;
            FormFileSelector avatar;
            FormFileSelector cover;

            activeProfileSettings.AddRange(new Drawable[]
            {
                new SettingsItemV2(username = new FormTextBox
                {
                    Caption = OfflineProfileStrings.Username,
                    Current = { Value = profile.Username },
                }),
                new SettingsItemV2(country = new FormEnumDropdown<CountryCode>
                {
                    Caption = OfflineProfileStrings.Country,
                    Current = { Value = profile.CountryCode },
                }),
                // file selectors aren't supported by SettingsItemV2, so they are padded the same way.
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = avatar = new FormFileSelector(@".png", @".jpg", @".jpeg")
                    {
                        Caption = OfflineProfileStrings.Avatar,
                        AllowClear = true,
                        Current = { Value = toFileInfo(manager.GetAvatarPath(profile)) },
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = cover = new FormFileSelector(@".png", @".jpg", @".jpeg")
                    {
                        Caption = OfflineProfileStrings.Banner,
                        AllowClear = true,
                        Current = { Value = toFileInfo(manager.GetCoverPath(profile)) },
                    },
                },
                new SettingsButtonV2
                {
                    Text = OfflineProfileStrings.ViewProfile,
                    Action = () => profileOverlay?.ShowUser(profile.CreateUser()),
                },
                new DangerousSettingsButtonV2
                {
                    Text = OfflineProfileStrings.DeleteProfile,
                    Action = () => dialogOverlay?.Push(new DeleteOfflineProfileDialog(profile, () => manager.Delete(profile))),
                },
            });

            username.OnCommit += (_, _) =>
            {
                string? error = manager.Rename(profile, username.Current.Value);

                if (error != null)
                {
                    showError(error);
                    username.Current.Value = profile.Username;
                }
            };

            country.Current.BindValueChanged(c => manager.SetCountry(profile, c.NewValue));

            avatar.Current.BindValueChanged(file => applyImage(file.NewValue, path => manager.SetAvatar(profile, path), () => manager.ClearAvatar(profile)));
            cover.Current.BindValueChanged(file => applyImage(file.NewValue, path => manager.SetCover(profile, path), () => manager.ClearCover(profile)));
        }

        private void applyImage(FileInfo? file, Action<string> set, Action clear)
        {
            if (file == null)
            {
                clear();
                return;
            }

            try
            {
                set(file.FullName);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to set image {file.FullName}");
                showError(e.Message);
            }
        }

        private static FileInfo? toFileInfo(string? path) => path == null ? null : new FileInfo(path);

        private void showError(string? error) => notifications?.Post(new SimpleErrorNotification { Text = error ?? string.Empty });

        private partial class DeleteOfflineProfileDialog : DeletionDialog
        {
            public DeleteOfflineProfileDialog(OfflineProfile profile, Action delete)
            {
                BodyText = OfflineProfileStrings.DeleteProfileConfirmation(profile.Username);
                DangerousAction = delete;
            }
        }
    }
}
