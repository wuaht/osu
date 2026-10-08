// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Platform;

namespace osu.Game.Configuration
{
    // Settings which only exist in this client are stored in a separate file rather than game.ini.
    // game.ini is shared with an official osu!(lazer) installation using the same data folder,
    // which drops unknown settings whenever it saves.
    public partial class OsuConfigManager
    {
        /// <summary>
        /// The file storing the settings which only exist in this client.
        /// </summary>
        public const string CLIENT_SETTINGS_FILENAME = @"slop.ini";

        /// <summary>
        /// The prefix of all settings which only exist in this client.
        /// </summary>
        private const string client_setting_prefix = @"Slop";

        /// <summary>
        /// Set during the construction of the base class, which loads the settings before the constructor of this class runs.
        /// </summary>
        private Storage? clientSettingsStorage;

        private bool usingClientSettingsFile;

        protected override string Filename => usingClientSettingsFile ? CLIENT_SETTINGS_FILENAME : base.Filename;

        /// <summary>
        /// Whether the given setting only exists in this client, and is therefore stored in <see cref="CLIENT_SETTINGS_FILENAME"/>.
        /// </summary>
        public static bool IsClientSetting(OsuSetting setting) => setting.ToString().StartsWith(client_setting_prefix, StringComparison.Ordinal);

        protected override void PerformLoad()
        {
            // Client settings are loaded from game.ini as well, as they were stored there before.
            // They are moved to the client settings file the next time the settings are saved.
            base.PerformLoad();

            // Loaded afterwards, so that values in the client settings file take precedence.
            withClientSettingsFile(base.PerformLoad);
        }

        protected override bool PerformSave()
        {
            // The base implementation writes all settings to a single file.
            if (clientSettingsStorage == null || string.IsNullOrEmpty(Filename))
                return false;

            string sharedFilename = Filename;
            string clientFilename = string.Empty;
            withClientSettingsFile(() => clientFilename = Filename);

            return save(sharedFilename, ConfigStore.Where(s => !IsClientSetting(s.Key)))
                   & save(clientFilename, ConfigStore.Where(s => IsClientSetting(s.Key)));

            bool save(string filename, IEnumerable<KeyValuePair<OsuSetting, IBindable>> settings)
            {
                try
                {
                    using (var stream = clientSettingsStorage.CreateFileSafely(filename))
                    using (var w = new StreamWriter(stream))
                    {
                        foreach (var p in settings)
                            w.WriteLine(@"{0} = {1}", p.Key, (p.Value.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty).Replace("\n", "").Replace("\r", ""));
                    }
                }
                catch
                {
                    return false;
                }

                return true;
            }
        }

        private void withClientSettingsFile(Action action)
        {
            usingClientSettingsFile = true;

            try
            {
                action();
            }
            finally
            {
                usingClientSettingsFile = false;
            }
        }
    }
}
