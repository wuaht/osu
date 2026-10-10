// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Components;

namespace osu.Game.Screens.Edit.Nominators
{
    /// <summary>
    /// A panel of the "request" screen, with an opaque or frosted glass background depending on the setting.
    /// </summary>
    public partial class NominatorsPanel : Container
    {
        public const float CORNER_RADIUS = 10;

        protected override Container<Drawable> Content { get; }

        private readonly BindableBool frosted = new BindableBool();

        private Box opaqueBackground = null!;
        private FrostedPanelBackground frostedBackground = null!;

        /// <param name="autoSizeAxes">The axes along which the panel is sized to fit its content.</param>
        public NominatorsPanel(Axes autoSizeAxes = Axes.None)
        {
            Masking = true;
            CornerRadius = CORNER_RADIUS;

            AutoSizeAxes = autoSizeAxes;

            Content = new Container
            {
                RelativeSizeAxes = Axes.Both & ~autoSizeAxes,
                AutoSizeAxes = autoSizeAxes,
            };
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopBnTrackerFrostedPanels, frosted);

            InternalChildren = new Drawable[]
            {
                opaqueBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background5,
                },
                frostedBackground = new FrostedPanelBackground(),
                Content,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            frosted.BindValueChanged(f =>
            {
                opaqueBackground.Alpha = f.NewValue ? 0 : 1;
                frostedBackground.Alpha = f.NewValue ? 1 : 0;
            }, true);
        }
    }
}
