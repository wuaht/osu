// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit.Hitsounding
{
    /// <summary>
    /// A cell of the header column on the left of the hitsound editor, next to a part of the timeline.
    /// </summary>
    public partial class HitsoundHeaderCell : Container
    {
        private readonly Container content;

        protected override Container<Drawable> Content => content;

        public HitsoundHeaderCell()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = content = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding { Horizontal = 10 },
            };
        }

        private readonly Bindable<GlassStyle> glassStyle = new Bindable<GlassStyle>();

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            Box background;

            AddInternal(background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Depth = float.MaxValue,
                Colour = colourProvider.Background4,
            });

            // the glass background of the lanes shows through the headers as well.
            config.BindWith(OsuSetting.SlopHitsoundEditorLanesGlassStyle, glassStyle);
            glassStyle.BindValueChanged(s => background.Alpha = s.NewValue != GlassStyle.Standard ? HitsoundLaneHeaders.FROSTED_HEADER_ALPHA : 1, true);
        }
    }
}
