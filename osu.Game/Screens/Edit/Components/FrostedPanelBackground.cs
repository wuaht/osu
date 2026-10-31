// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osuTK;

namespace osu.Game.Screens.Edit.Components
{
    /// <summary>
    /// A frosted glass background for editor panels. The content behind it stays visible, but is blurred and slightly darkened,
    /// such that text and controls on top of it stay readable even on bright or busy backgrounds.
    /// </summary>
    public partial class FrostedPanelBackground : BackdropBlurContainer
    {
        /// <summary>
        /// The width of the refracting edge of liquid glass at the highest thickness setting.
        /// </summary>
        private const float max_liquid_glass_thickness = 30;

        /// <summary>
        /// The glass style of the background. Nothing is done with the content behind it in the <see cref="GlassStyle.Standard"/> style,
        /// so the background should be hidden then.
        /// </summary>
        public Bindable<GlassStyle> Style { get; } = new Bindable<GlassStyle>(GlassStyle.Frosted);

        private float glassCornerRadius;

        /// <summary>
        /// The corner radius of the panel, which the edge of liquid glass follows.
        /// </summary>
        public float GlassCornerRadius
        {
            get => glassCornerRadius;
            set
            {
                glassCornerRadius = value;

                if (IsLoaded)
                    updateGlass();
            }
        }

        private LiquidGlassConfig liquidGlass = null!;

        public FrostedPanelBackground()
        {
            RelativeSizeAxes = Axes.Both;

            // The backdrop is blurred at a quarter of the resolution, which is visually indistinguishable but much cheaper.
            EffectBufferScale = new Vector2(0.25f);

            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = EditorPanelStyle.FrostedPanelTint,
            };
        }

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(OsuConfigManager? config)
        {
            liquidGlass = new LiquidGlassConfig(config, () =>
            {
                if (IsLoaded)
                    updateGlass();
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Style.BindValueChanged(_ => updateGlass(), true);
        }

        private void updateGlass()
        {
            // the refraction of liquid glass needs a sharper backdrop than the blur.
            EffectBufferScale = new Vector2(Style.Value == GlassStyle.LiquidGlass ? 0.5f : 0.25f);

            liquidGlass.ApplyTo(this, Style.Value, EditorPanelStyle.FROSTED_PANEL_BLUR_SIGMA, max_liquid_glass_thickness, GlassCornerRadius);
        }
    }
}
