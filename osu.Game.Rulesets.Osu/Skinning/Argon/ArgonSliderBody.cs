// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Extensions.Color4Extensions;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Argon
{
    public partial class ArgonSliderBody : FrostedPlaySliderBody
    {
        // Eventually this would be a user setting.
        public float BodyAlpha { get; init; } = 1;

        protected override void LoadComplete()
        {
            const float path_radius = ArgonMainCirclePiece.OUTER_GRADIENT_SIZE / 2;

            base.LoadComplete();

            AccentColourBindable.BindValueChanged(accent => BorderColour = accent.NewValue, true);
            ScaleBindable.BindValueChanged(scale => PathRadius = path_radius * scale.NewValue, true);

            // This border size thing is kind of weird, hey.
            const float intended_thickness = ArgonMainCirclePiece.GRADIENT_THICKNESS / path_radius;

            BorderSize = intended_thickness / Default.DrawableSliderPath.BORDER_PORTION;
        }

        protected override Default.DrawableSliderPath CreateSliderPath() => new DrawableSliderPath();

        protected override Color4 GetBodyAccentColour(ISkinSource skin, Color4 hitObjectAccentColour)
        {
            // frosted slider bodies are more transparent to let the blurred backdrop show through.
            float alpha = FrostedSliders.Value ? BodyAlpha * (0.85f - 0.75f * Frostiness.Value) : BodyAlpha;

            return base.GetBodyAccentColour(skin, hitObjectAccentColour).Opacity(alpha);
        }

        private partial class DrawableSliderPath : FrostedDrawableSliderPath
        {
            public DrawableSliderPath()
            {
                BackdropTintStrength = 0.6f;
            }

            protected override Color4 ColourAt(float position)
            {
                if (CalculatedBorderPortion != 0f && position <= CalculatedBorderPortion)
                    return BorderColour;

                return AccentColour.Darken(4);
            }
        }
    }
}
