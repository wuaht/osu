// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Input;
using osu.Game.Overlays;
using osu.Game.Overlays.Volume;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Tests.Slop
{
    [Category("slop")]
    public partial class TestSceneAltScrollVolumeBlocking : OsuManualInputManagerTestScene
    {
        private VolumeOverlay volume = null!;

        [Test]
        public void TestAltScrollOverHoverHandlingChildOfBlocker()
        {
            AddStep("load content", () => loadContent(new BlockingContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new HoverHandlingBox { RelativeSizeAxes = Axes.Both },
            }));

            performAltScroll();
            AddAssert("volume not adjusted", () => volume.State.Value, () => Is.EqualTo(Visibility.Hidden));
        }

        [Test]
        public void TestAltScrollOverHoverHandlingChildWithoutBlocker()
        {
            AddStep("load content", () => loadContent(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = new HoverHandlingBox { RelativeSizeAxes = Axes.Both },
            }));

            performAltScroll();
            AddAssert("volume adjusted", () => volume.State.Value, () => Is.EqualTo(Visibility.Visible));
        }

        private void loadContent(Drawable content)
        {
            volume = new VolumeOverlay();

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[] { (typeof(VolumeOverlay), volume) },
                Children = new[]
                {
                    content,
                    volume,
                    new ScrollAdjustsVolume(requireAltPressed: true),
                }
            };
        }

        private void performAltScroll()
        {
            AddStep("hold alt", () => InputManager.PressKey(Key.AltLeft));
            AddStep("perform scroll", () =>
            {
                InputManager.MoveMouseTo(Content);
                InputManager.ScrollVerticalBy(1);
            });
            AddStep("release alt", () => InputManager.ReleaseKey(Key.AltLeft));
        }

        private partial class BlockingContainer : Container, IBlockGlobalAltScrollVolume;

        private partial class HoverHandlingBox : Box
        {
            protected override bool OnHover(HoverEvent e) => true;
        }
    }
}
