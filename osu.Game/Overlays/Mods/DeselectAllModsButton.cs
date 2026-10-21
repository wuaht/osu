// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Utils;
using osuTK.Input;

namespace osu.Game.Overlays.Mods
{
    public partial class DeselectAllModsButton : ShearedButton, IHasTooltip
    {
        private readonly Bindable<IReadOnlyList<Mod>> selectedMods = new Bindable<IReadOnlyList<Mod>>();
        private readonly Bindable<string> defaultMods = new Bindable<string>();

        private readonly ModSelectOverlay modSelectOverlay;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        public LocalisableString TooltipText => modSelectOverlay.AllowDefaultMods ? SlopSongSelectStrings.DeselectAllModsTooltip : default;

        public DeselectAllModsButton(ModSelectOverlay modSelectOverlay)
        {
            this.modSelectOverlay = modSelectOverlay;

            Width = ModSelectOverlay.BUTTON_WIDTH;

            Text = CommonStrings.DeselectAll;
            Action = deselectAll;

            selectedMods.BindTo(modSelectOverlay.SelectedMods);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            config.BindWith(OsuSetting.SlopDefaultMods, defaultMods);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            selectedMods.BindValueChanged(_ => updateEnabledState());
            defaultMods.BindValueChanged(_ => updateEnabledState());
            modSelectOverlay.Ruleset.BindValueChanged(_ => updateEnabledState(), true);
        }

        private void updateEnabledState()
        {
            Enabled.Value = selectedMods.Value.Any(m => m.Type != ModType.System) || getDefaultMods().Length > 0;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Right && modSelectOverlay.AllowDefaultMods && modSelectOverlay.Ruleset.Value is RulesetInfo ruleset)
            {
                // like osu!stable, right clicking stores the current mods as the default of the current ruleset.
                var mods = selectedMods.Value.Where(m => m.Type != ModType.System).ToArray();
                DefaultMods.Set(config, ruleset.ShortName, mods);

                notifications?.Post(new SimpleNotification
                {
                    Text = mods.Length > 0
                        ? SlopSongSelectStrings.DefaultModsSet(ruleset.Name, string.Join(' ', mods.Select(m => m.Acronym)))
                        : SlopSongSelectStrings.DefaultModsCleared(ruleset.Name),
                });

                return true;
            }

            return base.OnMouseDown(e);
        }

        private void deselectAll()
        {
            modSelectOverlay.DeselectAll();

            // like osu!stable, the default mods are selected again unless control is held.
            if (GetContainingInputManager()?.CurrentState.Keyboard.ControlPressed == true)
                return;

            var defaults = getDefaultMods();

            if (defaults.Length == 0 || selectedMods.Disabled)
                return;

            var mods = selectedMods.Value.ToList();

            foreach (var mod in defaults)
            {
                if (mods.All(m => m.GetType() != mod.GetType()) && ModUtils.CheckCompatibleSet(mods.Append(mod)))
                    mods.Add(mod);
            }

            selectedMods.Value = mods;
        }

        private Mod[] getDefaultMods()
        {
            if (!modSelectOverlay.AllowDefaultMods || modSelectOverlay.Ruleset.Value is not RulesetInfo ruleset || !ruleset.Available)
                return Array.Empty<Mod>();

            return DefaultMods.Get(defaultMods.Value, ruleset.CreateInstance());
        }
    }
}
