// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Fposu.UI
{
    /// <summary>
    /// Displays the regular osu! playfield (along with the beatmap background, like McOsu) on a <see cref="FposuScreen"/>.
    /// </summary>
    public partial class FposuPlayfieldAdjustmentContainer : PlayfieldAdjustmentContainer
    {
        public readonly FposuScreen Screen;

        /// <summary>
        /// Displays the objects in 3D instead of on the screen, with the depth mod.
        /// </summary>
        public readonly FposuDepthLayer DepthLayer;

        /// <summary>
        /// Contains the overlays of the ruleset (e.g. of the flashlight and blinds mods), so that they are displayed on the screen along with the playfield.
        /// </summary>
        public readonly Container RulesetOverlays;

        /// <summary>
        /// Contains the break and skip overlays, so that they are displayed on the screen along with the playfield.
        /// </summary>
        public readonly Container BreakAndSkipOverlays;

        private readonly OsuPlayfieldAdjustmentContainer content;

        protected override Container<Drawable> Content => content;

        public FposuPlayfieldAdjustmentContainer(FposuCamera camera)
        {
            InternalChildren = new Drawable[]
            {
                Screen = new FposuScreen(camera)
                {
                    Children = new Drawable[]
                    {
                        new BeatmapBackground(),
                        content = new OsuPlayfieldAdjustmentContainer { AlignWithStoryboard = true },
                        RulesetOverlays = new Container { RelativeSizeAxes = Axes.Both },
                        BreakAndSkipOverlays = new Container { RelativeSizeAxes = Axes.Both },
                    }
                },
                DepthLayer = new FposuDepthLayer(Screen),
            };
        }

        /// <summary>
        /// The background of the beatmap, dimmed according to the background dim setting.
        /// The regular background is hidden behind the 3D environment. Hidden with <see cref="OsuSetting.SlopFposuTransparentBackground"/>.
        /// </summary>
        private partial class BeatmapBackground : Sprite
        {
            private IBindable<double> dimLevel = null!;
            private IBindable<bool> transparent = null!;

            [BackgroundDependencyLoader]
            private void load(IBindable<WorkingBeatmap> beatmap, OsuConfigManager config)
            {
                RelativeSizeAxes = Axes.Both;
                FillMode = FillMode.Fill;
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;

                Texture = beatmap.Value.GetBackground();

                dimLevel = config.GetBindable<double>(OsuSetting.DimLevel);
                dimLevel.BindValueChanged(dim => Colour = OsuColour.Gray(1 - (float)dim.NewValue), true);

                transparent = config.GetBindable<bool>(OsuSetting.SlopFposuTransparentBackground);
                transparent.BindValueChanged(t => Alpha = t.NewValue ? 0 : 1, true);
            }
        }
    }
}
