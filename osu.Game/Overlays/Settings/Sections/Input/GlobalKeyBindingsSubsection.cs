// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Input
{
    public partial class GlobalKeyBindingsSubsection : KeyBindingsSubsection
    {
        protected override LocalisableString Header { get; }

        private readonly GlobalActionCategory category;
        private readonly GlobalActionCategory[] conflictingCategories;

        /// <param name="header">The header of the subsection.</param>
        /// <param name="category">The category of the actions to list.</param>
        /// <param name="conflictingCategories">Further categories which are active at the same time, whose bindings are checked for conflicts as well.</param>
        public GlobalKeyBindingsSubsection(LocalisableString header, GlobalActionCategory category, params GlobalActionCategory[] conflictingCategories)
        {
            Header = header;
            this.category = category;
            this.conflictingCategories = conflictingCategories;
            Defaults = GlobalActionContainer.GetDefaultBindingsFor(category);
        }

        protected override IEnumerable<RealmKeyBinding> GetKeyBindings(Realm realm)
        {
            var bindings = realm.All<RealmKeyBinding>()
                                .Where(b => b.RulesetName == null && b.Variant == null)
                                .AsEnumerable().Detach();

            var actionsInSection = conflictingCategories.Prepend(category).SelectMany(GlobalActionContainer.GetGlobalActionsFor).Cast<int>().ToHashSet();
            return bindings.Where(kb => actionsInSection.Contains(kb.ActionInt));
        }
    }
}
