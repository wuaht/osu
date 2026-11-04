// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// Song select artwork of a legacy skin (<c>songselect-top</c>, <c>songselect-bottom</c> and <c>selection-mode</c>), positioned like in stable.
    /// Displayed behind the song select UI, so skins which paint a full stable layout into <c>selection-mode</c> still leave the lazer UI readable.
    /// </summary>
    public partial class LegacySongSelectBackground : CompositeDrawable
    {
        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        public LegacySongSelectBackground()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            updateArtwork();
        }

        private void onSkinChanged() => Schedule(updateArtwork);

        private void updateArtwork()
        {
            ClearInternal();

            if (skin.GetUserLegacyTexture("songselect-top") is Texture top)
            {
                AddInternal(new Sprite { Texture = top });

                // stable repeats the rightmost column of the texture to fill wider screens.
                var lastColumn = top.Crop(new RectangleF(top.Width - 1, 0, 1, top.Height));
                lastColumn.ScaleAdjust = top.ScaleAdjust;

                AddInternal(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = top.DisplayHeight,
                    Padding = new MarginPadding { Left = top.DisplayWidth - 1 },
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = lastColumn,
                    }
                });
            }

            if (skin.GetUserLegacyTexture("songselect-bottom") is Texture bottom)
            {
                AddInternal(new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = bottom.DisplayHeight,
                    Texture = bottom,
                });
            }

            if (skin.GetUserLegacyTexture("selection-mode") is Texture mode)
            {
                AddInternal(new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    X = LegacyModeButton.STABLE_X,
                    Texture = mode,
                });
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }
    }

    /// <summary>
    /// The interactive part of stable's mode button, shown when the user's legacy skin provides <c>selection-mode</c>.
    /// Its artwork is drawn by <see cref="LegacySongSelectBackground"/>; this adds the hover texture and the current ruleset icon, and cycles the ruleset on click.
    /// </summary>
    public partial class LegacyModeButton : OsuClickableContainer
    {
        public const float STABLE_X = 224;

        public readonly Bindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved]
        private RulesetStore rulesets { get; set; } = null!;

        private Sprite overSprite = null!;
        private Sprite rulesetIcon = null!;

        public LegacyModeButton()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            X = STABLE_X;
            Size = new Vector2(92, 90);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                overSprite = new Sprite
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Alpha = 0,
                },
                rulesetIcon = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Blending = BlendingParameters.Additive,
                },
            };

            Action = cycleRuleset;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skin.SourceChanged += onSkinChanged;
            Ruleset.BindValueChanged(_ => updateTextures(), true);
        }

        private void onSkinChanged() => Schedule(updateTextures);

        private void updateTextures()
        {
            bool visible = skin.GetUserLegacyTexture("selection-mode") != null;

            Alpha = visible ? 1 : 0;

            if (!visible)
                return;

            // like stable, the hover texture and ruleset icons fall back to the default legacy skin.
            overSprite.Texture = skin.GetTexture("selection-mode-over");
            rulesetIcon.Texture = Ruleset.Value == null ? null : skin.GetTexture($"mode-{Ruleset.Value.ShortName}-small");
        }

        private void cycleRuleset()
        {
            if (Ruleset.Disabled)
                return;

            var available = rulesets.AvailableRulesets.ToList();

            if (available.Count == 0)
                return;

            int index = available.FindIndex(r => r.Equals(Ruleset.Value));
            Ruleset.Value = available[(index + 1) % available.Count];
        }

        protected override bool OnHover(HoverEvent e)
        {
            overSprite.FadeIn(100, Easing.OutQuint);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            overSprite.FadeOut(100, Easing.OutQuint);
            base.OnHoverLost(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSkinChanged;
        }
    }
}
