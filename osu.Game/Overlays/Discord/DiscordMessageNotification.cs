// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Discord
{
    /// <summary>
    /// A notification about a Discord message, which shows the avatar of its author.
    /// </summary>
    public partial class DiscordMessageNotification : SimpleNotification
    {
        private readonly DiscordTextureStore? textures;

        /// <param name="avatarUrl">The avatar of the author of the message.</param>
        /// <param name="textures">
        /// The textures of the Discord overlay, which the avatar is loaded with as the notification isn't part of the overlay.
        /// Without them, the icon of the notification is shown instead.
        /// </param>
        public DiscordMessageNotification(string avatarUrl, DiscordTextureStore? textures)
        {
            this.textures = textures;

            if (textures == null)
                return;

            IconDrawable.Alpha = 0;
            IconContent.Add(new DiscordAvatar(avatarUrl, 28)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            });
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            if (textures != null)
                dependencies.Cache(textures);

            return dependencies;
        }
    }
}
