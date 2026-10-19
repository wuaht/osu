// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    public partial class DrawableManiaJudgement : DrawableJudgement
    {
        public DrawableManiaJudgement()
        {
            // Extend the dimensions of this drawable to the entire parenting container.
            // This allows skin implementations (i.e. LegacyManiaJudgementPiece) to freely choose the anchor based on skin settings.
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            RelativeSizeAxes = Axes.Both;
            Size = new Vector2(1f);
        }

        private readonly Bindable<bool> noJudgementAnimations = new Bindable<bool>();

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager config)
        {
            config?.BindWith(OsuSetting.SlopNoManiaJudgementAnimations, noJudgementAnimations);
        }

        protected override bool DisableScaleAnimations => noJudgementAnimations.Value;

        protected override Drawable CreateDefaultJudgement(HitResult result) => new DefaultManiaJudgementPiece(result);
    }
}
