// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Displays the line which the selection is mirrored over during an angled flip.
    /// </summary>
    public partial class AngledFlipPreviewLine : CompositeDrawable
    {
        private readonly IBindable<AngledFlipLine?> previewLine;

        private Box line = null!;

        public AngledFlipPreviewLine(OsuSelectionAngledFlipHandler flipHandler)
        {
            previewLine = flipHandler.PreviewLine.GetBoundCopy();

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChild = line = new Box
            {
                Origin = Anchor.Centre,
                // long enough to always cross the whole screen.
                Width = 10000,
                Height = 2,
                Colour = colours.Yellow,
                Alpha = 0,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            previewLine.BindValueChanged(l =>
            {
                if (l.NewValue is not AngledFlipLine value)
                {
                    line.Alpha = 0;
                    return;
                }

                line.Alpha = 1;
                line.Position = value.Origin;
                line.Rotation = value.Degrees;
            }, true);
        }
    }
}
