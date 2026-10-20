// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;

namespace osu.Game.Audio
{
    /// <summary>
    /// A container which applies the hitsound volume setting to all samples inside it.
    /// Hitsounds don't receive the global effect volume, so hitsounds played outside of gameplay need to be placed inside one of these.
    /// </summary>
    public partial class HitsoundVolumeContainer : AudioContainer
    {
        private readonly Bindable<double> hitsoundVolume = new Bindable<double>(1);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.HitsoundVolume, hitsoundVolume);
            Volume.BindTo(hitsoundVolume);
        }
    }
}
