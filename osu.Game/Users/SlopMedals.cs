// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Users
{
    /// <summary>
    /// Medals which only exist in this client and are unlocked locally.
    /// </summary>
    public static class SlopMedals
    {
        /// <summary>
        /// Unlocked by watching a <see cref="Configuration.SlopAutoplayEngine.Realistic"/> autoplay all the way to the results screen.
        /// </summary>
        public static Medal Miku => new Medal
        {
            Name = @"World is Mine",
            InternalName = @"slop-miku",
            Description = @"Sit back and watch Miku play a whole map. She had it under control the entire time.",
            LocalTexture = @"Slop/medal-miku",
        };
    }
}
