// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Legacy
{
    public partial class LegacyApproachCircle : Sprite
    {
        [Resolved]
        private DrawableHitObject drawableObject { get; set; } = null!;

        private IBindable<Color4> accentColour = null!;

        private readonly Bindable<bool> whiteApproachCircles = new Bindable<bool>();

        [Resolved(CanBeNull = true)]
        private OsuConfigManager? gameConfig { get; set; }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var texture = skin.GetTexture(@"approachcircle");
            Debug.Assert(texture != null);
            Texture = texture.WithMaximumSize(OsuHitObject.OBJECT_DIMENSIONS * 2);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            accentColour = drawableObject.AccentColour.GetBoundCopy();
            accentColour.BindValueChanged(_ => updateColour());

            gameConfig?.BindWith(OsuSetting.SlopWhiteApproachCircles, whiteApproachCircles);
            whiteApproachCircles.BindValueChanged(_ => updateColour(), true);
        }

        private void updateColour()
        {
            Colour = whiteApproachCircles.Value ? Color4.White : LegacyColourCompatibility.DisallowZeroAlpha(accentColour.Value);
        }
    }
}
