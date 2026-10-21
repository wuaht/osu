// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Utils;

namespace osu.Game.Overlays.Mods
{
    /// <summary>
    /// The mods which are selected by default for each ruleset (like osu!stable's default mods).
    /// </summary>
    public static class DefaultMods
    {
        /// <summary>
        /// Reads the default mods of every ruleset from their serialised form.
        /// Invalid input results in no default mods.
        /// </summary>
        public static Dictionary<string, APIMod[]> Deserialise(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return new Dictionary<string, APIMod[]>();

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, APIMod[]>>(value) ?? new Dictionary<string, APIMod[]>();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to read the default mods.");
                return new Dictionary<string, APIMod[]>();
            }
        }

        public static string Serialise(Dictionary<string, APIMod[]> defaults)
            => defaults.Count == 0 ? string.Empty : JsonConvert.SerializeObject(defaults);

        /// <summary>
        /// Creates the default mods of <paramref name="ruleset"/> out of their serialised form <paramref name="value"/>.
        /// Mods which don't exist in the ruleset or aren't valid together are left out.
        /// </summary>
        public static Mod[] Get(string? value, Ruleset ruleset)
        {
            if (!Deserialise(value).TryGetValue(ruleset.ShortName, out var apiMods))
                return Array.Empty<Mod>();

            var mods = new List<Mod>();

            foreach (var apiMod in apiMods)
            {
                var mod = apiMod.ToMod(ruleset);

                if (mod is UnknownMod || mod.Type == ModType.System || mods.Any(m => m.GetType() == mod.GetType()))
                    continue;

                if (!ModUtils.CheckCompatibleSet(mods.Append(mod)))
                    continue;

                mods.Add(mod);
            }

            return mods.ToArray();
        }

        /// <summary>
        /// Stores <paramref name="mods"/> as the default mods of the ruleset with <paramref name="rulesetShortName"/>, in the serialised form <paramref name="value"/>.
        /// System mods are never stored.
        /// </summary>
        public static string Set(string? value, string rulesetShortName, IEnumerable<Mod> mods)
        {
            var defaults = Deserialise(value);
            var apiMods = mods.Where(m => m.Type != ModType.System).Select(m => new APIMod(m)).ToArray();

            if (apiMods.Length == 0)
                defaults.Remove(rulesetShortName);
            else
                defaults[rulesetShortName] = apiMods;

            return Serialise(defaults);
        }

        public static Mod[] Get(OsuConfigManager config, Ruleset ruleset) => Get(config.Get<string>(OsuSetting.SlopDefaultMods), ruleset);

        public static void Set(OsuConfigManager config, string rulesetShortName, IEnumerable<Mod> mods)
            => config.SetValue(OsuSetting.SlopDefaultMods, Set(config.Get<string>(OsuSetting.SlopDefaultMods), rulesetShortName, mods));
    }
}
