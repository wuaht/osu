// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit
{
    internal class AutoplayCursorOpacityMenuItem : MenuItem
    {
        private readonly Bindable<float> opacity;

        private readonly Dictionary<float, TernaryStateRadioMenuItem> menuItemLookup = new Dictionary<float, TernaryStateRadioMenuItem>();

        public AutoplayCursorOpacityMenuItem(Bindable<float> opacity)
            : base(SlopSettingsStrings.EditorAutoplayCursorOpacity)
        {
            Items = new[]
            {
                createMenuItem(0.2f),
                createMenuItem(0.4f),
                createMenuItem(0.6f),
                createMenuItem(0.8f),
                createMenuItem(1),
            };

            this.opacity = opacity;
            opacity.BindValueChanged(o =>
            {
                foreach (var kvp in menuItemLookup)
                    kvp.Value.State.Value = kvp.Key == o.NewValue ? TernaryState.True : TernaryState.False;
            }, true);
        }

        private TernaryStateRadioMenuItem createMenuItem(float value)
        {
            var item = new TernaryStateRadioMenuItem($"{Math.Round(value * 100)}%", MenuItemType.Standard, _ => opacity.Value = value);
            menuItemLookup[value] = item;
            return item;
        }
    }
}
