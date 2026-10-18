// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    public abstract partial class SkinnableCursor : CompositeDrawable
    {
        private const float pressed_scale = 1.2f;
        private const float released_scale = 1f;

        public virtual void Expand()
        {
            ExpandTarget?.ScaleTo(released_scale)
                        .ScaleTo(pressed_scale, 400, Easing.OutElasticHalf);
        }

        public virtual void Contract()
        {
            ExpandTarget?.ScaleTo(released_scale, 400, Easing.OutQuad);
        }

        /// <summary>
        /// Applies a colour effect to this cursor, or resets it when <paramref name="colour"/> is <c>null</c>.
        /// By default, the whole cursor is tinted. Cursors with coloured parts should override this,
        /// as tinting multiplies with the existing colours and makes them darker.
        /// </summary>
        public virtual void ApplyColourEffect(Colour4? colour) => Colour = colour ?? Colour4.White;

        /// <summary>
        /// The an optional piece of the cursor to expand when in a clicked state.
        /// If null, the whole cursor will be affected by expansion.
        /// </summary>
        public Drawable? ExpandTarget { get; protected set; }
    }
}
