// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Screens.Edit.Components
{
    /// <summary>
    /// The shape of anchors (slider control points and selection box handles) in the editor.
    /// </summary>
    public enum EditorAnchorShape
    {
        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.AnchorShapeCircle))]
        Circle,

        [LocalisableDescription(typeof(SlopSettingsStrings), nameof(SlopSettingsStrings.AnchorShapeSquare))]
        Square,
    }

    /// <summary>
    /// A filled shape (or an outline of it) which is displayed as either a circle or a square.
    /// </summary>
    public partial class EditorAnchorShapeContainer : Container
    {
        private EditorAnchorShape shape;

        public EditorAnchorShape Shape
        {
            get => shape;
            set
            {
                shape = value;
                updateCornerRadius();
            }
        }

        /// <param name="outline">Whether only an outline should be displayed (with its thickness specified via the border thickness).</param>
        public EditorAnchorShapeContainer(bool outline = false)
        {
            Masking = true;

            if (outline)
                BorderColour = Colour4.White;

            Child = new Box
            {
                RelativeSizeAxes = Axes.Both,
                AlwaysPresent = true,
                Alpha = outline ? 0 : 1,
            };
        }

        protected override void Update()
        {
            base.Update();
            updateCornerRadius();
        }

        private void updateCornerRadius() => CornerRadius = shape == EditorAnchorShape.Circle ? Math.Min(DrawWidth, DrawHeight) / 2 : 0;
    }
}
