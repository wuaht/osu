// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning;
using osu.Game.Rulesets.Osu.Skinning.Legacy;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneFrostedSkinOptions : OsuTestScene
    {
        private static readonly Color4 combo_colour = Color4.Red;
        private static readonly Color4 track_colour = Color4.Blue;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private LegacySliderBody sliderBody = null!;

        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        public void TestSliderTrackColour(bool frostedSliders, bool alwaysColoured, bool expectTrackColour)
        {
            AddStep("set frosted sliders", () => config.SetValue(OsuSetting.SlopFrostedSliders, frostedSliders));
            AddStep("set always coloured", () => config.SetValue(OsuSetting.SlopFrostedSlidersAlwaysColoured, alwaysColoured));

            AddStep("load slider body", () =>
            {
                var drawableSlider = new DrawableSlider(new Slider
                {
                    Path = new SliderPath(new[] { new PathControlPoint(Vector2.Zero), new PathControlPoint(new Vector2(100, 0)) }),
                })
                {
                    AccentColour = { Value = combo_colour },
                };

                Child = new SkinProvidingContainer(new TrackOverrideSkin())
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new DependencyProvidingContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        CachedDependencies = new (Type, object)[] { (typeof(DrawableHitObject), drawableSlider) },
                        Child = sliderBody = new LegacySliderBody(),
                    },
                };
            });

            AddUntilStep("loaded", () => sliderBody.IsLoaded);
            AddAssert("body colour", () => withoutAlpha(sliderBody.AccentColour), () => Is.EqualTo(expectTrackColour ? track_colour : combo_colour));

            AddStep("reset settings", () =>
            {
                config.SetValue(OsuSetting.SlopFrostedSliders, false);
                config.SetValue(OsuSetting.SlopFrostedSlidersAlwaysColoured, true);
            });
        }

        [TestCase(true, true, null, true)]
        [TestCase(true, false, null, false)]
        [TestCase(false, true, null, false)]
        [TestCase(true, true, "sliderstartcircle", false)]
        public void TestFrostedHitCircles(bool frostedSliders, bool frostedHitCircles, string? lookupPrefix, bool expectFrosted)
        {
            LegacyMainCirclePiece piece = null!;

            AddStep("set frosted sliders", () => config.SetValue(OsuSetting.SlopFrostedSliders, frostedSliders));
            AddStep("set frosted hit circles", () => config.SetValue(OsuSetting.SlopFrostedHitCircles, frostedHitCircles));

            AddStep("load circle", () => Child = new SkinProvidingContainer(new TrackOverrideSkin())
            {
                RelativeSizeAxes = Axes.Both,
                Child = piece = new LegacyMainCirclePiece(lookupPrefix, false),
            });

            AddUntilStep("loaded", () => piece.IsLoaded);
            AddAssert("backdrop blurred", () => piece.ChildrenOfType<BackdropBlurContainer>().Any(), () => Is.EqualTo(expectFrosted));

            AddStep("reset settings", () =>
            {
                config.SetValue(OsuSetting.SlopFrostedSliders, false);
                config.SetValue(OsuSetting.SlopFrostedHitCircles, true);
            });
        }

        private static Color4 withoutAlpha(Color4 colour) => new Color4(colour.R, colour.G, colour.B, 1);

        /// <summary>
        /// A skin which only specifies a slider track colour.
        /// </summary>
        private class TrackOverrideSkin : ISkin
        {
            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
            {
                if (lookup is OsuSkinColour.SliderTrackOverride)
                    return (IBindable<TValue>)(object)new Bindable<Color4>(track_colour);

                return null;
            }
        }
    }
}
