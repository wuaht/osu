// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit.Blueprints.HitCircles.Components
{
    public partial class HitCirclePiece : BlueprintPiece<HitCircle>
    {
        public HitCirclePiece()
        {
            Origin = Anchor.Centre;

            Size = OsuHitObject.OBJECT_DIMENSIONS;

            CornerRadius = Size.X / 2;
            CornerExponent = 2;

            // a thin, slightly translucent ring, to obstruct the selected objects as little as possible.
            InternalChild = new RingPiece(7);
        }

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        private bool partHighlighted;

        /// <summary>
        /// Whether this piece represents a selected part of a hit object (e.g. a slider node selected for hitsounding), which is displayed in red.
        /// </summary>
        public bool PartHighlighted
        {
            get => partHighlighted;
            set
            {
                if (partHighlighted == value)
                    return;

                partHighlighted = value;

                if (IsLoaded)
                    updateColour();
            }
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            updateColour();
        }

        // translucency is applied via the colour, as the alpha is used for showing / hiding the piece.
        private void updateColour() => Colour = (partHighlighted ? colours.Red : colours.Yellow).Opacity(0.85f);

        public override void UpdateFrom(HitCircle hitObject)
        {
            base.UpdateFrom(hitObject);

            Scale = new Vector2(hitObject.Scale);
        }
    }
}
