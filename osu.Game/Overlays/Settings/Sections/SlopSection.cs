// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings.Sections.Slop;

namespace osu.Game.Overlays.Settings.Sections
{
    /// <summary>
    /// Settings for features which are exclusive to slop!.
    /// </summary>
    public partial class SlopSection : SettingsSection
    {
        public override LocalisableString Header => SlopSettingsStrings.SlopSectionHeader;

        public override Drawable CreateIcon() => new SlopSectionIcon();

        public SlopSection()
        {
            Children = new Drawable[]
            {
                new GraphicsSettings(),
                new EditorSettings(),
                new FposuSettings(),
                new OfflineProfileSettings(),
                new OnlineSettings(),
            };
        }

        private partial class SlopSectionIcon : Sprite
        {
            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Texture = textures.Get(@"Icons/slop");
            }
        }
    }
}
