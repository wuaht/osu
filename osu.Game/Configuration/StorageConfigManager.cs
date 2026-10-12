// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace osu.Game.Configuration
{
    public class StorageConfigManager : IniConfigManager<StorageConfig>
    {
        protected override string Filename => "storage.ini";

        /// <remarks>
        /// Stored with the prefix of this client's settings files (<c>slop.storage.ini</c>), so the data location can be changed
        /// independently from an official osu!(lazer) installation using the same data folder.
        /// The shared <c>storage.ini</c> is only read as a starting point, as long as the prefixed file doesn't exist.
        /// </remarks>
        public StorageConfigManager(Storage storage)
            : base(storage, null, OsuConfigManager.CLIENT_SETTINGS_PREFIX)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(StorageConfig.FullPath, string.Empty);
        }
    }

    public enum StorageConfig
    {
        FullPath,
    }
}
