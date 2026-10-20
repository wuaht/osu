// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Configuration
{
    public enum ReleaseStream
    {
        // This client publishes stable releases and nightly builds (as GitHub prereleases) to its own releases, see release-build.ps1.
        // The names are kept, as they are stored in the config.
        [Description("Stable")]
        Lazer,

        [Description("Nightly (Unstable)")]
        Tachyon
    }
}
