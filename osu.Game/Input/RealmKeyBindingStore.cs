// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Database;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Rulesets;
using Realms;

namespace osu.Game.Input
{
    public class RealmKeyBindingStore
    {
        private readonly RealmAccess realm;
        private readonly ReadableKeyCombinationProvider keyCombinationProvider;

        public RealmKeyBindingStore(RealmAccess realm, ReadableKeyCombinationProvider keyCombinationProvider)
        {
            this.realm = realm;
            this.keyCombinationProvider = keyCombinationProvider;
        }

        /// <summary>
        /// For a given <see cref="GlobalAction"/>, return a human-readable string representing the bindings bound to the action.
        /// </summary>
        public LocalisableString GetBindingsStringFor(GlobalAction globalAction)
        {
            var combinations = GetReadableKeyCombinationsFor(globalAction);

            if (combinations.Count == 0)
                return ToastStrings.NoKeyBound;

            return string.Join(" / ", combinations);
        }

        /// <summary>
        /// Retrieve all user-defined key combinations (in a format that can be displayed) for a specific action.
        /// </summary>
        /// <param name="globalAction">The action to lookup.</param>
        /// <returns>A set of display strings for all the user's key configuration for the action.</returns>
        public IReadOnlyList<string> GetReadableKeyCombinationsFor(GlobalAction globalAction)
        {
            List<string> combinations = new List<string>();

            realm.Run(context =>
            {
                foreach (var action in context.All<RealmKeyBinding>().Where(b => string.IsNullOrEmpty(b.RulesetName) && (GlobalAction)b.ActionInt == globalAction))
                {
                    string str = keyCombinationProvider.GetReadableString(action.KeyCombination);

                    // even if found, the readable string may be empty for an unbound action.
                    if (str.Length > 0)
                        combinations.Add(str);
                }
            });

            return combinations;
        }

        /// <summary>
        /// Retrieve all user-defined key combinations (in a format that can be displayed) for a specific ruleset action.
        /// </summary>
        /// <param name="ruleset">The <see cref="RulesetInfo.ShortName"/> of the ruleset.</param>
        /// <param name="variant">The ID of the key binding variant to look up.</param>
        /// <param name="action">The ID of the specific action to look up.</param>
        /// <returns></returns>
        public IReadOnlyList<string> GetReadableKeyCombinationsFor(string ruleset, int variant, int action)
        {
            List<string> combinations = new List<string>();

            realm.Run(context =>
            {
                foreach (var binding in context.All<RealmKeyBinding>().Where(b => b.RulesetName == ruleset && b.Variant == variant && b.ActionInt == action))
                {
                    string str = keyCombinationProvider.GetReadableString(binding.KeyCombination);

                    // even if found, the readable string may be empty for an unbound action.
                    if (str.Length > 0)
                        combinations.Add(str);
                }
            });

            return combinations;
        }

        /// <summary>
        /// Register all defaults for this store.
        /// </summary>
        /// <param name="container">The container to populate defaults from.</param>
        /// <param name="rulesets">The rulesets to populate defaults from.</param>
        public void Register(KeyBindingContainer container, IEnumerable<RulesetInfo> rulesets)
        {
            realm.Run(r =>
            {
                using (var transaction = r.BeginWrite())
                {
                    // intentionally flattened to a list rather than querying against the IQueryable, as nullable fields being queried against aren't indexed.
                    // this is much faster as a result.
                    var existingBindings = r.All<RealmKeyBinding>().ToList();

                    insertDefaults(r, existingBindings, container.DefaultKeyBindings);

                    foreach (var ruleset in rulesets)
                    {
                        var instance = ruleset.CreateInstance();
                        foreach (int variant in instance.AllVariants)
                            insertDefaults(r, existingBindings, instance.GetDefaultKeyBindings(variant), ruleset.ShortName, variant);
                    }

                    transaction.Commit();
                }
            });
        }

        /// <summary>
        /// Swaps the key bindings of two actions, but only if each action has exactly one binding and they match the given combinations.
        /// Used to migrate changed defaults without touching bindings the user customised.
        /// </summary>
        /// <param name="rulesetName">The short name of the ruleset the actions belong to.</param>
        /// <param name="variant">The variant the actions belong to.</param>
        /// <param name="actionA">The first action, expected to be bound to <paramref name="oldA"/>.</param>
        /// <param name="oldA">The combination <paramref name="actionA"/> is expected to be bound to.</param>
        /// <param name="actionB">The second action, expected to be bound to <paramref name="oldB"/>.</param>
        /// <param name="oldB">The combination <paramref name="actionB"/> is expected to be bound to.</param>
        /// <returns>Whether the bindings were swapped.</returns>
        public bool SwapBindingsIfUnchanged(string rulesetName, int variant, object actionA, KeyCombination oldA, object actionB, KeyCombination oldB)
        {
            return realm.Write(r =>
            {
                var bindings = r.All<RealmKeyBinding>().Where(b => b.RulesetName == rulesetName && b.Variant == variant).ToList();
                var bindingsA = bindings.Where(b => b.ActionInt == (int)actionA).ToList();
                var bindingsB = bindings.Where(b => b.ActionInt == (int)actionB).ToList();

                if (bindingsA.Count != 1 || bindingsB.Count != 1)
                    return false;

                if (!bindingsA[0].KeyCombination.Equals(oldA) || !bindingsB[0].KeyCombination.Equals(oldB))
                    return false;

                bindingsA[0].KeyCombination = oldB;
                bindingsB[0].KeyCombination = oldA;
                return true;
            });
        }

        /// <summary>
        /// Moves the key binding of an action to a new combination, but only if the action has exactly one binding and it matches the given combination.
        /// Used to migrate changed defaults without touching bindings the user customised.
        /// </summary>
        /// <param name="rulesetName">The short name of the ruleset the action belongs to, or <c>null</c> for global actions.</param>
        /// <param name="variant">The variant the action belongs to, or <c>null</c> for global actions.</param>
        /// <param name="action">The action to rebind.</param>
        /// <param name="oldCombination">The combination <paramref name="action"/> is expected to be bound to.</param>
        /// <param name="newCombination">The combination to bind <paramref name="action"/> to.</param>
        /// <returns>Whether the binding was moved.</returns>
        public bool RebindIfUnchanged(string? rulesetName, int? variant, object action, KeyCombination oldCombination, KeyCombination newCombination)
        {
            return realm.Write(r =>
            {
                var bindings = r.All<RealmKeyBinding>().ToList()
                                .Where(b => b.RulesetName == rulesetName && b.Variant == variant && b.ActionInt == (int)action)
                                .ToList();

                if (bindings.Count != 1 || !bindings[0].KeyCombination.Equals(oldCombination))
                    return false;

                bindings[0].KeyCombination = newCombination;
                return true;
            });
        }

        private void insertDefaults(Realm realm, List<RealmKeyBinding> existingBindings, IEnumerable<IKeyBinding> defaults, string? rulesetName = null, int? variant = null)
        {
            // compare counts in database vs defaults for each action type.
            foreach (var defaultsForAction in defaults.GroupBy(k => k.Action))
            {
                IEnumerable<RealmKeyBinding> existing = existingBindings.Where(k =>
                    k.RulesetName == rulesetName
                    && k.Variant == variant
                    && k.ActionInt == (int)defaultsForAction.Key);

                int defaultsCount = defaultsForAction.Count();
                int existingCount = existing.Count();

                if (defaultsCount > existingCount)
                {
                    // insert any defaults which are missing.
                    realm.Add(defaultsForAction.Skip(existingCount).Select(k => new RealmKeyBinding(k.Action, k.KeyCombination, rulesetName, variant)));
                }
                else if (defaultsCount < existingCount)
                {
                    // generally this shouldn't happen, but if the user has more key bindings for an action than we expect,
                    // remove the last entries until the count matches for sanity.
                    foreach (var k in existing.TakeLast(existingCount - defaultsCount).ToArray())
                    {
                        realm.Remove(k);

                        // Remove from the local flattened/cached list so future lookups don't query now deleted rows.
                        existingBindings.Remove(k);
                    }
                }
            }
        }

        /// <summary>
        /// Keys which should not be allowed for gameplay input purposes.
        /// </summary>
        private static readonly IEnumerable<InputKey> banned_keys = new[]
        {
            InputKey.MouseWheelDown,
            InputKey.MouseWheelLeft,
            InputKey.MouseWheelUp,
            InputKey.MouseWheelRight
        };

        public static bool CheckValidForGameplay(KeyCombination combination)
        {
            foreach (var key in banned_keys)
            {
                if (combination.Keys.Contains(key))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Clears all <see cref="RealmKeyBinding.KeyCombination"/>s from the provided <paramref name="keyBindings"/>
        /// which are assigned to more than one binding.
        /// </summary>
        /// <param name="keyBindings">The <see cref="RealmKeyBinding"/>s to de-duplicate.</param>
        /// <returns>Number of bindings cleared.</returns>
        public static int ClearDuplicateBindings(IEnumerable<IKeyBinding> keyBindings)
        {
            int countRemoved = 0;

            var lookup = keyBindings.ToLookup(kb => kb.KeyCombination);

            foreach (var group in lookup)
            {
                if (group.Select(kb => kb.Action).Distinct().Count() <= 1)
                    continue;

                foreach (var binding in group)
                    binding.KeyCombination = new KeyCombination(InputKey.None);

                countRemoved += group.Count();
            }

            return countRemoved;
        }
    }
}
