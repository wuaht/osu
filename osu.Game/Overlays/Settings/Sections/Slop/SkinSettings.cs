// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Skinning;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Slop
{
    public partial class SkinSettings : SettingsSubsection
    {
        protected override LocalisableString Header => SlopSettingsStrings.SkinsHeader;

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "taiko", "catch", "mania", "ruleset" });

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        private readonly List<RulesetSkinDropdown> dropdowns = new List<RulesetSkinDropdown>();

        private IDisposable? realmSubscription;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                createDropdown(SlopSettingsStrings.TaikoSkin, config.GetBindable<string>(OsuSetting.SlopTaikoSkin)),
                createDropdown(SlopSettingsStrings.CatchSkin, config.GetBindable<string>(OsuSetting.SlopCatchSkin)),
                createDropdown(SlopSettingsStrings.ManiaSkin, config.GetBindable<string>(OsuSetting.SlopManiaSkin)),
            };
        }

        private Drawable createDropdown(LocalisableString caption, Bindable<string> current)
        {
            var dropdown = new RulesetSkinDropdown
            {
                Caption = caption,
                Current = current,
            };

            dropdowns.Add(dropdown);
            return new SettingsItemV2(dropdown);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            realmSubscription = realm.RegisterForNotifications(r => r.All<SkinInfo>()
                                                                     .Where(s => !s.DeletePending)
                                                                     .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase), skinsChanged);
        }

        private void skinsChanged(IRealmCollection<SkinInfo> sender, ChangeSet? changes)
        {
            // This can only mean that realm is recycling.
            if (!sender.Any())
                return;

            var names = new Dictionary<string, string> { [string.Empty] = string.Empty };

            // Protected skins come first, matching the main skin dropdown.
            foreach (var skin in sender.Where(s => s.Protected).Concat(sender.Where(s => !s.Protected)))
                names.TryAdd(skin.ID.ToString(), skin.ToString());

            Schedule(() =>
            {
                foreach (var dropdown in dropdowns)
                    dropdown.SetSkins(names);
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            realmSubscription?.Dispose();
        }

        private partial class RulesetSkinDropdown : FormDropdown<string>
        {
            private Dictionary<string, string> names = new Dictionary<string, string>();

            public void SetSkins(Dictionary<string, string> skins)
            {
                names = skins;

                // A skin which no longer exists falls back to the main skin.
                if (!names.ContainsKey(Current.Value))
                    Current.Value = string.Empty;

                Items = names.Keys.ToList();
            }

            protected override LocalisableString GenerateItemText(string item)
            {
                if (string.IsNullOrEmpty(item))
                    return SlopSettingsStrings.UseMainSkin;

                return names.TryGetValue(item, out string? name) ? name : item;
            }
        }
    }
}
