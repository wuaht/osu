// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    public partial class RulesetEditorBindingsSubsection : KeyBindingsSubsection
    {
        protected override LocalisableString Header { get; }

        public RulesetInfo Ruleset { get; }

        private const int variant = Rulesets.Ruleset.EDITOR_VARIANT;

        /// <param name="ruleset">The ruleset.</param>
        /// <param name="slop">Whether to list the editor actions which are exclusive to this client instead of all others.</param>
        public RulesetEditorBindingsSubsection(RulesetInfo ruleset, bool slop = false)
        {
            Ruleset = ruleset;

            var rulesetInstance = ruleset.CreateInstance();
            var slopActions = rulesetInstance.GetSlopEditorKeyBindings().Select(b => b.Action).ToHashSet();

            Header = ruleset.Name;
            Defaults = rulesetInstance.GetDefaultKeyBindings(variant).Where(b => slopActions.Contains(b.Action) == slop);
        }

        /// <summary>
        /// Whether this subsection lists any key bindings.
        /// </summary>
        public bool HasKeyBindings => Defaults.Any();

        protected override IEnumerable<RealmKeyBinding> GetKeyBindings(Realm realm)
        {
            string rulesetName = Ruleset.ShortName;

            return realm.All<RealmKeyBinding>()
                        .Where(b => b.RulesetName == rulesetName && b.Variant == variant);
        }

        protected override KeyBindingRow CreateKeyBindingRow(object action, IEnumerable<KeyBinding> defaults)
            => new KeyBindingRow(action)
            {
                AllowMainMouseButtons = true,
                Defaults = defaults.Select(d => d.KeyCombination),
            };
    }
}
