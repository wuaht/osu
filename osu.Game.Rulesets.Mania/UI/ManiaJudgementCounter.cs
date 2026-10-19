// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// A compact list of the amount of each judgement received so far, laid out in 640x480 units.
    /// </summary>
    public partial class ManiaJudgementCounter : CompositeDrawable
    {
        private const float reference_height = 480;
        private const float font_size = 10;
        private const float row_height = 11;
        private const float value_position = 48;

        private readonly ManiaPlayfield playfield;

        private readonly Bindable<ManiaJudgementCounterPosition> position = new Bindable<ManiaJudgementCounterPosition>();

        private readonly List<Row> rows = new List<Row>();

        private Row holdOk = null!;
        private Row holdMiss = null!;

        public ManiaJudgementCounter(ManiaPlayfield playfield)
        {
            this.playfield = playfield;

            AutoSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.SlopManiaJudgementCounter, position);

            addRow(@"MA", new Color4(153, 204, 255, 255), HitResult.Perfect);
            addRow(@"PR", new Color4(242, 203, 48, 255), HitResult.Great);
            addRow(@"GR", new Color4(20, 204, 143, 255), HitResult.Good);
            addRow(@"GD", new Color4(26, 178, 255, 255), HitResult.Ok);
            addRow(@"BD", new Color4(255, 26, 179, 255), HitResult.Meh);
            addRow(@"MS", new Color4(204, 41, 41, 255), HitResult.Miss);
            holdOk = addRow(@"OK", new Color4(242, 203, 48, 255), null);
            holdMiss = addRow(@"NG", new Color4(204, 41, 41, 255), null);

            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = rows,
            };

            Row addRow(string label, Color4 colour, HitResult? result)
            {
                var row = new Row(label, colour, result);
                rows.Add(row);
                return row;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            playfield.NewResult += onNewResult;
            playfield.RevertResult += onRevertResult;

            position.BindValueChanged(p => Alpha = p.NewValue == ManiaJudgementCounterPosition.Disabled ? 0 : 1, true);
        }

        private void onNewResult(DrawableHitObject drawableHitObject, JudgementResult result) => getRow(result)?.Add(1);

        private void onRevertResult(JudgementResult result) => getRow(result)?.Add(-1);

        private Row? getRow(JudgementResult result)
        {
            if (result.HitObject is HoldNoteBody)
            {
                switch (result.Type)
                {
                    case HitResult.IgnoreHit:
                        return holdOk;

                    case HitResult.ComboBreak:
                        return holdMiss;

                    default:
                        return null;
                }
            }

            foreach (var row in rows)
            {
                if (row.Result == result.Type)
                    return row;
            }

            return null;
        }

        protected override void Update()
        {
            base.Update();

            if (Parent == null)
                return;

            float scale = Parent.DrawHeight / reference_height;
            float height = rows.Count * row_height;

            Scale = new Vector2(scale);

            switch (position.Value)
            {
                case ManiaJudgementCounterPosition.BottomLeft:
                    Position = new Vector2(2, reference_height - height - 2) * scale;
                    break;

                case ManiaJudgementCounterPosition.StageLeft:
                    float stageLeft = Parent.ToLocalSpace(playfield.Stages[0].ScreenSpaceDrawQuad.TopLeft).X;
                    Position = new Vector2(stageLeft - 72 * scale, (reference_height / 2 - height / 2) * scale);
                    break;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            playfield.NewResult -= onNewResult;
            playfield.RevertResult -= onRevertResult;
        }

        private partial class Row : CompositeDrawable
        {
            public readonly HitResult? Result;

            private readonly OsuSpriteText valueText;

            private int count;

            public Row(string label, Color4 colour, HitResult? result)
            {
                Result = result;

                Size = new Vector2(value_position, row_height);

                InternalChildren = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = label,
                        Colour = colour,
                        Font = OsuFont.Default.With(size: font_size, weight: FontWeight.Bold),
                    },
                    valueText = new OsuSpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Text = @"0",
                        Font = OsuFont.Default.With(size: font_size, weight: FontWeight.Bold),
                    },
                };
            }

            public void Add(int amount)
            {
                count += amount;
                valueText.Text = count.ToString();
            }
        }
    }
}
