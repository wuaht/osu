// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.OfflineProfiles;

namespace osu.Game.Overlays.OfflineProfiles
{
    /// <summary>
    /// Selects the active <see cref="OfflineProfile"/> (or a guest).
    /// </summary>
    public partial class OfflineProfileDropdown : FormDropdown<OfflineProfileDropdown.Option>
    {
        /// <summary>
        /// An item of the dropdown, which is either a profile or a guest.
        /// </summary>
        public record Option(OfflineProfile? Profile);

        [Resolved]
        private OfflineProfileManager manager { get; set; } = null!;

        private readonly IBindableList<OfflineProfile> profiles = new BindableList<OfflineProfile>();
        private readonly IBindable<OfflineProfile?> activeProfile = new Bindable<OfflineProfile?>();

        /// <summary>
        /// Whether the current value is being updated from the manager, in which case it shouldn't be applied to the manager.
        /// </summary>
        private bool updatingFromManager;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            profiles.BindTo(manager.Profiles);
            activeProfile.BindTo(manager.ActiveProfile);

            profiles.BindCollectionChanged((_, _) => updateItems(), true);
            activeProfile.BindValueChanged(_ => updateCurrent(), true);

            manager.ProfileChanged += onProfileChanged;

            Current.BindValueChanged(option =>
            {
                if (!updatingFromManager)
                    manager.SetActiveProfile(option.NewValue.Profile);
            });
        }

        // usernames may have changed.
        private void onProfileChanged(OfflineProfile profile) => Schedule(updateItems);

        private void updateItems()
        {
            updatingFromManager = true;

            Items = new[] { new Option(null) }.Concat(profiles.Select(p => new Option(p))).ToArray();
            updateCurrent();

            updatingFromManager = false;
        }

        private void updateCurrent()
        {
            bool wasUpdating = updatingFromManager;
            updatingFromManager = true;

            Current.Value = new Option(activeProfile.Value);

            updatingFromManager = wasUpdating;
        }

        protected override LocalisableString GenerateItemText(Option item) => item.Profile?.Username ?? OfflineProfileStrings.Guest;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (manager.IsNotNull())
                manager.ProfileChanged -= onProfileChanged;
        }
    }
}
